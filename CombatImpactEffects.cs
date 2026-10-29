using System;
using System.Drawing;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        void DrawCombatImpactEffects(Graphics graphics, float heroX, float floor, float time)
        {
            bool babai = combat.Babai != null;
            int aimIndex = Math.Max(0, Math.Min(attackTarget, combat.Enemies.Count - 1));
            bool dog = combat.Enemies[aimIndex].Kind == EnemyKind.StrayDogs;
            PointF aim = babai ? BabaiTargetPoint : dog ? DogTargetPoint(aimIndex) : PointF.Empty;
            float target = babai || dog ? aim.X + 100 : EnemyCenter(aimIndex);
            if (currentHero.Health > 0 && attackTimeline.Kind == AttackAnimation.HeroStrike && time >= .33f && time <= .5f)
            {
                float flight = Motion.Ease((time - .33f) / .17f);
                float x = heroX + 70 + (target - heroX - 170) * flight;
                float y = floor - 245 - (float)Math.Sin(flight * Math.PI) * 28;
                if (babai || dog) y += (aim.Y - (floor - 245)) * flight;
                if (selected == 3) FitImage(graphics, coinIcon, new RectangleF(x - 17, y - 17, 34, 34));
                else DrawHeroProjectile(graphics, x, y, flight);
            }
            float afterHit = time - attackTimeline.HitTime;
            if ((currentHero.Health > 0 || attackTimeline.Kind != AttackAnimation.HeroStrike) && attackTimeline.IsRunning && attackTimeline.Kind != AttackAnimation.DogHowl && attackTimeline.Kind != AttackAnimation.TohaTransform && attackTimeline.Kind != AttackAnimation.MimicratorTransform && attackTimeline.Kind != AttackAnimation.MimicratorRest && afterHit >= 0 && afterHit < .18f)
            {
                bool enemyHit = IsBabaiLasso || IsTohaRamming || attackTimeline.Kind == AttackAnimation.RatBite || attackTimeline.Kind == AttackAnimation.DogBite || attackTimeline.Kind == AttackAnimation.HedgehogBite || attackTimeline.Kind == AttackAnimation.HedgehogNeedles || attackTimeline.Kind == AttackAnimation.MimicratorMagic || attackTimeline.Kind == AttackAnimation.MimicratorBeacon || attackTimeline.Kind == AttackAnimation.MimicratorBear || attackTimeline.Kind == AttackAnimation.MimicratorShell;
                float x = enemyHit || attackTimeline.Kind == AttackAnimation.HeroGuard ? heroX : target;
                float hitY = floor - 180;
                if ((babai || dog) && !enemyHit && attackTimeline.Kind == AttackAnimation.HeroStrike) { x = aim.X; hitY = aim.Y; }
                int alpha = (int)(210 * (1 - Motion.Ease(afterHit / .18f)));
                using (var pen = new Pen(Color.FromArgb(alpha, 255, 218, 137), 3))
                    for (int i = 0; i < 6; i++)
                    {
                        double angle = i * Math.PI / 3;
                        float radius = 13 + 20 * Motion.Ease(afterHit / .18f);
                        graphics.DrawLine(pen, x + (float)Math.Cos(angle) * 8, hitY + (float)Math.Sin(angle) * 8, x + (float)Math.Cos(angle) * radius, hitY + (float)Math.Sin(angle) * radius);
                    }
            }
        }
    }
}
