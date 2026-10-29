using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        Image combatDockIcons;
        // Four views of one owned atlas; include the painted edge and soft shadow.
        static readonly RectangleF[] DockIconSources = {
            new RectangleF(54, 73, 524, 527), new RectangleF(643, 78, 567, 522),
            new RectangleF(48, 640, 554, 569), new RectangleF(641, 634, 573, 557)
        };
        void DrawDockIcon(Graphics g, int icon, RectangleF slot)
        {
            var source = DockIconSources[icon];
            float scale = Math.Min(slot.Width / source.Width, slot.Height / source.Height);
            var area = new RectangleF(slot.X + (slot.Width - source.Width * scale) / 2,
                slot.Y + (slot.Height - source.Height * scale) / 2, source.Width * scale, source.Height * scale);
            DrawInterfaceImage(g, combatDockIcons, area, source);
        }
        int hoveredCombatCard = -1;
        bool DockHovered(RectangleF area) { return combatPointer.HasValue && inventoryView == "" && area.Contains(combatPointer.Value); }
        bool HandVisible { get { return combat != null && (combat.Phase == CombatPhase.Player || CombatBusy); } }
        RectangleF HandCardSlot(int index, bool raised)
        {
            var slot = CombatLayout.CardSlot(CanvasWidth, CanvasHeight, combat.Hand.Count, index);
            if (raised)
            {
                float center = slot.X + slot.Width / 2;
                slot.Width *= 1.22f; slot.Height *= 1.22f;
                slot.X = center - slot.Width / 2; slot.Y = CanvasHeight - slot.Height - 17;
            }
            return slot;
        }
        bool HandCardContains(PointF point, int index, bool raised)
        {
            var slot = HandCardSlot(index, raised);
            float angle = raised ? 0 : CombatLayout.CardAngle(combat.Hand.Count, index);
            double radians = -angle * Math.PI / 180;
            float cx = slot.X + slot.Width / 2, cy = slot.Y + slot.Height / 2;
            float dx = point.X - cx, dy = point.Y - cy;
            var local = new PointF(cx + (float)(dx * Math.Cos(radians) - dy * Math.Sin(radians)),
                cy + (float)(dx * Math.Sin(radians) + dy * Math.Cos(radians)));
            // Match the fitted artwork rather than the transparent slot margins.
            var source = CardArtSource(selected, combat.Hand[index].Attack);
            float scale = Math.Min(slot.Width / source.Width, slot.Height / source.Height);
            var art = new RectangleF(cx - source.Width * scale / 2, cy - source.Height * scale / 2, source.Width * scale, source.Height * scale);
            return art.Contains(local);
        }
        int CombatCardAt(PointF point, bool retainHoverFootprint)
        {
            if (!HandVisible || GameplayPaused || CombatBusy) return -1;
            if (hoveredCombatCard >= 0 && hoveredCombatCard < combat.Hand.Count &&
                (HandCardContains(point, hoveredCombatCard, true) || retainHoverFootprint && HandCardContains(point, hoveredCombatCard, false))) return hoveredCombatCard;
            for (int i = combat.Hand.Count - 1; i >= 0; i--)
                if (i != hoveredCombatCard && HandCardContains(point, i, false)) return i;
            return -1;
        }
        void DrawCombatHand(Graphics g)
        {
            if (!HandVisible) return;
            for (int i = 0; i < combat.Hand.Count; i++)
                if (i != hoveredCombatCard || CombatBusy) DrawHandCard(g, i, false);
            if (!CombatBusy && hoveredCombatCard >= 0 && hoveredCombatCard < combat.Hand.Count)
                DrawHandCard(g, hoveredCombatCard, true);
        }
        void DrawHandCard(Graphics g, int index, bool raised)
        {
            var slot = HandCardSlot(index, raised);
            var saved = g.Save();
            try
            {
                g.TranslateTransform(slot.X + slot.Width / 2, slot.Y + slot.Height / 2);
                g.RotateTransform(raised ? 0 : CombatLayout.CardAngle(combat.Hand.Count, index));
                g.TranslateTransform(-slot.X - slot.Width / 2, -slot.Y - slot.Height / 2);
                Rect(g, slot.X + 3, slot.Y + 5, slot.Width, slot.Height, Color.FromArgb(85, 0, 0, 0));
                if (raised)
                {
                    using (var glow = new Pen(Color.FromArgb(100, gold), 8))
                        g.DrawRectangle(glow, slot.X - 1, slot.Y - 1, slot.Width + 2, slot.Height + 2);
                }
                DrawCombatCard(g, combat.Hand[index], index, slot);
            }
            finally { g.Restore(saved); }
        }
        void DockText(Graphics g, string value, RectangleF area, float size, Color color)
        {
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var brush = new SolidBrush(color))
                g.DrawString(value, HudFont(size, true), brush, area, format);
        }
        void DrawCombatDock(Graphics g)
        {
            var shadeArea = new RectangleF(0, CanvasHeight - 170, CanvasWidth, 170);
            using (var shade = new LinearGradientBrush(shadeArea, Color.FromArgb(0, 9, 15, 20), Color.FromArgb(120, 9, 15, 20), 90))
                g.FillRectangle(shade, shadeArea);
            DrawCombatResource(g);
            DrawCombatPile(g, false);
            DrawCombatPile(g, true);
            if (combat.Phase == CombatPhase.Victory && !CombatBusy) DrawRouteButton(g);
            else if (combat.Phase == CombatPhase.Defeat && !CombatBusy)
                Button(g, "Поражение · В меню", CanvasWidth - 250, CanvasHeight - 205, 225, 60, ReturnToMainMenu);
            else DrawEndTurnDock(g);
        }
        void DrawCombatResource(Graphics g)
        {
            var area = CombatLayout.Resource(CanvasWidth, CanvasHeight);
            bool wallet = selected == 3;
            var tone = wallet ? gold : colors[selected];
            DrawDockIcon(g, wallet ? 3 : 2, area);
            DockText(g, wallet ? "КОШЕЛЁК" : "ЭНЕРГИЯ", new RectangleF(area.X - 15, area.Y - 26, area.Width + 30, 22), 12, tone);
            string amount = wallet ? currentHero.MoneyDisplay : combat.Energy.ToString("0.#") + " / 3";
            float font = amount.Length > 7 ? 15 : amount.Length > 4 ? 21 : 29;
            DockText(g, amount, new RectangleF(area.X + 8, area.Y + (wallet ? 39 : 32), area.Width - 16, 37), font, text);
            if (wallet && currentHero.Debt > 0)
                DockText(g, "Долг: " + currentHero.Debt.ToString("0.#"), new RectangleF(area.X - 34, area.Y - 49, area.Width + 68, 20), 12, text);
        }
        void DrawCombatPile(Graphics g, bool discarded)
        {
            var area = discarded ? CombatLayout.DiscardPile(CanvasWidth, CanvasHeight) : CombatLayout.DrawPile(CanvasWidth, CanvasHeight);
            bool hover = DockHovered(area);
            int count = discarded ? combat.DiscardCount : combat.DrawCount;
            DrawDockIcon(g, discarded ? 1 : 0, new RectangleF(area.X + 6, area.Y + (hover ? 0 : 3), 88, 67));
            var badge = new RectangleF(area.X + 64, area.Y + 40, 30, 29);
            using (var fill = new SolidBrush(Color.FromArgb(250, 12, 19, 22))) g.FillEllipse(fill, badge);
            using (var edge = new Pen(hover ? text : gold, 1.5f)) g.DrawEllipse(edge, badge);
            DockText(g, count.ToString(), badge, 16, text);
            DockText(g, discarded ? "Сброс" : "Колода", new RectangleF(area.X, area.Bottom - 20, area.Width, 20), 14, hover ? text : gold);
            actions.Add(Tuple.Create(area, (Action)(() => { deckPage = 0; SetInventory(discarded ? "discard" : "draw"); })));
        }
        void DrawEndTurnDock(Graphics g)
        {
            var area = CombatLayout.EndTurn(CanvasWidth, CanvasHeight);
            bool enabled = !CombatBusy && combat.Phase == CombatPhase.Player;
            bool hover = enabled && DockHovered(area);
            Color edge = enabled ? gold : muted;
            var points = new[] { new PointF(area.X+14,area.Y), new PointF(area.Right-14,area.Y), new PointF(area.Right,area.Y+area.Height/2), new PointF(area.Right-14,area.Bottom), new PointF(area.X+14,area.Bottom), new PointF(area.X,area.Y+area.Height/2) };
            using (var path = new GraphicsPath())
            {
                path.AddPolygon(points);
                using (var fill = new LinearGradientBrush(area, hover ? Color.FromArgb(255, 91, 71, 42) : Color.FromArgb(255, 45, 48, 40), Color.FromArgb(255, 18, 26, 27), 90)) g.FillPath(fill, path);
                using (var rim = new Pen(edge, hover ? 3 : 2)) g.DrawPath(rim, path);
            }
            DockText(g, enabled ? "Завершить ход" : combat.Phase == CombatPhase.Player ? "Розыгрыш карты" : combat.Phase == CombatPhase.Enemy ? "Ход противника" : "Бой завершён", new RectangleF(area.X + 9, area.Y + 9, area.Width - 18, 29), 18, enabled ? text : muted);
            DockText(g, enabled ? "Enter / Пробел" : "Дождитесь завершения", new RectangleF(area.X + 10, area.Y + 39, area.Width - 20, 19), 11, edge);
            if (enabled) actions.Add(Tuple.Create(area, (Action)EndCombatTurn));
            DockText(g, combat.Phase == CombatPhase.Player ? "ВАШ ХОД · " + combat.Turn : "ХОД ПРОТИВНИКА", new RectangleF(area.X, area.Y - 30, area.Width, 21), 12, gold);
        }
    }
}
