using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// Run in a separate process per build so image caches and JIT warmup do not mix.
internal static class RenderBenchmark
{
    static Type gameType;
    static int frameCount = 120;
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    static void Call(object game, string name, params object[] args) { gameType.GetMethod(name, Private).Invoke(game, args); }
    static object Read(object game, string name) { return gameType.GetField(name, Private).GetValue(game); }
    [STAThread]
    static void Main(string[] args)
    {
        int width = args.Length > 1 ? int.Parse(args[1]) : 1280;
        int height = args.Length > 2 ? int.Parse(args[2]) : 840;
        if (args.Length > 3) frameCount = int.Parse(args[3]);
        Console.WriteLine("viewport=" + width + "x" + height + "; frames=" + frameCount + "; process64=" + Environment.Is64BitProcess);
        var startup = Stopwatch.StartNew();
        var assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
        gameType = assembly.GetType("ShpilSvoih.Game", true);
        gameType.GetField("routeSeeds", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, new Random(12345));
        using (var game = (Form)Activator.CreateInstance(gameType))
        using (var frame = new Bitmap(width, height, PixelFormat.Format32bppPArgb))
        using (var graphics = Graphics.FromImage(frame))
        {
            game.CreateControl(); game.ClientSize = frame.Size;
            startup.Stop();
            Console.WriteLine("startup-ms=" + startup.Elapsed.TotalMilliseconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
            Call(game, "StartTestDogs");
            Measure(game, graphics, "combat-idle");
            Call(game, "ReturnToMainMenu");
            Call(game, "OpenCheckout"); Call(game, "TakeBuff"); Call(game, "OpenActMap");
            Measure(game, graphics, "map");
            Call(game, "StartTestMimicrator");
            foreach (string form in new[] { "Fairy", "Beacon", "Bear", "Mussel" })
            {
                var field = gameType.GetField("mimicratorVisualForm", Private);
                field.SetValue(game, Enum.Parse(field.FieldType, form));
                Measure(game, graphics, "mimicrator-" + form);
            }
            object store = Read(game, "assetStore");
            var images = (IDictionary)store.GetType().GetField("images", Private).GetValue(store);
            long bytes = 0;
            foreach (Image im in images.Values) bytes += (long)im.Width * im.Height * 4;
            PrintCache(game, "actorArtCache");
            PrintCache(game, "interfaceArtCache");
            using (var process = Process.GetCurrentProcess())
                Console.WriteLine("private-MiB=" + (process.PrivateMemorySize64 / 1048576d).ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
            Console.WriteLine("decoded-images=" + images.Count + "; pixel-MiB=" + (bytes / 1048576d).ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
        }
    }
    static void PrintCache(Form game, string name)
    {
        var field = gameType.GetField(name, Private);
        if (field == null) return;
        object cache = field.GetValue(game);
        string line = name;
        foreach (string property in new[] { "Count", "Bytes", "Hits", "Misses", "Evictions" })
        {
            var info = cache.GetType().GetProperty(property);
            if (info != null) line += "; " + property + "=" + info.GetValue(cache, null);
        }
        Console.WriteLine(line);
    }

    static void Measure(Form game, Graphics graphics, string label)
    {
        using (var paint = new PaintEventArgs(graphics, new Rectangle(Point.Empty, game.ClientSize)))
        {
            for (int i = 0; i < 12; i++) Call(game, "OnPaint", paint);
            var samples = new double[frameCount];
            int collections = GC.CollectionCount(0);
            for (int i = 0; i < samples.Length; i++)
            {
                Call(game, "AdvanceLivingAnimation", 1f / 30);
                Call(game, "AdvanceCombatAmbient", 1f / 30);
                var clock = Stopwatch.StartNew(); Call(game, "OnPaint", paint); clock.Stop();
                samples[i] = clock.Elapsed.TotalMilliseconds;
            }
            Array.Sort(samples);
            Console.WriteLine(label + ": median-ms=" + samples[samples.Length / 2].ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                + "; p95-ms=" + samples[(int)(samples.Length * .95)].ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                + "; gen0=" + (GC.CollectionCount(0) - collections));
        }
    }
}
