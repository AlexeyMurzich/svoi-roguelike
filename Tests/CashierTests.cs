using System;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;

namespace ShpilSvoih
{
    public static class CashierTests
    {
        sealed class Voice : IMusicVoice, IAudioVolumeVoice
        {
            public bool Paused = true, Disposed;
            public double Volume;
            public int Changes;
            public void SetPaused(bool value) { Paused = value; Changes++; }
            public void SetVolume(double value) { Volume = value; }
            public void Dispose() { Disposed = true; }
        }
        public static void Clock()
        {
            foreach (int hz in new[] { 30, 60, 120 })
            {
                var motion = new CashierAnimation(); int starts = 0;
                for (int i = 0; i < hz * 60; i++) if (motion.Advance(1f / hz)) starts++;
                TestSuite.Check(starts == 4 && motion.Events == 4, "one event per scheduled start at every frame rate");
                TestSuite.Check(Math.Abs(motion.Seconds - 60) < .00001, "clock is independent of frame rate");
                double time = motion.Seconds;
                motion.Advance(float.NaN); motion.Advance(float.PositiveInfinity); motion.Advance(-1);
                TestSuite.Check(motion.Seconds == time, "invalid animation deltas ignored");
                motion.Reset(); motion.Advance(5.9f);
                TestSuite.Check(!motion.Glitching && motion.Events == 0, "cashier does not glitch immediately on entry");
                TestSuite.Check(motion.Advance(.12f) && motion.SplitFrame && motion.PoseSeconds == motion.LastGlitch, "start is reported once and freezes the breathing pose");
                motion.Advance(.12f);
                TestSuite.Check(motion.Glitching && !motion.SplitFrame, "recovery gaps prevent a continuous screen flash");
                motion.Advance(2f);
                TestSuite.Check(!motion.Glitching && motion.NextGlitch > motion.Seconds + 10, "two-second event followed by a long quiet interval");
                motion.Reset(); TestSuite.Check(!motion.Advance(1000) && motion.Events > 1, "large time jump never replays expired audio events");
            }
        }
        public static void Sound()
        {
            int loads = 0, made = 0; Voice voice = null;
            var clip = new MusicPcm(44100, 2, new byte[4410 * 4]);
            using (var player = new CashierSound(() => { loads++; return clip; }, pcm => { made++; return voice = new Voice(); }))
            {
                player.Play(); player.Play();
                TestSuite.Check(loads == 1 && made == 1 && !voice.Paused, "one cached clip and no overlapping glitch voices");
                TestSuite.Check(voice.Volume == AudioLevels.Cashier, "explicit quiet software level");
                player.SetPaused(true); player.Advance(3);
                TestSuite.Check(player.Active && voice.Paused, "pause holds voice and expiry clock");
                player.SetPaused(false); player.Advance(.1f);
                TestSuite.Check(player.Active && made == 1, "resume keeps existing voice");
                player.Advance(.2f);
                TestSuite.Check(!player.Active && voice.Disposed, "finished one-shot releases its voice/device lease");
                player.Play(); TestSuite.Check(loads == 1 && made == 2, "later event reuses decoded PCM");
                player.Stop(); player.Stop(); TestSuite.Check(voice.Disposed && !player.Active, "exit stop is idempotent");
                player.Dispose(); player.Play(); TestSuite.Check(made == 2, "disposed sound cannot restart");
            }
            using (var player = new CashierSound(() => clip, pcm => { throw new InvalidOperationException("No device"); }))
            {
                player.Play(); TestSuite.Check(!player.Active && player.LastError == "No device", "audio failure does not break checkout");
            }
            var embedded = CashierSound.LoadEmbedded();
            TestSuite.Check(embedded.SampleRate == 44100 && embedded.Channels == 2 && embedded.Data.Length == 88200 * 4, "complete supplied two-second effect embedded");
            using (var sha = SHA256.Create())
                TestSuite.Check(BitConverter.ToString(sha.ComputeHash(embedded.Data)).Replace("-", "").ToLowerInvariant() == "6fd4f8ebc39b9b818c892ec7f8e4007f9d035e0301ab620d99f6572989c927fb", "runtime matches approved prepared samples");
        }
    }
    public static partial class UiTests
    {
        public static void TestCashierCheckout()
        {
            using (var game = new Game())
            {
                game.CreateControl(); Call(game, "OpenCheckout");
                var clock = Read<CashierAnimation>(game, "cashierAnimation");
                var sound = Read<CashierSound>(game, "cashierSound");
                foreach (var size in Sizes)
                {
                    game.ClientSize = size; clock.Reset(); string original;
                    using (var frame = Render(game)) { original = Digest(frame); frame.Save(Path.Combine(TestSuite.OutputDirectory, "cashier-" + size.Width + "x" + size.Height + ".png")); }
                    using (var frame = Render(game)) TestSuite.Check(Digest(frame) == original && clock.Seconds == 0 && !sound.Active, "rendering never advances time or starts audio");
                    Call(game, "AdvanceLivingAnimation", 1.1f);
                    using (var frame = Render(game)) TestSuite.Check(Digest(frame) != original, "cashier breathes without an input action");
                    clock.Reset(); Call(game, "AdvanceLivingAnimation", 6.025f);
                    using (var frame = Render(game)) frame.Save(Path.Combine(TestSuite.OutputDirectory, "cashier-glitch-" + size.Width + "x" + size.Height + ".png"));
                    TestSuite.Check(clock.Glitching && clock.SplitFrame && !sound.Active, "offscreen animation stays silent");
                    Call(game, "SetInventory", "pause"); string paused;
                    using (var frame = Render(game)) paused = Digest(frame);
                    double seconds = clock.Seconds; Call(game, "AdvanceLivingAnimation", 3f);
                    using (var frame = Render(game)) TestSuite.Check(clock.Seconds == seconds && Digest(frame) == paused, "pause freezes clock and glitch pixels");
                    Call(game, "SetInventory", "");
                    using (Render(game)) { }
                    Click(game, 337 + 302 + 140, 400);
                    using (Render(game)) { }
                    Click(game, 800, 741);
                    TestSuite.Check(Read<HeroState>(game, "currentHero").Effects.Chosen != null, "relic choice remains clickable during glitch");
                    Key(game, System.Windows.Forms.Keys.Escape);
                    TestSuite.Check(Read<bool>(game, "mapScreen") && !sound.Active && clock.Seconds == 0, "leaving checkout resets glitch clock and sound");
                    Call(game, "OpenCheckout");
                }
                Call(game, "ReturnToMainMenu");
                TestSuite.Check(!sound.Active && clock.Events == 0, "menu transition clears cashier state");
                Call(game, "OpenCheckout"); game.ClientSize = new Size(1280, 840);
                using (var partial = new Bitmap(1280, 840, System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
                using (var full = new Bitmap(1280, 840, System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
                {
                    var all = new Rectangle(0, 0, 1280, 840);
                    PaintClient(game, partial, all);
                    foreach (float dt in new[] { 1.1f, 4.925f, .18f, .14f, 2f })
                    {
                        Call(game, "AdvanceLivingAnimation", dt);
                        var damage = (Rectangle)typeof(Game).GetProperty("MotionDamage", PrivateInstance).GetValue(game, null);
                        TestSuite.Check(damage.Width < 400, "cashier animation only invalidates its own column");
                        PaintClient(game, partial, damage); PaintClient(game, full, all);
                        TestSuite.Check(SamePixels(partial, full), "partial cashier breathing/glitch/recovery equals a full redraw");
                    }
                }
            }
        }
    }
}
