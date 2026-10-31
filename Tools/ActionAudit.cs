using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

// Frame-by-frame audit of every implemented encounter through the game's own
// timeline (issue #6). For each enemy/hero action it logs the start, impact,
// sound starts and HP/block changes; checks that motion-only frames change no
// pixels outside MotionDamage; exercises pause, focus loss, resize, exit and
// re-entry; and saves contact sheets. Software raster path only: it measures
// GDI+ scene rasterization, not the Direct3D presenter.
//
// ActionAudit.exe Svoi-vX.exe outputDirectory [fps] [visible|offscreen] [Encounter,...]
// "visible" shows and activates the window so the real focus-gated sounds start.
internal static class ActionAudit
{
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    static Type gameType;
    static Assembly gameAssembly;
    static string output;
    static int fps = 60;
    static bool visible = true;
    static StreamWriter log;
    static int problems;
    static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    sealed class Tile { public Bitmap Image; public string Label; }
    sealed class ActionRecord
    {
        public string Kind, Actor;
        public int Frames, Hits, StartTurn;
        public float Duration, HitTime;
        public List<string> Sounds = new List<string>();
        public List<Tile> Tiles = new List<Tile>();
        public HashSet<int> Captured = new HashSet<int>();
    }

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length < 2) { Console.WriteLine("ActionAudit.exe Svoi.exe outputDirectory [fps] [visible|offscreen] [Encounter,...]"); return 2; }
        output = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(output);
        if (args.Length > 2) fps = int.Parse(args[2], Invariant);
        if (args.Length > 3) visible = args[3] != "offscreen";
        gameAssembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
        gameType = gameAssembly.GetType("ShpilSvoih.Game", true);
        var routeSeeds = gameType.GetField("routeSeeds", BindingFlags.NonPublic | BindingFlags.Static);
        if (routeSeeds != null) routeSeeds.SetValue(null, new Random(12345));
        string[] encounters = args.Length > 4 ? args[4].Split(',') :
            new[] { "Rat", "StrayDogs", "Hedgehogs", "Doctors", "Babai", "Toha", "Mimicrator", "Fomatreshka", "SkaterMaster" };
        using (log = new StreamWriter(Path.Combine(output, "action-audit.txt"), false, new UTF8Encoding(false)))
        {
            Write("fps=" + fps + "; visible=" + visible + "; exe=" + Path.GetFileName(args[0]) + "; runtime=" + Environment.Version);
            foreach (var name in encounters)
            {
                try { Audit(name.Trim()); }
                catch (Exception error) { Problem(name + ": unhandled " + error); }
            }
            Write("SUMMARY problems=" + problems);
        }
        Console.WriteLine("problems=" + problems);
        return problems == 0 ? 0 : 1;
    }

    static void Write(string line) { log.WriteLine(line); log.Flush(); Console.WriteLine(line); }
    static void Problem(string line) { problems++; Write("PROBLEM " + line); }

    static FieldInfo FindField(Type type, string name)
    {
        for (var t = type; t != null; t = t.BaseType)
        {
            var field = t.GetField(name, Private | BindingFlags.Public);
            if (field != null) return field;
        }
        throw new MissingFieldException(type.Name, name);
    }
    static object Get(object target, string name) { return FindField(target.GetType(), name).GetValue(target); }
    static void Set(object target, string name, object value) { FindField(target.GetType(), name).SetValue(target, value); }
    static object Prop(object target, string name)
    {
        var property = target.GetType().GetProperty(name, Private | BindingFlags.Public);
        if (property == null) throw new MissingMemberException(target.GetType().Name, name);
        return property.GetValue(target, null);
    }
    static object Call(object target, string name, params object[] args)
    {
        foreach (var method in target.GetType().GetMethods(Private | BindingFlags.Public))
            if (method.Name == name && method.GetParameters().Length == args.Length) return method.Invoke(target, args);
        throw new MissingMethodException(target.GetType().Name, name);
    }

    static Form game;
    static object Combat { get { return Get(game, "combat"); } }
    static object Timeline { get { return Get(game, "attackTimeline"); } }
    static string Kind { get { return Prop(Timeline, "Kind").ToString(); } }
    static bool HitApplied { get { return (bool)Prop(Timeline, "HitApplied"); } }
    static float TimelineTime { get { return (float)Prop(Timeline, "Time"); } }
    static bool Busy { get { return (bool)Prop(game, "CombatBusy"); } }
    static string Phase { get { return Prop(Combat, "Phase").ToString(); } }
    static int Turn { get { return (int)Prop(Combat, "Turn"); } }

    sealed class Snapshot
    {
        public int HeroHealth, Block;
        public int[] Enemies;
        public string Describe()
        {
            return "hero=" + HeroHealth + " block=" + Block + " enemies=" + string.Join("/", Array.ConvertAll(Enemies, e => e.ToString()));
        }
        public bool Same(Snapshot other)
        {
            if (other.HeroHealth != HeroHealth || other.Block != Block || other.Enemies.Length != Enemies.Length) return false;
            for (int i = 0; i < Enemies.Length; i++) if (Enemies[i] != other.Enemies[i]) return false;
            return true;
        }
    }
    static Snapshot Take()
    {
        var combat = Combat;
        var hero = Get(combat, "Hero");
        var enemies = (IList)Prop(combat, "Enemies");
        var list = new int[enemies.Count];
        for (int i = 0; i < list.Length; i++) list[i] = (int)Prop(enemies[i], "Health") + ((bool)Prop(enemies[i], "Fled") ? 100000 : 0);
        return new Snapshot { HeroHealth = (int)Prop(hero, "Health"), Block = (int)Prop(combat, "Block"), Enemies = list };
    }

    // Sound starts are detected from the players' own state: a new voice object
    // or a play counter increment. No device access is required for detection.
    static readonly HashSet<object> seenVoices = new HashSet<object>();
    static int skaterPlays;
    static List<string> PollSounds()
    {
        var started = new List<string>();
        int plays = (int)Prop(Get(game, "skaterSounds"), "PlayCount");
        for (; skaterPlays < plays; skaterPlays++) started.Add("skater");
        foreach (var voice in (IEnumerable)Get(Get(game, "cardSounds"), "voices"))
            if (seenVoices.Add(voice)) started.Add("card");
        foreach (var name in new[] { "tohaSound", "howlSound" })
        {
            var voice = Get(Get(game, name), "voice");
            if (voice != null && seenVoices.Add(voice)) started.Add(name == "tohaSound" ? "toha" : "howl");
        }
        return started;
    }

    static void Render(Bitmap target, Rectangle clip)
    {
        using (var graphics = Graphics.FromImage(target))
        {
            if (!clip.IsEmpty) graphics.SetClip(clip);
            Call(game, "PaintScene", graphics);
        }
    }

    static int DiffOutside(Bitmap a, Bitmap b, Rectangle allowed, out Rectangle bounds)
    {
        int count = 0, left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1;
        var rect = new Rectangle(0, 0, a.Width, a.Height);
        var da = a.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var db = b.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var rowA = new int[a.Width]; var rowB = new int[a.Width];
            for (int y = 0; y < a.Height; y++)
            {
                System.Runtime.InteropServices.Marshal.Copy(IntPtr.Add(da.Scan0, y * da.Stride), rowA, 0, a.Width);
                System.Runtime.InteropServices.Marshal.Copy(IntPtr.Add(db.Scan0, y * db.Stride), rowB, 0, a.Width);
                for (int x = 0; x < a.Width; x++)
                {
                    if (rowA[x] == rowB[x] || allowed.Contains(x, y)) continue;
                    count++; left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x); bottom = Math.Max(bottom, y);
                }
            }
        }
        finally { a.UnlockBits(da); b.UnlockBits(db); }
        bounds = count == 0 ? Rectangle.Empty : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
        return count;
    }

    static readonly List<double> fullTimes = new List<double>(), bandTimes = new List<double>();
    static long fullPixels, bandPixels;
    static int motionFrames, staleFrames;
    static Bitmap previous, current, scratch;
    static ActionRecord action;
    static readonly List<ActionRecord> actions = new List<ActionRecord>();

    static void Audit(string encounter)
    {
        Write("");
        Write("== " + encounter);
        seenVoices.Clear(); skaterPlays = 0; fullTimes.Clear(); bandTimes.Clear(); fullPixels = bandPixels = 0;
        motionFrames = staleFrames = 0; actions.Clear(); action = null;
        using (game = (Form)Activator.CreateInstance(gameType))
        {
            game.ClientSize = new Size(1280, 840);
            if (visible) { game.Show(); game.Activate(); Application.DoEvents(); }
            else game.CreateControl();
            var kindType = gameAssembly.GetType("ShpilSvoih.EnemyKind", true);
            gameType.GetMethod("StartTestEncounter", Private, null, new[] { kindType, typeof(int) }, null)
                .Invoke(game, new object[] { Enum.Parse(kindType, encounter), 3 });
            // Shuten's debug multiplier makes each encounter reach its deaths quickly; no rule is changed.
            Call(Get(game, "currentHero"), "EnableShutGod");
            StopTimer();
            Write("focus=" + game.ContainsFocus + "; visible=" + game.Visible + "; music=" + Prop(game, "DesiredMusicTrack"));
            PollSounds();
            previous = new Bitmap(game.ClientSize.Width, game.ClientSize.Height, PixelFormat.Format32bppPArgb);
            current = new Bitmap(previous.Width, previous.Height, PixelFormat.Format32bppPArgb);
            scratch = new Bitmap(previous.Width, previous.Height, PixelFormat.Format32bppPArgb);
            try
            {
                Render(previous, Rectangle.Empty);
                SaveFrame(previous, encounter + "-start.png");
                bool pauseChecked = false, focusChecked = false, resizeChecked = false;
                for (int round = 0; round < 12 && (Phase == "Player" || Phase == "Enemy"); round++)
                {
                    int card = AttackCard();
                    if (card >= 0)
                    {
                        var before = Take();
                        Call(game, "PlayCombatCard", card);
                        AfterUserAction("hero card " + card, before);
                        RunUntilIdle(encounter, ref pauseChecked, ref focusChecked, ref resizeChecked);
                        if (Phase != "Player") continue;
                    }
                    var beforeTurn = Take();
                    Call(game, "EndCombatTurn");
                    AfterUserAction("end turn", beforeTurn);
                    RunUntilIdle(encounter, ref pauseChecked, ref focusChecked, ref resizeChecked);
                }
                Write("result phase=" + Phase + " turn=" + Turn + " " + Take().Describe());
                CheckDeathSnapshots();
                Summarize(encounter);
                CheckExitAndReentry(encounter, kindType);
            }
            finally
            {
                previous.Dispose(); current.Dispose(); scratch.Dispose();
                foreach (var record in actions) foreach (var tile in record.Tiles) tile.Image.Dispose();
                actions.Clear();
            }
        }
    }

    static void StopTimer()
    {
        // The audit owns the clock: WinForms timer ticks would add wall-clock time.
        var timer = (Timer)Get(game, "combatTimer");
        if (timer != null) timer.Stop();
    }

    static int AttackCard()
    {
        var combat = Combat;
        var hand = (IList)Prop(combat, "Hand");
        int fallback = -1;
        for (int i = 0; i < hand.Count; i++)
        {
            if (!(bool)Call(combat, "CanPlay", i)) continue;
            if ((bool)Get(hand[i], "Attack")) return i;
            if (fallback < 0) fallback = i;
        }
        return fallback;
    }

    static void AfterUserAction(string label, Snapshot before)
    {
        StopTimer();
        BeginActionIfNeeded();
        foreach (var sound in PollSounds()) NoteSound(sound);
        Render(previous, Rectangle.Empty);
        var after = Take();
        if (!after.Same(before) && Kind != "None" && !HitApplied)
            Problem(label + ": model changed before impact " + before.Describe() + " -> " + after.Describe());
    }

    static void NoteSound(string sound)
    {
        if (action != null && !actionEnded) action.Sounds.Add(sound + "@" + TimelineTime.ToString("F2", Invariant));
        else Write("  sound " + sound + " outside an action (turn " + Turn + ")");
    }

    // AttackTimeline.revision changes on every Start/Cancel: consecutive actions of the
    // same kind (two dogs, two doctors, three shots) are separate records.
    static int Revision { get { return (int)Get(Timeline, "revision"); } }
    static int recordRevision = -1;
    static void BeginActionIfNeeded()
    {
        if (Kind == "None" || (!actionEnded && recordRevision == Revision)) return;
        StartRecord();
    }
    static bool actionEnded = true;
    static void StartRecord()
    {
        int acting = (int)Get(game, "actingEnemyIndex");
        string actor = Kind == "HeroStrike" || Kind == "HeroGuard" ? "hero" : acting >= 0 ? Prop(((IList)Prop(Combat, "Enemies"))[acting], "Name").ToString() : "?";
        action = new ActionRecord { Kind = Kind, Actor = actor, StartTurn = Turn,
            Duration = (float)Prop(Timeline, "Duration"), HitTime = (float)Prop(Timeline, "HitTime") };
        actions.Add(action);
        actionEnded = false;
        recordRevision = Revision;
        Capture(0, "start");
    }

    static void RunUntilIdle(string encounter, ref bool pauseChecked, ref bool focusChecked, ref bool resizeChecked)
    {
        float step = 1f / fps;
        for (int frame = 0; frame < fps * 30; frame++)
        {
            if (!Busy && (Phase != "Enemy")) { EndRecord(); return; }
            if (!pauseChecked && Kind != "None" && !HitApplied && TimelineTime > .05f) { pauseChecked = true; CheckPause(); }
            else if (!focusChecked && Kind != "None" && !HitApplied && TimelineTime > .1f && actions.Count > 1) { focusChecked = true; CheckFocus(); }
            else if (!resizeChecked && Kind != "None" && HitApplied && actions.Count > 2) { resizeChecked = true; CheckResize(); }
            StepFrame(step);
        }
        Problem(encounter + ": combat did not become idle in 30 s; kind=" + Kind + " phase=" + Phase);
    }

    static void StepFrame(float step)
    {
        string kind0 = Kind; bool hit0 = HitApplied, busy0 = Busy; int rev0 = Revision;
        var before = Take();
        Call(game, "AdvanceAudio", step);
        Call(game, "AdvanceLivingAnimation", step);
        Call(game, "AdvanceCombatAmbient", step);
        Call(game, "AdvanceCombatAnimation", step);
        Call(game, "SyncMusic");
        StopTimer();
        string kind1 = Kind; bool hit1 = HitApplied, busy1 = Busy; int rev1 = Revision;
        var after = Take();
        bool restarted = rev1 != rev0;
        // Impact of the action that was running, then of a follow-up started in the same tick.
        bool oldImpact = kind0 != "None" && !hit0 && (restarted || hit1);
        bool newImpact = restarted && kind1 != "None" && hit1;
        if (oldImpact && action != null && !actionEnded) action.Hits++;
        if (!after.Same(before) && !oldImpact && !newImpact)
            Problem("model changed without an impact frame: " + kind0 + "->" + kind1 + " " + before.Describe() + " -> " + after.Describe());
        if (!restarted && kind1 != "None" && action != null && !actionEnded) action.Frames++;
        if (restarted || (kind1 == "None" && !actionEnded))
        {
            EndRecord(); BeginActionIfNeeded();
            if (newImpact && action != null && !actionEnded) action.Hits++;
        }
        foreach (var sound in PollSounds()) NoteSound(sound);
        CaptureKeyFrames();

        // Exactly what the game invalidates after this tick.
        bool full = busy0 && (!busy1 || hit0 != hit1 || kind0 != kind1 || restarted);
        var damage = full ? game.ClientRectangle : (Rectangle)Prop(game, "MotionDamage");
        var clock = Stopwatch.StartNew();
        Render(current, Rectangle.Empty);
        clock.Stop();
        fullTimes.Add(clock.Elapsed.TotalMilliseconds);
        fullPixels += (long)game.ClientSize.Width * game.ClientSize.Height;
        bandPixels += (long)damage.Width * damage.Height;
        if (!full)
        {
            motionFrames++;
            if (motionFrames % 4 == 0)
            {
                var band = Stopwatch.StartNew();
                Render(scratch, damage);
                band.Stop();
                bandTimes.Add(band.Elapsed.TotalMilliseconds);
            }
            Rectangle bounds;
            int stale = DiffOutside(previous, current, damage, out bounds);
            if (stale > 0)
            {
                staleFrames++;
                if (staleFrames <= 3)
                {
                    Problem("pixels changed outside MotionDamage " + damage + ": " + stale + " px in " + bounds + " during " + kind1 + " t=" + TimelineTime.ToString("F3", Invariant));
                    SaveFrame(current, "stale-" + staleFrames + "-" + kind1 + ".png");
                }
            }
        }
        var swap = previous; previous = current; current = swap;
    }

    static void EndRecord()
    {
        if (action == null || actionEnded) return;
        actionEnded = true;
        // One idle frame after completion: no effect may remain on screen.
        var tile = new Bitmap(previous.Width, previous.Height, PixelFormat.Format32bppPArgb);
        Render(tile, Rectangle.Empty);
        action.Tiles.Add(new Tile { Image = Crop(tile), Label = "after" });
        tile.Dispose();
        Write("  action " + action.Kind + " by " + action.Actor + " turn " + action.StartTurn + ": frames=" + action.Frames +
            " real=" + (action.Duration / 1.2f).ToString("F3", Invariant) + "s hit@" + (action.HitTime / 1.2f).ToString("F3", Invariant) +
            "s impacts=" + action.Hits + " sounds=[" + string.Join(", ", action.Sounds.ToArray()) + "]");
        // Every action owns exactly one impact callback (also transforms, summon and rest).
        if (action.Hits != 1) Problem(action.Kind + " applied " + action.Hits + " impact callbacks");
    }

    static void CaptureKeyFrames()
    {
        if (action == null || actionEnded || Kind != action.Kind) return;
        float t = TimelineTime, step = 1.2f / fps;
        var marks = new[] { action.HitTime * .5f, action.HitTime - step, action.HitTime + step * .5f, action.HitTime + .18f, action.Duration - step * 1.5f };
        var labels = new[] { "approach", "pre-hit", "impact", "+0.18", "end" };
        for (int i = 0; i < marks.Length; i++)
            if (t >= marks[i] && !action.Captured.Contains(i + 1)) { action.Captured.Add(i + 1); Capture(i + 1, labels[i]); }
    }

    static void Capture(int index, string label)
    {
        var tile = new Bitmap(previous.Width, previous.Height, PixelFormat.Format32bppPArgb);
        Render(tile, Rectangle.Empty);
        action.Tiles.Add(new Tile { Image = Crop(tile), Label = label + " " + TimelineTime.ToString("F2", Invariant) });
        tile.Dispose();
    }

    static Bitmap Crop(Bitmap frame)
    {
        // The combat band only, at half size, for compact contact sheets.
        var source = new Rectangle(0, 64, frame.Width, Math.Min(frame.Height - 64, 560));
        var tile = new Bitmap(source.Width / 2, source.Height / 2, PixelFormat.Format32bppPArgb);
        using (var graphics = Graphics.FromImage(tile))
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
            graphics.DrawImage(frame, new Rectangle(0, 0, tile.Width, tile.Height), source, GraphicsUnit.Pixel);
        }
        return tile;
    }

    static void SaveFrame(Bitmap frame, string name) { frame.Save(Path.Combine(output, name), ImageFormat.Png); }

    static void CheckPause()
    {
        float time = TimelineTime;
        var before = Take();
        Call(game, "SetInventory", "pause");
        bool soundsPaused = SoundsPaused();
        for (int i = 0; i < fps; i++) Call(game, "AdvanceCombatAnimation", 1f / fps);
        bool frozen = Math.Abs(TimelineTime - time) < 1e-6 && Take().Same(before);
        bool timer = ((Timer)Get(game, "combatTimer")).Enabled;
        Call(game, "SetInventory", "");
        StopTimer();
        Write("  pause during " + Kind + ": frozen=" + frozen + " soundsPaused=" + soundsPaused + " timerStopped=" + !timer);
        if (!frozen || !soundsPaused || timer) Problem("pause did not freeze action/sound/timer");
    }

    static void CheckFocus()
    {
        Call(game, "OnDeactivate", EventArgs.Empty);
        bool timer = ((Timer)Get(game, "combatTimer")).Enabled;
        bool paused = SoundsPaused() && (bool)Prop(Get(game, "music"), "IsPaused") == true;
        Call(game, "OnActivated", EventArgs.Empty);
        bool resumed = !visible || !game.ContainsFocus || ((Timer)Get(game, "combatTimer")).Enabled;
        StopTimer();
        Write("  focus loss during " + Kind + ": timerStopped=" + !timer + " soundsPaused=" + paused + " resumedTimer=" + resumed);
        if (timer || !paused || !resumed) Problem("focus loss did not stop/resume consistently");
    }

    static bool SoundsPaused()
    {
        foreach (var name in new[] { "cardSounds", "skaterSounds" })
            if (!(bool)Prop(Get(game, name), "IsPaused")) return false;
        foreach (var name in new[] { "tohaSound", "howlSound" })
            if (Get(Get(game, name), "voice") != null && !(bool)Get(Get(game, name), "paused")) return false;
        return true;
    }

    static void CheckResize()
    {
        foreach (var size in new[] { new Size(1920, 1080), new Size(900, 1400), new Size(1280, 840) })
        {
            game.ClientSize = size;
            using (var frame = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb))
            {
                Render(frame, Rectangle.Empty);
                if (size.Width != 1280) SaveFrame(frame, "resize-" + size.Width + "x" + size.Height + "-" + Kind + ".png");
            }
        }
        Render(previous, Rectangle.Empty);
        Write("  resize during " + Kind + " t=" + TimelineTime.ToString("F2", Invariant) + ": rendered 1920x1080, 900x1400, 1280x840");
    }

    static void CheckDeathSnapshots()
    {
        var deaths = Get(game, "deathVisuals");
        int snapshots = (int)Prop(deaths, "SnapshotCount");
        bool running = (bool)Prop(deaths, "IsRunning");
        Write("death visuals: running=" + running + " snapshots=" + snapshots);
        if (!running && snapshots != 0) Problem("finished death animation kept " + snapshots + " snapshot bitmap(s)");
    }

    static void Summarize(string encounter)
    {
        fullTimes.Sort(); bandTimes.Sort();
        Write("frames=" + fullTimes.Count + " motion-only=" + motionFrames + " stale-frames=" + staleFrames +
            " full-median-ms=" + Median(fullTimes) + " band-median-ms=" + Median(bandTimes) +
            " invalidated-share=" + (fullPixels == 0 ? "0" : (bandPixels / (double)fullPixels).ToString("F3", Invariant)));
        var counts = new Dictionary<string, int>();
        foreach (var record in actions) foreach (var sound in record.Sounds)
        {
            string key = record.Kind + ":" + sound.Split('@')[0];
            int n; counts.TryGetValue(key, out n); counts[key] = n + 1;
        }
        foreach (var pair in counts) Write("sound-map " + pair.Key + " x" + pair.Value);
        WriteResources();
        SaveSheet(encounter);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern int GetGuiResources(IntPtr process, int kind);
    static void WriteResources()
    {
        var line = new StringBuilder("resources");
        foreach (var name in new[] { "actorArtCache", "interfaceArtCache" })
        {
            var cache = Get(game, name);
            line.Append("; " + name + " count=" + Prop(cache, "Count") + " MiB=" +
                ((long)Prop(cache, "Bytes") / 1048576d).ToString("F2", Invariant) + " misses=" + Prop(cache, "Misses"));
        }
        using (var process = Process.GetCurrentProcess())
        {
            line.Append("; private-MiB=" + (process.PrivateMemorySize64 / 1048576d).ToString("F1", Invariant));
            try { line.Append("; gdi=" + GetGuiResources(process.Handle, 0) + " user=" + GetGuiResources(process.Handle, 1)); }
            catch (DllNotFoundException) { line.Append("; gdi=n/a"); }
            catch (EntryPointNotFoundException) { line.Append("; gdi=n/a"); }
        }
        line.Append("; gen0=" + GC.CollectionCount(0) + " gen2=" + GC.CollectionCount(2));
        Write(line.ToString());
    }

    static string Median(List<double> values)
    {
        return values.Count == 0 ? "-" : values[values.Count / 2].ToString("F2", Invariant);
    }

    static void SaveSheet(string encounter)
    {
        var rows = actions.FindAll(a => a.Tiles.Count > 0);
        if (rows.Count > 10) rows = rows.GetRange(0, 10);
        if (rows.Count == 0) return;
        int columns = 0; foreach (var row in rows) columns = Math.Max(columns, row.Tiles.Count);
        var size = rows[0].Tiles[0].Image.Size;
        using (var sheet = new Bitmap(columns * size.Width, rows.Count * (size.Height + 18), PixelFormat.Format32bppPArgb))
        using (var graphics = Graphics.FromImage(sheet))
        using (var font = new Font(FontFamily.GenericSansSerif, 10))
        {
            graphics.Clear(Color.Black);
            for (int r = 0; r < rows.Count; r++)
                for (int c = 0; c < rows[r].Tiles.Count; c++)
                {
                    int x = c * size.Width, y = r * (size.Height + 18);
                    graphics.DrawImage(rows[r].Tiles[c].Image, x, y + 18, size.Width, size.Height);
                    graphics.DrawString(rows[r].Kind + " · " + rows[r].Tiles[c].Label, font, Brushes.White, x + 4, y + 2);
                }
            sheet.Save(Path.Combine(output, encounter + "-actions.png"), ImageFormat.Png);
        }
    }

    static void CheckExitAndReentry(string encounter, Type kindType)
    {
        var deaths = Get(game, "deathVisuals");
        Call(game, "ReturnToMainMenu");
        int voices = (int)Prop(Get(game, "cardSounds"), "ActiveVoices") + (int)Prop(Get(game, "skaterSounds"), "ActiveVoices") +
            (Get(Get(game, "tohaSound"), "voice") != null ? 1 : 0) + (Get(Get(game, "howlSound"), "voice") != null ? 1 : 0);
        // The main menu keeps its own idle animation timer; combat state must be gone.
        bool clean = Get(game, "combat") == null && voices == 0 && (int)Prop(deaths, "SnapshotCount") == 0 && Kind == "None";
        Write("exit: combat cleared, voices=" + voices + ", snapshots=" + Prop(deaths, "SnapshotCount") + ", clean=" + clean);
        if (!clean) Problem("exit left combat state, sounds, snapshots or timer behind");
        gameType.GetMethod("StartTestEncounter", Private, null, new[] { kindType, typeof(int) }, null)
            .Invoke(game, new object[] { Enum.Parse(kindType, encounter), 3 });
        StopTimer();
        bool fresh = Kind == "None" && (int)Prop(Get(game, "skaterSounds"), "ActiveVoices") == 0 && (int)Prop(Combat, "Turn") == 1;
        Write("re-entry: fresh=" + fresh + " phase=" + Phase);
        if (!fresh && encounter != "Toha") Problem("re-entry did not start from a clean timeline");
        Call(game, "ReturnToMainMenu");
    }
}
