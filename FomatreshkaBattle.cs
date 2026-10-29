using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        readonly Image[] fomatreshkaArt = new Image[7];
        float fomatreshkaEntrance = FomatreshkaRules.EntranceDuration;
        float fomatreshkaFlee = -1;
        bool FomatreshkaFleeing { get { return fomatreshkaFlee >= 0 && fomatreshkaFlee < FomatreshkaRules.FleeDuration; } }

        internal void StartTestFomatreshka() { StartTestEncounter(EnemyKind.Fomatreshka); }

        void LoadFomatreshkaArt()
        {
            if (fomatreshkaArt[0] != null) return;
            string[] files = { "01-idle-hair.png", "02-summon.png", "03-gorilla.png", "04-empress.png",
                "minion-01-glasses.png", "minion-03-ponytail.png", "minion-04-redhead.png" };
            for (int i = 0; i < files.Length; i++)
                fomatreshkaArt[i] = assetStore.Get("enemies/fomatreshka/" + files[i], "enemies.fomatreshka." + files[i]);
        }

        RectangleF FomatreshkaArea(int index)
        {
            float height = index == 0 ? 340 : 275;
            float width = index == 0 ? 340 : 183;
            return new RectangleF(EnemyCenter(index) - width / 2, CombatFloor - height, width, height);
        }

        void StartFomatreshkaAction(CombatEnemy actor)
        {
            combat.PrepareNextEnemyAction();
            var kind = actor.Kind == EnemyKind.Fan ? AttackAnimation.FanStrike : actor.WillSummon ?
                AttackAnimation.FomatreshkaSummon : actor.WillDrink ? AttackAnimation.FomatreshkaDrink : AttackAnimation.FomatreshkaShot;
            combatNotice = actor.Kind == EnemyKind.Fan ? "Фанатка атакует" : actor.Intent;
            StartCombatAnimation(kind, () =>
            {
                int block = combat.Block;
                combat.ResolveNextEnemyImpact();
                if (kind == AttackAnimation.FomatreshkaSummon) fomatreshkaEntrance = 0;
                if (combat.Block < block) guardVisualTime = .3f;
                SyncDeaths();
                combatNotice = combat.Phase == CombatPhase.Defeat ? "Поражение" : kind == AttackAnimation.FomatreshkaSummon ?
                    "На помощь прибежали три фанатки" : kind == AttackAnimation.FomatreshkaDrink ?
                    "Бодрость +6 · следующий выстрел усилен" : combat.LastDamage == 0 ? "Удар заблокирован" : "Получено урона: " + combat.LastDamage;
            });
        }

        void DrawFomatreshkaGlow(Graphics g, RectangleF area)
        {
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(area.X + area.Width * .18f, area.Y - 9, area.Width * .64f, area.Height + 23);
                using (var glow = new PathGradientBrush(path))
                {
                    glow.CenterColor = Color.FromArgb(48 + (int)(8 * Math.Sin(livingSeconds * 2)), 135, 255, 62);
                    glow.SurroundColors = new[] { Color.FromArgb(0, 100, 255, 30) };
                    g.FillPath(glow, path);
                }
            }
            for (int i = 0; i < 7; i++)
            {
                float progress = (float)((livingSeconds * .4 + i / 7.0) % 1);
                float x = area.X + area.Width * (.28f + .43f * (float)((i * .618) % 1));
                float y = area.Bottom - area.Height * progress;
                using (var spark = new Pen(Color.FromArgb((int)(150 * Math.Sin(progress * Math.PI)), 175, 255, 93), 2))
                    g.DrawLine(spark, x, y, x + 1, y - 7);
            }
        }

        void DrawFomatreshkaSprite(Graphics g, int index, bool fleeing)
        {
            var enemy = combat.Enemies[index];
            var area = FomatreshkaArea(index);
            int pose = enemy.Kind == EnemyKind.Fan ? index + 3 : enemy.ActionsTaken == 0 ? 0 : 1;
            bool active = actingEnemyIndex == index && attackTimeline.IsRunning;
            if (index == 0 && active)
            {
                if (attackTimeline.Kind == AttackAnimation.FomatreshkaSummon) pose = attackTimeline.Time < .18f ? 0 : 1;
                else if (attackTimeline.Kind == AttackAnimation.FomatreshkaDrink) pose = 2;
                else if (attackTimeline.Kind == AttackAnimation.FomatreshkaShot) pose = 3;
            }
            float opacity = 1;
            if (fleeing)
            {
                float p = Motion.Ease(fomatreshkaFlee / FomatreshkaRules.FleeDuration);
                area.Offset((CanvasWidth - area.Left + 180) * p, (float)Math.Sin(p * 35 + index) * 5);
                opacity = 1 - p;
            }
            else if (index > 0)
            {
                float p = Motion.Ease(fomatreshkaEntrance / FomatreshkaRules.EntranceDuration);
                area.Offset((CanvasWidth - area.Left + 120) * (1 - p), (float)Math.Sin(p * 24 + index) * (1 - p) * 5);
                if (active && attackTimeline.Kind == AttackAnimation.FanStrike) area.Offset(BiteOffset(attackTimeline.Time), 0);
            }
            if (index == 0 && enemy.IsAlive && (enemy.Vigor > 0 || active && attackTimeline.Kind == AttackAnimation.FomatreshkaShot))
                DrawFomatreshkaGlow(g, area);
            SoftGroundShadow(g, new RectangleF(area.X + area.Width / 2 - 43, area.Bottom - 6, 86, 17), (int)(100 * opacity));
            Image image = fomatreshkaArt[pose];
            float scale = Math.Min(area.Width / image.Width, area.Height / image.Height);
            var target = new RectangleF(area.X + (area.Width - image.Width * scale) / 2,
                area.Bottom - image.Height * scale, image.Width * scale, image.Height * scale);
            var saved = g.Save();
            try
            {
                if (!fleeing) LivingMotion.Body(g, area, livingSeconds, 9 + index, false);
                if (index == 0 && active && attackTimeline.Kind == AttackAnimation.FomatreshkaShot)
                    g.TranslateTransform((float)Math.Sin(attackTimeline.Time * 65) * 2, 0);
                actorArtCache.Draw(g, image, target, new RectangleF(0, 0, image.Width, image.Height), opacity, false);
            }
            finally { g.Restore(saved); }
        }
    }
}
