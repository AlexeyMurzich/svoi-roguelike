using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;

namespace ShpilSvoih
{
    // A single UI-thread-owned swap chain. Scene rasterization remains independent
    // of presentation: GDI+ draws only into the retained CPU bitmap.
    // No GDI pixels ever target the flip-model HWND.
    sealed class FramePresenter : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        struct Parameters
        {
            public uint Width, Height, Format, Count, MultiSample, Quality, SwapEffect;
            public IntPtr Window;
            public int Windowed, Depth;
            public uint DepthFormat, Flags, Refresh, Interval;
        }
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int CreateDevice(IntPtr self, uint adapter, int type, IntPtr window, uint flags, ref Parameters parameters, IntPtr displayMode, out IntPtr device);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int CreateSurface(IntPtr self, uint width, uint height, int format, int pool, out IntPtr surface, IntPtr shared);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int BackBuffer(IntPtr self, uint chain, uint index, int type, out IntPtr surface);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int LockSurface(IntPtr self, out LockedRect data, IntPtr rectangle, uint flags);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int UnlockSurface(IntPtr self);
        [StructLayout(LayoutKind.Sequential)] struct LockedRect { public int Pitch; public IntPtr Bits; }
        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")]
        static extern void CopyMemory(IntPtr target, IntPtr source, UIntPtr bytes);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int UpdateSurface(IntPtr self, IntPtr source, IntPtr sourceRect, IntPtr target, IntPtr point);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int Stretch(IntPtr self, IntPtr source, IntPtr sourceRect, IntPtr target, IntPtr targetRect, int filter);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int Present(IntPtr self, IntPtr sourceRect, IntPtr targetRect, IntPtr window, IntPtr dirty, uint flags);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int SetLatency(IntPtr self, uint frames);
        [DllImport("d3d9.dll", ExactSpelling = true)]
        static extern int Direct3DCreate9Ex(uint version, out IntPtr direct3D);

        const int FormatXrgb = 22, PoolDefault = 0, PoolSystemMemory = 2;
        const uint SwapFlipEx = 5, IntervalOne = 1, HardwareVertexProcessing = 0x40, PreserveFpu = 0x2;
        // Slots are the stable COM ABI of IDirect3D9Ex / IDirect3DDevice9Ex / IDirect3DSurface9.
        const int CreateDeviceExSlot = 20, CreateSurfaceSlot = 36, BackBufferSlot = 18,
            LockSlot = 13, UnlockSlot = 14, UpdateSlot = 30, StretchSlot = 34, PresentExSlot = 121,
            LatencySlot = 126, ResetExSlot = 132;
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int ResetDevice(IntPtr self, ref Parameters parameters, IntPtr displayMode);

        IntPtr api, device, staging, upload, back;
        Bitmap frame;
        bool acquired;
        LockSurface lockSurface;
        UnlockSurface unlockSurface;
        UpdateSurface update;
        Stretch stretch;
        Present present;
        BackBuffer getBackBuffer;
        public Size Pixels { get; private set; }
        public Size WindowSize { get; private set; }
        public bool Ready { get { return device != IntPtr.Zero; } }
        public long Presented { get; private set; }
        public long SurfaceBytes { get { return Ready ? ((long)Pixels.Width * Pixels.Height * 3 + (long)WindowSize.Width * WindowSize.Height * 3) * 4 : 0; } }

