using System;
using System.Drawing;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        float CombatFloor { get { return Math.Min(CanvasHeight - 295, (160 + CanvasHeight - 285) / 2 + 160); } }
        float HeroCenter { get { return CanvasWidth * .24f; } }

        RectangleF HeroDeathArea { get { return selected == 3 ? new RectangleF(HeroSpriteCenter - 170, CombatFloor - 335, 340, 335) : new RectangleF(HeroSpriteCenter - 220, CombatFloor - 365, 440, 365); } }

        RectangleF EnemyArea(int index)
        {
            float center = EnemyCenter(index);
            if (combat.Enemies[index].Kind == EnemyKind.Babai) return BabaiBodyArea;
            if (combat.Enemies[index].Kind == EnemyKind.Toha) return new RectangleF(center - 260, CombatFloor - 350, 520, 350);
            if (combat.Enemies[index].Kind == EnemyKind.Mimicrator) return new RectangleF(center - 220, CombatFloor - 350, 440, 350);
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
            if (combat.Babai != null && combat.Babai.Caught && BabaiDisplayDistance <= .01f) return;
            float move = attackTimeline.Kind == AttackAnimation.HeroStrike ? HeroAttackOffset(attackTimeline.Time) : 0;
            float width = selected == 3 ? 310 : 340;
            var area = new RectangleF(HeroSpriteCenter - width / 2 + move, CombatFloor - 335, width, 335);
            var saved = graphics.Save();
            try
            {
                ApplyTohaHeroRecoil(graphics);
                ApplyBabaiCapture(graphics, area);
                LivingMotion.Body(graphics, area, livingSeconds, selected, selected == 0);
                if (selected == 3) DrawShutenPose(graphics, area);
                else DrawHeroCombatPose(graphics, area);
            }
            finally { graphics.Restore(saved); }
        }

        bool EnemyIsAnimating(int index)
        {
            return actingEnemyIndex == index && attackTimeline.IsRunning &&
                attackTimeline.Kind != AttackAnimation.HeroStrike && attackTimeline.Kind != AttackAnimation.HeroGuard;
        }

        void PaintCombatEnemy(Graphics graphics, int index)
        {
            var area = EnemyArea(index);
            if (actingEnemyIndex == index && (attackTimeline.Kind == AttackAnimation.RatBite || attackTimeline.Kind == AttackAnimation.DogBite || attackTimeline.Kind == AttackAnimation.HedgehogBite))
                area.Offset(BiteOffset(attackTimeline.Time), 0);
            var kind = combat.Enemies[index].Kind;
            var saved = graphics.Save();
            try
            {
                if (kind != EnemyKind.StrayDogs && kind != EnemyKind.Mimicrator && kind != EnemyKind.Toha && kind != EnemyKind.Babai)
                    LivingMotion.Body(graphics, area, livingSeconds, kind == EnemyKind.RabidHedgehog ? 7 : 4 + index, false);
                if (kind == EnemyKind.Babai) DrawBabaiBody(graphics);
                else if (kind == EnemyKind.Toha) DrawToha(graphics, area);
                else if (kind == EnemyKind.Mimicrator) DrawMimicratorCombat(graphics, area, index);
                else if (kind == EnemyKind.Rat) DrawRatPose(graphics, area);
                else if (kind == EnemyKind.StrayDogs) DrawDogPoseFor(graphics, area, index);
                else DrawHedgehogPose(graphics, area, kind == EnemyKind.RabidHedgehog, actingEnemyIndex == index);
                // Mouth anchors belong to the resting poses, never to a different attack frame.
                if (!EnemyIsAnimating(index))
                {
                    if (kind == EnemyKind.Rat)
                    {
                        var target = ratPoses.Target(area);
                        LivingMotion.Saliva(graphics, new PointF(target.Left + target.Width * .25f, target.Top + target.Height * .56f), target.Width / 430, livingSeconds, .1);
                    }
                    else if (kind == EnemyKind.RabidHedgehog)
                        LivingMotion.Saliva(graphics, new PointF(area.Left + area.Width * .29f, area.Bottom - area.Height * .19f), area.Width / 360, livingSeconds, .55);
                }
            }
            finally { graphics.Restore(saved); }
        }
    }
}
