using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using ShpilSvoih;

internal static class SkaterProbe
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
    static Game game;
    static Timer timer;
    static string output, log = "";
    static int stage;
    static bool inTick;
    static double since;
    static readonly Stopwatch clock = new Stopwatch();
    static T Read<T>(string name) { return (T)typeof(Game).GetField(name, Private).GetValue(game); }
    static object Call(string name, params object[] args) { return typeof(Game).GetMethod(name, Private).Invoke(game, args); }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); log += "PASS " + message + "\r\n"; }
    static void Capture(string name)
    {
        game.Refresh(); using (var image = new Bitmap(game.ClientSize.Width, game.ClientSize.Height))
        { game.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save(Path.Combine(output, name + ".png")); }
    }
    static void Next() { stage++; since = clock.Elapsed.TotalSeconds; }
    static void Kill(int index)
    {
        var fight = Read<CombatState>("combat");
        var area = (RectangleF)Call("EnemyTargetArea", index);
        game.Refresh(); var point = game.DrawingViewport.ToClient(new PointF(area.X + area.Width / 2, area.Y + area.Height / 2));
        Call("OnClick", game, new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0));
        Check(fight.SelectedTarget == index, "native click selects target " + index);
        var card = fight.Hand[0]; card.Attack = true; card.Skill = false; card.Damage = 1000; card.GoldCost = 0; card.ManaCost = 0;
        Call("PlayCombatCard", 0);
    }
    static void Tick(object sender, EventArgs args)
    {
        if (inTick) return; inTick = true;
        try
        {
            if (clock.Elapsed.TotalSeconds > 40) throw new TimeoutException("Skater probe timeout");
            var fight = Read<CombatState>("combat"); var timeline = Read<AttackTimeline>("attackTimeline");
            double elapsed = clock.Elapsed.TotalSeconds - since;
            if (stage == 0)
            {
                Call("StartTestSkaters"); fight = Read<CombatState>("combat");
                Check(fight.Enemies.Count == 3 && fight.Enemies[0].Name == "Магистр скейтер" && fight.Enemies[1].Name == "Падаван скейтер", "native approved trio and labels");
                Capture("skaters-ready"); Call("EndCombatTurn"); Next();
            }
            else if (stage == 1 && timeline.Time > .3f)
            { Capture("master-slash"); Call("SetInventory", "pause"); Next(); }
            else if (stage == 2 && elapsed > .25)
            { Check(fight.Hero.Health == 50, "pause before saber impact holds HP"); Call("SetInventory", ""); Next(); }
            else if (stage == 3 && Read<int>("actingEnemyIndex") == 1 && timeline.Time > .25f)
            { Capture("padawan-left-shot"); Check(fight.Hero.Health == 38, "master impact applied once"); Next(); }
            else if (stage == 4 && Read<int>("actingEnemyIndex") == 2 && timeline.Time > .25f)
            { Capture("padawan-right-shot"); Check(fight.Hero.Health == 34, "first blaster impact applied once"); Next(); }
            else if (stage == 5 && fight.Turn == 2 && !timeline.IsRunning)
            { Check(fight.Hero.Health == 30, "three distinct impacts total twenty"); Call("OnDeactivate", EventArgs.Empty); Next(); }
            else if (stage == 6 && elapsed > .2)
            { Check(!Read<Timer>("combatTimer").Enabled, "focus loss freezes motion"); Call("OnActivated", EventArgs.Empty); Kill(1); Next(); }
            else if (stage == 7 && !timeline.IsRunning && !Read<DeathVisuals>("deathVisuals").IsRunning)
            { Check(!fight.Enemies[1].IsAlive && fight.Enemies[2].IsAlive, "first padawan dies independently"); Capture("one-padawan-defeated"); Kill(2); Next(); }
            else if (stage == 8 && !timeline.IsRunning && !Read<DeathVisuals>("deathVisuals").IsRunning)
            { Check(!fight.Enemies[2].IsAlive && fight.Enemies[0].IsAlive, "second padawan dies independently"); Kill(0); Next(); }
            else if (stage == 9 && fight.Phase == CombatPhase.Victory && !timeline.IsRunning && !Read<DeathVisuals>("deathVisuals").IsRunning)
            {
                Capture("victory"); Check(fight.Hero.Health == 30 && fight.Hero.Doctors.Items.Count == 0, "ordinary victory does not grant doctor treatment or relic");
                Call("ReturnToMainMenu"); Check(Read<CombatState>("combat") == null, "menu clears battle"); timer.Stop(); game.Close();
            }
        }
        catch (Exception e) { log += "FAIL " + e + "\r\n"; Environment.ExitCode = 1; timer.Stop(); game.Close(); }
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
        Check(LoopingMusicVoice.OpenDeviceCount == 0, "close releases audio");
        File.WriteAllText(Path.Combine(output, "skaters-live.txt"), log); Console.WriteLine(log); return Environment.ExitCode;
    }
}
