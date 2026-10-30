using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using ShpilSvoih;

internal static class NegativeRelicProbe
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
    static Game game;
    static string output, log = "";
    static T Read<T>(string name) { return (T)typeof(Game).GetField(name, Private).GetValue(game); }
    static void Write(string name, object value) { typeof(Game).GetField(name, Private).SetValue(game, value); }
    static object Call(string name, params object[] args) { return typeof(Game).GetMethod(name, Private).Invoke(game, args); }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); log += "PASS " + message + "\r\n"; }
    static void Capture(string name)
    {
        game.Refresh();
        using (var frame = new Bitmap(game.ClientSize.Width, game.ClientSize.Height))
        { game.DrawToBitmap(frame, new Rectangle(Point.Empty, frame.Size)); frame.Save(Path.Combine(output, name + ".png")); }
    }
    static void FinishFight()
    {
        var fight = Read<CombatState>("combat");
        for (int n = 0; n < 200 && fight.Phase != CombatPhase.Victory && fight.Phase != CombatPhase.Defeat; n++)
        {
            if (Read<AttackTimeline>("attackTimeline").IsRunning || Read<DeathVisuals>("deathVisuals").IsRunning) { Call("AdvanceCombatAnimation", 1f); continue; }
            int card = fight.Hand.ToList().FindIndex(c => fight.CanPlay(fight.Hand.IndexOf(c)));
            if (card >= 0) Call("PlayCombatCard", card); else Call("EndCombatTurn");
        }
        Call("AdvanceCombatAnimation", 1f); Check(fight.Phase == CombatPhase.Victory, "native card pipeline wins affected fight");
    }
    [STAThread] static int Main(string[] args)
    {
        output = Path.GetFullPath(args[0]); Directory.CreateDirectory(output);
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        using (game = new Game())
        {
            game.ClientSize = new Size(1280, 840); game.ShowInTaskbar = false;
            game.Shown += delegate { game.BeginInvoke((Action)delegate {
                try
                {
                    Check(game.IsHandleCreated && game.Visible, "live native game window");
                    Call("StartTestDoctorEvent");
                    var run = Read<MapRun>("mapRun"); var hero = Read<HeroState>("currentHero");
                    hero.ReceiveDamage(20); run.DoctorsAt(run.Pending.Id).Treat(hero);
                    Check(hero.Health == hero.MaxHealth && hero.Doctors.LostOrgans.Count == 1, "treatment fully heals and gives exactly one negative relic"); Capture("treatment");
                    Call("CompleteMapRoom");
                    foreach (Organ organ in Enum.GetValues(typeof(Organ))) hero.Doctors.Lose(organ, hero);
                    hero.HealFully(); Call("SetInventory", "relics"); Capture("negative-inventory"); Call("SetInventory", "");
                    Check(hero.MaxHealth == 37 && (int)Call("NegativeFights", Organ.Eye) == 3, "permanent heart and three future fights");
                    foreach (var card in hero.Deck) { card.Attack = true; card.Skill = false; card.Damage = 1000; card.GoldCost = 0; card.ManaCost = 0; }
                    for (int step = 0; step < 3; step++)
                    {
                        var route = new MapRun(ActMap.Generate(42)); Write("mapRun", route); Call("EnterMapNode", route.Current.Exits[0]);
                        var fight = Read<CombatState>("combat");
                        Check(fight.HiddenCosts && fight.LiverPenaltyActive && (int)Call("NegativeFights", Organ.Eye) == 3 - step && (int)Call("NegativeFights", Organ.Liver) == 3 - step, "current fight counted once: " + (step + 1));
                        Capture("fight-" + (step + 1)); Call("SetInventory", "relics"); Capture("inventory-fight-" + (step + 1)); Call("SetInventory", "");
                        FinishFight(); Call("CompleteMapRoom");
                    }
                    Check((int)Call("NegativeFights", Organ.Eye) == 0 && (int)Call("NegativeFights", Organ.Liver) == 0 && hero.MaxHealth == 37, "temporary penalties expire; heart persists");
                    Call("SetInventory", "relics"); Capture("expired");
                    foreach (DoctorDrop item in Enum.GetValues(typeof(DoctorDrop))) hero.Doctors.Give(item);
                    Write("doctorRelicPage", 1); Capture("second-page"); Check(Read<int>("doctorRelicPage") == 1, "mixed relic inventory pagination");
                    Call("ReturnToMainMenu"); Check(Read<CombatState>("combat") == null, "menu clears combat");
                }
                catch (Exception error) { log += "FAIL " + error + "\r\n"; Environment.ExitCode = 1; }
                finally { game.Close(); }
            }); };
            Application.Run(game);
        }
        Check(LoopingMusicVoice.OpenDeviceCount == 0, "native close releases audio");
        File.WriteAllText(Path.Combine(output, "negative-relics-live.txt"), log); Console.WriteLine(log); return Environment.ExitCode;
    }
}
