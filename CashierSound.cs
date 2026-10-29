using System;
using System.IO;
using System.Threading;

namespace ShpilSvoih
{
    // A single finite voice on the existing mixer; never changes the music gain.
    public sealed class CashierSound : IDisposable
    {
        readonly Func<MusicPcm> load;
        readonly Func<MusicPcm, IMusicVoice> create;
        MusicPcm clip;
        IMusicVoice voice;
        double remaining;
        bool paused = true, disposed;
        public bool Active { get { return voice != null; } }
        public string LastError { get; private set; }
        public CashierSound() : this(LoadEmbedded, pcm => new LoopingMusicVoice(pcm, false)) { }
        public CashierSound(Func<MusicPcm> load, Func<MusicPcm, IMusicVoice> create)
        {
            if (load == null || create == null) throw new ArgumentNullException("load");
            this.load = load; this.create = create; LastError = "";
        }
        public static MusicPcm LoadEmbedded()
        {
            using (var stream = typeof(CashierSound).Assembly.GetManifestResourceStream("sfx.cashier.glitch.pcm.gz"))
            {
                if (stream == null) throw new FileNotFoundException("Missing cashier glitch.");
                return MusicPcm.Read(stream, CancellationToken.None);
            }
        }
        public void Play()
        {
            if (disposed || Active) return;
            try
            {
                if (clip == null) clip = load();
                voice = create(clip);
                var volume = voice as IAudioVolumeVoice;
                if (volume != null) volume.SetVolume(AudioLevels.Cashier);
                remaining = clip.Data.Length / (double)(clip.SampleRate * clip.Channels * 2) + .12;
                paused = true; SetPaused(false); LastError = "";
            }
            catch (InvalidOperationException e) { Fail(e); }
            catch (IOException e) { Fail(e); }
            catch (System.Runtime.InteropServices.ExternalException e) { Fail(e); }
        }
        void Fail(Exception e) { Stop(); LastError = e.Message; }
        public void SetPaused(bool value)
        {
            if (disposed || voice == null || paused == value) return;
            try { voice.SetPaused(value); paused = value; }
            catch (InvalidOperationException e) { Fail(e); }
        }
        public void Advance(float seconds)
        {
            if (!Active || paused || !Motion.IsFinite(seconds) || seconds <= 0) return;
            remaining -= seconds;
            if (remaining <= 0) Stop();
        }
        public void Stop()
        {
            if (voice != null) voice.Dispose();
            voice = null; remaining = 0; paused = true;
        }
        public void Dispose()
        {
            if (disposed) return;
            Stop(); clip = null; disposed = true;
        }
    }
}
