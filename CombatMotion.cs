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
            else if (time < .36f) ratPoses.Draw(g, time < .255f ? 0 : 1, area, 1);
            else if (time < .47f) ratPoses.Draw(g, 1, area, 1);
            else if (time < .56f) ratPoses.Draw(g, time < .515f ? 1 : 2, area, 1);
            else if (time < .72f) ratPoses.Draw(g, 2, area, 1);
            else ratPoses.Draw(g, time < .885f ? 2 : 0, area, 1);
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
            DrawBabaiHeroContact(graphics);
            if (!deathVisuals.Draw(graphics, -1, HeroDeathArea, null)) PaintCombatHero(graphics);
            DrawStunBirds(graphics);
            DrawBabaiBed(graphics);
            DrawCombatHealth(graphics, heroX - 125, floor + 8, 250, currentHero.Health, currentHero.MaxHealth);
            DrawBlockBadge(graphics, heroX + 129, floor + 8, combat.Block);
            DrawHeroStatuses(graphics);
            for (int i = 0; i < combat.Enemies.Count; i++)
            {
                int index = i;
                var enemy = combat.Enemies[index];
                float center = EnemyCenter(index);
                if (!deathVisuals.Draw(graphics, enemy.Id, EnemyArea(index), null)) PaintCombatEnemy(graphics, index);
                if (!enemy.IsAlive) continue;
                int width = combat.Enemies.Count == 1 ? 250 : 180;
                bool selectedTarget = combat.SelectedTarget == index;
                TextAt(graphics, enemy.Name, center - width / 2, floor - ((enemy.Kind == EnemyKind.Mimicrator || enemy.Kind == EnemyKind.Toha || enemy.Kind == EnemyKind.Babai) ? 414 : 315), combat.Enemies.Count == 1 ? 23 : 18, selectedTarget ? gold : text, width + 30, true);
                DrawEnemyIntent(graphics, enemy, center, floor - ((enemy.Kind == EnemyKind.Mimicrator || enemy.Kind == EnemyKind.Toha || enemy.Kind == EnemyKind.Babai) ? 380 : 280));
                DrawCombatHealth(graphics, center - width / 2, floor + 8, width, enemy.Health, enemy.MaxHealth);
                DrawBlockBadge(graphics, center + width / 2 + 4, floor + 8, enemy.Block);
                if (enemy.Thorns > 0) DrawCombatHint(graphics, new CombatHint(6, enemy.Thorns.ToString(), "Шипы", "За каждую атакующую карту по этому врагу: " + enemy.Thorns + " ответного урона, включая добивающий удар. Блок поглощает этот урон."), new RectangleF(center - 14, floor + 30, 28, 28), true);
                if (selectedTarget && combat.Enemies.Count > 1)
                {
                    Stroke(graphics, center - width / 2 - 3, floor + 5, width + 6, 27, gold);
                    using (var targetBrush = new SolidBrush(gold))
                        graphics.FillPolygon(targetBrush, new[] { new PointF(center - 8, floor - 336), new PointF(center + 8, floor - 336), new PointF(center, floor - 326) });
                }
                if (!CombatBusy && combat.Phase == CombatPhase.Player)
                    actions.Add(Tuple.Create(EnemyTargetArea(index), (Action)(() => SelectCombatTarget(index))));
            }
            DrawMimicratorActionEffects(graphics);
            DrawBabaiEffects(graphics);
            DrawCombatImpactEffects(graphics, HeroSpriteCenter, floor, time);
        }
    }
}
