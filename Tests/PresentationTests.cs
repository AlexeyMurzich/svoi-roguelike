using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ShpilSvoih
{
    public static partial class UiTests
    {
        static readonly BindingFlags NativePrivate = BindingFlags.NonPublic | BindingFlags.Instance;
        static object NativeRead(object target, string name) { return target.GetType().GetField(name, NativePrivate).GetValue(target); }
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int NativeSurface(IntPtr self, uint width, uint height, int format, int pool, out IntPtr surface, IntPtr shared);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int NativeReadback(IntPtr self, IntPtr source, IntPtr target);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int NativeLock(IntPtr self, out NativeLocked data, IntPtr rect, uint flags);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int NativeUnlock(IntPtr self);
        [StructLayout(LayoutKind.Sequential)] struct NativeLocked { public int Pitch; public IntPtr Bits; }
        static T NativeMethod<T>(IntPtr instance, int slot) where T : class
        {
            return (T)(object)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), slot * IntPtr.Size), typeof(T));
        }
        static void NativeCheck(int result) { if (result < 0) Marshal.ThrowExceptionForHR(result); }
        static Bitmap ClientReference(Game game)
        {
            var frame = new Bitmap(game.ClientSize.Width, game.ClientSize.Height, PixelFormat.Format32bppPArgb);
            PaintClient(game, frame, game.ClientRectangle);
            return frame;
        }
        static int NativeBusy(IntPtr self, IntPtr source, IntPtr destination, IntPtr window, IntPtr dirty, uint flags)
        { return unchecked((int)0x8876021C); }
        public static void TestNativePresentation()
        {
            using (var game = new Game())
            {
                game.Show(); game.ClientSize = new Size(1280, 840); Call(game, "StartTestDogs");
                Read<Timer>(game, "combatTimer").Stop();
                Read<AttackTimeline>(game, "attackTimeline").Cancel();
                game.Invalidate(); game.Update();
                TestSuite.Check(Read<string>(game, "presentationFailure") == "" && Read<long>(game, "presentedFrames") > 0, "real HWND uses flip presentation without fallback");
                var presenter = Read<object>(game, "framePresenter");
                using (var expected = ClientReference(game))
                {
                    ((Bitmap)NativeRead(presenter, "frame")).Save(Path.Combine(TestSuite.OutputDirectory, "native-actual.png"));
                    expected.Save(Path.Combine(TestSuite.OutputDirectory, "native-expected.png"));
                    TestSuite.Check(SamePixels((Bitmap)NativeRead(presenter, "frame"), expected), "native raster frame equals the reference renderer pixel-for-pixel");
                }
                for (int i = 0; i < 6; i++)
                {
                    Call(game, "AdvanceLivingAnimation", .09f);
                    var damage = (Rectangle)typeof(Game).GetProperty("MotionDamage", PrivateInstance).GetValue(game, null);
                    game.Invalidate(damage); game.Update();
                    using (var expected = ClientReference(game))
                        TestSuite.Check(SamePixels((Bitmap)NativeRead(presenter, "frame"), expected), "retained native partial frame has no stale pixels");
                }
                ((Bitmap)NativeRead(presenter, "frame")).Save(Path.Combine(TestSuite.OutputDirectory, "native-dogs.png"));
                NativeGpuPixels(presenter, new Size(1280, 840));
                Call(game, "SetInventory", "pause");
                var presentMethod = presenter.GetType().GetField("present", NativePrivate);
                var originalPresent = presentMethod.GetValue(presenter);
                presentMethod.SetValue(presenter, Delegate.CreateDelegate(presentMethod.FieldType, typeof(UiTests).GetMethod("NativeBusy", BindingFlags.Static | BindingFlags.NonPublic)));
                int hp = Read<CombatState>(game, "combat").Hero.Health, turn = Read<CombatState>(game, "combat").Turn;
                game.Invalidate(); game.Update();
                TestSuite.Check(Read<bool>(game, "presentationPending") && !Read<bool>(game, "presenterFailed"), "busy GPU defers without treating it as a device failure");
                TestSuite.Check(Read<Timer>(game, "combatTimer").Enabled, "last paused frame retries through the existing timer");
                presentMethod.SetValue(presenter, originalPresent);
                for (int retry = 0; retry < 10 && Read<bool>(game, "presentationPending"); retry++)
                { System.Threading.Thread.Sleep(20); Call(game, "OnMotionTick", game, EventArgs.Empty); game.Update(); }
                TestSuite.Check(!Read<bool>(game, "presentationPending"), "deferred paused frame eventually presents");
                TestSuite.Check(Read<CombatState>(game, "combat").Hero.Health == hp && Read<CombatState>(game, "combat").Turn == turn, "presentation retries never advance paused gameplay");
                Call(game, "SetInventory", ""); Read<Timer>(game, "combatTimer").Stop();
                var oldFrame = (Bitmap)NativeRead(presenter, "frame");
                game.ClientSize = new Size(2560, 1080); game.Invalidate(); game.Update();
                TestSuite.Throws<ArgumentException>(() => oldFrame.GetPixel(0, 0), "resize releases prior native CPU surface");
                TestSuite.Check(object.ReferenceEquals(presenter, Read<object>(game, "framePresenter")), "resize resets the existing device instead of recreating it");
                presenter = Read<object>(game, "framePresenter");
                var frame = (Bitmap)NativeRead(presenter, "frame");
                TestSuite.Check((long)frame.Width * frame.Height <= 1920 * 1080, "native large-window rasterization has the same fixed pixel budget");
                TestSuite.Check(Read<string>(game, "presentationFailure") == "", "ultrawide native resize stays accelerated");
                Click(game, game.DrawingViewport.Width * .84f, 430);
                TestSuite.Check(Read<CombatState>(game, "combat").SelectedTarget == 1, "native reduced raster preserves native-coordinate target input");
                uint handles;
                using (var process = System.Diagnostics.Process.GetCurrentProcess()) handles = GetGuiResources(process.Handle, 0);
                for (int i = 0; i < 18; i++)
                {
                    game.ClientSize = i % 2 == 0 ? new Size(1280, 840) : new Size(1920, 1080);
                    game.Invalidate(); game.Update();
                }
                using (var process = System.Diagnostics.Process.GetCurrentProcess())
                    TestSuite.Check(GetGuiResources(process.Handle, 0) <= handles + 2, "repeated native resizes release old DCs and bitmaps");
                TestSuite.Check(Read<string>(game, "presentationFailure") == "", "native resize stress never silently falls back");
                Call(game, "OnDeactivate", EventArgs.Empty);
                TestSuite.Check(!Read<Timer>(game, "combatTimer").Enabled, "native renderer preserves focus pause");
                Call(game, "OnActivated", EventArgs.Empty); Read<Timer>(game, "combatTimer").Stop();
                Call(game, "ToggleFullscreen"); game.Update();
                TestSuite.Check(Read<string>(game, "presentationFailure") == "", "fullscreen swap-chain recreation succeeds");
                Call(game, "ToggleFullscreen"); game.Update();
                presenter = Read<object>(game, "framePresenter");
                Call(game, "ReleasePresenter");
                TestSuite.Check((IntPtr)NativeRead(presenter, "device") == IntPtr.Zero && (IntPtr)NativeRead(presenter, "api") == IntPtr.Zero && NativeRead(presenter, "frame") == null, "release frees native device, COM root and CPU frame");
                Write(game, "presenterFailed", true);
                long count = Read<long>(game, "presentedFrames"); game.Invalidate(); game.Update();
                TestSuite.Check(Read<object>(game, "framePresenter") == null && Read<long>(game, "presentedFrames") == count, "software fallback never shares the HWND with a flip chain");
                using (ClientReference(game)) { }
                game.Close();
            }
        }
        static void NativeGpuPixels(object presenter, Size size)
        {
            IntPtr device = (IntPtr)NativeRead(presenter, "device"), surface = IntPtr.Zero;
            try
            {
                NativeCheck(NativeMethod<NativeSurface>(device, 36)(device, (uint)size.Width, (uint)size.Height, 22, 2, out surface, IntPtr.Zero));
                NativeCheck(NativeMethod<NativeReadback>(device, 32)(device, (IntPtr)NativeRead(presenter, "back"), surface));
                NativeLocked data;
                NativeCheck(NativeMethod<NativeLock>(surface, 13)(surface, out data, IntPtr.Zero, 0x10));
                try
                {
                    var frame = (Bitmap)NativeRead(presenter, "frame");
                    foreach (var point in new[] { new Point(40, 40), new Point(500, 400), new Point(800, 500), new Point(1100, 700) })
                    {
                        int value = Marshal.ReadInt32(IntPtr.Add(data.Bits, point.Y * data.Pitch + point.X * 4));
                        var expected = frame.GetPixel(point.X, point.Y); var actual = Color.FromArgb(value);
                        TestSuite.Check(expected.R == actual.R && expected.G == actual.G && expected.B == actual.B, "GPU back buffer contains the composed pixels, not a blank/stale surface");
                    }
                }
                finally { NativeCheck(NativeMethod<NativeUnlock>(surface, 14)(surface)); }
            }
            finally { if (surface != IntPtr.Zero) Marshal.Release(surface); }
        }
        public static void TestStableSampling()
        {
            using (var game = new Game())
            {
                game.CreateControl(); game.ClientSize = new Size(1280, 840);
                Call(game, "OpenCheckout");
                using (ClientReference(game)) { }
                var background = Read<Bitmap>(game, "checkoutBackdrop");
                for (int i = 0; i < 8; i++) { Call(game, "AdvanceLivingAnimation", .1f); using (ClientReference(game)) { } }
                TestSuite.Check(object.ReferenceEquals(background, Read<Bitmap>(game, "checkoutBackdrop")), "checkout movement reuses its rasterized background");
                Call(game, "TakeBuff"); Call(game, "OpenActMap");
                TestSuite.Check(Read<Bitmap>(game, "checkoutBackdrop") == null, "leaving checkout releases its unused background cache");
                using (var partial = ClientReference(game))
                    for (int i = 0; i < 8; i++)
                    {
                        Call(game, "MoveMapTo", i % 2 == 0 ? 0f : 900f); Call(game, "AdvanceMapMotion", .08f);
                        var damage = (Rectangle)typeof(Game).GetProperty("MotionDamage", PrivateInstance).GetValue(game, null);
                        TestSuite.Check(damage.Top > 0, "camera motion excludes the static HUD");
                        PaintClient(game, partial, damage);
                        using (var full = ClientReference(game))
                        {
                            if (!SamePixels(partial, full)) { partial.Save(Path.Combine(TestSuite.OutputDirectory, "map-actual.png")); full.Save(Path.Combine(TestSuite.OutputDirectory, "map-expected.png")); }
                            TestSuite.Check(SamePixels(partial, full), "partial map scroll exactly matches a full reference frame");
                        }
                    }
                using (var layer = new Bitmap(400, 400))
                using (var graphics = Graphics.FromImage(layer))
                    for (int i = 0; i < 30; i++)
                    {
                        graphics.ResetTransform(); LivingMotion.Body(graphics, new RectangleF(20, 30, 250, 300), i * .1, i % 8, false);
                        using (var matrix = graphics.Transform)
                        {
                            var m = matrix.Elements;
                            TestSuite.Check(m[0] == 1 && m[3] == 1 && m[1] == 0 && m[2] == 0 && m[5] == 0, "ambient ground motion never shears, stretches or lifts the silhouette");
                        }
                    }
                game.Dispose();
                TestSuite.Throws<ArgumentException>(() => background.GetPixel(0, 0), "checkout raster releases with the game");
            }
        }
    }
}
