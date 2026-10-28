using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// Visible, focused window. Reports paint starts, not display scanout / GPU presentation.
internal static class LiveFrameBenchmark
{
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    static Type gameType;
    static Form game;
    static readonly List<double> frames = new List<double>();
    static readonly List<double> ticks = new List<double>();
    static readonly Stopwatch clock = new Stopwatch();
    static Process process;
    static int phase = -1;
    static double phaseStart, cpuStart, sampleSeconds = 7;
    static long lastPaint, lastTick;
    static bool mapUp;
    static object timeline, guard;
    static Type timelineType;
    static object Call(string name, params object[] args) { return gameType.GetMethod(name, Private).Invoke(game, args); }
    static object Read(string name) { return gameType.GetField(name, Private).GetValue(game); }
    static string Number(double value) { return value.ToString("F2", CultureInfo.InvariantCulture); }
    static double Percentile(List<double> samples, double percentile)
    {
        samples.Sort(); return samples.Count == 0 ? 0 : samples[Math.Min(samples.Count - 1, (int)(samples.Count * percentile))];
    }
    static void BeginPhase(int next)
    {
        phase = next; frames.Clear(); ticks.Clear(); lastPaint = lastTick = 0;
        phaseStart = clock.Elapsed.TotalSeconds; cpuStart = process.TotalProcessorTime.TotalMilliseconds;
    }
    static void KeepGuardLoop()
    {
        if (phase == 1 && !(bool)timelineType.GetProperty("IsRunning").GetValue(timeline, null))
            timelineType.GetMethod("Start").Invoke(timeline, new object[] { guard, (Action)(() => { }) });
    }
    static void Report()
    {
        double seconds = clock.Elapsed.TotalSeconds - phaseStart;
        Console.WriteLine((phase == 0 ? "idle" : phase == 1 ? "animation-loop" : "map-scroll") + "; kind=" + guard + "; focused=" + game.ContainsFocus +
            "; client=" + game.ClientSize.Width + "x" + game.ClientSize.Height +
            "; paints-per-sec=" + Number(frames.Count / seconds) +
            "; frame-p50-ms=" + Number(Percentile(frames, .5)) + "; frame-p95-ms=" + Number(Percentile(frames, .95)) +
            "; tick-p50-ms=" + Number(Percentile(ticks, .5)) +
            "; cpu-ms-per-sec=" + Number((process.TotalProcessorTime.TotalMilliseconds - cpuStart) / seconds));
    }
    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        if (args.Length > 1) sampleSeconds = double.Parse(args[1], CultureInfo.InvariantCulture);
        gameType = Assembly.LoadFrom(Path.GetFullPath(args[0])).GetType("ShpilSvoih.Game", true);
        gameType.GetField("routeSeeds", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, new Random(12345));
        using (process = Process.GetCurrentProcess())
        using (game = (Form)Activator.CreateInstance(gameType))
        using (var control = new Timer { Interval = 100 })
        {
            Call("StartFullscreen");
            timeline = Read("attackTimeline"); timelineType = timeline.GetType();
            guard = Enum.Parse(timelineType.GetProperty("Kind").PropertyType, args.Length > 2 ? args[2] : "HeroGuard");
            game.Paint += (sender, e) =>
            {
                long now = Stopwatch.GetTimestamp();
                if (phase >= 0 && lastPaint != 0) frames.Add((now - lastPaint) * 1000d / Stopwatch.Frequency);
                lastPaint = now;
            };
            ((Timer)Read("combatTimer")).Tick += (sender, e) =>
            {
                long now = Stopwatch.GetTimestamp();
                if (phase >= 0 && lastTick != 0) ticks.Add((now - lastTick) * 1000d / Stopwatch.Frequency);
                lastTick = now; KeepGuardLoop();
            };
            control.Tick += (sender, e) =>
            {
                if (phase == 2 && !(bool)gameType.GetProperty("MapMoving", Private).GetValue(game, null))
                {
                    mapUp = !mapUp;
                    float maximum = (float)gameType.GetProperty("MapMaxScroll", Private).GetValue(game, null);
                    Call("MoveMapTo", mapUp ? maximum : 0f);
                }
                if (phase < 0 && clock.Elapsed.TotalSeconds > 2) BeginPhase(0);
                else if (phase >= 0 && clock.Elapsed.TotalSeconds - phaseStart > sampleSeconds)
                {
                    Report();
                    if (phase == 2) { control.Stop(); game.Close(); }
                    else if (phase == 1)
                    {
                        Call("ReturnToMainMenu"); Call("OpenCheckout"); Call("TakeBuff"); Call("OpenActMap");
                        BeginPhase(2); mapUp = false; Call("MoveMapTo", 0f);
                    }
                    else
                    {
                        BeginPhase(1); timelineType.GetMethod("Cancel").Invoke(timeline, null);
                        KeepGuardLoop(); Call("WakeMotion");
                    }
                }
            };
            game.Shown += (sender, e) => { Call(args.Length > 3 ? args[3] : "StartTestIllyaToha"); clock.Start(); control.Start(); };
            Application.Run(game);
        }
    }
}
