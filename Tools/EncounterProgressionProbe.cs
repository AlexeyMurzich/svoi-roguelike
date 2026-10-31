using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using ShpilSvoih;

// Native-window regression against the packaged game assembly, not a replacement renderer.
internal static class EncounterProgressionProbe
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
    static Game game;
    static Timer timer;
    static MapRun run;
    static int[] remaining;
    static readonly Stopwatch clock = new Stopwatch();
    static readonly HashSet<EnemyKind> easy = new HashSet<EnemyKind>();
    static string output, log = "";
    static int stage, completed, nextId;
    static double frozen, since;
    static T Read<T>(string name) { return (T)typeof(Game).GetField(name, Private).GetValue(game); }
    static void Write(string name, object value) { typeof(Game).GetField(name, Private).SetValue(game, value); }
    static void Call(string name, params object[] args) { typeof(Game).GetMethod(name, Private).Invoke(game, args); }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); log += "PASS " + message + "\r\n"; }
    static bool Busy { get { return (bool)typeof(Game).GetProperty("CombatBusy", Private).GetValue(game, null); } }
    static void Capture(string name)
    {
        game.Invalidate(); game.Update();
        for (int retry = 0; retry < 12 && Read<bool>("presentationPending"); retry++)
        { System.Threading.Thread.Sleep(15); Call("InvalidateMotionFrame"); game.Update(); }
        Check(!Read<bool>("presentationPending") && !Read<bool>("presenterFailed"), "native GPU frame: " + name);
        object presenter = Read<object>("framePresenter");
        Check(presenter != null, "GPU presenter active: " + name);
        var frame = (Bitmap)presenter.GetType().GetField("frame", Private).GetValue(presenter);
        frame.Save(Path.Combine(output, name + ".png"));
    }
    static void Tick(object sender, EventArgs args)
    {
        try
        {
            if (clock.Elapsed.TotalSeconds > 90) throw new TimeoutException("Progression probe exceeded 90 seconds");
            if (stage == 0)
            {
                Write("selected", 3); Call("OpenCheckout"); Call("TakeBuff"); Call("OpenActMap");
                run = new MapRun(ActMap.Generate(42)); Write("mapRun", run);
                var intel = Read<Dictionary<int, IntelNode>>("mapIntel"); intel.Clear();
                foreach (var node in run.Map.Nodes)
                    intel.Add(node.Id, new IntelNode { Kind = node.Kind == RoomKind.Fight ? IntelKind.Fight : IntelKind.Other, Revealed = true, Identity = node.Kind == RoomKind.Fight ? OrdinaryEncounters.Preview(run, node.Id) : "fixture" });
                remaining = new int[run.Map.Nodes.Count];
                foreach (var node in run.Map.Nodes.Reverse())
                    if (node.Exits.Count > 0) remaining[node.Id] = node.Exits.Max(id => remaining[id] + (run.Map.Nodes[id].Kind == RoomKind.Fight ? 1 : 0));
                stage = 1; return;
            }
            if (stage == 1)
            {
                if (run.Finished)
                {
                    Check(completed >= 5 && easy.Count == 3, "route includes all three different easy fights and repeated hard fights");
                    Check(run.Map.Nodes.Count(n => n.Kind == RoomKind.Fight) == ActMap.Generate(42).Nodes.Count(n => n.Kind == RoomKind.Fight), "room types remain unchanged");
                    timer.Stop(); game.Close(); return;
                }
                nextId = run.Current.Exits.OrderByDescending(id => remaining[id] + (run.Map.Nodes[id].Kind == RoomKind.Fight ? 1 : 0)).First();
                var node = run.Map.Nodes[nextId];
                float focus = (float)typeof(Game).GetMethod("FocusMap", Private).Invoke(game, new object[] { node }); Call("SnapMap", focus);
                if (node.Kind == RoomKind.Fight)
                {
                    Check(Read<Dictionary<int, IntelNode>>("mapIntel")[nextId].Identity == EnemyCatalog.Name(run.OrdinaryEnemyAt(nextId)), "native investigation matches cached next encounter " + (completed + 1));
                    Write("hoveredMapNode", nextId); Capture("map-before-" + (completed + 1));
                }
                Call("EnterMapNode", nextId); stage = 2; return;
            }
            if (stage == 2)
            {
                var node = run.Map.Nodes[nextId];
                if (node.Kind != RoomKind.Fight && node.Kind != RoomKind.Elite) { stage = 5; return; }
                var fight = Read<CombatState>("combat");
                if (node.Kind == RoomKind.Fight)
                {
                    Check(fight.Enemy == run.OrdinaryEnemyAt(nextId), "combat opens exact cached opponent " + (completed + 1));
                    Check(completed < 3 ? easy.Add(fight.Enemy) && fight.Enemy != EnemyKind.Babai : fight.Enemy == EnemyKind.Babai, "native fight obeys pool progression " + (completed + 1));
                    Capture("battle-" + (completed + 1));
                    if (completed == 3)
                    {
                        game.ClientSize = new Size(3840, 2160); Capture("first-hard-4k"); game.ClientSize = new Size(1280, 840);
                        Call("SetInventory", "pause"); frozen = Read<double>("livingSeconds"); since = clock.Elapsed.TotalSeconds; stage = 3; return;
                    }
                }
                stage = 4; return;
            }
            if (stage == 3)
            {
                if (clock.Elapsed.TotalSeconds - since < .3) return;
                Check(Read<double>("livingSeconds") == frozen && run.CompletedOrdinaryFights == 3, "native pause freezes time and does not consume hard encounter");
                Call("SetInventory", ""); Call("OnDeactivate", EventArgs.Empty);
                Check(!Read<Timer>("combatTimer").Enabled, "focus loss stops timer without reroll");
                Call("OnActivated", EventArgs.Empty); stage = 4; return;
            }
            if (stage == 4)
            {
                var fight = Read<CombatState>("combat");
                if (Busy) { Call("AdvanceCombatAnimation", 1f); return; }
                if (fight.Phase == CombatPhase.Victory) { stage = 5; return; }
                Check(fight.Phase != CombatPhase.Defeat, "native route fixture survives");
                foreach (var card in fight.Hero.Deck) { card.Attack = true; card.Skill = false; card.Damage = 1000; card.ManaCost = 0; card.GoldCost = 0; }
                int at = fight.Hand.ToList().FindIndex(c => fight.CanPlay(fight.Hand.IndexOf(c)));
                if (at >= 0) Call("PlayCombatCard", at); else Call("EndCombatTurn");
                return;
            }
            if (stage == 5)
            {
                bool ordinary = run.Map.Nodes[nextId].Kind == RoomKind.Fight;
                var elapsed = Stopwatch.StartNew(); Call("CompleteMapRoom"); elapsed.Stop();
                if (ordinary) completed++;
                Check(run.CompletedOrdinaryFights == completed && run.Current.Id == nextId, "native room completion and history update");
                log += "INFO native completion including UI refresh: " + elapsed.Elapsed.TotalMilliseconds.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) + " ms\r\n";
                foreach (var node in run.Map.Nodes.Where(n => n.Kind == RoomKind.Fight && !run.Visited(n.Id)))
                    Check(Read<Dictionary<int, IntelNode>>("mapIntel")[node.Id].Identity == OrdinaryEncounters.Preview(run, node.Id), "future native intel refreshed " + node.Id);
                stage = 1;
            }
        }
        catch (Exception error) { log += "FAIL " + error + "\r\n"; Environment.ExitCode = 1; timer.Stop(); game.Close(); }
    }
    [STAThread] static int Main(string[] args)
    {
        output = Path.GetFullPath(args[0]); Directory.CreateDirectory(output);
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        using (game = new Game()) using (timer = new Timer { Interval = 30 })
        {
            game.ClientSize = new Size(1280, 840); game.ShowInTaskbar = false;
            timer.Tick += Tick; game.Shown += delegate { clock.Start(); timer.Start(); }; Application.Run(game);
        }
        Check(LoopingMusicVoice.OpenDeviceCount == 0, "closing native window releases audio");
        File.WriteAllText(Path.Combine(output, "encounters-live.txt"), log); Console.WriteLine(log);
        return Environment.ExitCode;
    }
}
