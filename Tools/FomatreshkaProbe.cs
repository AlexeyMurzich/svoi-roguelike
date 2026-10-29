using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using ShpilSvoih;

internal static class FomatreshkaProbe
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
    static Game game; static Timer timer; static string output, log = "";
    static readonly Stopwatch clock = new Stopwatch();
    static int stage; static double since, frozen;
    static T Read<T>(string name) { return (T)typeof(Game).GetField(name, Private).GetValue(game); }
    static void Call(string name, params object[] args) { typeof(Game).GetMethod(name, Private).Invoke(game, args); }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); log += "PASS " + message + "\r\n"; }
    static void Next() { stage++; since = clock.Elapsed.TotalSeconds; }
    static bool Busy { get { return (bool)typeof(Game).GetProperty("CombatBusy", Private).GetValue(game, null); } }
    static void Capture(string name)
    {
        game.Invalidate(); game.Update();
        for (int i = 0; i < 12 && Read<bool>("presentationPending"); i++)
        { System.Threading.Thread.Sleep(15); Call("InvalidateMotionFrame"); game.Update(); }
        Check(!Read<bool>("presentationPending") && !Read<bool>("presenterFailed"), "native presentation: " + name);
        var presenter = Read<object>("framePresenter"); Check(presenter != null, "GPU active: " + name);
        ((Bitmap)presenter.GetType().GetField("frame", Private).GetValue(presenter)).Save(Path.Combine(output, name + ".png"));
    }
    static void Tick(object sender, EventArgs args)
    {
        try
        {
            if (clock.Elapsed.TotalSeconds > 50) throw new TimeoutException("Fomatreshka probe timeout");
            if (!game.ContainsFocus) { game.Activate(); return; }
            double elapsed = clock.Elapsed.TotalSeconds - since;
            var fight = Read<CombatState>("combat"); var timeline = Read<AttackTimeline>("attackTimeline");
            if (stage == 0) { Call("StartTestFomatreshka"); Capture("fomatreshka-idle"); Call("EndCombatTurn"); Next(); }
            else if (stage == 1 && timeline.Kind == AttackAnimation.FomatreshkaSummon && timeline.Time > .25f)
            { Capture("fomatreshka-summon"); Next(); }
            else if (stage == 2 && !Busy && fight.Turn == 2)
            { Capture("fomatreshka-fans"); Check(fight.Enemies.Count == 4 && fight.Hero.Health == 50, "first turn summons three without damage"); Call("EndCombatTurn"); Next(); }
            else if (stage == 3 && timeline.Kind == AttackAnimation.FomatreshkaDrink && timeline.Time > .25f)
            { Capture("fomatreshka-gorilla"); Next(); }
            else if (stage == 4 && !Busy && fight.Turn == 3)
            {
                Check(fight.Enemies[0].Vigor == 6 && fight.Hero.Health == 41, "three minions attack before drink");
                Capture("fomatreshka-vigor");
                game.ClientSize = new Size(3840, 2160); Capture("fomatreshka-4k"); game.ClientSize = new Size(1280, 840);
                Call("SetInventory", "pause"); frozen = Read<double>("livingSeconds"); Next();
            }
            else if (stage == 5 && elapsed > .3)
            {
                Check(Read<double>("livingSeconds") == frozen, "pause freezes glow"); Call("SetInventory", "");
                Call("OnDeactivate", EventArgs.Empty); frozen = Read<double>("livingSeconds"); Next();
            }
            else if (stage == 6 && elapsed > .3)
            {
                Check(Read<double>("livingSeconds") == frozen && !Read<Timer>("combatTimer").Enabled, "focus loss freezes all motion");
                Call("OnActivated", EventArgs.Empty); Call("EndCombatTurn"); Next();
            }
            else if (stage == 7 && timeline.Kind == AttackAnimation.FomatreshkaShot && timeline.Time > .25f)
            { Capture("fomatreshka-empress"); Next(); }
            else if (stage == 8 && !Busy && fight.Turn == 4)
            {
                Check(fight.Enemies[0].Vigor == 0 && fight.Hero.Health == 18, "shot and three minions deal 23, consume vigor");
                fight.Hand[0].Attack = true; fight.Hand[0].Damage = 100; fight.SelectTarget(0); Call("PlayCombatCard", 0); Next();
            }
            else if (stage == 9 && fight.Phase == CombatPhase.Victory && Read<float>("fomatreshkaFlee") > .1f)
            { Capture("fomatreshka-flee"); Check(fight.Enemies[1].Fled && fight.Enemies[2].Fled && fight.Enemies[3].Fled, "all surviving fans flee immediately"); Next(); }
            else if (stage == 10 && !Busy)
            {
                Capture("fomatreshka-victory"); Call("ReturnToMainMenu");
                Check(Read<float>("fomatreshkaFlee") == -1 && Read<CombatState>("combat") == null, "exit clears combat clocks and state");
                timer.Stop(); game.Close();
            }
        }
        catch (Exception error) { log += "FAIL " + error + "\r\n"; Environment.ExitCode = 1; timer.Stop(); game.Close(); }
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
        File.WriteAllText(Path.Combine(output, "fomatreshka-live.txt"), log); Console.WriteLine(log);
        return Environment.ExitCode;
    }
}
