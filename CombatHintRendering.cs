using System;
using System.Collections.Generic;
using System.Drawing;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        Image combatIcons;
        readonly List<Tuple<RectangleF, CombatHint>> combatHintAreas = new List<Tuple<RectangleF, CombatHint>>();
        PointF? combatPointer;

        void UpdateCombatHover(PointF point, bool inside)
        {
            bool show = inside && !debugConsoleOpen && HasLiveFight && combat != null && inventoryView == "" && !hoveredRelic;
            PointF? next = show ? (PointF?)point : null;
            int previous = CombatHoverIdentity(combatPointer);
            combatPointer = next;
            if (previous != CombatHoverIdentity(next)) Invalidate();
        }

        int CombatHoverIdentity(PointF? pointer)
        {
            if (!pointer.HasValue) return -1;
            for (int i = 0; i < combatHintAreas.Count; i++)
                if (combatHintAreas[i].Item1.Contains(pointer.Value)) return i;
            return RouteButtonVisible && RouteButtonArea.Contains(pointer.Value) ? int.MaxValue : -1;
        }

        void DrawCombatHintIcon(Graphics g, int icon, RectangleF area)
        {
            if (icon == 20) { LoadBabaiArt(); FitImage(g, babaiBed, area); }
            else if (icon == 19)
            {
                LoadTohaArt();
                FitImage(g, combat != null && combat.Enemies[0].IsDarkToha ? tohaDark : tohaLight, area);
            }
            else if (icon == 16)
                DrawInterfaceImage(g, menuIcons, area, new RectangleF(0, 0, menuIcons.Width / 2f, menuIcons.Height));
            else if (icon == 17) FitImage(g, coinIcon, area);
            else if (icon == 18) FitImage(g, relicImages["investigation"], area);
            else
            {
                float w = combatIcons.Width / 4f, h = combatIcons.Height / 4f;
                DrawInterfaceImage(g, combatIcons, area, new RectangleF(icon % 4 * w, icon / 4 * h, w, h));
            }
        }

        void DrawCombatHint(Graphics g, CombatHint hint, RectangleF area, bool numberInside)
        {
            DrawCombatHintIcon(g, hint.Icon, area);
            if (hint.Number.Length > 0)
            {
                float x = numberInside ? area.X + area.Width * .54f : area.Right - 1;
                float y = numberInside ? area.Y + area.Height * .46f : area.Y + area.Height * .35f;
                var font = HudFont(numberInside ? 16 : 19, true);
                using (var shadow = new SolidBrush(Color.FromArgb(245, 8, 12, 16)))
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                            g.DrawString(hint.Number, font, shadow, x + dx, y + dy);
                using (var brush = new SolidBrush(text)) g.DrawString(hint.Number, font, brush, x, y);
            }
            float extra = numberInside ? 4 : hint.Number.Length * 11;
            combatHintAreas.Add(Tuple.Create(new RectangleF(area.X, area.Y, area.Width + extra, area.Height), hint));
        }

        void DrawHeroStatuses(Graphics g)
        {
            var hints = CombatHints.Hero(combat);
            float width = hints.Count * 34;
            for (int i = 0; i < hints.Count; i++)
                DrawCombatHint(g, hints[i], new RectangleF(HeroCenter - width / 2 + i * 34, CombatFloor + 30, 28, 28), true);
        }

        void DrawEnemyIntent(Graphics g, CombatEnemy enemy, float center, float y)
        {
            var hints = CombatHints.Intent(enemy);
            float width = 0;
            foreach (var hint in hints) width += 46 + hint.Number.Length * 11 + 8;
            float x = center - width / 2;
            foreach (var hint in hints)
            {
                DrawCombatHint(g, hint, new RectangleF(x, y, 46, 46), false);
                x += 46 + hint.Number.Length * 11 + 8;
            }
        }

        void DrawBlockBadge(Graphics g, float x, float y, int block)
        {
            if (block <= 0) return;
            DrawCombatHint(g, new CombatHint(15, "", "Блок · " + block, "Поглощает " + block + " урона. Оставшийся блок исчезнет в начале следующего собственного хода."), new RectangleF(x, y - 9, 41, 41), true);
            string value = block.ToString();
            var font = HudFont(16, true);
            float left = x + (41 - g.MeasureString(value, font).Width) / 2;
            using (var shadow = new SolidBrush(Color.Black)) g.DrawString(value, font, shadow, left + 1, y + 3);
            using (var brush = new SolidBrush(text)) g.DrawString(value, font, brush, left, y + 2);
        }

        void DrawCombatTooltip(Graphics g)
        {
            if (!combatPointer.HasValue || inventoryView != "" || hoveredRelic) return;
            foreach (var entry in combatHintAreas)
            {
                if (!entry.Item1.Contains(combatPointer.Value)) continue;
                var hint = entry.Item2;
                const float padding = 15;
                float width = Math.Min(330, CanvasWidth - 32), inner = width - padding * 2;
                var titleFont = HudFont(18, true);
                var bodyFont = HudFont(15, false);
                float th = g.MeasureString(hint.Title, titleFont, (int)inner).Height;
                float bh = g.MeasureString(hint.Description, bodyFont, (int)inner).Height;
                float height = padding * 2 + th + 7 + bh;
                float x = Math.Min(CanvasWidth - width - 16, Math.Max(16, entry.Item1.Right + 10));
                float y = entry.Item1.Bottom > CombatFloor ? entry.Item1.Top - height - 10 : entry.Item1.Bottom + 10;
                y = Math.Max(78, Math.Min(CanvasHeight - height - 16, y));
                Rect(g, x + 4, y + 5, width, height, Color.FromArgb(180, 0, 0, 0));
                Rect(g, x, y, width, height, Color.FromArgb(255, 25, 19, 33));
                RelicFrame(g, x, y, width, height, true);
                using (var brush = new SolidBrush(gold))
                    g.DrawString(hint.Title, titleFont, brush, new RectangleF(x + padding, y + padding, inner, th + 2));
                using (var brush = new SolidBrush(text))
                    g.DrawString(hint.Description, bodyFont, brush, new RectangleF(x + padding, y + padding + th + 7, inner, bh + 2));
                return;
            }
        }
    }
}
