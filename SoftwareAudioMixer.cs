using System;
using System.Collections.Generic;

namespace ShpilSvoih
{
    // Deterministic PCM mixer, independent of Windows. Fixed 44.1kHz stereo output.
    public sealed class SoftwareAudioMixer
    {
        public const int SampleRate = 44100;
        readonly object gate = new object();
        readonly List<Channel> channels = new List<Channel>();
        public sealed class Channel : IMusicVoice, IAudioVolumeVoice
        {
            readonly SoftwareAudioMixer owner;
            internal readonly MusicPcm Clip;
            internal readonly bool Loop;
            internal double Position, Consumed, Gain = 1;
            internal bool Paused = true, Disposed;
            internal Channel(SoftwareAudioMixer owner, MusicPcm clip, bool loop)
            { this.owner = owner; Clip = clip; Loop = loop; }
            internal int Frames { get { return Clip.Data.Length / (Clip.Channels * 2); } }
            internal bool Active { get { return !Disposed && !Paused && (Loop || Position < Frames); } }
            public uint PlayedBytes { get { lock (owner.gate) return (uint)Math.Min(uint.MaxValue, Consumed * Clip.Channels * 2); } }
            public double Volume { get { lock (owner.gate) return Gain; } }
            public void SetVolume(double value)
            {
                if (double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value > 1)
                    throw new ArgumentOutOfRangeException("value");
                lock (owner.gate) if (!Disposed) Gain = value;
            }
            public void SetPaused(bool value) { lock (owner.gate) if (!Disposed) Paused = value; }
            public void Dispose()
            { lock (owner.gate) { if (Disposed) return; Disposed = true; owner.channels.Remove(this); } }
        }
        public Channel Add(MusicPcm pcm, bool loop)
        {
            if (pcm == null) throw new ArgumentNullException("pcm");
            lock (gate) { var c = new Channel(this, pcm, loop); channels.Add(c); return c; }
        }
        public bool HasActive
        { get { lock (gate) { foreach (var c in channels) if (c.Active) return true; return false; } } }
        public bool HasUnpaused
        { get { lock (gate) { foreach (var c in channels) if (!c.Disposed && !c.Paused) return true; return false; } } }
        static double Sample(Channel c, int frame, int channel)
        {
            int index = (frame * c.Clip.Channels + (c.Clip.Channels == 1 ? 0 : channel)) * 2;
            return (short)(c.Clip.Data[index] | c.Clip.Data[index + 1] << 8) / 32768.0;
        }
        public void Render(short[] output)
        {
            if (output == null || output.Length % 2 != 0) throw new ArgumentException("Stereo output required", "output");
            lock (gate)
                for (int i = 0; i < output.Length; i += 2)
                {
                    double left = 0, right = 0;
                    foreach (var c in channels)
                    {
                        if (!c.Active) continue;
                        int a = (int)c.Position, b = a + 1;
                        if (b >= c.Frames) b = c.Loop ? 0 : a;
                        double part = c.Position - a;
                        left += (Sample(c, a, 0) * (1 - part) + Sample(c, b, 0) * part) * c.Gain;
                        right += (Sample(c, a, 1) * (1 - part) + Sample(c, b, 1) * part) * c.Gain;
                        double step = c.Clip.SampleRate / (double)SampleRate;
                        if (!c.Loop) step = Math.Min(step, c.Frames - c.Position);
                        c.Position += step; c.Consumed += step;
                        if (c.Loop && c.Position >= c.Frames) c.Position %= c.Frames;
                    }
                    output[i] = (short)Math.Round(Math.Max(-.98, Math.Min(.98, left)) * 32767);
                    output[i + 1] = (short)Math.Round(Math.Max(-.98, Math.Min(.98, right)) * 32767);
                }
        }
    }
}
