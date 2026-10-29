using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace ShpilSvoih
{
    public static partial class UiTests
    {
        static double OutputReserveMilliseconds(object output)
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var slots = (Array)output.GetType().GetField("slots", flags).GetValue(output);
            var scratch = (short[])output.GetType().GetField("scratch", flags).GetValue(output);
            return slots.Length * scratch.Length * 2 * 1000.0 / 176400;
        }
        public static void TestMenuAudioContinuity()
        {
            using (var game = new Game())
            {
                game.Show(); game.Activate(); Call(game, "ChooseMenuHero", 3);
                var music = Read<MusicPlayer>(game, "music");
                var loading = Stopwatch.StartNew();
                while (music.IsLoading && loading.ElapsedMilliseconds < 15000)
                { Application.DoEvents(); Thread.Sleep(5); }
                TestSuite.Check(!music.IsLoading && music.LastError == "", "real menu song loads successfully");
                // Collect discarded loading buffers before timing continuous output.
                GC.Collect(); Thread.Sleep(150); Application.DoEvents();
                var output = typeof(LoopingMusicVoice).GetField("output", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                TestSuite.Check(output != null, "menu owns the native output");
                TestSuite.Check(OutputReserveMilliseconds(output) >= 200 && OutputReserveMilliseconds(output) <= 300,
                    "menu stream has a bounded reserve for long renderer/GC stalls");
                var underruns = output.GetType().GetField("underruns", BindingFlags.Instance | BindingFlags.NonPublic);
                int before = (int)underruns.GetValue(output);
                uint position = LoopingMusicVoice.OutputPlayedBytes;
                var driverGate = output.GetType().GetField("gate", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(output);
                var clock = Stopwatch.StartNew(); double resize = 0, collect = 0, stall = 0;
                while (clock.Elapsed.TotalSeconds < 3)
                {
                    game.Invalidate(); game.Update(); Application.DoEvents();
                    double time = clock.Elapsed.TotalSeconds;
                    if (time - resize > .4)
                    { resize = time; game.ClientSize = game.ClientSize.Width == 1280 ? new Size(1440, 900) : new Size(1280, 840); }
                    if (time - collect > .7) { collect = time; GC.Collect(); }
                    // Bounded scheduling delay, shorter than the new output reserve.
                    if (time - stall > .6) { stall = time; lock (driverGate) Thread.Sleep(25); }
                    Thread.Sleep(5);
                }
                double elapsed = clock.Elapsed.TotalSeconds;
                double played = unchecked(LoopingMusicVoice.OutputPlayedBytes - position) / 176400.0;
                int lost = (int)underruns.GetValue(output) - before;
                TestSuite.Note("Menu output: " + played.ToString("F3") + "s audio / " + elapsed.ToString("F3") + "s wall; underruns=" + lost);
                TestSuite.Check(!music.IsPaused && game.ContainsFocus, "continuity fixture retains foreground playback");
                TestSuite.Check(played >= elapsed * .98 && played <= elapsed * 1.02, "menu music keeps real-time speed under redraw, resize and collection");
                TestSuite.Check(lost == 0 && LoopingMusicVoice.OutputError == "", "menu output queue remains fed during UI and GC load");
                TestSuite.Check(music.Gain == AudioLevels.Music, "continuity fix does not modulate music gain");
                Call(game, "StartTestMimicrator"); loading.Restart();
                while (music.IsLoading && loading.ElapsedMilliseconds < 15000)
                { Application.DoEvents(); Thread.Sleep(5); }
                TestSuite.Check(!music.IsLoading && music.LastError == "", "battle song loads after menu transport closes");
                output = typeof(LoopingMusicVoice).GetField("output", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                TestSuite.Check(output != null && OutputReserveMilliseconds(output) <= 100,
                    "battle effects retain a short queue after leaving the menu");
                game.Close();
            }
            TestSuite.Check(LoopingMusicVoice.OpenDeviceCount == 0, "menu closes its event-driven output cleanly");
        }
    }
}
