using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using ShpilSvoih;

// Real-window input/FlipEx probe, run against the packaged game assembly.
internal static class CombatDockProbe
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
    static Game game;
    static Timer timer;
    static string output, log = "";
    static Stopwatch clock = new Stopwatch();
    static int stage, initialCards;
    static decimal initialMoney;
    static double since, pausedLiving;
    static T Read<T>(string field) { return (T)typeof(Game).GetField(field, Private).GetValue(game); }
    static void Call(string name, params object[] args) { typeof(Game).GetMethod(name, Private).Invoke(game, args); }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); log += "PASS " + message + "\r\n"; }
    static void Next() { stage++; since = clock.Elapsed.TotalSeconds; }
    static void Click(RectangleF area)
    {
        var point = game.DrawingViewport.ToClient(new PointF(area.X + area.Width / 2, area.Y + area.Height / 2));
        Call("OnClick", game, new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0));
    }
    static void Hover(int index)
    {
        var fight = Read<CombatState>("combat");
        var slot = CombatLayout.CardSlot(game.DrawingViewport.Width, game.DrawingViewport.Height, fight.Hand.Count, index);
        var point = game.DrawingViewport.ToClient(new PointF(slot.X + slot.Width / 2, slot.Y + 55));
        Call("OnMapMouseMove", game, new MouseEventArgs(MouseButtons.None, 0, point.X, point.Y, 0));
    }
    static void Capture(string name)
    {
        game.Invalidate(); game.Update();
        for (int retry = 0; retry < 12 && Read<bool>("presentationPending"); retry++)
        { System.Threading.Thread.Sleep(15); Call("InvalidateMotionFrame"); game.Update(); }
        Check(!Read<bool>("presentationPending") && !Read<bool>("presenterFailed"), "native frame presented: " + name);
        object presenter = Read<object>("framePresenter");
        Check(presenter != null, "GPU presenter remains active: " + name);
        var frame = (Bitmap)presenter.GetType().GetField("frame", Private).GetValue(presenter);
        frame.Save(Path.Combine(output, name + ".png"));
    }
    static void Tick(object sender, EventArgs args)
    {
        try
        {
            if (clock.Elapsed.TotalSeconds > 30) throw new TimeoutException("Dock probe exceeded 30 seconds");
            if (!game.ContainsFocus) { game.Activate(); return; }
            var fight = Read<CombatState>("combat");
            float w = game.DrawingViewport.Width, h = game.DrawingViewport.Height;
            double elapsed = clock.Elapsed.TotalSeconds - since;
            if (stage == 0)
            {
                Call("StartTestBattle"); Next();
            }
            else if (stage == 1 && elapsed > .4)
            {
                Capture("dock-shuten-native");
                initialCards = fight.Hand.Count; initialMoney = fight.Hero.Money;
                Hover(0); Check(Read<int>("hoveredCombatCard") == 0, "native pointer selects first card");
                Capture("dock-hover-native");
                var area = (RectangleF)typeof(Game).GetMethod("HandCardSlot", Private).Invoke(game, new object[] { 0, true });
                Click(area); Check(Read<AttackTimeline>("attackTimeline").IsRunning, "raised native card click starts one action");
                Next();
            }
            else if (stage == 2 && elapsed > 1.5)
            {
                Check(fight.Hand.Count == initialCards - 1 && fight.Hero.Money == initialMoney - 10, "native card applies exactly one cost");
                Click(CombatLayout.DrawPile(w, h));
                Check(Read<string>("inventoryView") == "draw", "native draw pile opens correct overlay");
                pausedLiving = Read<double>("livingSeconds"); Capture("dock-draw-native"); Next();
            }
            else if (stage == 3 && elapsed > .3)
            {
                Check(Read<double>("livingSeconds") == pausedLiving, "pile overlay freezes ambient time");
                Call("SetInventory", ""); game.Invalidate(); game.Update();
                Click(CombatLayout.EndTurn(w, h));
                Check(fight.Phase == CombatPhase.Enemy && Read<int>("hoveredCombatCard") == -1, "native end turn clears hover and starts enemy turn");
                Capture("dock-enemy-turn-native"); Next();
            }
            else if (stage == 4 && elapsed > 1.8)
            {
                Check(fight.Phase == CombatPhase.Player && fight.Turn == 2, "native enemy action returns control");
                typeof(Game).GetMethod("StartTestEncounter", Private, null, new[] { typeof(EnemyKind), typeof(int) }, null).Invoke(game, new object[] { EnemyKind.Rat, 0 });
                Capture("dock-energy-native"); Hover(0); Capture("dock-energy-hover-native"); Next();
            }
            else if (stage == 5 && elapsed > .3)
            {
                game.ClientSize = new Size(3840, 2160); Capture("dock-4k-native");
                game.ClientSize = new Size(1280, 840); Call("ReturnToMainMenu");
                Check(Read<int>("hoveredCombatCard") == -1, "menu transition clears card selection");
                timer.Stop(); game.Close();
            }
        }
        catch (Exception failure) { log += "FAIL " + failure + "\r\n"; Environment.ExitCode = 1; timer.Stop(); game.Close(); }
    }
    [STAThread] static int Main(string[] args)
    {
        output = Path.GetFullPath(args[0]); Directory.CreateDirectory(output);
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        using (game = new Game())
        using (timer = new Timer { Interval = 25 })
        {
            game.ClientSize = new Size(1280, 840); game.ShowInTaskbar = false;
            timer.Tick += Tick; game.Shown += delegate { clock.Start(); timer.Start(); };
            Application.Run(game);
        }
        var cleanup = Stopwatch.StartNew();
        while (LoopingMusicVoice.OpenDeviceCount != 0 && cleanup.ElapsedMilliseconds < 1000) System.Threading.Thread.Sleep(5);
        Check(LoopingMusicVoice.OpenDeviceCount == 0, "native close releases audio output");
        File.WriteAllText(Path.Combine(output, "dock-live.txt"), log); Console.WriteLine(log);
        return Environment.ExitCode;
    }
}
