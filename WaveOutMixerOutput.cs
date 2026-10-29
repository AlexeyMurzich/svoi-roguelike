using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading;

namespace ShpilSvoih
{
    // The sole Windows output. Session/device volume is NEVER modified.
    // Driver events refill 1024-frame buffers: menu music reserves more than effects.
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
        [DllImport("avrt.dll", CharSet = CharSet.Unicode, EntryPoint = "AvSetMmThreadCharacteristicsW")]
        static extern IntPtr AvSetMmThreadCharacteristics(string task, ref uint index);
        [DllImport("avrt.dll")] static extern bool AvSetMmThreadPriority(IntPtr task, int priority);
        [DllImport("avrt.dll")] static extern bool AvRevertMmThreadCharacteristics(IntPtr task);
        static readonly uint HeaderSize = (uint)Marshal.SizeOf(typeof(Header));
        static readonly int FlagsOffset = (int)Marshal.OffsetOf(typeof(Header), "Flags");
        readonly object gate = new object();
        readonly SoftwareAudioMixer mixer;
        const int BufferFrames = 1024;
        readonly BufferSlot[] slots;
        readonly AutoResetEvent wake = new AutoResetEvent(false);
        readonly ManualResetEvent completed = new ManualResetEvent(false);
        readonly WaitHandle[] signals;
        readonly short[] scratch = new short[BufferFrames * 2];
        Thread worker;
        IntPtr device;
        volatile bool stopping;
        bool paused = true, disposed;
        string error = "";
        int underruns;
        long lastService, maxServiceGap;
        bool multimediaScheduled;
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
        public WaveOutMixerOutput(SoftwareAudioMixer mixer, bool menuBuffering)
        {
            this.mixer = mixer;
            slots = new BufferSlot[menuBuffering ? 12 : 4];
            signals = new WaitHandle[] { wake, completed };
            try
            {
                var format = new Format { Tag = 1, Channels = 2, Rate = 44100, BytesPerSecond = 176400, Alignment = 4, Bits = 16 };
                Check(waveOutOpen(out device, uint.MaxValue, ref format, completed.SafeWaitHandle.DangerousGetHandle(), IntPtr.Zero, 0x50000));
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
                if (paused != value)
                {
                    // Prime while the device is still paused: never restart an empty queue.
                    if (!value) FillAvailable();
                    Check(value ? waveOutPause(device) : waveOutRestart(device));
                    paused = value; lastService = 0;
                }
            }
            wake.Set();
        }
        void Pump()
        {
            IntPtr task = IntPtr.Zero;
            try
            {
                uint index = 0;
                try
                {
                    task = AvSetMmThreadCharacteristics("Audio", ref index);
                    multimediaScheduled = task != IntPtr.Zero;
                    if (multimediaScheduled) AvSetMmThreadPriority(task, 1);
                }
                catch (DllNotFoundException) { }
                catch (EntryPointNotFoundException) { }
                while (!stopping)
                {
                    bool active;
                    lock (gate)
                    {
                        if (stopping) break;
                        // Reset before observing flags, so completions during refill are retained.
                        completed.Reset();
                        active = !paused && mixer.HasActive;
                        if (active)
                        {
                            long now = Stopwatch.GetTimestamp();
                            if (lastService != 0) maxServiceGap = Math.Max(maxServiceGap, now - lastService);
                            lastService = now;
                            int pending = 0; bool submitted = false;
                            foreach (var slot in slots)
                            {
                                submitted |= slot.Queued;
                                if (slot.Queued && (Marshal.ReadInt32(slot.Header, FlagsOffset) & 1) == 0) pending++;
                            }
                            if (submitted && pending == 0) underruns++;
                            FillAvailable();
                        }
                        else lastService = 0;
                    }
                    // Completion wakes us immediately; timeout is only a driver watchdog.
                    WaitHandle.WaitAny(signals, active ? 100 : Timeout.Infinite);
                }
            }
            catch (Exception failure) { lock (gate) { error = failure.Message; stopping = true; } }
            finally { if (task != IntPtr.Zero) AvRevertMmThreadCharacteristics(task); }
        }
        void FillAvailable()
        {
            foreach (var slot in slots)
            {
                uint flags = (uint)Marshal.ReadInt32(slot.Header, FlagsOffset);
                if (slot.Queued && (flags & 1) == 0) continue;
                mixer.Render(scratch);
                Marshal.Copy(scratch, 0, slot.Data, scratch.Length);
                Check(waveOutWrite(device, slot.Header, HeaderSize)); slot.Queued = true;
            }
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
            completed.Dispose(); wake.Dispose();
        }
    }
}
