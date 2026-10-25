using System;
using System.IO;
using System.Media;
using System.Text;

namespace ShpilSvoih
{
    // Shared immutable PCM data; each window owns its player and read-only stream.
    sealed class HowlSound : IDisposable
    {
        const int SampleRate = 22050;
        const int SampleCount = 18742;
        static readonly byte[] wave = CreateWave();
        readonly MemoryStream stream;
        readonly SoundPlayer player;
        bool disposed;

        public HowlSound()
        {
            stream = new MemoryStream(wave, false);
            player = new SoundPlayer(stream);
            player.Load();
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
                player.Play();
            }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        }

        public void Stop()
        {
            if (!disposed)
                player.Stop();
        }

        public void Dispose()
        {
            if (disposed)
                return;
            player.Stop();
            player.Dispose();
            stream.Dispose();
            disposed = true;
        }
    }
}
