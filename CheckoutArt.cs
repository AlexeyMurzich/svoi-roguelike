using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        bool checkout = false;
        int buffCursor = 0;
        HeroState currentHero;
        Image checkoutBackground;
        readonly Dictionary<string, Image> relicImages = new Dictionary<string, Image>();
        void OpenCheckout()
        {
            menuMusicHero = -1;
            StopMapMotion();
            ResetCombat();
            mapRoutes.Clear();
            encounteredEnemies.Clear();
            testEnemyOverride = null;
            mapScreen = false;
            SetInventory("");
            legendOpen = false;
            hoveredRelic = false;
            hoveredMapNode = -1;
            mapRun = null;
            mapIntel.Clear();
            checkout = true;
            preview = false;
            buffCursor = 0;
            currentHero = new HeroState(selected, 50, 0, new Balance());
            notice = "";
        }

        bool HandleCheckoutKey(KeyEventArgs e)
        {
            if (!checkout)
                return false;
            if (e.KeyCode == Keys.Escape)
            {
                if (currentHero.Effects.Chosen != null)
                    OpenActMap();
                else
                    checkout = false;
            }

            if (currentHero.Effects.Chosen == null)
            {
                if (e.KeyCode == Keys.Left)
                    buffCursor = (buffCursor + 2) % 3;
                if (e.KeyCode == Keys.Right)
                    buffCursor = (buffCursor + 1) % 3;
                if (e.KeyCode == Keys.Enter)
                    TakeBuff();
                if (e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D3)
                    buffCursor = (int)e.KeyCode - (int)Keys.D1;
            }

            RefreshScene();
            return true;
        }

        void TakeBuff()
        {
            if (currentHero.Effects.Choose(StartingBuff.For(selected)[buffCursor].Id))
                StarterDeck.AddTo(currentHero);
        }

        void LoadCheckoutArt()
        {
            checkoutBackground = assetStore.Get("checkout.png", "checkout.png");
            foreach (string id in new[]
            {
                "ring",
                "coupon",
                "phone",
                "id",
                "baton",
                "armor",
                "feather",
                "processor",
                "investigation",
                "exchange",
                "mystery"
            }

            )
                relicImages[id] = assetStore.Get(Path.Combine("relics", id + ".png"), "relics." + id + ".png");
        }

        void DisposeCheckoutArt()
        {
            checkoutBackground = null;
            relicImages.Clear();
        }

        void FitImage(Graphics g, Image im, RectangleF area)
        {
            float ratio = Math.Min(area.Width / im.Width, area.Height / im.Height);
            var target = new RectangleF(area.X + (area.Width - im.Width * ratio) / 2, area.Y + (area.Height - im.Height * ratio) / 2, im.Width * ratio, im.Height * ratio);
            DrawInterfaceImage(g, im, target, new RectangleF(0, 0, im.Width, im.Height));
        }

        void ShadowText(Graphics g, string value, float x, float y, float size, Color c, float width, bool bold = false)
        {
            TextAt(g, value, x + 2, y + 2, size, Color.FromArgb(230, 4, 15, 12), width, bold);
            TextAt(g, value, x, y, size, c, width, bold);
        }

        void RelicFrame(Graphics g, float x, float y, float w, float h, bool active)
        {
            using (var shade = new LinearGradientBrush(new PointF(x, y), new PointF(x, y + h), Color.FromArgb(active ? 218 : 205, 17, 42, 34), Color.FromArgb(232, 13, 26, 24)))
                g.FillRectangle(shade, x, y, w, h);
            Stroke(g, x, y, w, h, active ? gold : Color.FromArgb(113, 117, 139, 115));
            Stroke(g, x + 5, y + 5, w - 10, h - 10, active ? Color.FromArgb(100, 226, 190, 122) : Color.FromArgb(50, 166, 191, 156));
            using (var p = new Pen(active ? gold : Color.FromArgb(130, 163, 175, 143), 3))
            {
                float a = 14;
                g.DrawLine(p, x, y + a, x, y);
                g.DrawLine(p, x, y, x + a, y);
                g.DrawLine(p, x + w - a, y, x + w, y);
                g.DrawLine(p, x + w, y, x + w, y + a);
                g.DrawLine(p, x, y + h - a, x, y + h);
                g.DrawLine(p, x, y + h, x + a, y + h);
                g.DrawLine(p, x + w - a, y + h, x + w, y + h);
                g.DrawLine(p, x + w, y + h, x + w, y + h - a);
            }
        }

        void DrawCheckout(Graphics g)
        {
            bool taken = currentHero.Effects.Chosen != null;
            if (checkoutBackground != null)
                g.DrawImage(checkoutBackground, new RectangleF(0, 0, 1280, 840), Viewport.CoverSource(checkoutBackground.Size, new SizeF(1280, 840)), GraphicsUnit.Pixel);
            else
                Rect(g, 0, 0, 1280, 840, Color.FromArgb(21, 64, 43));
            using (var scrim = new LinearGradientBrush(new Rectangle(438, 12, 818, 175), Color.FromArgb(210, 9, 25, 19), Color.FromArgb(80, 9, 25, 19), 90f))
                g.FillRectangle(scrim, 438, 12, 818, 175);
            ShadowText(g, "СМОЛЕВИЧИ / СОЦИАЛ / ЛИЧНАЯ РЕЛИКВИЯ", 470, 30, 12, Color.FromArgb(188, 218, 175), 750, true);
            ShadowText(g, taken ? "Покупка за счёт заведения." : "Бонус по карте своих.", 466, 61, 36, text, 760, true);
            ShadowText(g, taken ? "Всё пробито. Этот предмет теперь с вами." : "Выберите один предмет перед выходом в город.", 470, 119, 17, Color.FromArgb(219, 226, 207), 750);
            using (var heroShadow = new LinearGradientBrush(new Rectangle(13, 246, 304, 469), Color.FromArgb(125, 8, 24, 18), Color.FromArgb(40, 8, 24, 18), 0f))
                g.FillRectangle(heroShadow, 13, 246, 304, 469);
            ShadowText(g, names[selected], 32, 260, 27, colors[selected], 275, true);
            Portrait(g, selected, 11, 308, 306, 363);
            ShadowText(g, "КАССА № 1", 34, 685, 12, Color.FromArgb(190, 236, 175), 250, true);
            ShadowText(g, taken ? "Чек закрыт · приятного пути" : "Один предмет. За счёт заведения.", 34, 711, 13, text, 270);
            var items = StartingBuff.For(selected);
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                var item = items[i];
                float x = 337 + i * 302;
                bool active = i == buffCursor;
                RelicFrame(g, x, 235, 285, 454, active);
                TextAt(g, "0" + (i + 1), x + 20, 250, 12, active ? gold : muted, 100, true);
                string id = item.Icon == "placeholder" ? "mystery" : item.Icon;
                using (var glow = new LinearGradientBrush(new Rectangle((int)x + 27, 273, 231, 163), Color.FromArgb(active ? 45 : 20, 217, 179, 89), Color.FromArgb(0, 217, 179, 89), 90f))
                    g.FillEllipse(glow, x + 27, 273, 231, 163);
                if (relicImages.ContainsKey(id))
                    FitImage(g, relicImages[id], new RectangleF(x + 56, 275, 173, 163));
                else
                    TextAt(g, "?", x + 120, 300, 50, gold, 100, true);
                TextAt(g, item.Name, x + 21, 453, 21, text, 245, true);
                TextAt(g, item.Description, x + 21, 519, 15, Color.FromArgb(213, 220, 200), 245);
                if (taken && active)
                    TextAt(g, "ПОЛУЧЕНО  ✓", x + 21, 663, 11, gold, 245, true);
                else if (item.Icon == "placeholder")
                    TextAt(g, "ТАЙНАЯ РЕЛИКВИЯ · ЗАГЛУШКА", x + 21, 663, 9, Color.FromArgb(177, 186, 163), 245, true);
                if (!taken)
                    actions.Add(Tuple.Create(new RectangleF(x, 235, 285, 454), (Action)(() => buffCursor = index)));
            }

            using (var strip = new LinearGradientBrush(new Rectangle(321, 705, 938, 76), Color.FromArgb(210, 9, 29, 20), Color.FromArgb(175, 14, 38, 26), 0f))
                g.FillRectangle(strip, 321, 705, 938, 76);
            if (!taken)
            {
                ShadowText(g, "Бесплатно. Один баф на старт.", 339, 728, 13, text, 280);
                Button(g, "Забрать: " + items[buffCursor].Name + "  →", 628, 715, 598, 51, TakeBuff, true);
                Stroke(g, 628, 715, 598, 51, gold);
            }
            else
            {
                ShadowText(g, "✓  " + currentHero.Effects.Chosen.Name, 339, 713, 17, gold, 420, true);
                string detail = "Баф выбран. Следующий этап — карта маршрута и первый бой.";
                if (currentHero.Effects.Has("ring"))
                    detail = "+300 золота начислено. Бонус золота: " + currentHero.Money + ".";
                if (currentHero.Effects.Has("phone"))
                    detail = "Телефон с вами. Теперь можно уходить в долг ценой здоровья.";
                ShadowText(g, detail, 339, 750, 12, text, 430);
                Button(g, "Выйти в Смолевичи  →", 800, 715, 426, 51, OpenActMap, true);
            }

            if (!taken)
                Button(g, "← Персонажи", 28, 784, 265, 39, () =>
                {
                    checkout = false;
                });
            using (var footer = new SolidBrush(Color.FromArgb(180, 5, 19, 13)))
                g.FillRectangle(footer, 321, 789, 938, 35);
            ShadowText(g, taken ? "Реликвия получена" : "← → или 1–3 — выбрать · Enter — забрать · Esc — назад", 339, 796, 12, text, 880);
        }
    }
}
