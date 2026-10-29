using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using ShpilSvoih;

// Real menu playback: driver starvation, physical playback drift and render load.
internal static class MenuAudioProbe
{
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    static Game game;
    static System.Windows.Forms.Timer timer;
    static readonly Stopwatch watch = new Stopwatch();
    static object output;
    static string log = "";
    static int phase = -1, previousUnderruns, hero = 3;
    static double began, lastResize, lastGc;
    static uint beganBytes;
    static int pausedTicks;
    static double lastStall;
    static uint beganVoiceBytes;
    static readonly string[] phases = { "idle-menu", "redraw-menu", "resize-and-GC", "50ms-service-stalls" };
    static T Read<T>(object target, string name) { return (T)target.GetType().GetField(name, Private).GetValue(target); }
    static void Call(string name, params object[] args) { typeof(Game).GetMethod(name, Private).Invoke(game, args); }
    static void Begin()
    {
        phase++; began = watch.Elapsed.TotalSeconds; beganBytes = LoopingMusicVoice.OutputPlayedBytes;
        pausedTicks = 0;
        beganVoiceBytes = Read<IMusicVoice>(Read<MusicPlayer>(game, "music"), "voice") is LoopingMusicVoice ? ((LoopingMusicVoice)Read<IMusicVoice>(Read<MusicPlayer>(game, "music"), "voice")).PlayedBytes : 0;
        previousUnderruns = Read<int>(output, "underruns");
    }
    static void Tick(object sender, EventArgs args)
    {
        try
        {
            if (watch.Elapsed.TotalSeconds > 45) throw new TimeoutException("Menu audio probe timed out");
            var music = Read<MusicPlayer>(game, "music");
            if (music.LastError != "") throw new Exception(music.LastError);
            if (phase < 0)
            {
                if (music.IsLoading) return;
                output = typeof(LoopingMusicVoice).GetField("output", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                if (output == null) return;
                Begin();
            }
            double now = watch.Elapsed.TotalSeconds;
            if (!game.ContainsFocus) game.Activate();
            if (music.IsPaused) pausedTicks++;
            if (phase >= 1) { game.Invalidate(); game.Update(); }
            if (phase == 3 && now - lastStall > .5)
            {
                lastStall = now;
                lock (Read<object>(output, "gate")) Thread.Sleep(50);
            }
            if (phase == 2)
            {
                if (now - lastResize > .25)
                { lastResize = now; game.ClientSize = game.ClientSize.Width == 1280 ? new System.Drawing.Size(1600, 900) : new System.Drawing.Size(1280, 840); }
                if (now - lastGc > .5)
                { lastGc = now; GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true); }
            }
            if (now - began < 6) return;
            int underruns = Read<int>(output, "underruns") - previousUnderruns;
            double gap = Read<long>(output, "maxServiceGap") * 1000.0 / Stopwatch.Frequency;
            double elapsed = watch.Elapsed.TotalSeconds - began;
            double played = unchecked(LoopingMusicVoice.OutputPlayedBytes - beganBytes) / 176400.0;
            log += "hero=" + hero + "; " + phases[phase] + ": underruns=" + underruns + "; max-service-ms=" + gap.ToString("F2") +
                "; wall-seconds=" + elapsed.ToString("F3") + "; played-seconds=" + played.ToString("F3") +
                "; paused-ticks=" + pausedTicks + "; consumed-bytes=" + unchecked(((LoopingMusicVoice)Read<IMusicVoice>(music, "voice")).PlayedBytes - beganVoiceBytes) + "; gain=" + music.Gain + "; output-error=" + LoopingMusicVoice.OutputError + "\r\n";
            if (phase == phases.Length - 1) { timer.Stop(); game.Close(); }
            else Begin();
        }
        catch (Exception error) { log += "FAIL " + error + "\r\n"; Environment.ExitCode = 1; timer.Stop(); game.Close(); }
    }
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length > 1) hero = int.Parse(args[1]);
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        using (game = new Game())
        using (timer = new System.Windows.Forms.Timer { Interval = 20 })
        {
            game.ShowInTaskbar = false;
            timer.Tick += Tick;
            game.Shown += delegate { watch.Start(); Call("ChooseMenuHero", hero); timer.Start(); };
            Application.Run(game);
        }
        File.WriteAllText(args[0], log); Console.WriteLine(log); return Environment.ExitCode;
    }
}
