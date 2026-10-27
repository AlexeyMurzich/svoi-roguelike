using System;
using System.IO;
using System.Threading;

namespace ShpilSvoih
{
    // Two small approved PCM clips, decoded once per game; one independent native voice.
    public sealed class TohaAttackSound : IDisposable
    {
        readonly Func<bool, MusicPcm> load;
        readonly Func<MusicPcm, IMusicVoice> create;
        readonly MusicPcm[] clips = new MusicPcm[2];
        IMusicVoice voice;
        bool paused = true, disposed;
        public string LastError { get; private set; }

        public TohaAttackSound() : this(LoadEmbedded, pcm => new LoopingMusicVoice(pcm, false)) { }
        public TohaAttackSound(Func<bool, MusicPcm> load, Func<MusicPcm, IMusicVoice> create)
        {
            if (load == null || create == null) throw new ArgumentNullException("load");
            this.load = load; this.create = create; LastError = "";
        }
        public static MusicPcm LoadEmbedded(bool dark)
        {
            string name = dark ? "sfx.toha-dark.pcm.gz" : "sfx.toha-light.pcm.gz";
            using (var stream = typeof(TohaAttackSound).Assembly.GetManifestResourceStream(name))
            {
                if (stream == null) throw new FileNotFoundException("Missing attack sound.", name);
                return MusicPcm.Read(stream, CancellationToken.None);
            }
        }
        public void Play(bool dark, bool pause)
        {
            if (disposed) return;
            Stop();
            try
            {
                int index = dark ? 1 : 0;
                if (clips[index] == null) clips[index] = load(dark);
                voice = create(clips[index]);
                paused = true;
                SetPaused(pause);
                LastError = "";
            }
            catch (InvalidOperationException error) { Fail(error); }
            catch (IOException error) { Fail(error); }
            catch (System.Runtime.InteropServices.ExternalException error) { Fail(error); }
        }
        void Fail(Exception error) { Stop(); LastError = error.Message; }
        public void SetPaused(bool value)
        {
            if (disposed || voice == null || paused == value) return;
            try { voice.SetPaused(value); paused = value; }
            catch (InvalidOperationException error) { Fail(error); }
        }
        public void Stop()
        {
            if (voice != null) voice.Dispose();
            voice = null; paused = true;
        }
        public void Dispose()
        {
            if (disposed) return;
            Stop(); clips[0] = clips[1] = null; disposed = true;
        }
    }
}
