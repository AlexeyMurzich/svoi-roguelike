using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using ShpilSvoih;

internal static class CashierProbe
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
    static Game game; static Timer timer;
    static string output, log = "";
    static readonly Stopwatch clock = new Stopwatch();
    static int stage; static double since, frozen;
    static uint played;
    static T Read<T>(string name) { return (T)typeof(Game).GetField(name, Private).GetValue(game); }
    static void Call(string name, params object[] args) { typeof(Game).GetMethod(name, Private).Invoke(game, args); }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); log += "PASS " + message + "\r\n"; }
    static void Next() { stage++; since = clock.Elapsed.TotalSeconds; }
    static void Capture(string name)
    {
        game.Invalidate(); game.Update();
        for (int i = 0; i < 12 && Read<bool>("presentationPending"); i++)
        { System.Threading.Thread.Sleep(15); Call("InvalidateMotionFrame"); game.Update(); }
        Check(!Read<bool>("presentationPending") && !Read<bool>("presenterFailed"), "native frame presented: " + name);
        var presenter = Read<object>("framePresenter");
        Check(presenter != null, "GPU presenter active: " + name);
        var frame = (Bitmap)presenter.GetType().GetField("frame", Private).GetValue(presenter);
        frame.Save(Path.Combine(output, name + ".png"));
    }
    static void Tick(object sender, EventArgs args)
    {
        try
        {
            if (clock.Elapsed.TotalSeconds > 25) throw new TimeoutException("Cashier probe timeout");
            if (!game.ContainsFocus) { game.Activate(); return; }
            double elapsed = clock.Elapsed.TotalSeconds - since;
            var animation = Read<CashierAnimation>("cashierAnimation");
            var sound = Read<CashierSound>("cashierSound");
            if (stage == 0) { game.StartTestCheckout(); Next(); }
            else if (stage == 1 && elapsed > .4)
            {
                Capture("cashier-native");
                Check(animation.Seconds > 0 && !sound.Active, "natural idle advances without immediate glitch audio");
                Call("AdvanceLivingAnimation", (float)(6.025 - animation.Seconds));
                Check(animation.Glitching && animation.SplitFrame && sound.Active, "visual glitch and supplied one-shot start together");
                Check(LoopingMusicVoice.OpenDeviceCount == 1, "glitch uses the existing single output mixer");
                Capture("cashier-glitch-native"); Next();
            }
            else if (stage == 2 && elapsed > .15)
            {
                Call("SetInventory", "pause"); frozen = animation.Seconds; Next();
            }
            else if (stage == 3 && elapsed > .12) { played = LoopingMusicVoice.OutputPlayedBytes; Next(); }
            else if (stage == 4 && elapsed > .3)
            {
                Check(animation.Seconds == frozen && sound.Active, "pause freezes cashier clock and keeps its voice");
                Check(LoopingMusicVoice.OutputPlayedBytes == played, "pause holds physical audio output position");
                Call("SetInventory", ""); Next();
            }
            else if (stage == 5 && elapsed > .16)
            {
                Check(animation.Seconds > frozen && sound.Active, "resume preserves and advances the same glitch");
                Call("OnDeactivate", EventArgs.Empty); frozen = animation.Seconds; Next();
            }
            else if (stage == 6 && elapsed > .12) { played = LoopingMusicVoice.OutputPlayedBytes; Next(); }
            else if (stage == 7 && elapsed > .3)
            {
                Check(animation.Seconds == frozen && !Read<Timer>("combatTimer").Enabled, "deactivation freezes animation and stops its timer");
                Check(LoopingMusicVoice.OutputPlayedBytes == played, "deactivation holds physical glitch audio position");
                Call("OnActivated", EventArgs.Empty);
                Check(Read<Timer>("combatTimer").Enabled, "activation wakes the cashier timer"); Next();
            }
            else if (stage == 8 && elapsed > 2.2)
            {
                Check(!sound.Active && LoopingMusicVoice.OpenDeviceCount == 0, "finished one-shot releases audio output in quiet checkout");
                Call("TakeBuff"); var hero = Read<HeroState>("currentHero");
                int cards = hero.Deck.Count; decimal money = hero.Money;
                Call("TakeBuff");
                Check(hero.Effects.Chosen != null && hero.Deck.Count == cards && hero.Money == money, "relic payment and starter deck remain one-time");
                Capture("cashier-relic-native");
                Call("OpenActMap");
                Check(!sound.Active && animation.Events == 0, "leaving checkout clears sound and animation schedule");
                game.StartTestCheckout(); game.ClientSize = new Size(3840, 2160);
                Capture("cashier-4k-native");
                game.ClientSize = new Size(1280, 840);
                Call("AdvanceLivingAnimation", 6.025f);
                Check(sound.Active, "re-entry can start a fresh glitch");
                Call("ReturnToMainMenu");
                Check(!sound.Active && LoopingMusicVoice.OpenDeviceCount == 0, "menu exit stops an active glitch immediately");
                timer.Stop(); game.Close();
            }
        }
        catch (Exception e) { log += "FAIL " + e + "\r\n"; Environment.ExitCode = 1; timer.Stop(); game.Close(); }
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
        Check(LoopingMusicVoice.OpenDeviceCount == 0, "native close releases audio");
        File.WriteAllText(Path.Combine(output, "cashier-live.txt"), log); Console.WriteLine(log);
        return Environment.ExitCode;
    }
}
