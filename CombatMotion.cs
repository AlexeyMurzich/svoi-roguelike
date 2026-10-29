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
            if (combat.Enemy == EnemyKind.Fomatreshka) return CanvasWidth * (index == 0 ? .86f : .48f + (index - 1) * .135f);
            if (combat.Enemies.Count == 1) return CanvasWidth * .74f;
            return CanvasWidth * (index == 0 ? .62f : .84f);
        }

        void DrawCombatActors(Graphics graphics, float heroX, float floor)
        {
            float time = attackTimeline.Time;
            TextAt(graphics, "ХОД " + combat.Turn, CanvasWidth / 2 - 42, 100, 14, gold, 140, true);
            DrawDistrictHeroContact(graphics);
            if (!deathVisuals.Draw(graphics, -1, HeroDeathArea, null)) PaintCombatHero(graphics);
            DrawStunBirds(graphics);
            DrawCombatHealth(graphics, heroX - 125, floor + 8, 250, currentHero.Health, currentHero.MaxHealth);
            DrawBlockBadge(graphics, heroX + 129, floor + 8, combat.Block);
            DrawHeroStatuses(graphics);
            for (int i = 0; i < combat.Enemies.Count; i++)
            {
                int index = i;
                var enemy = combat.Enemies[index];
                float center = EnemyCenter(index);
                if (enemy.Fled) { if (FomatreshkaFleeing) DrawFomatreshkaSprite(graphics, index, true); continue; }
                if (!deathVisuals.Draw(graphics, enemy.Id, EnemyArea(index), null)) PaintCombatEnemy(graphics, index);
                if (enemy.Kind == EnemyKind.Babai) DrawBabaiBed(graphics);
                if (!enemy.IsAlive) continue;
                int width = combat.Enemy == EnemyKind.Fomatreshka ? (int)(CanvasWidth * .104f) : combat.Enemies.Count == 1 ? 250 : 180;
                bool selectedTarget = combat.SelectedTarget == index;
                float nameY = floor - ((enemy.Kind == EnemyKind.Mimicrator || enemy.Kind == EnemyKind.Toha || enemy.Kind == EnemyKind.Babai) ? 414 : 315);
                if (combat.Enemy == EnemyKind.Fomatreshka) nameY = floor - (index == 0 ? 418 : 347);
                if (enemy.Kind == EnemyKind.StrayDogs) nameY = Math.Max(150, dogPoses.Target(index == 1 ? 1 : 0, 2, EnemyArea(index)).Top - 90);
                TextAt(graphics, enemy.Name, center - width / 2, nameY, combat.Enemies.Count == 1 ? 23 : 18, selectedTarget ? gold : text, width + 30, true);
                DrawEnemyIntent(graphics, enemy, center, (enemy.Kind == EnemyKind.StrayDogs || combat.Enemy == EnemyKind.Fomatreshka) ? nameY + 34 : floor - ((enemy.Kind == EnemyKind.Mimicrator || enemy.Kind == EnemyKind.Toha || enemy.Kind == EnemyKind.Babai) ? 380 : 280));
                DrawCombatHealth(graphics, center - width / 2, floor + 8, width, enemy.Health, enemy.MaxHealth);
                DrawBlockBadge(graphics, center + width / 2 + 4, floor + 8, enemy.Block);
                if (enemy.Vigor > 0) TextAt(graphics, "Бодрость +" + enemy.Vigor, center - width / 2, floor + 35, 13, Color.FromArgb(171, 255, 109), width + 10, true);
                if (enemy.Thorns > 0) DrawCombatHint(graphics, new CombatHint(6, enemy.Thorns.ToString(), "Шипы", "За каждую атакующую карту по этому врагу: " + enemy.Thorns + " ответного урона, включая добивающий удар. Блок поглощает этот урон."), new RectangleF(center - 14, floor + 30, 28, 28), true);
                if (selectedTarget && combat.Enemies.Count > 1)
                {
                    Stroke(graphics, center - width / 2 - 3, floor + 5, width + 6, 27, gold);
                    using (var targetBrush = new SolidBrush(gold))
                        graphics.FillPolygon(targetBrush, new[] { new PointF(center - 8, nameY - 21), new PointF(center + 8, nameY - 21), new PointF(center, nameY - 11) });
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
