using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace ShpilSvoih
{
    public static class MusicTests
    {
        sealed class Voice : IMusicVoice
        {
            public bool Paused = true, Disposed;
            public void SetPaused(bool value) { Paused = value; }
            public void Dispose() { Disposed = true; }
        }
        static void Await(Func<bool> condition, string message)
        {
            var watch = Stopwatch.StartNew();
            while (!condition() && watch.ElapsedMilliseconds < 10000) Thread.Sleep(5);
            TestSuite.Check(condition(), message);
        }
        public static void StateAndCancellation()
        {
            var voices = new List<Voice>();
            int loads = 0;
            Func<string, CancellationToken, MusicPcm> loader = (id, cancel) =>
            {
                Interlocked.Increment(ref loads);
                if (id == "illya")
                    for (int i = 0; i < 20; i++) { cancel.ThrowIfCancellationRequested(); Thread.Sleep(5); }
                return new MusicPcm(22050, 1, new byte[882]);
            };
            using (var player = new MusicPlayer(loader, pcm => { var v = new Voice(); lock (voices) voices.Add(v); return v; }))
            {
                player.SetTrack("pupsolet", false);
                Await(() => !player.IsLoading, "first track loads asynchronously");
                Voice first; lock (voices) first = voices[0];
                TestSuite.Check(!first.Paused && player.LastError == "", "active track starts");
                player.SetTrack("pupsolet", false);
                TestSuite.Check(loads == 1 && !first.Disposed, "repaint or repeated hero click does not restart song");
                player.SetTrack("pupsolet", true);
                TestSuite.Check(first.Paused, "pause suspends existing buffer");
                player.SetTrack("pupsolet", false);
                TestSuite.Check(!first.Paused && loads == 1, "resume keeps same buffer");
                player.SetTrack("illya", false);
                player.SetTrack("mimicrator", true);
                Await(() => !player.IsLoading, "latest requested theme wins");
                TestSuite.Check(first.Disposed && player.Track == "mimicrator" && player.IsPaused, "prior voice stopped before next theme");
                player.SetTrack("", true);
                TestSuite.Check(player.Track == "" && !player.IsLoading, "leaving scene stops music");
            }
            lock (voices) foreach (var voice in voices) TestSuite.Check(voice.Disposed, "all voices disposed");
            var late = new ManualResetEvent(false);
            var released = new ManualResetEvent(false);
            var stale = new MusicPlayer((id, cancel) => { late.Set(); released.WaitOne(); return new MusicPcm(22050, 1, new byte[882]); }, pcm => new Voice());
            stale.SetTrack("illya", false);
            TestSuite.Check(late.WaitOne(1000), "delayed load starts");
            stale.Dispose(); released.Set();
            Await(() => !stale.IsLoading, "dispose cancels outstanding load");
            using (var failed = new MusicPlayer((id, cancel) => { throw new IOException("test error"); }, pcm => new Voice()))
            {
                failed.SetTrack("shuten", false);
                Await(() => !failed.IsLoading, "audio error completes without failing game");
                TestSuite.Check(failed.LastError == "test error", "audio error remains diagnosable");
            }
        }
        public static void EmbeddedSongs()
        {
            string[] ids = { "pupsolet", "illya", "runtso", "shuten", "mimicrator", "babai" };
            int[] frames = { 1955835, 1644930, 7673400, 8061480, 8004863, 8022767 };
            for (int i = 0; i < ids.Length; i++)
            {
                var pcm = MusicPlayer.LoadEmbedded(ids[i], CancellationToken.None);
                TestSuite.Check(pcm.SampleRate == (ids[i] == "babai" ? 48000 : 44100) && pcm.Channels == 2, "original sample rate and channels: " + ids[i]);
                TestSuite.Check(pcm.Data.Length == frames[i] * 4, "expected prepared sample count: " + ids[i]);
                TestSuite.Check(Array.Exists(pcm.Data, value => value != 0), "embedded song is not silence: " + ids[i]);
                if (i < 4) TestSuite.Check(MusicTracks.Hero(i) == ids[i], "hero theme mapping");
            }
            TestSuite.Throws<InvalidDataException>(() => new MusicPcm(44100, 2, new byte[3]), "partial sample rejected");
            TestSuite.Throws<ArgumentOutOfRangeException>(() => MusicTracks.Hero(-1), "invalid hero rejected");
        }
        public static void NativeLoop()
        {
            // Silent 20ms buffer: validate the actual Windows device without a test beep.
            var pcm = new MusicPcm(22050, 1, new byte[882]);
            using (var voice = new LoopingMusicVoice(pcm))
            {
                voice.SetPaused(false); Thread.Sleep(220);
                uint played = voice.PlayedBytes;
                TestSuite.Check(played > pcm.Data.Length * 2, "native buffer wraps repeatedly without a restart timer");
                voice.SetPaused(true); Thread.Sleep(60);
                uint paused = voice.PlayedBytes;
                Thread.Sleep(120);
                TestSuite.Check(voice.PlayedBytes == paused, "native pause holds playback position");
                voice.SetPaused(false); Thread.Sleep(100);
                TestSuite.Check(voice.PlayedBytes > paused, "native resume continues playback");
            }
        }
    }

    public static partial class UiTests
    {
        static string MusicChoice(Game game)
        {
            return (string)typeof(Game).GetProperty("DesiredMusicTrack", PrivateInstance).GetValue(game, null);
        }
        public static void TestMusicScenes()
        {
            using (var game = new Game())
            {
                game.CreateControl();
                TestSuite.Check(MusicChoice(game) == "", "startup waits for explicit hero selection");
                using (Render(game)) { }
                for (int i = 0; i < 4; i++)
                {
                    Click(game, 50 + i * 188, 530);
                    TestSuite.Check(MusicChoice(game) == MusicTracks.Hero(i), "actual hero click selects correct theme");
                    using (Render(game)) { }
                }
                Key(game, Keys.Enter);
                TestSuite.Check(MusicChoice(game) == "", "checkout stops hero theme");
                Call(game, "StartTestMimicrator");
                TestSuite.Check(MusicChoice(game) == "mimicrator", "elite combat selects elite theme");
                var fight = Read<CombatState>(game, "combat");
                Call(game, "EndCombatTurn"); Call(game, "AdvanceCombatAnimation", 5f);
                TestSuite.Check(MusicChoice(game) == "mimicrator" && fight.Turn == 2, "form and turn changes do not change theme");
                Call(game, "ReturnToMainMenu");
                TestSuite.Check(MusicChoice(game) == "", "return to menu waits for hero selection");
                Call(game, "StartTestBabai");
                TestSuite.Check(MusicChoice(game) == "babai", "Babai owns his separate battle theme");
                Call(game, "EndCombatTurn"); Call(game, "AdvanceCombatAnimation", 5f);
                TestSuite.Check(MusicChoice(game) == "babai", "Babai theme persists across turns");
                Call(game, "StartTestDogs");
                TestSuite.Check(MusicChoice(game) == "", "ordinary battles do not borrow hero songs");
                foreach (string launch in new[] { "StartTestIllyaToha", "StartTestHedgehogs" })
                {
                    Call(game, launch);
                    TestSuite.Check(MusicChoice(game) == "", "hero themes excluded from " + launch);
                }
                Call(game, "ReturnToMainMenu");
                TestSuite.Check(Read<MusicPlayer>(game, "music").Track == "", "offscreen windows never open audio device");
            }
        }
        public static void TestConstantBattleMusic()
        {
            using (var game = new Game())
            {
                game.CreateControl();
                Func<double> gain = () => (double)typeof(Game).GetProperty("DesiredMusicGain", PrivateInstance).GetValue(game, null);
                Call(game, "ChooseMenuHero", 3);
                TestSuite.Check(MusicChoice(game) == "shuten" && gain() == AudioLevels.Music, "selected hero song belongs to menu");
                Call(game, "StartTestMimicrator");
                TestSuite.Check(MusicChoice(game) == "mimicrator" && gain() == AudioLevels.Music, "battle starts already quiet");
                var fight = Read<CombatState>(game, "combat");
                var hand = (System.Collections.Generic.List<CardData>)typeof(CombatState).GetField("hand", PrivateInstance).GetValue(fight);
                foreach (bool attack in new[] { true, false })
                {
                    hand.Clear(); hand.Add(new CardData { Name = "Audio", Attack = attack, Skill = !attack,
                        Damage = attack ? 6 : 0, Block = attack ? 0 : 5, GoldCost = 10 });
                    Call(game, "PlayCombatCard", 0);
                    foreach (float elapsed in new[] { .02f, .13f, .30f, .80f })
                    {
                        Call(game, "AdvanceAudio", elapsed); Call(game, "AdvanceCombatAnimation", elapsed);
                        Call(game, "SyncMusic");
                        TestSuite.Check(gain() == AudioLevels.Music && Read<MusicPlayer>(game, "music").Gain == AudioLevels.Music,
                            "preparation, impact and tail never modulate music");
                    }
                }
                Call(game, "SetInventory", "pause"); Call(game, "SetInventory", "");
                TestSuite.Check(gain() == AudioLevels.Music, "pause keeps fixed battle level");
                Call(game, "EndCombatTurn"); Call(game, "AdvanceCombatAnimation", 5f); Call(game, "SyncMusic");
                TestSuite.Check(gain() == AudioLevels.Music, "enemy turn never modulates music");
                Call(game, "StartTestBabai"); Call(game, "SyncMusic");
                TestSuite.Check(MusicChoice(game) == "babai" && gain() == AudioLevels.Music, "Babai uses the same fixed background level");
                Call(game, "ReturnToMainMenu"); Call(game, "ChooseMenuHero", 3);
                TestSuite.Check(MusicChoice(game) == "shuten" && gain() == AudioLevels.Music, "menu restores its own fixed level");
            }
        }
    }
}