        static T Method<T>(IntPtr instance, int slot) where T : class
        {
            return (T)(object)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), slot * IntPtr.Size), typeof(T));
        }
        static void Check(int result) { if (result < 0) Marshal.ThrowExceptionForHR(result); }
        public FramePresenter(IntPtr window, Size windowSize, Size pixels)
        {
            if (window == IntPtr.Zero || pixels.Width <= 0 || pixels.Height <= 0 || windowSize.Width <= 0 || windowSize.Height <= 0)
                throw new ArgumentException("Invalid presentation target.");
            WindowSize = windowSize; Pixels = pixels;
            try
            {
                Check(Direct3DCreate9Ex(32, out api));
                var parameters = PresentationParameters(window, windowSize);
                // FPU_PRESERVE: Direct3D must not change combat/math floating-point precision.
                Check(Method<CreateDevice>(api, CreateDeviceExSlot)(api, 0, 1, window, HardwareVertexProcessing | PreserveFpu, ref parameters, IntPtr.Zero, out device));
                Check(Method<SetLatency>(device, LatencySlot)(device, 2));
                getBackBuffer = Method<BackBuffer>(device, BackBufferSlot);
                update = Method<UpdateSurface>(device, UpdateSlot); stretch = Method<Stretch>(device, StretchSlot);
                present = Method<Present>(device, PresentExSlot);
                CreateSurfaces();
            }
            catch { Dispose(); throw; }
        }
        static Parameters PresentationParameters(IntPtr window, Size size)
        {
            return new Parameters { Width = (uint)size.Width, Height = (uint)size.Height, Format = FormatXrgb,
                Count = 2, SwapEffect = SwapFlipEx, Window = window, Windowed = 1, Interval = IntervalOne };
        }
        void CreateSurfaces()
        {
            var create = Method<CreateSurface>(device, CreateSurfaceSlot);
            Check(create(device, (uint)Pixels.Width, (uint)Pixels.Height, FormatXrgb, PoolSystemMemory, out staging, IntPtr.Zero));
            Check(create(device, (uint)Pixels.Width, (uint)Pixels.Height, FormatXrgb, PoolDefault, out upload, IntPtr.Zero));
            lockSurface = Method<LockSurface>(staging, LockSlot); unlockSurface = Method<UnlockSurface>(staging, UnlockSlot);
            frame = new Bitmap(Pixels.Width, Pixels.Height, PixelFormat.Format32bppPArgb);
        }
        void ReleaseSurfaces()
        {
            if (frame != null) { frame.Dispose(); frame = null; }
            Release(ref back); Release(ref upload); Release(ref staging);
        }
        public void Resize(IntPtr window, Size windowSize, Size pixels)
        {
            if (!Ready) throw new ObjectDisposedException("FramePresenter");
            if (window == IntPtr.Zero || windowSize.Width <= 0 || windowSize.Height <= 0 || pixels.Width <= 0 || pixels.Height <= 0)
                throw new ArgumentException("Invalid presentation target.");
            if (acquired) throw new InvalidOperationException("Cannot resize an acquired frame.");
            if (WindowSize == windowSize && Pixels == pixels) return;
            ReleaseSurfaces();
            var parameters = PresentationParameters(window, windowSize);
            Check(Method<ResetDevice>(device, ResetExSlot)(device, ref parameters, IntPtr.Zero));
            WindowSize = windowSize; Pixels = pixels;
            CreateSurfaces();
        }
        public Graphics BeginFrame()
        {
            if (!Ready) throw new ObjectDisposedException("FramePresenter");
            if (acquired) throw new InvalidOperationException("Frame already acquired.");
            var graphics = Graphics.FromImage(frame);
            acquired = true;
            return graphics;
        }
        public void EndFrame() { acquired = false; }
        void UploadFrame()
        {
            var data = frame.LockBits(new Rectangle(Point.Empty, Pixels), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try
            {
                LockedRect target;
                Check(lockSurface(staging, out target, IntPtr.Zero, 0));
                try
                {
                    if (target.Pitch == data.Stride)
                        CopyMemory(target.Bits, data.Scan0, (UIntPtr)((long)data.Stride * Pixels.Height));
                    else
                        for (int y = 0; y < Pixels.Height; y++)
                            CopyMemory(IntPtr.Add(target.Bits, y * target.Pitch), IntPtr.Add(data.Scan0, y * data.Stride), (UIntPtr)(Pixels.Width * 4));
                }
                finally { Check(unlockSurface(staging)); }
            }
            finally { frame.UnlockBits(data); }
        }
        public bool PresentFrame()
        {
            if (!Ready) throw new ObjectDisposedException("FramePresenter");
            if (acquired) throw new InvalidOperationException("Release frame before presenting.");
            Release(ref back);
            Check(getBackBuffer(device, 0, 0, 0, out back));
            UploadFrame();
            // Keep CPU uploads separate from the queued flip surfaces.
            Check(update(device, staging, IntPtr.Zero, upload, IntPtr.Zero));
            Check(stretch(device, upload, IntPtr.Zero, back, IntPtr.Zero, Pixels == WindowSize ? 0 : 2));
            // Whole, complete frame; never immediate/unsynchronized presentation.
            int result = present(device, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1); // DONOTWAIT
            if (result == unchecked((int)0x8876021C)) return false; // WASSTILLDRAWING
            Check(result);
            Presented++;
            return true;
        }
        static void Release(ref IntPtr resource)
        {
            if (resource == IntPtr.Zero) return;
            Marshal.Release(resource); resource = IntPtr.Zero;
        }
        public void Dispose()
        {
            try { EndFrame(); }
            finally { ReleaseSurfaces(); Release(ref device); Release(ref api); }
        }
    }
}
