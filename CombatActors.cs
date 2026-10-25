using System;
using System.Drawing;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        float CombatFloor { get { return Math.Min(CanvasHeight - 295, (160 + CanvasHeight - 285) / 2 + 160); } }
        float HeroCenter { get { return CanvasWidth * .24f; } }

        RectangleF HeroDeathArea { get { return new RectangleF(HeroCenter - 170, CombatFloor - 335, 340, 335); } }

        RectangleF EnemyArea(int index)
        {
            float center = EnemyCenter(index);
            if (combat.Enemies[index].Kind == EnemyKind.Hedgehog || combat.Enemies[index].Kind == EnemyKind.RabidHedgehog)
                return combat.Enemies.Count > 1 ? new RectangleF(center - 180, CombatFloor - 280, 360, 280) : new RectangleF(center - 215, CombatFloor - 315, 430, 315);
            return combat.Enemies[index].Kind == EnemyKind.Rat
                ? new RectangleF(center - 215, CombatFloor - 265, 430, 265)
                : new RectangleF(center - 135, CombatFloor - 245, 270, 245);
        }

        RectangleF EnemyTargetArea(int index)
        {
            float width = combat.Enemies.Count > 1 ? Math.Min(300, EnemyCenter(1) - EnemyCenter(0) - 12) : 300;
            float top = CombatFloor - 340, bottom = Math.Min(CombatFloor + 35, CanvasHeight - 265);
            return new RectangleF(EnemyCenter(index) - width / 2, top, width, bottom - top);
        }

        void PaintCombatHero(Graphics graphics)
        {
            float move = attackTimeline.Kind == AttackAnimation.HeroStrike ? HeroAttackOffset(attackTimeline.Time) : 0;
            float width = selected == 3 ? 310 : 240;
            var area = new RectangleF(HeroCenter - width / 2 + move, CombatFloor - 335, width, 335);
            if (selected == 3) DrawShutenPose(graphics, area);
            else Portrait(graphics, selected, area.X, area.Y, area.Width, area.Height);
        }

        void PaintCombatEnemy(Graphics graphics, int index)
        {
            var area = EnemyArea(index);
            if (actingEnemyIndex == index && (attackTimeline.Kind == AttackAnimation.RatBite || attackTimeline.Kind == AttackAnimation.DogBite || attackTimeline.Kind == AttackAnimation.HedgehogBite))
                area.Offset(BiteOffset(attackTimeline.Time), 0);
            if (combat.Enemies[index].Kind == EnemyKind.Rat) DrawRatPose(graphics, area);
            else if (combat.Enemies[index].Kind == EnemyKind.StrayDogs) DrawDogPoseFor(graphics, area, index);
            else DrawHedgehogPose(graphics, area, combat.Enemies[index].Kind == EnemyKind.RabidHedgehog, actingEnemyIndex == index);
        }
    }
}
