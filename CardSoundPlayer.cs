using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace ShpilSvoih
{
    // Native voices mix independently of music and the existing enemy effects.
    // At most two voices: the new foreground and one quickly fading old tail.
    public sealed class CardSoundPlayer : IDisposable
    {
        sealed class Playing
        {
            public IMusicVoice Voice;
            public double Age, Duration, Gain, TailTime;
            public bool Tail;
        }
        static readonly string[] heroes = { "pupsolet", "illya", "runtso", "shuten" };
        readonly Func<int, bool, MusicPcm> load;
        readonly Func<MusicPcm, IMusicVoice> create;
        readonly MusicPcm[] clips = new MusicPcm[8];
        readonly MusicPcm[] instantClips = new MusicPcm[8];
        readonly List<Playing> voices = new List<Playing>(2);
        bool paused = true, disposed;
        public string LastError { get; private set; }
        public int ActiveVoices { get { return voices.Count; } }
        public bool IsPaused { get { return paused; } }
        public CardSoundPlayer() : this(LoadEmbedded, pcm => new LoopingMusicVoice(pcm, false)) { }
        public CardSoundPlayer(Func<int, bool, MusicPcm> load, Func<MusicPcm, IMusicVoice> create)
        {
            if (load == null || create == null) throw new ArgumentNullException("load");
            this.load = load; this.create = create; LastError = "";
        }
        public static MusicPcm LoadEmbedded(int hero, bool attack)
        {
            if (hero < 0 || hero > 3) throw new ArgumentOutOfRangeException("hero");
            string name = "sfx.cards." + heroes[hero] + (attack ? "-attack" : "-guard") + ".pcm.gz";
            using (var stream = typeof(CardSoundPlayer).Assembly.GetManifestResourceStream(name))
            {
                if (stream == null) throw new FileNotFoundException("Missing card sound", name);
                return MusicPcm.Read(stream, CancellationToken.None);
            }
        }
        // Called once when combat art is loaded, never on a render frame.
        public void Prepare(int hero)
        {
            if (hero < 0 || hero > 3) throw new ArgumentOutOfRangeException("hero");
            if (disposed) return;
            try
            {
                for (int i = 0; i < 2; i++)
                    if (clips[hero * 2 + i] == null) clips[hero * 2 + i] = load(hero, i == 0);
            }
            catch (IOException failure) { LastError = failure.Message; }
        }
        static void Volume(Playing voice)
        {
            var volume = voice.Voice as IAudioVolumeVoice;
            if (volume != null) volume.SetVolume(voice.Gain * (voice.Tail ? Math.Max(0, 1 - voice.TailTime / .09) : 1));
        }
        public void SoftenPrevious()
        {
            foreach (var voice in voices) voice.Tail = true;
        }
        public void Play(int hero, bool attack, bool suspend)
        {
            Play(hero, attack, suspend, false);
        }
        public void Play(int hero, bool attack, bool suspend, bool instant)
        {
            if (hero < 0 || hero > 3) throw new ArgumentOutOfRangeException("hero");
            if (disposed) return;
            Prepare(hero);
            var pcm = clips[hero * 2 + (attack ? 0 : 1)];
            if (pcm == null) return;
            if (instant)
            {
                int index = hero * 2 + (attack ? 0 : 1);
                if (instantClips[index] == null)
                {
                    int stride = pcm.Channels * 2;
                    int skip = Math.Min((int)Math.Round((attack ? .5 : .2) / 1.2 * pcm.SampleRate) * stride, pcm.Data.Length - stride);
                    var bytes = new byte[pcm.Data.Length - skip];
                    Buffer.BlockCopy(pcm.Data, skip, bytes, 0, bytes.Length);
                    // Edge fade avoids a cut transient when skipping the preparation.
                    int fadeFrames = Math.Min(66, bytes.Length / stride);
                    for (int frame = 0; frame < fadeFrames; frame++)
                        for (int channel = 0; channel < pcm.Channels; channel++)
                        {
                            int at = frame * stride + channel * 2;
                            short original = (short)(bytes[at] | bytes[at + 1] << 8);
                            short sample = (short)(original * frame / (double)fadeFrames);
                            bytes[at] = (byte)sample; bytes[at + 1] = (byte)(sample >> 8);
                        }
                    instantClips[index] = new MusicPcm(pcm.SampleRate, pcm.Channels, bytes);
                }
                pcm = instantClips[index];
            }
            SoftenPrevious();
            while (voices.Count >= 2) RemoveAt(0);
            IMusicVoice candidate = null;
            try
            {
                candidate = create(pcm);
                var item = new Playing { Voice = candidate, Duration = pcm.Data.Length / (double)(pcm.SampleRate * pcm.Channels * 2), Gain = AudioLevels.Card };
                Volume(item);
                candidate.SetPaused(suspend);
                voices.Add(item); candidate = null; paused = suspend; LastError = "";
            }
            catch (InvalidOperationException failure) { LastError = failure.Message; }
            catch (System.Runtime.InteropServices.ExternalException failure) { LastError = failure.Message; }
            finally { if (candidate != null) candidate.Dispose(); }
        }
        void RemoveAt(int index) { voices[index].Voice.Dispose(); voices.RemoveAt(index); }
        public void Advance(double seconds)
        {
            if (disposed || paused || double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0) return;
            for (int i = voices.Count - 1; i >= 0; i--)
            {
                var v = voices[i]; v.Age += seconds;
                if (v.Tail) v.TailTime += seconds;
                if (v.Age >= v.Duration || (v.Tail && v.TailTime >= .09)) { RemoveAt(i); continue; }
                if (v.Tail)
                    try { Volume(v); }
                    catch (InvalidOperationException failure) { LastError = failure.Message; RemoveAt(i); }
            }
        }
        public void SetPaused(bool value)
        {
            if (disposed || paused == value) return;
            paused = value;
            for (int i = voices.Count - 1; i >= 0; i--)
                try { voices[i].Voice.SetPaused(value); }
                catch (InvalidOperationException failure) { LastError = failure.Message; RemoveAt(i); }
        }
        public void Stop()
        {
            while (voices.Count > 0) RemoveAt(voices.Count - 1);
            paused = true;
        }
        public void Dispose()
        {
            if (disposed) return;
            Stop(); Array.Clear(clips, 0, clips.Length); Array.Clear(instantClips, 0, instantClips.Length); disposed = true;
        }
    }

}
