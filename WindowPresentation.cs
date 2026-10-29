using System;
using System.Drawing;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        [StructLayout(LayoutKind.Sequential)]
        struct NativeRect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)]
        struct PaintInfo
        {
            public IntPtr DC;
            public int Erase;
            public NativeRect Area;
            public int Restore, Update;
            public int Reserved0, Reserved1, Reserved2, Reserved3, Reserved4, Reserved5, Reserved6, Reserved7;
        }
        [DllImport("user32.dll")] static extern IntPtr BeginPaint(IntPtr window, out PaintInfo info);
        [DllImport("user32.dll")] static extern bool EndPaint(IntPtr window, ref PaintInfo info);
        FramePresenter framePresenter;
        bool nativePaint, presenterFailed, presentationPending;
        string presentationFailure = "";
        Size framePixels;
        long presentedFrames, rasterTicks, presentTicks, deferredFrames;
        bool UseNativePresentation { get { return !presenterFailed && Environment.GetEnvironmentVariable("SVOI_SOFTWARE_RENDERER") != "1"; } }
        void ReleasePresenter()
        {
            if (framePresenter != null) framePresenter.Dispose();
            framePresenter = null;
            presentationPending = false;
        }
        static Size RasterSize(Size client)
        {
            long pixels = (long)client.Width * client.Height;
            if (pixels <= RenderPixelBudget) return client;
            double density = Math.Sqrt(RenderPixelBudget / (double)pixels);
            return new Size(Math.Max(1, (int)(client.Width * density)), Math.Max(1, (int)(client.Height * density)));
        }
        bool PaintWindowFrame()
        {
            if (!UseNativePresentation || ClientSize.Width <= 0 || ClientSize.Height <= 0 || WindowState == FormWindowState.Minimized) return false;
            PaintInfo info;
            BeginPaint(Handle, out info);
            try
            {
                bool complete = framePresenter == null || framePresenter.WindowSize != ClientSize;
                if (complete)
                {
                    if (framePresenter == null) framePresenter = new FramePresenter(Handle, ClientSize, RasterSize(ClientSize));
                    else framePresenter.Resize(Handle, ClientSize, RasterSize(ClientSize));
                }
                framePixels = framePresenter.Pixels;
                renderDensity = framePixels.Width / (float)ClientSize.Width;
                nativePaint = true;
                long rasterStart = Stopwatch.GetTimestamp();
                try
                {
                    using (var graphics = framePresenter.BeginFrame())
                    {
                        var clip = complete ? new Rectangle(Point.Empty, framePixels) : Rectangle.FromLTRB(
                            (int)Math.Floor(info.Area.Left * renderDensity), (int)Math.Floor(info.Area.Top * renderDensity),
                            (int)Math.Ceiling(info.Area.Right * renderDensity), (int)Math.Ceiling(info.Area.Bottom * renderDensity));
                        graphics.SetClip(clip);
                        using (var args = new PaintEventArgs(graphics, clip)) OnPaint(args);
                    }
                }
                finally { framePresenter.EndFrame(); }
                rasterTicks += Stopwatch.GetTimestamp() - rasterStart;
                long presentStart = Stopwatch.GetTimestamp();
                presentationPending = !framePresenter.PresentFrame();
                presentTicks += Stopwatch.GetTimestamp() - presentStart;
                if (presentationPending) { deferredFrames++; WakeMotion(); }
                else presentedFrames++;
                return true;
            }
            catch (Exception error)
            {
                // Driver/device loss never kills the game or changes combat state.
                // The fallback owns the same HWND only after releasing the flip chain.
                if (!(error is ExternalException) && !(error is DllNotFoundException) && !(error is EntryPointNotFoundException) && !(error is NotSupportedException) && !(error is OutOfMemoryException)) throw;
                presentationFailure = error.GetType().Name + ": " + error.Message;
                presenterFailed = true;
                ReleasePresenter();
                Invalidate();
                return true;
            }
            finally
            {
                nativePaint = false; framePixels = Size.Empty; renderDensity = 1;
                EndPaint(Handle, ref info);
            }
        }
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x0014 && Visible && UseNativePresentation) { message.Result = new IntPtr(1); return; }
            if (message.Msg == 0x007E) { ReleasePresenter(); Invalidate(); }
            if (message.Msg == 0x000F && Visible && !resourcesDisposed && PaintWindowFrame()) { message.Result = IntPtr.Zero; return; }
            base.WndProc(ref message);
        }
        protected override void OnHandleDestroyed(EventArgs e)
        {
            ReleasePresenter();
            base.OnHandleDestroyed(e);
        }
    }
}
