using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using ShpilSvoih;

// Focused integration probe against the standalone release EXE, with no loose assets.
internal static class AudioSmoke
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static Game game;
    static Timer timer;
    static string output;
    static Stopwatch clock = new Stopwatch();
    static int stage;
    static double since;
    static string log = "";
    static T Read<T>(string name) { return (T)typeof(Game).GetField(name, Private).GetValue(game); }
    static void Call(string name, params object[] args) { typeof(Game).GetMethod(name, Private).Invoke(game, args); }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); log += "PASS " + message + "\r\n"; }
    static LoopingMusicVoice CardVoice()
    {
        var items = (IList)typeof(CardSoundPlayer).GetField("voices", Private).GetValue(Read<CardSoundPlayer>("cardSounds"));
        return (LoopingMusicVoice)items[items.Count - 1].GetType().GetField("Voice").GetValue(items[items.Count - 1]);
    }
    static void Play(bool attack)
    {
        var fight = Read<CombatState>("combat");
        var hand = (IList)typeof(CombatState).GetField("hand", Private).GetValue(fight);
        hand.Clear(); hand.Add(new CardData { Name = "Audio probe", Attack = attack, Skill = !attack, Damage = attack ? 6 : 0, Block = attack ? 0 : 5, ManaCost = 1, GoldCost = 10 });
        Call("PlayCombatCard", 0);
    }
    static void Next() { stage++; since = clock.Elapsed.TotalSeconds; }
    static void Tick(object sender, EventArgs args)
    {
        try
        {
            if (clock.Elapsed.TotalSeconds > 30) throw new TimeoutException("Audio smoke exceeded 30 seconds");
            if (!game.ContainsFocus) { game.Activate(); return; }
            var cards = Read<CardSoundPlayer>("cardSounds"); var music = Read<MusicPlayer>("music");
            if (stage == 0)
            {
                Call("ChooseMenuHero", 3);
                Check(music.Track == "shuten", "hero selection owns the menu song");
                Call("StartTestMimicrator"); Next();
            }
            else if (stage == 1 && !music.IsLoading && clock.Elapsed.TotalSeconds - since > .2)
            {
                Check(music.Track == "mimicrator" && music.Gain == AudioLevels.Music && music.LastError == "", "battle music starts at fixed quiet level");
                Play(true); Check(cards.ActiveVoices == 1 && !cards.IsPaused, "accepted attack starts one native effect"); Next();
                Check(LoopingMusicVoice.OpenDeviceCount == 1, "music and card use exactly one Windows output");
            }
            else if (stage == 2 && clock.Elapsed.TotalSeconds - since > .13)
            {
                Check(music.Gain == AudioLevels.Music, "card preparation does not change music level");
                Check(CardVoice().Volume == AudioLevels.Card, "card uses reduced software level");
                Call("SetInventory", "pause");
                var voice = CardVoice(); uint at = voice.PlayedBytes;
                System.Threading.Thread.Sleep(60);
                uint deviceAt = LoopingMusicVoice.OutputPlayedBytes;
                System.Threading.Thread.Sleep(90);
                Check(cards.IsPaused && music.IsPaused && voice.PlayedBytes == at, "pause holds both channels at exact positions");
                Check(LoopingMusicVoice.OutputPlayedBytes == deviceAt, "pause also holds the physical Windows output position");
                Call("SetInventory", ""); Check(!cards.IsPaused && !music.IsPaused, "resume keeps both existing native voices"); Next();
            }
            else if (stage == 3 && clock.Elapsed.TotalSeconds - since > .82)
            {
                Check(!Read<AttackTimeline>("attackTimeline").IsRunning, "card animation completes after resumed impact");
                Play(false); Check(cards.ActiveVoices <= 2, "second card bounds overlap");
                using (var bitmap = new Bitmap(game.ClientSize.Width, game.ClientSize.Height))
                { game.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(output, "audio-battle.png")); }
                Next();
            }
            else if (stage == 4 && clock.Elapsed.TotalSeconds - since > 1.9)
            {
                Check(cards.ActiveVoices == 0 && music.Gain == AudioLevels.Music, "finished effects release voices without a music swell");
                Call("StartTestMimicrator"); Check(cards.ActiveVoices == 0, "scene change clears old effects"); Next();
            }
            else if (stage == 5 && !music.IsLoading && clock.Elapsed.TotalSeconds - since > .2)
            {
                Check(music.Track == "mimicrator" && music.LastError == "", "standalone elite loads separate music");
                Call("StartTestIllyaToha"); Next();
            }
            else if (stage == 6 && !music.IsLoading && clock.Elapsed.TotalSeconds - since > .3)
            {
                Check(music.Track == "" && music.LastError == "", "Toha battle does not borrow Illya menu theme");
                Check(cards.ActiveVoices <= 1 && cards.LastError == "", "automatic opening cards produce a single bounded accent");
                Call("StartTestBabai"); Next();
            }
            else if (stage == 7 && !music.IsLoading) { Next(); }
            else if (stage == 8 && clock.Elapsed.TotalSeconds - since > .6)
            {
                Check(music.Track == "babai" && music.LastError == "" && !music.IsPaused, "Babai theme plays through native mixer");
                var babaiVoice = (LoopingMusicVoice)typeof(MusicPlayer).GetField("voice", Private).GetValue(music);
                Check(babaiVoice != null && babaiVoice.PlayedBytes > 44100, "Babai PCM advances on the native device");
                using (var bitmap = new Bitmap(game.ClientSize.Width, game.ClientSize.Height))
                { game.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(output, "babai-music.png")); }
                Check(music.Gain == AudioLevels.Music && LoopingMusicVoice.OpenDeviceCount == 1, "Babai uses fixed background gain and one device");
                Call("SetInventory", "pause"); Check(music.IsPaused, "Babai theme pauses with game");
                uint babaiAt = babaiVoice.PlayedBytes; System.Threading.Thread.Sleep(120);
                Check(babaiVoice.PlayedBytes == babaiAt, "Babai pause preserves actual sample position");
                Call("SetInventory", ""); Check(!music.IsPaused, "Babai theme resumes with game");
                Call("ReturnToMainMenu"); Check(music.Track == "", "Babai theme stops on leaving battle");
                timer.Stop(); game.Close();
            }
        }
        catch (Exception failure)
        {
            log += "FAIL " + failure + "\r\n"; Environment.ExitCode = 1; timer.Stop(); game.Close();
        }
    }
    [STAThread]
    static int Main(string[] args)
    {
        output = Path.GetFullPath(args[0]); Directory.CreateDirectory(output);
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        using (game = new Game())
        using (timer = new Timer { Interval = 25 })
        {
            game.StartPosition = FormStartPosition.CenterScreen; game.ShowInTaskbar = false;
            timer.Tick += Tick;
            game.Shown += delegate { clock.Start(); timer.Start(); };
            Application.Run(game);
        }
        var cleanup = Stopwatch.StartNew();
        while (LoopingMusicVoice.OpenDeviceCount != 0 && cleanup.ElapsedMilliseconds < 1000) System.Threading.Thread.Sleep(5);
        Check(LoopingMusicVoice.OpenDeviceCount == 0, "closing game releases the shared output and worker");
        File.WriteAllText(Path.Combine(output, "audio-live-smoke.txt"), log);
        Console.WriteLine(log); return Environment.ExitCode;
    }
}
