using System;

namespace ShpilSvoih
{
    // Logical voice facade over ONE software mixer/device per process.
    public sealed class LoopingMusicVoice : IMusicVoice, IAudioVolumeVoice
    {
        static readonly object gate = new object();
        static SoftwareAudioMixer mixer;
        static WaveOutMixerOutput output;
        static int leases;
        static bool menuBuffering;
        internal static void SetMenuBuffering(bool value) { lock (gate) menuBuffering = value; }
        SoftwareAudioMixer.Channel channel;
        public static int OpenDeviceCount { get { lock (gate) return output == null ? 0 : 1; } }
        public static string OutputError { get { lock (gate) return output == null ? "" : output.LastError; } }
        public static uint OutputPlayedBytes { get { lock (gate) return output == null ? 0 : output.PlayedBytes; } }
        public LoopingMusicVoice(MusicPcm pcm) : this(pcm, true) { }
        public LoopingMusicVoice(MusicPcm pcm, bool loop)
        {
            if (pcm == null) throw new ArgumentNullException("pcm");
            lock (gate)
            {
                if (output == null)
                {
                    var next = new SoftwareAudioMixer();
                    output = new WaveOutMixerOutput(next, loop && menuBuffering); mixer = next;
                }
                channel = mixer.Add(pcm, loop); leases++;
            }
        }
        public uint PlayedBytes { get { lock (gate) return channel == null ? 0 : channel.PlayedBytes; } }
        public double Volume { get { lock (gate) return channel == null ? 0 : channel.Volume; } }
        public void SetVolume(double gain) { lock (gate) if (channel != null) channel.SetVolume(gain); }
        public void SetPaused(bool value)
        {
            lock (gate)
            {
                if (channel == null) return;
                channel.SetPaused(value); output.RefreshPause();
            }
        }
        public void Dispose()
        {
            lock (gate)
            {
                if (channel == null) return;
                channel.Dispose(); channel = null; leases--;
                if (leases == 0) { output.Dispose(); output = null; mixer = null; }
                else try { output.RefreshPause(); } catch (InvalidOperationException) { }
            }
        }
    }
}
