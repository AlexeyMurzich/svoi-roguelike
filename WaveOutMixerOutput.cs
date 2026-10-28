using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace ShpilSvoih
{
    // The sole Windows output. Session/device volume is NEVER modified.
    // Three 512-frame buffers tolerate Windows scheduler jitter (about 35ms queue).
    sealed class WaveOutMixerOutput : IDisposable
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
        sealed class BufferSlot
        {
            public IntPtr Data, Header;
            public bool Prepared, Queued;
        }
        [DllImport("winmm.dll")] static extern uint waveOutOpen(out IntPtr device, uint id, ref Format format, IntPtr callback, IntPtr instance, uint flags);
        [DllImport("winmm.dll")] static extern uint waveOutPrepareHeader(IntPtr device, IntPtr header, uint size);
        [DllImport("winmm.dll")] static extern uint waveOutWrite(IntPtr device, IntPtr header, uint size);
        [DllImport("winmm.dll")] static extern uint waveOutPause(IntPtr device);
        [DllImport("winmm.dll")] static extern uint waveOutRestart(IntPtr device);
        [DllImport("winmm.dll")] static extern uint waveOutReset(IntPtr device);
        [DllImport("winmm.dll")] static extern uint waveOutUnprepareHeader(IntPtr device, IntPtr header, uint size);
        [DllImport("winmm.dll")] static extern uint waveOutClose(IntPtr device);
        [DllImport("winmm.dll")] static extern uint waveOutGetPosition(IntPtr device, ref Position position, uint size);
        static readonly uint HeaderSize = (uint)Marshal.SizeOf(typeof(Header));
        static readonly int FlagsOffset = (int)Marshal.OffsetOf(typeof(Header), "Flags");
        readonly object gate = new object();
        readonly SoftwareAudioMixer mixer;
        readonly BufferSlot[] slots = new BufferSlot[3];
        readonly AutoResetEvent wake = new AutoResetEvent(false);
        readonly short[] scratch = new short[1024];
        Thread worker;
        IntPtr device;
        volatile bool stopping;
        bool paused = true, disposed;
        string error = "";
        public string LastError { get { lock (gate) return error; } }
        public uint PlayedBytes
        {
            get
            {
                lock (gate)
                {
                    if (disposed) return 0;
                    var position = new Position { Type = 4 };
                    Check(waveOutGetPosition(device, ref position, 12)); return position.Value;
                }
            }
        }
        static void Check(uint result)
        { if (result != 0) throw new InvalidOperationException("Windows audio error: " + result); }
        public WaveOutMixerOutput(SoftwareAudioMixer mixer)
        {
            this.mixer = mixer;
            try
            {
                var format = new Format { Tag = 1, Channels = 2, Rate = 44100, BytesPerSecond = 176400, Alignment = 4, Bits = 16 };
                Check(waveOutOpen(out device, uint.MaxValue, ref format, IntPtr.Zero, IntPtr.Zero, 0));
                Check(waveOutPause(device));
                for (int i = 0; i < slots.Length; i++)
                {
                    var slot = new BufferSlot(); slots[i] = slot;
                    slot.Data = Marshal.AllocHGlobal(scratch.Length * 2);
                    slot.Header = Marshal.AllocHGlobal((int)HeaderSize);
                    Marshal.StructureToPtr(new Header { Data = slot.Data, Length = (uint)(scratch.Length * 2) }, slot.Header, false);
                    Check(waveOutPrepareHeader(device, slot.Header, HeaderSize)); slot.Prepared = true;
                }
                worker = new Thread(Pump) { IsBackground = true, Name = "Svoi audio mixer", Priority = ThreadPriority.AboveNormal }; worker.Start();
            }
            catch { Dispose(); throw; }
        }
        public void RefreshPause()
        {
            // Completed channels can still have a short queued tail to resume.
            bool value = !mixer.HasUnpaused;
            lock (gate)
            {
                if (disposed) return;
                if (error != "") throw new InvalidOperationException(error);
                if (paused != value) { Check(value ? waveOutPause(device) : waveOutRestart(device)); paused = value; }
            }
            wake.Set();
        }
        void Pump()
        {
            try
            {
                while (!stopping)
                {
                    bool active;
                    lock (gate)
                    {
                        if (stopping) break;
                        active = !paused && mixer.HasActive;
                        if (active)
                            foreach (var slot in slots)
                            {
                                uint flags = (uint)Marshal.ReadInt32(slot.Header, FlagsOffset);
                                if (slot.Queued && (flags & 1) == 0) continue;
                                mixer.Render(scratch);
                                Marshal.Copy(scratch, 0, slot.Data, scratch.Length);
                                Check(waveOutWrite(device, slot.Header, HeaderSize)); slot.Queued = true;
                            }
                    }
                    wake.WaitOne(active ? 2 : Timeout.Infinite);
                }
            }
            catch (Exception failure) { lock (gate) { error = failure.Message; stopping = true; } }
        }
        public void Dispose()
        {
            lock (gate) { if (disposed) return; disposed = true; stopping = true; }
            wake.Set(); if (worker != null) worker.Join();
            if (device != IntPtr.Zero)
            {
                waveOutReset(device);
                foreach (var slot in slots)
                    if (slot != null && slot.Prepared) waveOutUnprepareHeader(device, slot.Header, HeaderSize);
                waveOutClose(device); device = IntPtr.Zero;
            }
            foreach (var slot in slots)
                if (slot != null)
                {
                    if (slot.Header != IntPtr.Zero) Marshal.FreeHGlobal(slot.Header);
                    if (slot.Data != IntPtr.Zero) Marshal.FreeHGlobal(slot.Data);
                }
            wake.Dispose();
        }
    }
}
