using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;

namespace ShpilSvoih
{
    public static partial class UiTests
    {
        static int HeroArtCount(Game game)
        {
            int count = 0;
            foreach (object atlas in Read<Array>(game, "heroCombatPoses")) if (atlas != null) count++;
            return count;
        }

        public static void TestHeroCombatMinimum()
        {
            using (var sheet = new Bitmap(1020, 1140))
            using (var g = Graphics.FromImage(sheet))
            using (var font = new Font("Segoe UI", 12))
            {
                g.Clear(Color.FromArgb(28, 36, 40));
                for (int id = 0; id < 3; id++)
                    using (var game = new Game())
                    {
                        game.CreateControl(); Call(game, "StartTestBattle");
                        Write(game, "selected", id);
                        foreach (bool attack in new[] { true, false })
                        {
                            var hero = new HeroState(id, 50, 0, new Balance());
                            var fight = new CombatState(hero, new Random(42));
                            var hand = (List<CardData>)typeof(CombatState).GetField("hand", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fight);
                            hand.Clear(); hand.Add(new CardData { Name = "Fixture", Attack = attack, Skill = !attack, Damage = attack ? 6 : 0, Block = attack ? 0 : 5, ManaCost = 1 });
                            Write(game, "currentHero", hero); Write(game, "combat", fight);
                            int hp = fight.EnemyHealth, block = fight.Block;
                            decimal energy = fight.Energy;
                            Call(game, "PlayCombatCard", 0);
                            var timeline = Read<AttackTimeline>(game, "attackTimeline");
                            TestSuite.Check(timeline.Kind == (attack ? AttackAnimation.HeroStrike : AttackAnimation.HeroGuard), "hero uses matching timeline");
                            Call(game, "PlayCombatCard", 0);
                            Write(game, "inventoryView", "pause"); Call(game, "AdvanceCombatAnimation", 1f);
                            TestSuite.Check(fight.EnemyHealth == hp && fight.Block == block && fight.Energy == energy, "pause and repeated click do not apply card early");
                            Write(game, "inventoryView", "");
                            if (attack)
                            {
                                Call(game, "AdvanceCombatAnimation", .22f / AttackTimeline.Speed);
                                DrawHeroTestCell(game, g, font, id, 0, "Windup");
                                Call(game, "AdvanceCombatAnimation", .32f / AttackTimeline.Speed);
                                DrawHeroTestCell(game, g, font, id, 1, "Strike");
                            }
                            else
                            {
                                Call(game, "AdvanceCombatAnimation", .25f / AttackTimeline.Speed);
                                DrawHeroTestCell(game, g, font, id, 2, "Guard");
                            }
                            int expectedHp = attack ? hp - fight.DamageFor(hand.Count > 0 ? hand[0] : new CardData { Attack = true, Damage = 6 }) : hp;
                            // Fixture has no modifiers: verify the visible impact and payment once.
                            TestSuite.Check(fight.EnemyHealth == expectedHp && fight.Block == block + (attack ? 0 : 5) && fight.Energy == energy - 1, "one impact and one payment");
                            Call(game, "AdvanceCombatAnimation", 2f); Call(game, "AdvanceCombatAnimation", 2f);
                            TestSuite.Check(fight.EnemyHealth == expectedHp && fight.Block == block + (attack ? 0 : 5) && fight.Energy == energy - 1, "completed timeline does not repeat card");
                        }
                    }
                sheet.Save(Path.Combine(TestSuite.OutputDirectory, "hero-combat-contact-sheet.png"));
            }
        }

        static void DrawHeroTestCell(Game game, Graphics g, Font font, int row, int column, string label)
        {
            var saved = g.Save();
            try
            {
                g.TranslateTransform(column * 340, row * 380);
                g.DrawString(new[] { "Пупсолет", "Илля", "Рунцо" }[row] + " / " + label, font, Brushes.White, 10, 5);
                Call(game, "DrawHeroCombatPose", g, new RectangleF(0, 35, 330, 335));
            }
            finally { g.Restore(saved); }
        }
    }
}
