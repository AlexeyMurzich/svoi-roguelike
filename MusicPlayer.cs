using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ShpilSvoih
{
    public static class MusicTracks
    {
        static readonly string[] heroes = { "pupsolet", "illya", "runtso", "shuten" };
        public static string Hero(int index)
        {
            if (index < 0 || index >= heroes.Length) throw new ArgumentOutOfRangeException("index");
            return heroes[index];
        }
        public static bool IsValid(string track)
        {
            return track == "" || track == "mimicrator" || Array.IndexOf(heroes, track) >= 0;
        }
    }

    public sealed class MusicPcm
    {
        public readonly int SampleRate;
        public readonly ushort Channels;
        public readonly byte[] Data;
        public MusicPcm(int sampleRate, ushort channels, byte[] data)
        {
            if (sampleRate < 8000 || sampleRate > 192000 || (channels != 1 && channels != 2) ||
                data == null || data.Length == 0 || data.Length > 256 * 1024 * 1024 || data.Length % (channels * 2) != 0)
                throw new InvalidDataException("Invalid PCM music format.");
            SampleRate = sampleRate; Channels = channels; Data = data;
        }
        public static MusicPcm Read(Stream compressed, CancellationToken cancel)
        {
            using (var gzip = new GZipStream(compressed, CompressionMode.Decompress))
            using (var reader = new BinaryReader(gzip, Encoding.ASCII))
            {
                if (Encoding.ASCII.GetString(reader.ReadBytes(8)) != "SVOIPCM1")
                    throw new InvalidDataException("Invalid music header.");
                int rate = reader.ReadInt32();
                ushort channels = reader.ReadUInt16(), bits = reader.ReadUInt16();
                int length = reader.ReadInt32();
                if (bits != 16 || (channels != 1 && channels != 2) || rate < 8000 || rate > 192000 ||
                    length <= 0 || length > 256 * 1024 * 1024 || length % (channels * 2) != 0)
                    throw new InvalidDataException("Invalid music sample count.");
                cancel.ThrowIfCancellationRequested();
                var bytes = new byte[length];
                for (int offset = 0; offset < length; )
                {
                    cancel.ThrowIfCancellationRequested();
                    int read = gzip.Read(bytes, offset, Math.Min(65536, length - offset));
                    if (read == 0) throw new EndOfStreamException("Incomplete music data.");
                    offset += read;
                }
                if (gzip.ReadByte() != -1) throw new InvalidDataException("Unexpected trailing music data.");
                return new MusicPcm(rate, channels, bytes);
            }
        }
    }

    public interface IMusicVoice : IDisposable
    {
        void SetPaused(bool value);
    }

    // Decode on a worker, one active voice. Stale loads always remain silent.
    public sealed class MusicPlayer : IDisposable
    {
        readonly object gate = new object();
        readonly Func<string, CancellationToken, MusicPcm> load;
        readonly Func<MusicPcm, IMusicVoice> createVoice;
        CancellationTokenSource cancellation;
        IMusicVoice voice;
        string track = "", error = "";
        bool paused = true, disposed, loading;
        long generation;
        public MusicPlayer() : this(LoadEmbedded, pcm => new LoopingMusicVoice(pcm)) { }
        public MusicPlayer(Func<string, CancellationToken, MusicPcm> loader, Func<MusicPcm, IMusicVoice> factory)
        {
            if (loader == null || factory == null) throw new ArgumentNullException("loader");
            load = loader; createVoice = factory;
        }
        public string Track { get { lock (gate) return track; } }
        public string LastError { get { lock (gate) return error; } }
        public bool IsLoading { get { lock (gate) return loading; } }
        public bool IsPaused { get { lock (gate) return paused; } }
        public static MusicPcm LoadEmbedded(string id, CancellationToken cancel)
        {
            if (!MusicTracks.IsValid(id) || id == "") throw new ArgumentOutOfRangeException("id");
            var stream = typeof(Game).Assembly.GetManifestResourceStream("music." + id + ".pcm.gz");
            if (stream == null) throw new InvalidDataException("Missing music: " + id);
            return MusicPcm.Read(stream, cancel);
        }
        public void SetTrack(string id, bool suspend)
        {
            if (id == null || !MusicTracks.IsValid(id)) throw new ArgumentOutOfRangeException("id");
            lock (gate)
            {
                if (disposed) return;
                if (track == id)
                {
                    if (paused != suspend)
                    {
                        paused = suspend;
                        if (voice != null)
                        {
                            try { voice.SetPaused(paused); }
                            catch (Exception failure) { error = failure.Message; voice.Dispose(); voice = null; }
                        }
                    }
                    return;
                }
                generation++;
                if (cancellation != null) cancellation.Cancel();
                if (voice != null) { voice.Dispose(); voice = null; }
                track = id; paused = suspend; loading = id != ""; error = "";
                if (!loading) return;
                var source = new CancellationTokenSource();
                cancellation = source;
                long version = generation;
                Task.Run(() => LoadVoice(id, version, source));
            }
        }
        void LoadVoice(string id, long version, CancellationTokenSource source)
        {
            IMusicVoice candidate = null;
            try
            {
                var pcm = load(id, source.Token);
                source.Token.ThrowIfCancellationRequested();
                candidate = createVoice(pcm); // Newly created voices are paused.
                lock (gate)
                {
                    if (disposed || generation != version) return;
                    candidate.SetPaused(paused);
                    voice = candidate; candidate = null; loading = false;
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception failure)
            {
                lock (gate)
                    if (!disposed && generation == version) { error = failure.Message; loading = false; }
            }
            finally
            {
                if (candidate != null) candidate.Dispose();
                lock (gate)
                {
                    if (object.ReferenceEquals(cancellation, source)) cancellation = null;
                    source.Dispose();
                }
            }
        }
        public void Dispose()
        {
            lock (gate)
            {
                if (disposed) return;
                disposed = true; generation++; track = ""; loading = false;
                if (cancellation != null) cancellation.Cancel();
                if (voice != null) { voice.Dispose(); voice = null; }
            }
        }
    }
}
