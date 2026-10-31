using System;
using System.IO;
using System.Threading;

namespace ShpilSvoih
{
    // Two shared decoded clips and at most one finite voice for each of three actors.
    public sealed class SkaterAttackSound : IDisposable
    {
        readonly Func<bool, MusicPcm> load;
        readonly Func<MusicPcm, IMusicVoice> create;
        readonly MusicPcm[] clips = new MusicPcm[2];
        readonly IMusicVoice[] voices = new IMusicVoice[3];
        readonly double[] remaining = new double[3];
        bool paused = true, disposed;
        public string LastError { get; private set; }
        public int PlayCount { get; private set; }
        public bool IsPaused { get { return paused; } }
        public int ActiveVoices { get { int n = 0; foreach (var voice in voices) if (voice != null) n++; return n; } }
        public SkaterAttackSound() : this(LoadEmbedded, pcm => new LoopingMusicVoice(pcm, false)) { }
        public SkaterAttackSound(Func<bool, MusicPcm> load, Func<MusicPcm, IMusicVoice> create)
        {
            if (load == null || create == null) throw new ArgumentNullException();
            this.load = load; this.create = create; LastError = "";
        }
        public static MusicPcm LoadEmbedded(bool master)
        {
            string name = "sfx.skaters." + (master ? "lightsaber" : "blaster") + ".pcm.gz";
            using (var stream = typeof(Game).Assembly.GetManifestResourceStream(name))
            {
                if (stream == null) throw new FileNotFoundException("Missing skater attack sound", name);
                return MusicPcm.Read(stream, CancellationToken.None);
            }
        }
        void StopAt(int i) { if (voices[i] != null) voices[i].Dispose(); voices[i] = null; remaining[i] = 0; }
        public void Play(int actor, bool suspend)
        {
            if (actor < 0 || actor > 2) throw new ArgumentOutOfRangeException("actor");
            if (disposed) return;
            SetPaused(suspend);
            StopAt(actor);
            try
            {
                int clip = actor == 0 ? 0 : 1;
                if (clips[clip] == null) clips[clip] = load(actor == 0);
                voices[actor] = create(clips[clip]);
                var volume = voices[actor] as IAudioVolumeVoice;
                if (volume != null) volume.SetVolume(AudioLevels.Enemy);
                remaining[actor] = clips[clip].Data.Length / (double)(clips[clip].SampleRate * clips[clip].Channels * 2) + .12;
                voices[actor].SetPaused(suspend); paused = suspend; PlayCount++; LastError = "";
            }
            catch (IOException e) { StopAt(actor); LastError = e.Message; }
            catch (InvalidOperationException e) { StopAt(actor); LastError = e.Message; }
            catch (System.Runtime.InteropServices.ExternalException e) { StopAt(actor); LastError = e.Message; }
        }
        public void Advance(float seconds)
        {
            if (disposed || paused || !Motion.IsFinite(seconds) || seconds <= 0) return;
            for (int i = 0; i < 3; i++) if (voices[i] != null) { remaining[i] -= seconds; if (remaining[i] <= 0) StopAt(i); }
        }
        public void SetPaused(bool value)
        {
            if (disposed || paused == value) return;
            paused = value;
            for (int i = 0; i < 3; i++) if (voices[i] != null)
                try { voices[i].SetPaused(value); }
                catch (InvalidOperationException e) { StopAt(i); LastError = e.Message; }
        }
        public void Stop() { for (int i = 0; i < 3; i++) StopAt(i); paused = true; }
        public void Dispose() { if (disposed) return; Stop(); clips[0] = clips[1] = null; disposed = true; }
    }
}
