using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using ShpilSvoih;

// Review the two approved PNG relics through the packaged game and its native presenter.
internal static class PupsoletRelicProbe
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
    static Game game;
    static Timer timer;
    static readonly Stopwatch clock = new Stopwatch();
    static readonly Size[] sizes = { new Size(800, 560), new Size(1280, 840), new Size(1920, 1080), new Size(2560, 1080), new Size(900, 1400), new Size(3840, 2160) };
    static string output, log = "";
    static int stage, relic, size;
    static T Read<T>(string field) { return (T)typeof(Game).GetField(field, Private).GetValue(game); }
    static void Write(string field, object value) { typeof(Game).GetField(field, Private).SetValue(game, value); }
    static void Call(string name, params object[] args) { typeof(Game).GetMethod(name, Private).Invoke(game, args); }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); log += "PASS " + message + "\r\n"; }
    static void Capture(string name)
    {
        game.Invalidate(); game.Update();
        for (int retry = 0; retry < 12 && Read<bool>("presentationPending"); retry++)
        { System.Threading.Thread.Sleep(15); Call("InvalidateMotionFrame"); game.Update(); }
        Check(!Read<bool>("presentationPending") && !Read<bool>("presenterFailed"), "native GPU frame: " + name);
        var presenter = Read<object>("framePresenter"); Check(presenter != null, "GPU presenter active");
        var frame = (Bitmap)presenter.GetType().GetField("frame", Private).GetValue(presenter); frame.Save(Path.Combine(output, name + ".png"));
    }
    static void Tick(object sender, EventArgs args)
    {
        try
        {
            if (clock.Elapsed.TotalSeconds > 60) throw new TimeoutException("Relic probe exceeded 60 seconds");
            string icon = relic == 0 ? "soten-kisshun" : "zoltraak";
            if (stage == 0)
            {
                Write("selected", 0); Call("OpenCheckout"); Write("buffCursor", relic);
                var images = Read<Dictionary<string, Image>>("relicImages");
                Check(images.Count == 13 && images.ContainsKey(icon), "approved icon is registered and owned");
                var owned = Read<ImageAssets>("assetStore").Get(Path.Combine("relics", icon + ".png"), "relics." + icon + ".png");
                Check(ReferenceEquals(images[icon], owned), "image ownership has no duplicate decode");
                Capture("checkout-" + relic);
                Call("TakeBuff"); Call("OpenActMap"); stage = 1; return;
            }
            if (stage == 1)
            {
                game.ClientSize = sizes[size];
                var chosen = Read<HeroState>("currentHero").Effects.Chosen;
                Check(chosen.Icon == icon && chosen.Id == (relic == 0 ? "shield" : "zalt"), "chosen relic points at approved PNG without changing rules");
                Write("hoveredRelic", true); Capture("map-relic-" + relic + "-" + size);
                Call("SetInventory", "relics"); Capture("inventory-" + relic + "-" + size); Call("SetInventory", "");
                size++; if (size < sizes.Length) return;
                game.ClientSize = sizes[1]; Call("EnterMapNode", Read<MapRun>("mapRun").Current.Exits[0]); stage = 2; return;
            }
            if (stage == 2)
            {
                Check(Read<CombatState>("combat") != null, "ordinary combat starts with selected relic");
                Write("hoveredRelic", true); Capture("battle-relic-" + relic);
                Call("SetInventory", "pause"); Check(Read<HeroState>("currentHero").Effects.Chosen.Icon == icon, "pause preserves chosen icon"); Call("SetInventory", "");
                Call("OnDeactivate", EventArgs.Empty); Check(!Read<Timer>("combatTimer").Enabled, "focus loss stops combat timer"); Call("OnActivated", EventArgs.Empty);
                if (relic == 0) { relic++; size = 0; stage = 0; Call("ReturnToMainMenu"); return; }
                timer.Stop(); game.Close();
            }
        }
        catch (Exception error) { log += "FAIL " + error + "\r\n"; Environment.ExitCode = 1; timer.Stop(); game.Close(); }
    }
    [STAThread] static int Main(string[] args)
    {
        output = Path.GetFullPath(args[0]); Directory.CreateDirectory(output);
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        using (game = new Game()) using (timer = new Timer { Interval = 100 })
        {
            game.ClientSize = sizes[1]; game.ShowInTaskbar = false;
            timer.Tick += Tick; game.Shown += delegate { clock.Start(); timer.Start(); }; Application.Run(game);
        }
        Check(LoopingMusicVoice.OpenDeviceCount == 0, "native close releases audio");
        File.WriteAllText(Path.Combine(output, "relics-live.txt"), log); Console.WriteLine(log);
        return Environment.ExitCode;
    }
}
