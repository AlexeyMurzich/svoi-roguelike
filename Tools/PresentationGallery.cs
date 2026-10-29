using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// Runs real WM_PAINT + FlipEx, then saves the exact retained raster for review.
// This is application instrumentation, not an offscreen-only renderer benchmark.
internal static class PresentationGallery
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static Type type;
    static Form game;
    static string directory;
    static object Read(string name) { return type.GetField(name, Private).GetValue(game); }
    static void Write(string name, object value) { type.GetField(name, Private).SetValue(game, value); }
    static void Call(string name, params object[] args) { type.GetMethod(name, Private).Invoke(game, args); }
    static void Capture(string label)
    {
        ((Timer)Read("combatTimer")).Stop();
        game.Invalidate(); game.Update();
        for (int retry = 0; retry < 10 && (bool)Read("presentationPending"); retry++)
        { System.Threading.Thread.Sleep(20); Call("InvalidateMotionFrame"); game.Update(); }
        ((Timer)Read("combatTimer")).Stop();
        if ((bool)Read("presentationPending")) throw new Exception("Frame remained deferred.");
        if ((string)Read("presentationFailure") != "") throw new Exception((string)Read("presentationFailure"));
        object presenter = Read("framePresenter");
        var frame = (Bitmap)presenter.GetType().GetField("frame", Private).GetValue(presenter);
        frame.Save(Path.Combine(directory, label + ".png"));
        Console.WriteLine(label + "; client=" + game.ClientSize + "; raster=" + frame.Size + "; presented=" + Read("presentedFrames"));
    }
    static int SimulateDeviceLoss(IntPtr self, IntPtr source, IntPtr destination, IntPtr window, IntPtr dirty, uint flags)
    { return unchecked((int)0x88760868); }
    static void VerifyDriverFallback()
    {
        object presenter = Read("framePresenter");
        var present = presenter.GetType().GetField("present", Private);
        present.SetValue(presenter, Delegate.CreateDelegate(present.FieldType, typeof(PresentationGallery).GetMethod("SimulateDeviceLoss", BindingFlags.Static | BindingFlags.NonPublic)));
        game.Invalidate(); game.Update(); game.Update();
        if (!(bool)Read("presenterFailed") || Read("framePresenter") != null || (string)Read("presentationFailure") == "")
            throw new Exception("Driver loss did not safely release and fall back.");
        using (var image = new Bitmap(game.Width, game.Height)) { game.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save(Path.Combine(directory, "device-loss-fallback.png")); }
        Console.WriteLine("device-loss; fallback=True; native-released=True; redraw=ok");
    }
    [STAThread] static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        directory = Path.GetFullPath(args[1]); Directory.CreateDirectory(directory);
        type = Assembly.LoadFrom(Path.GetFullPath(args[0])).GetType("ShpilSvoih.Game", true);
        using (game = (Form)Activator.CreateInstance(type))
        {
            game.Show(); game.ClientSize = new Size(1280, 840);
            Capture("main");
            Call("OpenCheckout"); Capture("checkout");
            Call("TakeBuff"); Call("OpenActMap"); Capture("map");
            foreach (string name in new[] { "Battle", "Dogs", "Hedgehogs", "IllyaToha", "Mimicrator", "Babai" })
            {
                Call("StartTest" + name); Capture(name);
                Call("AdvanceLivingAnimation", .6f); Call("AdvanceCombatAmbient", .6f);
                Capture(name + "-moving");
            }
            Call("StartTestIllyaToha"); Write("tohaVisualDark", true); Capture("Toha-dark");
            Call("StartTestDogs"); game.ClientSize = new Size(3840, 2160); Capture("Dogs-4k");
            Call("ReturnToMainMenu"); game.ClientSize = new Size(1280, 840); Capture("main-return");
            VerifyDriverFallback();
            game.Close();
        }
    }
}
