using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using ShpilSvoih;

internal static class DoctorMusicProbe
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
    static Game game;
    static Timer timer;
    static string output, log = "";
    static int stage = -2;
    static LoopingMusicVoice song;
    static uint pausedPosition;
    static LoopingMusicVoice CurrentVoice() { return (LoopingMusicVoice)typeof(MusicPlayer).GetField("voice", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Read<MusicPlayer>("music")); }
    static bool inTick;
    static double since;
    static readonly Stopwatch clock = new Stopwatch();
    static T Read<T>(string name) { return (T)typeof(Game).GetField(name, Private).GetValue(game); }
    static void Write(string name, object value) { typeof(Game).GetField(name, Private).SetValue(game, value); }
    static object Call(string name, params object[] args) { return typeof(Game).GetMethod(name, Private).Invoke(game, args); }
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        log += "PASS " + message + "\r\n";
    }
    static void Capture(string name)
    {
        game.Refresh();
        using (var frame = new Bitmap(game.ClientSize.Width, game.ClientSize.Height))
        {
            game.DrawToBitmap(frame, new Rectangle(Point.Empty, frame.Size));
            frame.Save(Path.Combine(output, name + ".png"));
        }
    }
    static void Next() { stage++; since = clock.Elapsed.TotalSeconds; }
    static void Tick(object sender, EventArgs args)
    {
        if (inTick) return;
        inTick = true;
        try
        {
            if (clock.Elapsed.TotalSeconds > 55) throw new TimeoutException("Doctor probe timeout");
            double elapsed = clock.Elapsed.TotalSeconds - since;
            var fight = Read<CombatState>("combat");
            var timeline = Read<AttackTimeline>("attackTimeline");
            if (stage == -2)
            { Call("StartTestDoctorEvent"); Next(); }
            else if (stage == -1 && !Read<MusicPlayer>("music").IsLoading && !Read<MusicPlayer>("music").IsPaused)
            {
                song = CurrentVoice();
                Check(song != null && Read<MusicPlayer>("music").Track == "doctors" && Read<MusicPlayer>("music").LastError == "", "choice screen loads user song asynchronously");
                Capture("event"); Next();
            }
            else if (stage == 0 && elapsed > .35)
            {
                Check(song.PlayedBytes > 0, "native user song advances before choice");
                var run = Read<MapRun>("mapRun"); var visit = run.DoctorsAt(run.Pending.Id);
                Check(visit.Choice == DoctorChoice.None, "native unique event offers two choices");
                visit.Fight(); Call("EnsureCombat"); Capture("battle-idle");
                Check(Object.ReferenceEquals(song, CurrentVoice()), "starting battle preserves same voice and position");
                Call("EndCombatTurn"); Next();
            }
            else if (stage == 1 && timeline.IsRunning && timeline.Time > .28f)
            { Capture("female-attack"); Call("SetInventory", "pause"); Next(); }
            else if (stage == 2 && elapsed > .3)
            {
                Check(fight.Hero.Health == 50, "pause before impact preserves HP");
                Check(Read<MusicPlayer>("music").IsPaused, "pause suspends doctor song");
                pausedPosition = song.PlayedBytes; stage = 20; since = clock.Elapsed.TotalSeconds;
            }
            else if (stage == 20 && elapsed > .2)
            {
                Check(song.PlayedBytes == pausedPosition, "native pause holds song position");
                Call("SetInventory", "");
                Check(Object.ReferenceEquals(song, CurrentVoice()) && !Read<MusicPlayer>("music").IsPaused, "resume continues same doctor voice");
                stage = 3; since = clock.Elapsed.TotalSeconds;
            }
            else if (stage == 3 && Read<int>("actingEnemyIndex") == 1 && timeline.Time > .28f)
            { Capture("male-attack"); Next(); }
            else if (stage == 4 && fight.Turn == 2 && !timeline.IsRunning)
            {
                Check(fight.Hero.Health == 41 && fight.Poison == 1 && fight.Bleeding == 1, "5 direct plus separate poison and bleeding at turn start");
                Capture("statuses"); Call("OnDeactivate", EventArgs.Empty); Next();
            }
            else if (stage == 5 && elapsed > .25)
            {
                Check(!Read<Timer>("combatTimer").Enabled, "focus loss freezes motion"); Check(Read<MusicPlayer>("music").IsPaused, "focus loss pauses doctor theme");
                Call("OnActivated", EventArgs.Empty);
                fight.Hand[0].Damage = 999; fight.Hand[0].Attack = true; fight.Hand[0].GoldCost = 0; fight.SelectTarget(0);
                Call("PlayCombatCard", 0); Next();
            }
            else if (stage == 6 && !timeline.IsRunning && !Read<DeathVisuals>("deathVisuals").IsRunning)
            {
                fight.Hand[0].Damage = 999; fight.Hand[0].Attack = true; fight.Hand[0].GoldCost = 0; fight.SelectTarget(1);
                Call("PlayCombatCard", 0); Next();
            }
            else if (stage == 7 && fight.Phase == CombatPhase.Victory && !timeline.IsRunning && !Read<DeathVisuals>("deathVisuals").IsRunning)
            {
                Check(fight.Hero.Health == fight.Hero.MaxHealth && fight.Hero.Doctors.Items.Count == 1, "native victory full heal and exactly one drop");
                Check(Object.ReferenceEquals(song, CurrentVoice()) && Read<MusicPlayer>("music").Track == "doctors", "victory retains same scene theme without restart");
                Capture("victory"); Call("SetInventory", "relics"); Capture("relic-inventory");
                foreach (DoctorDrop item in Enum.GetValues(typeof(DoctorDrop))) fight.Hero.Doctors.Give(item);
                Capture("all-relics");
                fight.Hero.ReceiveDamage(fight.Hero.Health);
                Check(fight.Hero.Doctors.HeartUsed && fight.Hero.Health > 0, "used heart remains visible in gray");
                Capture("all-relics-used-heart"); Call("SetInventory", "");
                var run = Read<MapRun>("mapRun"); Call("CompleteMapRoom"); Check(run.DoctorsCompleted && run.Pending == null, "native return completes unique event once");
                Call("SyncMusic"); Check(Read<MusicPlayer>("music").Track == "" && CurrentVoice() == null, "leaving event stops and releases song");
                Call("ReturnToMainMenu"); Call("StartTestDoctorEvent");
                run = Read<MapRun>("mapRun"); var hero = Read<HeroState>("currentHero"); hero.ReceiveDamage(30);
                run.DoctorsAt(run.Pending.Id).Treat(hero); Capture("treatment"); Check(hero.Health == hero.MaxHealth, "native treatment full heal");
                Check(Read<MusicPlayer>("music").Track == "doctors", "peaceful result retains event theme");
                Call("CompleteMapRoom"); Call("ReturnToMainMenu");
                Check(Read<CombatState>("combat") == null, "menu clears battle"); timer.Stop(); game.Close();
            }
        }
        catch (Exception error) { log += "FAIL " + error + "\r\n"; Environment.ExitCode = 1; timer.Stop(); game.Close(); }
        finally { inTick = false; }
    }
    [STAThread] static int Main(string[] args)
    {
        output = Path.GetFullPath(args[0]); Directory.CreateDirectory(output);
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        using (game = new Game()) using (timer = new Timer { Interval = 15 })
        {
            game.ClientSize = new Size(1280, 840); game.ShowInTaskbar = false;
            timer.Tick += Tick; game.Shown += delegate { clock.Start(); timer.Start(); }; Application.Run(game);
        }
        Check(LoopingMusicVoice.OpenDeviceCount == 0, "native close releases audio");
        File.WriteAllText(Path.Combine(output, "doctors-music-live.txt"), log); Console.WriteLine(log);
        return Environment.ExitCode;
    }
}
