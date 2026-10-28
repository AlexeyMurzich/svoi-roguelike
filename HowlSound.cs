using System;
using System.IO;
using System.Text;

namespace ShpilSvoih
{
    // Original procedural dog howl; native one-shot shares the music/effect transport.
    sealed class HowlSound : IDisposable
    {
        const int SampleRate = 22050;
        const int SampleCount = 18742;
        static readonly byte[] wave = CreateWave();
        IMusicVoice voice;
        bool disposed, paused = true;

        public HowlSound()
        {
        }

        static byte[] CreateWave()
        {
            using (var output = new MemoryStream())
            {
                using (var writer = new BinaryWriter(output, Encoding.ASCII, true))
                {
                    writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                    writer.Write(36 + SampleCount * 2);
                    writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                    writer.Write(16);
                    writer.Write((short)1);
                    writer.Write((short)1);
                    writer.Write(SampleRate);
                    writer.Write(SampleRate * 2);
                    writer.Write((short)2);
                    writer.Write((short)16);
                    writer.Write(Encoding.ASCII.GetBytes("data"));
                    writer.Write(SampleCount * 2);
                    double phase = 0, second = 0;
                    var random = new Random(711);
                    for (int i = 0; i < SampleCount; i++)
                    {
                        double time = i / (double)SampleRate, progress = i / (double)SampleCount;
                        double pitch = 370 + 190 * Math.Sin(Math.PI * progress) + 12 * Math.Sin(2 * Math.PI * 6 * time);
                        phase += 2 * Math.PI * pitch / SampleRate;
                        second += 2 * Math.PI * pitch * .81 / SampleRate;
                        double envelope = Math.Min(1, progress * 12) * Math.Pow(Math.Sin(Math.PI * progress), .65);
                        double voice = Math.Sin(phase) * .36 + Math.Sin(2 * phase) * .16 + Math.Sin(3 * phase) * .1
                            + Math.Sin(second) * .22 + Math.Sin(2 * second) * .08 + (random.NextDouble() - .5) * .04;
                        writer.Write((short)(voice * envelope * 26000));
                    }
                }
                return output.ToArray();
            }
        }

        public void Play()
        {
            if (disposed)
                return;
            try
            {
                Stop();
                var bytes = new byte[SampleCount * 2];
                Buffer.BlockCopy(wave, 44, bytes, 0, bytes.Length);
                voice = new LoopingMusicVoice(new MusicPcm(SampleRate, 1, bytes), false);
                ((IAudioVolumeVoice)voice).SetVolume(.55);
                paused = true;
                SetPaused(false);
            }
            catch (InvalidOperationException) { Stop(); }
            catch (System.Runtime.InteropServices.ExternalException) { Stop(); }
        }

        public void Stop()
        {
            if (voice != null) voice.Dispose();
            voice = null; paused = true;
        }

        public void SetPaused(bool value)
        {
            if (disposed || voice == null || paused == value) return;
            try { voice.SetPaused(value); paused = value; }
            catch (InvalidOperationException) { Stop(); }
        }

        public void Dispose()
        {
            if (disposed)
                return;
            Stop();
            disposed = true;
        }
    }
}
