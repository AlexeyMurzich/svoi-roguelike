using System;
using System.Threading;

namespace ShpilSvoih
{
    public static class SoftwareMixerTests
    {
        static MusicPcm Signal(int rate, ushort channels, int frames, bool leftOnly, double amplitude)
        {
            var data = new byte[frames * channels * 2];
            for (int i = 0; i < frames; i++)
                for (int c = 0; c < channels; c++)
                {
                    short v = (short)(Math.Sin(i * 2 * Math.PI * 440 / rate) * amplitude * 32767);
                    if (leftOnly && c == 1) v = 0;
                    int at = (i * channels + c) * 2; data[at] = (byte)v; data[at + 1] = (byte)(v >> 8);
                }
            return new MusicPcm(rate, channels, data);
        }
        public static void Isolation()
        {
            var alone = new SoftwareAudioMixer(); var mixed = new SoftwareAudioMixer();
            var music = Signal(44100, 2, 4410, false, .6);
            using (var reference = alone.Add(music, true))
            using (var foregroundMusic = mixed.Add(music, true))
            using (var effect = mixed.Add(Signal(48000, 2, 4800, true, .8), true))
            {
                reference.SetVolume(AudioLevels.Music); foregroundMusic.SetVolume(AudioLevels.Music);
                reference.SetPaused(false); foregroundMusic.SetPaused(false); effect.SetPaused(false);
                var a = new short[666]; var b = new short[666];
                foreach (double gain in new[] { 0.0, .18, .03, 1.0, .05, 0.0 })
                {
                    effect.SetVolume(gain); alone.Render(a); mixed.Render(b);
                    for (int i = 1; i < a.Length; i += 2)
                        TestSuite.Check(a[i] == b[i], "music samples remain bit-identical while effect level changes");
                    TestSuite.Check(foregroundMusic.Volume == AudioLevels.Music, "effect cannot touch background gain");
                }
                uint before = foregroundMusic.PlayedBytes;
                foregroundMusic.SetPaused(true); mixed.Render(b);
                TestSuite.Check(foregroundMusic.PlayedBytes == before, "music pause holds position while effect advances");
                effect.SetPaused(true); mixed.Render(b);
                TestSuite.Check(Array.TrueForAll(b, x => x == 0), "all paused channels yield silence");
            }
            TestSuite.Check(!mixed.HasActive, "dispose removes every channel");
        }
        public static void ResamplingAndHeadroom()
        {
            var mixer = new SoftwareAudioMixer();
            var mono = Signal(22050, 1, 2205, false, .9);
            using (var effect = mixer.Add(mono, false))
            {
                effect.SetVolume(AudioLevels.Card); effect.SetPaused(false);
                var data = new short[8820]; mixer.Render(data);
                TestSuite.Check(effect.PlayedBytes == mono.Data.Length, "22k mono one-shot preserves correct duration");
                int peak = 0;
                for (int i = 0; i < data.Length; i += 2)
                { TestSuite.Check(data[i] == data[i + 1], "mono expands equally to stereo"); peak = Math.Max(peak, Math.Abs((int)data[i])); }
                TestSuite.Check(peak < 5400, "quiet effect peak is software-scaled");
                mixer.Render(data); TestSuite.Check(Array.TrueForAll(data, x => x == 0), "one-shot cannot restart on next buffer");
            }
            using (var voice = mixer.Add(Signal(48000, 2, 4800, false, .8), false))
            {
                voice.SetPaused(false); var data = new short[9000]; mixer.Render(data);
                TestSuite.Check(voice.PlayedBytes == 4800 * 4, "48k one-shot resamples to correct 100ms");
                TestSuite.Throws<ArgumentOutOfRangeException>(() => voice.SetVolume(double.NaN), "invalid channel gain rejected");
            }
        }
        public static void SongCalibration()
        {
            var loud = Signal(44100, 2, 4410, false, .9);
            var quiet = Signal(44100, 2, 4410, false, .09);
            var a = AudioLevels.PrepareMusic(loud, CancellationToken.None);
            var b = AudioLevels.PrepareMusic(quiet, CancellationToken.None);
            double sumA = 0, sumB = 0; int peak = 0;
            for (int at = 0; at < a.Data.Length; at += 2)
            {
                short x = (short)(a.Data[at] | a.Data[at + 1] << 8), y = (short)(b.Data[at] | b.Data[at + 1] << 8);
                sumA += x * (double)x; sumB += y * (double)y; peak = Math.Max(peak, Math.Abs((int)x));
            }
            TestSuite.Check(Math.Abs(10 * Math.Log10(sumA / sumB)) < .02, "song source loudness calibrated once, not by gameplay");
            TestSuite.Check(peak <= AudioLevels.MusicSourcePeak * 32768, "normalization preserves peak headroom");
            TestSuite.Check(!object.ReferenceEquals(a.Data, loud.Data), "original PCM remains untouched");
            var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            TestSuite.Throws<OperationCanceledException>(() => AudioLevels.PrepareMusic(loud, cancelled.Token), "stale song preparation cancels");
        }
    }
}
