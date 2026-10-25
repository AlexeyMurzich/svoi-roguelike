using System;
using System.Drawing;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        void DrawRatPose(Graphics g, RectangleF area)
        {
            if (attackTimeline.Kind != AttackAnimation.RatBite)
            {
                ratPoses.Draw(g, 0, area, 1);
                return;
            }
            float time = attackTimeline.Time;
            if (time < .15f) ratPoses.Draw(g, 0, area, 1);
            else if (time < .36f) ratPoses.Blend(g, 0, 1, (time - .15f) / .21f, area);
            else if (time < .47f) ratPoses.Draw(g, 1, area, 1);
            else if (time < .56f) ratPoses.Blend(g, 1, 2, (time - .47f) / .09f, area);
            else if (time < .72f) ratPoses.Draw(g, 2, area, 1);
            else ratPoses.Blend(g, 2, 0, (time - .72f) / .33f, area);
        }

        static float HeroAttackOffset(float time)
        {
            if (time < .26f)
                return -5 * Motion.Ease(time / .26f);
            if (time < .5f)
                return -5 + 13 * Motion.Ease((time - .26f) / .24f);
            return 8 * (1 - Motion.Ease((time - .5f) / .45f));
        }

        static float BiteOffset(float time)
        {
            if (time < .18f)
                return 6 * Motion.Ease(time / .18f);
            if (time < .52f)
                return 6 - 30 * Motion.Ease((time - .18f) / .34f);
            return -24 * (1 - Motion.Ease((time - .52f) / .53f));
        }

        float EnemyCenter(int index)
        {
            if (combat.Enemies.Count == 1) return CanvasWidth * .74f;
            return CanvasWidth * (index == 0 ? .62f : .84f);
        }

        void DrawCombatActors(Graphics graphics, float heroX, float floor)
        {
            float time = attackTimeline.Time;
            TextAt(graphics, "ХОД " + combat.Turn, CanvasWidth / 2 - 42, 100, 14, gold, 140, true);
            if (!deathVisuals.Draw(graphics, -1, HeroDeathArea, null)) PaintCombatHero(graphics);
            if (combat.Rabies) TextAt(graphics, "Бешенство · +2 силы / −2 ловкости", heroX - 155, floor - 390, 14, Color.OrangeRed, 360, true);
            if (combat.WeakTurns > 0) TextAt(graphics, "Слабость · −25% · 1 ход", heroX - 125, floor - 365, 14, Color.Plum, 280, true);
            DrawCombatHealth(graphics, heroX - 125, floor + 8, 250, currentHero.Health, currentHero.MaxHealth);
            if (combat.Block > 0) TextAt(graphics, "Блок · " + combat.Block, heroX - 40, floor + 34, 15, Color.LightBlue, 180, true);
            for (int i = 0; i < combat.Enemies.Count; i++)
            {
                int index = i;
                var enemy = combat.Enemies[index];
                float center = EnemyCenter(index);
                bool active = actingEnemyIndex == index &&
                    (attackTimeline.Kind == AttackAnimation.RatBite || attackTimeline.Kind == AttackAnimation.DogBite || attackTimeline.Kind == AttackAnimation.DogHowl);
                if (!deathVisuals.Draw(graphics, enemy.Id, EnemyArea(index), null)) PaintCombatEnemy(graphics, index);
                if (!enemy.IsAlive) continue;
                int width = combat.Enemies.Count == 1 ? 250 : 180;
                bool selectedTarget = combat.SelectedTarget == index;
                TextAt(graphics, enemy.Name, center - width / 2, floor - 315, combat.Enemies.Count == 1 ? 23 : 18, selectedTarget ? gold : text, width + 30, true);
                bool howl = active && attackTimeline.IsRunning
                    ? attackTimeline.Kind == AttackAnimation.DogHowl : enemy.WillHowl;
                TextAt(graphics, howl ? "Вой · Слабость" : enemy.Kind == EnemyKind.Hedgehog ? "Иглы · 10 / Шипы · 2" : enemy.Kind == EnemyKind.RabidHedgehog ? "Укус · 5 / Бешенство" : "Укус · 5", center - 110, floor - 280, 15, gold, 250, true);
                DrawCombatHealth(graphics, center - width / 2, floor + 8, width, enemy.Health, enemy.MaxHealth);
                if (selectedTarget && combat.Enemies.Count > 1)
                {
                    Stroke(graphics, center - width / 2 - 3, floor + 5, width + 6, 27, gold);
                    using (var targetBrush = new SolidBrush(gold))
                        graphics.FillPolygon(targetBrush, new[] { new PointF(center - 8, floor - 336), new PointF(center + 8, floor - 336), new PointF(center, floor - 326) });
                }
                if (!CombatBusy && combat.Phase == CombatPhase.Player)
                    actions.Add(Tuple.Create(EnemyTargetArea(index), (Action)(() => SelectCombatTarget(index))));
            }
            float target = EnemyCenter(Math.Max(0, attackTarget));
            if (currentHero.Health > 0 && attackTimeline.Kind == AttackAnimation.HeroStrike && time >= .33f && time <= .5f)
            {
                float flight = Motion.Ease((time - .33f) / .17f);
                float x = heroX + 70 + (target - heroX - 170) * flight;
                float y = floor - 245 - (float)Math.Sin(flight * Math.PI) * 28;
                if (selected == 3) FitImage(graphics, coinIcon, new RectangleF(x - 17, y - 17, 34, 34));
                else using (var stroke = new Pen(colors[selected], 4)) graphics.DrawLine(stroke, x - 20, y - 12, x + 20, y + 12);
            }
            float afterHit = time - attackTimeline.HitTime;
            if ((currentHero.Health > 0 || attackTimeline.Kind != AttackAnimation.HeroStrike) && attackTimeline.IsRunning && attackTimeline.Kind != AttackAnimation.DogHowl && afterHit >= 0 && afterHit < .18f)
            {
                bool enemyHit = attackTimeline.Kind == AttackAnimation.RatBite || attackTimeline.Kind == AttackAnimation.DogBite || attackTimeline.Kind == AttackAnimation.HedgehogBite || attackTimeline.Kind == AttackAnimation.HedgehogNeedles;
                float x = enemyHit || attackTimeline.Kind == AttackAnimation.HeroGuard ? heroX : target;
                int alpha = (int)(210 * (1 - Motion.Ease(afterHit / .18f)));
                using (var pen = new Pen(Color.FromArgb(alpha, 255, 218, 137), 3))
                    for (int i = 0; i < 6; i++)
                    {
                        double angle = i * Math.PI / 3;
                        float radius = 13 + 20 * Motion.Ease(afterHit / .18f);
                        graphics.DrawLine(pen, x + (float)Math.Cos(angle) * 8, floor - 180 + (float)Math.Sin(angle) * 8, x + (float)Math.Cos(angle) * radius, floor - 180 + (float)Math.Sin(angle) * radius);
                    }
            }
        }
    }
}
