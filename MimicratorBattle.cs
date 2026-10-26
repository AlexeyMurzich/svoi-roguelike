using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        double mimicratorCombatSeconds;
        MimicratorForm mimicratorVisualForm, mimicratorTransformFrom, mimicratorTransformTo;
        PoseAtlas mimicratorShellPoses;

        bool CombatAmbientMoving
        {
            get
            {
                return HasLiveFight && combat != null && combat.Enemy == EnemyKind.Mimicrator &&
                    currentHero.Health > 0 && combat.Enemies[0].IsAlive &&
                    (combat.Phase == CombatPhase.Player || combat.Phase == CombatPhase.Enemy);
            }
        }

        bool NeedsMimicratorTransform
        {
            get
            {
                return CombatAmbientMoving && combat.Phase == CombatPhase.Player &&
                    mimicratorVisualForm != combat.Enemies[0].Form;
            }
        }

        void LoadMimicratorBattleArt()
        {
            LoadMimicratorArt();
            if (mimicratorShellPoses == null)
                mimicratorShellPoses = new PoseAtlas(assetStore.Get("enemies/mimicrator-shell.png", "enemies.mimicrator-shell.png"));
        }

        void AdvanceCombatAmbient(float elapsed)
        {
            if (!CombatAmbientMoving || inventoryView != "") return;
            mimicratorCombatSeconds += Motion.FrameDelta(elapsed);
            Invalidate();
        }

        void StartMimicratorTransform()
        {
            mimicratorTransformFrom = mimicratorVisualForm;
            mimicratorTransformTo = combat.Enemies[0].Form;
            combatNotice = "Мимикратор меняет облик";
            StartCombatAnimation(AttackAnimation.MimicratorTransform, () =>
            {
                mimicratorVisualForm = mimicratorTransformTo;
                combatNotice = combat.IsStunned ? "Оглушение · в этом ходу одна карта" : "Твой ход";
            });
        }

        void StartMimicratorAction(CombatEnemy actor)
        {
            bool stun = actor.WillStun, defend = actor.WillDefend;
            AttackAnimation kind = actor.Form == MimicratorForm.Fairy ? AttackAnimation.MimicratorMagic :
                actor.Form == MimicratorForm.Beacon ? AttackAnimation.MimicratorBeacon :
                actor.Form == MimicratorForm.Bear ? AttackAnimation.MimicratorBear : AttackAnimation.MimicratorShell;
            combatNotice = defend ? "Мидия закрывает раковину" : stun ? "Мигалка оглушает" :
                actor.Form == MimicratorForm.Bear ? "Мишка замахивается" : "Фея колдует";
            StartCombatAnimation(kind, () =>
            {
                int block = combat.Block;
                combat.ResolveNextEnemyAction();
                if (combat.Block < block) guardVisualTime = .3f;
                SyncDeaths();
                combatNotice = combat.Phase == CombatPhase.Defeat ? "Поражение" :
                    defend ? "Мидия · 12 блока" : stun && combat.IsStunned ? "Оглушение · в этом ходу одна карта" :
                    combat.LastDamage == 0 ? "Удар заблокирован" : "Получено урона: " + combat.LastDamage;
            });
        }

        void DrawMimicratorCombat(Graphics g, RectangleF area, int index)
        {
            PrepareMimicratorDisplaySprites();
            bool alive = combat.Enemies[index].IsAlive;
            bool morph = alive && attackTimeline.Kind == AttackAnimation.MimicratorTransform;
            float time = attackTimeline.Time;
            var form = mimicratorVisualForm;
            float size = 1, rotation = 0;
            if (morph)
            {
                float progress = Math.Min(1, time / attackTimeline.Duration);
                form = progress < .5f ? mimicratorTransformFrom : mimicratorTransformTo;
                size = .12f + .88f * Motion.Ease(Math.Abs(progress - .5f) * 2);
                rotation = (progress < .5f ? progress : progress - 1) * 130;
                float flare = (float)Math.Sin(progress * Math.PI);
                MimicratorGlow(g, new PointF(area.X + area.Width / 2, area.Y + area.Height / 2),
                    300 * flare + 20, 300 * flare + 20, Color.FromArgb((int)(145 * flare), 196, 151, 226));
                using (var pen = new Pen(Color.FromArgb((int)(175 * flare), 212, 184, 237), 2))
                    for (int ray = 0; ray < 8; ray++)
                    {
                        double angle = progress * 5 + ray * Math.PI / 4;
                        float radius = 45 + 85 * flare;
                        float x = area.X + area.Width / 2 + (float)Math.Cos(angle) * radius;
                        float y = area.Y + area.Height / 2 + (float)Math.Sin(angle) * radius * .6f;
                        g.DrawLine(pen, x - 5, y, x + 5, y);
                        g.DrawLine(pen, x, y - 5, x, y + 5);
                    }
            }
            var saved = g.Save();
            try
            {
                float centerX = area.X + area.Width / 2, centerY = area.Y + area.Height / 2;
                g.TranslateTransform(centerX, centerY);
                g.RotateTransform(rotation);
                g.ScaleTransform(size, size);
                var local = new RectangleF(-area.Width / 2, -area.Height / 2, area.Width, area.Height);
                DrawMimicratorForm(g, form, local, alive);
            }
            finally { g.Restore(saved); }
        }

        void DrawMimicratorForm(Graphics g, MimicratorForm form, RectangleF area, bool alive)
        {
            float time = attackTimeline.Time;
            float bob = (float)Math.Sin(mimicratorCombatSeconds * 1.25) * 4;
            if (form == MimicratorForm.Fairy)
            {
                float fit = area.Height / 650;
                var saved = g.Save();
                try
                {
                    g.TranslateTransform(area.X + area.Width / 2 - 640 * fit, area.Bottom - 764 * fit);
                    g.ScaleTransform(fit, fit);
                    DrawMimicratorOrbitLayer(g, false, mimicratorCombatSeconds);
                    var body = g.Save();
                    try
                    {
                        g.TranslateTransform(640, 444 + bob);
                        g.RotateTransform((float)Math.Sin(mimicratorCombatSeconds * .65) * .65f);
                        SpriteDrawing.FitBottom(g, mimicratorDisplayBody, new RectangleF(-223, -330, 446, 650), 1);
                    }
                    finally { g.Restore(body); }
                    DrawMimicratorOrbitLayer(g, true, mimicratorCombatSeconds);
                }
                finally { g.Restore(saved); }
                return;
            }

            float x = area.X + area.Width / 2, y = area.Bottom - 120 + bob;
            float tilt = (float)Math.Sin(mimicratorCombatSeconds * .9) * 3;
            float stretchX = 1, stretchY = 1;
            bool bearAttack = alive && attackTimeline.Kind == AttackAnimation.MimicratorBear;
            bool beaconAttack = alive && attackTimeline.Kind == AttackAnimation.MimicratorBeacon;
            if (bearAttack)
            {
                float travel = time < .28f ? -Motion.Ease(time / .28f) * .06f :
                    time < .7f ? -.06f + 1.06f * Motion.Ease((time - .28f) / .42f) :
                    1 - Motion.Ease((time - .7f) / .55f);
                x -= Math.Max(80, EnemyCenter(0) - HeroCenter - 120) * travel;
                y -= (float)Math.Sin(Math.Max(0, travel) * Math.PI) * 65;
                tilt = time < .7f ? 15 - travel * 35 : -20 * (1 - Motion.Ease((time - .7f) / .55f));
                float impact = (float)Math.Exp(-Math.Pow((time - .7) / .075, 2));
                stretchX += impact * .18f; stretchY -= impact * .12f;
            }
            if (beaconAttack)
            {
                tilt += (float)Math.Sin(time * 12) * 7;
                y -= (float)Math.Sin(Math.Min(1, time / .65f) * Math.PI) * 25;
            }
            var savedProp = g.Save();
            try
            {
                g.TranslateTransform(x, y);
                g.RotateTransform(tilt);
                g.ScaleTransform(stretchX, stretchY);
                Color light = form == MimicratorForm.Beacon ? Color.FromArgb(70, 97, 99, 224) :
                    form == MimicratorForm.Mussel ? Color.FromArgb(60, 112, 175, 216) : Color.FromArgb(42, 194, 131, 170);
                MimicratorGlow(g, PointF.Empty, 260, 230, light);
                if (form == MimicratorForm.Mussel)
                {
                    int frame = attackTimeline.Kind != AttackAnimation.MimicratorShell || !alive ? 0 :
                        time < .22f ? 0 : time < .48f ? 1 : 2;
                    mimicratorShellPoses.Draw(g, frame, new RectangleF(-125, -125, 250, 230), 1);
                    if (frame == 2 || combat.Enemies[0].Block > 0)
                        using (var pen = new Pen(Color.FromArgb(145, 141, 206, 234), 3))
                            g.DrawEllipse(pen, -139, -119, 278, 240);
                }
                else
                {
                    int item = form == MimicratorForm.Bear ? 0 : 2;
                    var crop = mimicratorSources[item];
                    float edge = form == MimicratorForm.Bear ? 220 : 175;
                    float fit = Math.Min(edge / crop.Width, edge / crop.Height);
                    SpriteDrawing.Image(g, mimicratorProps,
                        new RectangleF(-crop.Width * fit / 2, -crop.Height * fit / 2, crop.Width * fit, crop.Height * fit),
                        crop, 1, false);
                }
            }
            finally { g.Restore(savedProp); }
        }

        void DrawMimicratorActionEffects(Graphics g)
        {
            if (combat.Enemy != EnemyKind.Mimicrator || !attackTimeline.IsRunning) return;
            float time = attackTimeline.Time, hit = attackTimeline.HitTime;
            var kind = attackTimeline.Kind;
            if (kind == AttackAnimation.MimicratorMagic && time <= hit)
            {
                float progress = Motion.Ease(time / hit);
                var point = new PointF(EnemyCenter(0) - 70 + (HeroCenter - EnemyCenter(0) + 70) * progress,
                    CombatFloor - 235 - (float)Math.Sin(progress * Math.PI) * 45);
                MimicratorGlow(g, point, 80, 80, Color.FromArgb(135, 193, 134, 226));
                using (var brush = new SolidBrush(Color.FromArgb(220, 224, 195, 250)))
                    g.FillEllipse(brush, point.X - 8, point.Y - 8, 16, 16);
            }
            if (kind == AttackAnimation.MimicratorBeacon && time > .25f && time < hit + .18f)
            {
                float intensity = (float)Math.Sin(Math.Min(1, (time - .25f) / (hit - .25f)) * Math.PI / 2);
                if (time > hit) intensity *= 1 - (time - hit) / .18f;
                var from = new PointF(EnemyCenter(0) - 40, CombatFloor - 140);
                var to = new PointF(HeroCenter, CombatFloor - 245);
                using (var blue = new Pen(Color.FromArgb((int)(140 * intensity), 124, 157, 246), 8))
                using (var red = new Pen(Color.FromArgb((int)(195 * intensity), 236, 121, 159), 2))
                {
                    for (int part = 0; part < 12; part++)
                    {
                        float a = part / 12f, b = (part + 1) / 12f;
                        float wa = (float)Math.Sin(a * Math.PI * 6 + time * 9) * 12 * (float)Math.Sin(a * Math.PI);
                        float wb = (float)Math.Sin(b * Math.PI * 6 + time * 9) * 12 * (float)Math.Sin(b * Math.PI);
                        var p = new PointF(from.X + (to.X - from.X) * a, from.Y + (to.Y - from.Y) * a + wa);
                        var q = new PointF(from.X + (to.X - from.X) * b, from.Y + (to.Y - from.Y) * b + wb);
                        g.DrawLine(blue, p, q); g.DrawLine(red, p, q);
                    }
                }
            }
            if ((kind == AttackAnimation.MimicratorBear || kind == AttackAnimation.MimicratorMagic ||
                kind == AttackAnimation.MimicratorBeacon) && time >= hit && time < hit + .24f)
            {
                float progress = (time - hit) / .24f;
                using (var pen = new Pen(Color.FromArgb((int)(195 * (1 - progress)), 233, 201, 239), 4))
                {
                    float radius = 15 + progress * 60;
                    g.DrawEllipse(pen, HeroCenter - radius, CombatFloor - 205 - radius, radius * 2, radius * 2);
                }
            }
        }

        void DrawStunBirds(Graphics g)
        {
            if (!combat.IsStunned || combat.Phase != CombatPhase.Player || currentHero.Health <= 0) return;
            float centerY = CombatFloor - 328;
            for (int i = 0; i < 3; i++)
            {
                double angle = mimicratorCombatSeconds * 3.2 + i * Math.PI * 2 / 3;
                float x = HeroCenter + (float)Math.Cos(angle) * 61;
                float y = centerY + (float)Math.Sin(angle) * 17;
                float wing = 6 + (float)Math.Sin(mimicratorCombatSeconds * 16 + i) * 5;
                using (var outline = new Pen(Color.FromArgb(230, 92, 63, 90), 1.5f))
                using (var body = new SolidBrush(Color.FromArgb(255, 238, 206, 107)))
                using (var feather = new SolidBrush(Color.FromArgb(255, 255, 236, 162)))
                {
                    g.FillEllipse(body, x - 10, y - 6, 20, 13);
                    g.DrawEllipse(outline, x - 10, y - 6, 20, 13);
                    g.FillEllipse(feather, x - 7, y - wing - 5, 9, wing + 6);
                    g.FillPolygon(Brushes.Orange, new[] { new PointF(x + 8, y - 2), new PointF(x + 16, y + 1), new PointF(x + 8, y + 4) });
                    g.FillEllipse(Brushes.Black, x + 3, y - 4, 2.5f, 2.5f);
                }
            }
            TextAt(g, "Оглушение · карт осталось: " + Math.Max(0, 1 - combat.CardsPlayedThisTurn),
                HeroCenter - 160, CombatFloor - 416, 14, Color.FromArgb(237, 211, 142), 340, true);
        }
    }
}
