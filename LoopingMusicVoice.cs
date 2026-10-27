using System;
using System.Runtime.InteropServices;

namespace ShpilSvoih
{
    // A native buffer loop has no end-of-track timer or silence between repeats.
    // waveOut is independent of SoundPlayer, so dog howls do not stop the music.
    public sealed class LoopingMusicVoice : IMusicVoice
    {
        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        struct Format
        {
            public ushort Tag, Channels;
            public uint Rate, BytesPerSecond;
            public ushort Alignment, Bits, Extra;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct Header
        {
            public IntPtr Data;
            public uint Length, Recorded;
            public UIntPtr User;
            public uint Flags, Loops;
            public IntPtr Next;
            public UIntPtr Reserved;
        }
        [StructLayout(LayoutKind.Explicit, Size = 12)]
        struct Position
        {
            [FieldOffset(0)] public uint Type;
            [FieldOffset(4)] public uint Value;
        }
        [DllImport("winmm.dll")] static extern uint waveOutOpen(out IntPtr device, uint id, ref Format format, IntPtr callback, IntPtr instance, uint flags);
        [DllImport("winmm.dll")] static extern uint waveOutPrepareHeader(IntPtr device, IntPtr header, uint size);
        [DllImport("winmm.dll")] static extern uint waveOutWrite(IntPtr device, IntPtr header, uint size);
        [DllImport("winmm.dll")] static extern uint waveOutPause(IntPtr device);
        [DllImport("winmm.dll")] static extern uint waveOutRestart(IntPtr device);
        [DllImport("winmm.dll")] static extern uint waveOutReset(IntPtr device);
        [DllImport("winmm.dll")] static extern uint waveOutUnprepareHeader(IntPtr device, IntPtr header, uint size);
        [DllImport("winmm.dll")] static extern uint waveOutClose(IntPtr device);
        [DllImport("winmm.dll")] static extern uint waveOutSetVolume(IntPtr device, uint volume);
        [DllImport("winmm.dll")] static extern uint waveOutGetPosition(IntPtr device, ref Position position, uint size);
        IntPtr device, header;
        GCHandle data;
        bool prepared, disposed;
        static readonly uint HeaderSize = (uint)Marshal.SizeOf(typeof(Header));
        static void Check(uint result)
        {
            if (result != 0) throw new InvalidOperationException("Windows audio error: " + result);
        }
        public LoopingMusicVoice(MusicPcm pcm) : this(pcm, true) { }
        // One-shot effects share the native PCM transport without a loop or polling thread.
        public LoopingMusicVoice(MusicPcm pcm, bool loop)
        {
            if (pcm == null) throw new ArgumentNullException("pcm");
            var format = new Format { Tag = 1, Channels = pcm.Channels, Rate = (uint)pcm.SampleRate,
                BytesPerSecond = (uint)(pcm.SampleRate * pcm.Channels * 2), Alignment = (ushort)(pcm.Channels * 2), Bits = 16 };
            try
            {
                Check(waveOutOpen(out device, uint.MaxValue, ref format, IntPtr.Zero, IntPtr.Zero, 0));
                Check(waveOutPause(device));
                Check(waveOutSetVolume(device, 0x99999999));
                data = GCHandle.Alloc(pcm.Data, GCHandleType.Pinned);
                header = Marshal.AllocHGlobal((int)HeaderSize);
                var value = new Header { Data = data.AddrOfPinnedObject(), Length = (uint)pcm.Data.Length };
                Marshal.StructureToPtr(value, header, false);
                Check(waveOutPrepareHeader(device, header, HeaderSize)); prepared = true;
                value = (Header)Marshal.PtrToStructure(header, typeof(Header));
                if (loop) { value.Flags |= 4 | 8; value.Loops = uint.MaxValue; }
                Marshal.StructureToPtr(value, header, false);
                Check(waveOutWrite(device, header, HeaderSize));
            }
            catch { Dispose(); throw; }
        }
        public uint PlayedBytes
        {
            get
            {
                if (disposed) return 0;
                var position = new Position { Type = 4 };
                Check(waveOutGetPosition(device, ref position, 12));
                return position.Value;
            }
        }
        public void SetPaused(bool value)
        {
            if (!disposed) Check(value ? waveOutPause(device) : waveOutRestart(device));
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (device != IntPtr.Zero)
            {
                waveOutReset(device);
                if (prepared) waveOutUnprepareHeader(device, header, HeaderSize);
                waveOutClose(device); device = IntPtr.Zero;
            }
            if (header != IntPtr.Zero) { Marshal.FreeHGlobal(header); header = IntPtr.Zero; }
            if (data.IsAllocated) data.Free();
        }
    }
}
