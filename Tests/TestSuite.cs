using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace ShpilSvoih
{
    public static class TestSuite
    {
        private static int assertions;
        private static int passed;
        private static string filter = "";
        private static readonly List<string> failures = new List<string>();
        private static readonly List<string> results = new List<string>();
        public static string OutputDirectory;
        public static void Note(string message)
        {
            results.Add("INFO " + message);
        }

        public static void Check(bool condition, string message)
        {
            assertions++;
            if (!condition)
                throw new Exception(message);
        }

        public static void Throws<T>(Action action, string message)
            where T : Exception
        {
            try
            {
                action();
            }
            catch (T)
            {
                Check(true, message);
                return;
            }

            throw new Exception("Expected " + typeof(T).Name + ": " + message);
        }

        private static void Run(string name, Action test)
        {
            if (filter.Length > 0 && name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) return;
            try
            {
                test();
                passed++;
                results.Add("PASS " + name);
            }
            catch (Exception error)
            {
                failures.Add(name);
                results.Add("FAIL " + name + "\n" + error);
            }
        }

        private static HeroState Hero(int who, string buff)
        {
            var hero = new HeroState(who, 50, 0, new Balance());
            Check(hero.Effects.Choose(buff), "choose " + buff);
            return hero;
        }

        [STAThread]
        public static int Main(string[] arguments)
        {
            System.Windows.Forms.Application.SetUnhandledExceptionMode(System.Windows.Forms.UnhandledExceptionMode.ThrowException);
            OutputDirectory = Path.GetFullPath(arguments.Length > 0 ? arguments[0] : "test-results");
            filter = arguments.Length > 1 ? arguments[1] : "";
            Directory.CreateDirectory(OutputDirectory);
            Run("Music: asynchronous switching, pause, cancellation and disposal", MusicTests.StateAndCancellation);
            Run("Music: five embedded tracks preserve all samples", MusicTests.EmbeddedSongs);
            Run("Music: native loop, pause and resume", MusicTests.NativeLoop);
            Run("Music: actual hero clicks and elite scene transitions", UiTests.TestMusicScenes);
            Run("Debug console: infinite gold and exact triple multiplier", DebugModeTests.Rules);
            Run("Debug console: input, pause and scene isolation", UiTests.TestDebugConsole);
            Run("Battle backdrop: source, cache and six sizes", UiTests.TestBattleBackground);
            Run("Combat hints: statuses, intents and scaled hover", UiTests.TestCombatHints);
            Run("Illya: free automatic cards and confusion costs", IllyaTests.Rules);
            Run("Illya: live costs and animated payment", UiTests.TestIllyaCosts);
            Run("Hero combat minimum: timing, pause and poses", UiTests.TestHeroCombatMinimum);
            Run("Audit: neck attachment", UiTests.TestNeckAttachment);
            Run("Audit: sprite cache and cadence", UiTests.TestAnimationCachePolicy);
            Run("Living: lifecycle and fixed paws", UiTests.TestLivingScenes);
            Run("Living: visual gallery", UiTests.TestLivingGallery);
            Run("All 12 original relic effects", StartingBuffTests.Run);
            Run("Invalid hero state and balance rejected", () =>
            {
                Throws<ArgumentOutOfRangeException>(() => new HeroState(-1, 1, 0, new Balance()), "hero range");
                Throws<ArgumentOutOfRangeException>(() => new HeroState(4, 1, 0, new Balance()), "hero range upper");
                Throws<ArgumentOutOfRangeException>(() => new HeroState(0, -1, 0, new Balance()), "negative hp");
                Throws<ArgumentOutOfRangeException>(() => new HeroState(0, 1, -1, new Balance()), "negative initial money");
                Throws<ArgumentNullException>(() => new HeroState(0, 1, 0, null), "null balance");
                Throws<ArgumentOutOfRangeException>(() => new HeroState(0, 1, 0, new Balance { TearsPerDamage = -1 }), "negative coefficient");
                var h = new HeroState(0, 1, 0, new Balance());
                h.Settings.MoneyLostPerTurn = -1;
                Throws<ArgumentOutOfRangeException>(() => h.BeginTurn(), "invalid edited config");
            });
            Run("Unknown and foreign relics cannot be selected", () =>
            {
                var h = new HeroState(3, 50, 0, new Balance());
                Check(!h.Effects.Choose("unknown") && !h.Effects.Choose(null), "invalid ids fail without crash");
                Check(!h.Effects.Choose("shield") && h.Effects.Chosen == null, "foreign relic blocked");
                var catalog = (IList<StartingBuff>)StartingBuff.All;
                Throws<NotSupportedException>(() => catalog[0] = catalog[1], "catalog immutable");
            });
            Run("Damage clamp and tears arithmetic", () =>
            {
                var h = new HeroState(0, 8, 0, new Balance());
                h.ReceiveDamage(-20);
                Check(h.Health == 8 && h.Tears == 0, "negative damage ignored");
                h.ReceiveDamage(100);
                Check(h.Health == 0 && h.Tears == 8, "actual hp lost only");
                h.ReceiveDamage(2);
                Check(h.Tears == 8, "no tears after death");
                h = new HeroState(0, int.MaxValue, 0, new Balance { TearsPerDamage = int.MaxValue, BonusDamagePerTear = 10 });
                h.ReceiveDamage(int.MaxValue);
                Check(h.Tears == int.MaxValue && h.BonusDamage == int.MaxValue, "no integer wrap");
            });
            Run("Debt gate, wealth requirements and large debt", () =>
            {
                var h = Hero(3, "ring");
                Check(!h.PayMoneyCard(301) && h.Money == 300, "no phone no overspend");
                Check(!h.PayMoneyCard(-1), "negative price cannot mint gold");
                h = Hero(3, "phone");
                Check(h.PayMoneyCard(3) && h.PayMoneyCard(2), "debt spending works");
                Check(h.Health == 45 && h.Debt == 5, "debt removes health");
                Check(!h.PayMoneyCard(1, 10), "wealth gate retained");
                h.ChangeMoney(5);
                Check(h.Health == 45 && h.Debt == 0, "repayment is not healing");
                h.ChangeMoney(-10000000000m);
                Check(h.Health == 0 && !h.PayMoneyCard(0), "large debt clamps hp, dead cannot pay");
            });
            Run("Fractional gold discounts", () =>
            {
                var h = Hero(3, "coupon");
                for (int price = 0; price < 250; price++)
                    Check(h.Effects.GoldPrice(price) == price * .9m, "no repeated integer rounding");
                Throws<ArgumentOutOfRangeException>(() => h.Effects.GoldPrice(-1), "negative quote");
            });
            Run("Eternal cards cannot be unlocked or discounted twice", () =>
            {
                var h = Hero(1, "polish");
                var card = new CardData
                {
                    PolishName = true,
                    ManaCost = 3
                };
                h.Effects.OnReceived(card);
                card.Enchantment = "changed externally";
                card.Removable = card.Sellable = true;
                Check(!card.Removable && !card.Sellable, "enchantment invariant");
                h.Effects.OnReceived(card);
                Check(card.ManaCost == 1.5m && card.Enchantment == "wieczny", "no repeated half price");
                Throws<ArgumentNullException>(() => h.Effects.OnReceived(null), "null card");
                Throws<ArgumentOutOfRangeException>(() => h.Effects.OnReceived(new CardData { ManaCost = -1 }), "bad price");
            });
            Run("Family restriction resets, invalid values rejected", () =>
            {
                var h = Hero(2, "feather");
                Check(!h.UseFamily((CardFamily)99) && !h.UseFamily(CardFamily.None), "invalid family");
                Check(h.UseFamily(CardFamily.Feminist) && h.UseFamily(CardFamily.Feminist), "multiple same family");
                Check(!h.UseFamily(CardFamily.Patriarchal), "opposite family locked");
                h.BeginTurn();
                Check(h.UseFamily(CardFamily.Patriarchal), "next turn reset");
            });
            Run("Automatic cards: 2200 seed/hand combinations", () =>
            {
                var h = new HeroState(1, 50, 0, new Balance());
                for (int seed = 0; seed < 200; seed++)
                    for (int size = 0; size < 11; size++)
                    {
                        var cards = h.ChooseAutomaticCards(size, new Random(seed));
                        Check(cards.Length == Math.Min(size, 2), "automatic count");
                        Check(cards.Distinct().Count() == cards.Length, "distinct positions");
                        Check(cards.All(index => index >= 0 && index < size), "valid positions");
                    }

                Check(h.ChooseAutomaticCards(-1, null).Length == 0, "empty hand safe");
                Throws<ArgumentNullException>(() => h.ChooseAutomaticCards(2, null), "random required");
            });
            Run("Investigation is idempotent per map and distinct node", () =>
            {
                var h = Hero(2, "investigation");
                var nodes = new List<IntelNode>();
                foreach (var kind in new[]
                {
                    IntelKind.Question,
                    IntelKind.Elite,
                    IntelKind.Fight
                }

                )
                    for (int i = 0; i < 4; i++)
                        nodes.Add(new IntelNode { Kind = kind });
                Check(h.Effects.Reveal(nodes, new Random(1)) == 6, "two per type");
                Check(h.Effects.Reveal(nodes, new Random(2)) == 0 && nodes.Count(n => n.Revealed) == 6, "same map no extra reveal");
                var duplicate = new IntelNode
                {
                    Kind = IntelKind.Question
                };
                Check(h.Effects.Reveal(new List<IntelNode> { duplicate, duplicate, null }, new Random(1)) == 1, "aliased nodes counted once");
                Throws<ArgumentNullException>(() => h.Effects.Reveal(null, new Random()), "null map");
                Throws<ArgumentNullException>(() => h.Effects.Reveal(new List<IntelNode>(), null), "null rng");
            });
            Run("Exchange callback is atomic and cannot re-enter", () =>
            {
                var h = Hero(0, "exchange");
                string result;
                Throws<InvalidOperationException>(() => h.Effects.TryExchangeCombatReward<string>(() =>
                {
                    throw new InvalidOperationException();
                }, out result), "failed replacement");
                Check(!h.Effects.RewardExchangeUsed, "exception keeps charge");
                bool innerSucceeded = true;
                Check(h.Effects.TryExchangeCombatReward<string>(() =>
                {
                    string inner;
                    innerSucceeded = h.Effects.TryExchangeCombatReward<string>(() => "second", out inner);
                    return "first";
                }, out result), "outer replacement");
                Check(!innerSucceeded && result == "first" && h.Effects.RewardExchangeUsed, "single atomic exchange");
            });
            Run("Uniform viewport, click mapping and image crop", () =>
            {
                foreach (var size in UiTests.Sizes)
                {
                    var viewport = Viewport.Fit(size);
                    Check(viewport.Scale > 0, "valid scale");
                    foreach (var point in new[]
                    {
                        new PointF(20, 20),
                        new PointF(600, 420),
                        new PointF(1250, 810)
                    }

                    )
                    {
                        PointF mapped;
                        Check(viewport.TryMap(viewport.ToClient(point), out mapped), "inverse valid");
                        Check(Math.Abs(mapped.X - point.X) < 1.1f && Math.Abs(mapped.Y - point.Y) < 1.1f, "inverse tolerance");
                    }

                    PointF ignored;
                    Check(!viewport.TryMap(new Point(-1, -1), out ignored), "outside click ignored");
                }

                Check(Viewport.Fit(Size.Empty).Scale == 0, "zero viewport");
                var crop = Viewport.CoverSource(new Size(1600, 1000), new SizeF(1280, 840));
                Check(Math.Abs(crop.Width / crop.Height - 1280f / 840f) < .001, "background aspect ratio");
                Check(crop.X >= 0 && crop.Y >= 0 && crop.Right <= 1600 && crop.Bottom <= 1000, "crop stays in source");
            });
            Run("Image ownership, fallback and file unlocking", UiTests.TestAssets);
            Run("UI all heroes and relics at six window sizes", UiTests.TestInteraction);
            Run("Resize/repaint determinism and GDI resources", UiTests.TestRepaint);
            Run("Procedural act constraints, determinism and connectivity: 2000 seeds", MapTests.Generation);
            Run("Map traversal rejects skips and repeated actions", MapTests.Traversal);
            Run("Map UI, modal clicks, investigation and resizing", UiTests.TestMapInteraction);
            Run("Scrollable layout and actual deck model", MapMenuTests.LayoutAndDeck);
            Run("Map scrolling, dragging, top bar, deck and relic menus at six sizes", UiTests.TestScrollableMapAndMenus);
            Run("Fullscreen bounds, taskbar coverage, minimal HUD and pause", UiTests.TestFullscreenAndMinimalHud);
            Run("Return to main menu and fresh run at six sizes", UiTests.TestReturnToMainMenu);
            Run("Combat rules, recycling, resources, victory and defeat", CombatTests.Rules);
            Run("Combat UI, animation, pause and victory at six sizes", UiTests.TestCombatUi);
            Run("Hero portraits and relic hover across all twelve relics and six sizes", UiTests.TestHudPortraitsAndTooltips);
            Run("Smooth camera timing at 30/60/120 Hz", MotionTests.Timing);
            Run("Smooth camera, pause, pose frames and render timing", UiTests.TestSmoothMotion);
            Run("Two dogs: staggered cycles, targeting, shared block, deaths and generation", DogTests.Rules);
            Run("Dog bite, accelerated timing, howl and pause at six sizes", UiTests.TestDogUi);
            Run("Animation lifecycle and timing at 10/30/60/120 Hz", ReviewTests.AnimationLifecycle);
            Run("Debt atomicity, fractional charges, card contracts and configured draw", ReviewTests.ModelBoundaries);
            Run("Hands of one to six cards preserve controls and aspect ratio", ReviewTests.HandLayout);
            Run("Reviewed input, capture, focus, overlay and debt-death interactions", UiTests.TestReviewedInteractions);
            Run("Investigation reveals future enemy identity on map hover", UiTests.TestInvestigationPreview);
            Run("Dog legs preserve color and opacity through bite and howl frames", UiTests.TestDogLegStability);
            Run("Repeated battle/map/pause/resize transitions release resources", UiTests.TestSceneResourceStress);
            Run("Death snapshots lifecycle and resource ownership", DeathTests.Lifecycle);
            Run("Separate dog deaths, all hero deaths, rat death and credit-card guard", UiTests.TestDeathsAndGuard);
            Run("Refactor: atomic capture, invalid input and cleanup", UiTests.TestRefactorBoundaries);
            Run("Refactor: queued animation timing across frame rates", UiTests.TestRefactorTiming);
            Run("Refactor: Shuten pose registration and visual contact sheet", UiTests.TestRefactorArt);
            Run("Hedgehogs: rabies, dexterity, block, needles and thorns", HedgehogTests.Rules);
            Run("Hedgehogs: full pose extraction, animation, pause and resize", UiTests.TestHedgehogUi);
            Run("Hedgehogs pair: switching target per card, queue and deaths", HedgehogPairTests.Rules);
            Run("Hedgehogs pair: real clicks target different enemies in one turn", UiTests.TestHedgehogPairUi);
            Run("Mimicrator: depth, perspective and frame-rate independence", MimicratorTests.Orbits);
            Run("Mimicrator: layers, pause, focus, resize, menu and resource ownership", UiTests.TestMimicratorPreview);
            Run("Mimicrator: four orbit phases for visual review", UiTests.TestMimicratorContactSheet);
            Run("Mimicrator combat: forms, stun, auto cards and enemy block", MimicratorCombatTests.Rules);
            Run("Mimicrator combat: actions, transforms, birds, pause and deaths at six sizes", UiTests.TestMimicratorCombat);
            Run("Mimicrator combat: real elite room, investigation, victory and revisit", UiTests.TestMimicratorEliteRoom);
            Run("Mimicrator combat: animation contact sheet", UiTests.TestMimicratorAnimationSheet);
            if (passed == 0 && failures.Count == 0) failures.Add("No tests matched filter");
            string summary = "Suites: " + passed + " passed, " + failures.Count + " failed. Assertions: " + assertions + ".";
            results.Add(summary);
            results.Add("No statement/branch coverage percentage is claimed; this is behavioural regression coverage.");
            File.WriteAllLines(Path.Combine(OutputDirectory, "test-results.txt"), results, Encoding.UTF8);
            foreach (string line in results)
                Console.WriteLine(line);
            return failures.Count == 0 ? 0 : 1;
        }
    }
}
