using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        RectangleF CardArtSource(int hero, bool attack)
        {
            // Full approved cards stay proportional; text and costs below are live overlays.
            RectangleF source;
            if (hero == 0)
                source = new RectangleF(attack ? 85 : 793, 63, 663, 846);
            else if (hero == 1)
                source = new RectangleF(attack ? 163 : 814, 94, 563, 795);
            else if (hero == 2)
                source = new RectangleF(attack ? 128 : 791, 86, 615, 809);
            else
                source = new RectangleF(attack ? 95 : 795, 79, 645, 848);
            var sheet = cardSheets[hero];
            return new RectangleF(source.X * sheet.Width / 1536f, source.Y * sheet.Height / 1024f, source.Width * sheet.Width / 1536f, source.Height * sheet.Height / 1024f);
        }

        void DrawCombatCard(Graphics g, CardData card, int index, RectangleF slot)
        {
            bool usable = !CombatBusy && combat.CanPlay(index);
            var source = CardArtSource(selected, card.Attack);
            float scale = Math.Min(slot.Width / source.Width, slot.Height / source.Height);
            var area = new RectangleF(slot.X + (slot.Width - source.Width * scale) / 2, slot.Y + (slot.Height - source.Height * scale) / 2, source.Width * scale, source.Height * scale);
            bool standard = card.Name == "Удар" || card.Name == "Защита";
            if (standard)
                cardArtCache.Draw(g, cardSheets[selected], source, area, RenderViewport.Scale, card.Attack);
            else
            {
                RelicFrame(g, area.X, area.Y, area.Width, area.Height, usable);
                TextAt(g, card.Name, area.X + 40, area.Y + 12, 17, gold, area.Width - 45, true);
                FitImage(g, relicImages["mystery"], new RectangleF(area.X + 24, area.Y + 55, area.Width - 48, 110));
                TextAt(g, "Универсальная", area.X + 22, area.Bottom - 27, 12, gold, area.Width - 25);
            }

            float cx = area.X + area.Width * (selected == 3 && !card.Attack ? .83f : .13f), cy = area.Y + area.Height * .095f;
            using (var fill = new SolidBrush(Color.FromArgb(255, 49, 36, 15)))
                g.FillEllipse(fill, cx - 17, cy - 17, 34, 34);
            using (var rim = new Pen(gold, 2))
                g.DrawEllipse(rim, cx - 17, cy - 17, 34, 34);
            var price = combat.HiddenCosts ? "?" : combat.Cost(card).ToString("0.#");
            var font = HudFont(21, true);
            var size = g.MeasureString(price, font);
            using (var brush = new SolidBrush(text))
                g.DrawString(price, font, brush, cx - size.Width / 2, cy - size.Height / 2);
            float rulesY = area.Y + area.Height * .765f;
            Rect(g, area.X + 13, rulesY, area.Width - 26, area.Height * .125f, Color.FromArgb(255, 17, 24, 25));
            string rules = card.Attack ? combat.DamageFor(card) + " урона" : combat.BlockFor(card) + " блока";
            if (card.Attack && card.Block > 0)
                rules += ", " + combat.BlockFor(card) + " блока";
            TextAt(g, rules, area.X + 19, rulesY + 5, 16, text, area.Width - 32, true);
            TextAt(g, (index + 1).ToString(), area.Right - 21, area.Y + 33, 12, gold, 20, true);
            if (!usable)
                Rect(g, area.X, area.Y, area.Width, area.Height, Color.FromArgb(85, 0, 0, 0));
        }

        void DrawCombatHealth(Graphics g, float x, float y, float width, int hp, int max)
        {
            Rect(g, x, y, width, 21, Color.FromArgb(230, 15, 22, 24));
            Rect(g, x + 2, y + 2, (width - 4) * Math.Max(0, hp) / Math.Max(1, max), 17, Color.FromArgb(150, 41, 47));
            Stroke(g, x, y, width, 21, Color.FromArgb(150, gold));
            TextAt(g, hp + " / " + max, x + width / 2 - 27, y + 1, 13, text, 85, true);
        }

        void DrawCombat(Graphics g)
        {
            actions.Clear();
            combatHintAreas.Clear();
            SyncDeaths();
            g.InterpolationMode = InterpolationMode.Bilinear;
            if (combat.Enemy == EnemyKind.Doctors) DrawHospital(g);
            else if (UsesDistrictBackdrop) DrawDistrictBackdrop(g);
            else if (combat.Enemy == EnemyKind.Toha) DrawTohaBackground(g);
            else DrawSceneBackground(g, true);
            float heroX = HeroCenter;
            float floor = CombatFloor;
            DrawCombatActors(g, heroX, floor);
            string noticeText = combatNotice;
            if (combat.Phase == CombatPhase.Victory && IsDoctorQuestion)
                noticeText = DoctorRules.Name(mapRun.DoctorsAt(mapRun.Pending.Id).Drop) + " · полное лечение";
            TextAt(g, noticeText, CanvasWidth / 2 - 210, floor + 34, 16, gold, 440, true);
            DrawCombatDock(g);
            DrawCombatHand(g);

            DrawRunBar(g);
            DrawMapRelics(g);
            if (inventoryView != "")
                DrawRunInventory(g);
            else if (hoveredRelic)
                DrawRelicTooltip(g);
            if (inventoryView == "") DrawCombatTooltip(g);
        }
    }
}
