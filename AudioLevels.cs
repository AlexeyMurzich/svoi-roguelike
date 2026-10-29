using System;
using System.Threading;

namespace ShpilSvoih
{
    // Complete level policy: software gains only, never Windows session volume.
    public static class AudioLevels
    {
        public const double Music = .07, Card = .18, Enemy = .18, Howl = .16;
        public const double MusicReferenceRms = .18, MusicSourcePeak = .65;
        // One fixed calibration per song; no live compressor or ducking.
        public static MusicPcm PrepareMusic(MusicPcm pcm, CancellationToken cancel)
        {
            if (pcm == null) throw new ArgumentNullException("pcm");
            double sum = 0, peak = 0;
            for (int at = 0; at < pcm.Data.Length; at += 2)
            {
                if ((at & 65535) == 0) cancel.ThrowIfCancellationRequested();
                double sample = (short)(pcm.Data[at] | pcm.Data[at + 1] << 8) / 32768.0;
                sum += sample * sample; peak = Math.Max(peak, Math.Abs(sample));
            }
            if (peak < 1e-8) return pcm;
            double scale = Math.Min(MusicReferenceRms / Math.Sqrt(sum / (pcm.Data.Length / 2)), MusicSourcePeak / peak);
            var data = new byte[pcm.Data.Length];
            for (int at = 0; at < data.Length; at += 2)
            {
                if ((at & 65535) == 0) cancel.ThrowIfCancellationRequested();
                short sample = (short)Math.Round((short)(pcm.Data[at] | pcm.Data[at + 1] << 8) * scale);
                data[at] = (byte)sample; data[at + 1] = (byte)(sample >> 8);
            }
            return new MusicPcm(pcm.SampleRate, pcm.Channels, data);
        }
    }
}
