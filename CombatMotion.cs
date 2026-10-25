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

        void DrawShutenPose(Graphics graphics, RectangleF area)
        {
            float time = attackTimeline.Time;
            float guard = combat.Block > 0 ? 1 : Motion.Ease(guardVisualTime / .3f);
            var shieldArea = new RectangleF(area.X - 30, area.Y, area.Width + 60, area.Height);
            if (attackTimeline.Kind == AttackAnimation.HeroGuard)
            {
                float raised = guardAtActionStart + (1 - guardAtActionStart) * Motion.Ease(time / .25f);
                shutenPoses.Draw(graphics, 0, area, 1 - raised);
                SpriteDrawing.FitBottom(graphics, shutenDefense, shieldArea, raised);
            }
            else if (attackTimeline.Kind == AttackAnimation.HeroStrike)
            {
                if (time < .26f)
                {
                    float drawn = Motion.Ease(time / .26f);
                    shutenPoses.Draw(graphics, 0, area, (1 - drawn) * (1 - guardAtActionStart));
                    SpriteDrawing.FitBottom(graphics, shutenDefense, shieldArea, (1 - drawn) * guardAtActionStart);
                    shutenPoses.Draw(graphics, 1, area, drawn);
                }
                else if (time < .33f) shutenPoses.Draw(graphics, 1, area, 1);
                else if (time < .42f) shutenPoses.Blend(graphics, 1, 2, (time - .33f) / .09f, area);
                else if (time < .64f) shutenPoses.Draw(graphics, 2, area, 1);
                else
                {
                    float returned = Motion.Ease((time - .64f) / .31f);
                    shutenPoses.Draw(graphics, 2, area, 1 - returned);
                    shutenPoses.Draw(graphics, 0, area, returned * (1 - guard));
                    SpriteDrawing.FitBottom(graphics, shutenDefense, shieldArea, returned * guard);
                }
            }
            else
            {
                shutenPoses.Draw(graphics, 0, area, 1 - guard);
                SpriteDrawing.FitBottom(graphics, shutenDefense, shieldArea, guard);
            }
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
            float heroMove = attackTimeline.Kind == AttackAnimation.HeroStrike ? HeroAttackOffset(time) : 0;
            TextAt(graphics, "ХОД " + combat.Turn, CanvasWidth / 2 - 42, 100, 14, gold, 140, true);
            Action<Graphics> hero = canvas =>
            {
                var area = new RectangleF(heroX - 120 + heroMove, floor - 335, 240, 335);
                if (selected == 3) DrawShutenPose(canvas, area);
                else Portrait(canvas, selected, area.X, area.Y, area.Width, area.Height);
            };
            if (!deathVisuals.Draw(graphics, -1, new RectangleF(heroX - 160, floor - 335, 320, 335), hero)) hero(graphics);
            if (combat.WeakTurns > 0) TextAt(graphics, "Слабость · −25% · 1 ход", heroX - 125, floor - 365, 14, Color.Plum, 280, true);
            DrawCombatHealth(graphics, heroX - 125, floor + 8, 250, currentHero.Health, currentHero.MaxHealth);
            if (combat.Block > 0) TextAt(graphics, "Блок · " + combat.Block, heroX - 40, floor + 34, 15, Color.LightBlue, 180, true);
            for (int i = 0; i < combat.Enemies.Count; i++)
            {
                int index = i;
                var enemy = combat.Enemies[index];
                float center = EnemyCenter(index);
                bool active = actingEnemyIndex == index;
                bool bite = active && (attackTimeline.Kind == AttackAnimation.RatBite || attackTimeline.Kind == AttackAnimation.DogBite);
                float move = bite ? BiteOffset(time) : 0;
                var area = enemy.Kind == EnemyKind.Rat ? new RectangleF(center - 215 + move, floor - 265, 430, 265) : new RectangleF(center - 135 + move, floor - 245, 270, 245);
                Action<Graphics> paint = canvas =>
                {
                    if (enemy.Kind == EnemyKind.Rat) DrawRatPose(canvas, area);
                    else DrawDogPoseFor(canvas, area, index);
                };
                if (!deathVisuals.Draw(graphics, enemy.Id, area, paint)) paint(graphics);
                if (!enemy.IsAlive) continue;
                int width = combat.Enemies.Count == 1 ? 250 : 180;
                bool selectedTarget = combat.SelectedTarget == index;
                TextAt(graphics, enemy.Name, center - width / 2, floor - 315, combat.Enemies.Count == 1 ? 23 : 18, selectedTarget ? gold : text, width + 30, true);
                bool howl = active && attackTimeline.Kind == AttackAnimation.DogHowl || enemy.WillHowl;
                TextAt(graphics, howl ? "Вой · Слабость" : "Укус · 5", center - 65, floor - 280, 15, gold, 180, true);
                DrawCombatHealth(graphics, center - width / 2, floor + 8, width, enemy.Health, enemy.MaxHealth);
                if (selectedTarget && combat.Enemies.Count > 1) Stroke(graphics, center - width / 2 - 3, floor + 5, width + 6, 27, gold);
                if (!CombatBusy && combat.Phase == CombatPhase.Player)
                    actions.Add(Tuple.Create(new RectangleF(center - 115, floor - 270, 230, 310), (Action)(() => SelectCombatTarget(index))));
            }
            float target = EnemyCenter(Math.Max(0, attackTarget));
            if (attackTimeline.Kind == AttackAnimation.HeroStrike && time >= .33f && time <= .5f)
            {
                float flight = Motion.Ease((time - .33f) / .17f);
                float x = heroX + 70 + (target - heroX - 170) * flight;
                float y = floor - 245 - (float)Math.Sin(flight * Math.PI) * 28;
                if (selected == 3) FitImage(graphics, coinIcon, new RectangleF(x - 17, y - 17, 34, 34));
                else using (var stroke = new Pen(colors[selected], 4)) graphics.DrawLine(stroke, x - 20, y - 12, x + 20, y + 12);
            }
            float afterHit = time - attackTimeline.HitTime;
            if (attackTimeline.IsRunning && attackTimeline.Kind != AttackAnimation.DogHowl && afterHit >= 0 && afterHit < .18f)
            {
                bool enemyHit = attackTimeline.Kind == AttackAnimation.RatBite || attackTimeline.Kind == AttackAnimation.DogBite;
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
