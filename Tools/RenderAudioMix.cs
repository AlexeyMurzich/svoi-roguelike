using System;
using System.IO;
using System.Threading;
using ShpilSvoih;

// Offline review of actual assets through the production mixer. No sound device.
internal static class RenderAudioMix
{
    static void Header(BinaryWriter writer, int samples)
    {
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples * 2);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
        writer.Write((short)1); writer.Write((short)2); writer.Write(44100); writer.Write(176400);
        writer.Write((short)4); writer.Write((short)16);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples * 2);
    }
    static int Main(string[] args)
    {
        var mix = new SoftwareAudioMixer();
        var reference = new SoftwareAudioMixer();
        var effectsOnly = new SoftwareAudioMixer();
        var song = AudioLevels.PrepareMusic(MusicPlayer.LoadEmbedded("mimicrator", CancellationToken.None), CancellationToken.None);
        using (var music = mix.Add(song, true))
        using (var referenceMusic = reference.Add(song, true))
        {
            music.SetVolume(AudioLevels.Music); referenceMusic.SetVolume(AudioLevels.Music);
            music.SetPaused(false); referenceMusic.SetPaused(false);
            var rendered = new short[44100 * 9 * 2]; var baseline = new short[512]; var fx = new short[512]; var block = new short[512];
            SoftwareAudioMixer.Channel current = null, old = null, fxCurrent = null, fxOld = null;
            int next = 0; double fadeStart = 0; double[] starts = { 2, 2.85, 4.2, 5.4 };
            int[] heroes = { 0, 0, 3, 3 }; bool[] attacks = { true, false, true, false };
            int maximumError = 0, peak = 0;
            for (int at = 0; at < rendered.Length; at += block.Length)
            {
                double time = at / 88200.0;
                if (next < starts.Length && time >= starts[next])
                {
                    if (old != null) { old.Dispose(); fxOld.Dispose(); }
                    old = current; fxOld = fxCurrent; fadeStart = time;
                    var clip = CardSoundPlayer.LoadEmbedded(heroes[next], attacks[next]);
                    current = mix.Add(clip, false); fxCurrent = effectsOnly.Add(clip, false);
                    current.SetVolume(AudioLevels.Card); fxCurrent.SetVolume(AudioLevels.Card);
                    current.SetPaused(false); fxCurrent.SetPaused(false); next++;
                }
                if (old != null)
                {
                    double gain = AudioLevels.Card * Math.Max(0, 1 - (time - fadeStart) / .09);
                    old.SetVolume(gain); fxOld.SetVolume(gain);
                }
                mix.Render(block); reference.Render(baseline); effectsOnly.Render(fx);
                int length = Math.Min(block.Length, rendered.Length - at);
                for (int i = 0; i < length; i++)
                {
                    rendered[at + i] = block[i]; peak = Math.Max(peak, Math.Abs((int)block[i]));
                    maximumError = Math.Max(maximumError, Math.Abs(block[i] - baseline[i] - fx[i]));
                }
            }
            if (current != null) { current.Dispose(); fxCurrent.Dispose(); }
            if (old != null) { old.Dispose(); fxOld.Dispose(); }
            if (maximumError > 1 || peak >= 30000) throw new Exception("Background changed or mix exceeded headroom");
            using (var writer = new BinaryWriter(File.Create(args[0])))
            { Header(writer, rendered.Length); foreach (short sample in rendered) writer.Write(sample); }
            Console.WriteLine("Music contribution unchanged: subtraction error <= " + maximumError + " PCM unit. Peak: " + peak + "/32767. Music=" + AudioLevels.Music + ", card=" + AudioLevels.Card);
        }
        return 0;
    }
}
