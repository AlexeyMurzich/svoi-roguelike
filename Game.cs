using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace ShpilSvoih
{
    public sealed partial class Game : Form
    {
        readonly string[] names =
        {
            "Пупсолет",
            "Илля",
            "Рунцо",
            "Шутен"
        };
        readonly string[] tags =
        {
            "СЛЁЗЫ → УРОН",
            "ХАОС В РУКЕ",
            "ОДИН ХОД — ОДНА СТОРОНА",
            "ДЕНЬГИ ВМЕСТО ЭНЕРГИИ"
        };
        readonly string[] summaries =
        {
            "Получает урон, копит слёзы\nи бьёт всё больнее.",
            "Берёт больше карт.\nДве играются сами.",
            "Поддерживает мужчину\nили дерётся сама.",
            "Тратит деньги на карты.\nЗа долги платит здоровьем."
        };
        readonly string[] descriptions =
        {
            "Когда Пупсолет получает урон, она плачет. Полученный урон накапливает слёзы: чем их больше, тем сильнее её собственные удары.",
            "Илля всегда в замешательстве. Каждый ход он добирает на две карты больше, а в начале хода две случайные карты из руки разыгрываются автоматически.",
            "Рунцо выбирает роль: стоит за спиной своего мужчины и усиливает его патриархальными картами или использует феминистические карты и дерётся сама.",
            "Шутен постоянно теряет деньги и оплачивает ими карты. Чем больше денег у него есть, тем более крутые карты ему доступны. С телефоном можно уйти в долг — ценой здоровья."
        };
        readonly string[][] rules =
        {
            new[]
            {
                "РЕСУРС КАРТ",
                "Энергия",
                "ОСОБЫЙ РЕСУРС",
                "Слёзы от полученного урона",
                "УСИЛЕНИЕ",
                "Больше слёз — больше собственного урона"
            },
            new[]
            {
                "РЕСУРС КАРТ",
                "Энергия",
                "ДОБОР",
                "На 2 карты больше каждый ход",
                "ЗАМЕШАТЕЛЬСТВО",
                "2 случайные карты играются автоматически"
            },
            new[]
            {
                "РЕСУРС КАРТ",
                "Энергия у обоих типов карт",
                "ДВА НАПРАВЛЕНИЯ",
                "Усиление мужчины / собственные атаки",
                "ОГРАНИЧЕНИЕ ХОДА",
                "Можно играть только один тип карт за ход"
            },
            new[]
            {
                "РЕСУРС КАРТ",
                "Деньги",
                "ДОСТУП К КАРТАМ",
                "Сильные карты требуют больше денег",
                "ДОЛГ",
                "Телефон разрешает долг ценой здоровья"
            }
        };
        readonly Color background = Color.FromArgb(16, 21, 27), surface = Color.FromArgb(25, 32, 40), text = Color.FromArgb(239, 236, 222), muted = Color.FromArgb(150, 158, 167), gold = Color.FromArgb(226, 190, 122);
        readonly Color[] colors =
        {
            Color.FromArgb(113, 182, 211),
            Color.FromArgb(190, 157, 223),
            Color.FromArgb(216, 148, 157),
            Color.FromArgb(224, 180, 94)
        };
        readonly string[] filenames =
        {
            "pupsolet.png",
            "illya.png",
            "runtso.png",
            "shuten.png"
        };
        Dictionary<int, Image> sprites = new Dictionary<int, Image>();
        List<Tuple<RectangleF, Action>> actions = new List<Tuple<RectangleF, Action>>();
        int selected = 0;
        bool preview = false;
        string notice = "";
        private ImageAssets assetStore;
        bool resourcesDisposed;
        private readonly Dictionary<Tuple<float, bool>, Font> fonts = new Dictionary<Tuple<float, bool>, Font>();
        public Game()
        {
            Text = "Свои люди · Смолевичи";
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(1280, 840);
            MinimumSize = new Size(800, 560);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = background;
            KeyPreview = true;
            assetStore = new ImageAssets(typeof(Game).Assembly, Assets);
            try
            {
                LoadSprites();
                LoadCheckoutArt();
                mapBackground = assetStore.Get("map-smolevichi.png", "map-smolevichi.png");
                mapIcons = assetStore.Get("map-icons.png", "map-icons.png");
                menuIcons = assetStore.Get("menu-icons.png", "menu-icons.png");
                hudTexture = assetStore.Get("hud-texture.png", "hud-texture.png");
                healthIcon = assetStore.Get("health-icon.png", "health-icon.png");
                coinIcon = assetStore.Get("coin-icon.png", "coin-icon.png");
                ratEnemy = assetStore.Get("enemies/giant-rat.png", "enemies.giant-rat.png");
                LoadCombatArt();
            }
            catch
            {
                assetStore.Dispose();
                howlSound.Dispose();
                deathVisuals.Dispose();
                if (hedgehogPoses != null) hedgehogPoses.Dispose();
                if (rabidHedgehogPoses != null) rabidHedgehogPoses.Dispose();
                if (combatTimer != null) combatTimer.Dispose();
                throw;
            }

            if (assetStore.Warnings.Length > 0)
                notice = assetStore.Warnings[0];
            MouseClick += OnClick;
            MouseWheel += OnMapWheel;
            MouseDown += OnMapMouseDown;
            MouseMove += OnMapMouseMove;
            MouseLeave += OnHudMouseLeave;
            MouseUp += OnMapMouseUp;
            KeyDown += HandleGameKey;
        }

        private void RefreshScene()
        {
            actions.Clear();
            Invalidate();
        }

        public Viewport DrawingViewport
        {
            get
            {
                return mapScreen ? Viewport.FillMap(ClientSize) : Viewport.Fit(ClientSize);
            }
        }

        private void HandleGameKey(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F11 || (e.Alt && e.KeyCode == Keys.Enter))
            {
                ToggleFullscreen();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (e.Alt || e.Control)
                return;
            if (!mapScreen && (inventoryView == "pause" || (e.KeyCode == Keys.Escape && !checkout && !preview)))
            {
                if (e.KeyCode == Keys.Escape)
                    SetInventory(inventoryView == "pause" ? "" : "pause");
                e.Handled = true;
                e.SuppressKeyPress = true;
                RefreshScene();
                return;
            }

            if (mapScreen)
            {
                if (e.KeyCode != Keys.Escape && e.KeyCode != Keys.Enter && e.KeyCode != Keys.D1 && e.KeyCode != Keys.D2 && e.KeyCode != Keys.D3 && !(e.KeyCode >= Keys.D4 && e.KeyCode <= Keys.D6) && e.KeyCode != Keys.D && e.KeyCode != Keys.R && e.KeyCode != Keys.Up && e.KeyCode != Keys.Down && e.KeyCode != Keys.PageUp && e.KeyCode != Keys.PageDown && e.KeyCode != Keys.Home && e.KeyCode != Keys.End && e.KeyCode != Keys.Space && e.KeyCode != Keys.Left && e.KeyCode != Keys.Right)
                    return;
                HandleMapKey(e);
            }
            else if (checkout)
            {
                if (e.KeyCode != Keys.Escape && e.KeyCode != Keys.Left && e.KeyCode != Keys.Right && e.KeyCode != Keys.Enter && e.KeyCode != Keys.D1 && e.KeyCode != Keys.D2 && e.KeyCode != Keys.D3)
                    return;
                HandleCheckoutKey(e);
            }
            else
            {
                if (e.KeyCode != Keys.Escape && e.KeyCode != Keys.Left && e.KeyCode != Keys.Right && e.KeyCode != Keys.Enter)
                    return;
                if (e.KeyCode == Keys.Escape)
                {
                    if (preview)
                        preview = false;
                }

                if (e.KeyCode == Keys.Left)
                {
                    selected = (selected + 3) % 4;
                }

                if (e.KeyCode == Keys.Right)
                {
                    selected = (selected + 1) % 4;
                }

                if (e.KeyCode == Keys.Enter && !preview)
                    OpenCheckout();
            }

            e.Handled = true;
            e.SuppressKeyPress = true;
            RefreshScene();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            hoveredRelic = false;
            CancelMapDrag();
            if (mapScreen)
            {
                mapScroll = mapScroll >= previousMapMaxScroll - .5f ? MapMaxScroll : ClampMapScroll(mapScroll);
                previousMapMaxScroll = MapMaxScroll;
                SnapMap(mapScroll);
            }

            if (actions != null)
                actions.Clear();
            Invalidate();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (isFullscreen)
            {
                StartFullscreen();
                return;
            }

            Rectangle area = Screen.FromControl(this).WorkingArea;
            Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
        }

        string Assets
        {
            get
            {
                return Path.Combine(Path.GetDirectoryName(typeof(Game).Assembly.Location), "assets");
            }
        }

        void LoadSprites()
        {
            sprites.Clear();
            for (int i = 0; i < filenames.Length; i++)
                sprites[i] = assetStore.Get(filenames[i], filenames[i]);
        }

        void Import()
        {
            using (var dialog = new OpenFileDialog
            {
                Title = "Спрайт: " + names[selected],
                Filter = "PNG-изображение|*.png"
            }

            )
            {
                if (dialog.ShowDialog() != DialogResult.OK)
                    return;
                string temporary = null;
                try
                {
                    using (var check = Image.FromFile(dialog.FileName))
                    {
                        if (check.Width > 8192 || check.Height > 8192 || (long)check.Width * check.Height > 32000000)
                            throw new InvalidDataException("Слишком большое изображение.");
                    }

                    Directory.CreateDirectory(Assets);
                    string target = Path.Combine(Assets, filenames[selected]);
                    if (!string.Equals(Path.GetFullPath(dialog.FileName), target, StringComparison.OrdinalIgnoreCase))
                    {
                        temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
                        File.Copy(dialog.FileName, temporary);
                        if (File.Exists(target))
                            File.Replace(temporary, target, null);
                        else
                            File.Move(temporary, target);
                        temporary = null;
                    }

                    assetStore.Reload(filenames[selected]);
                    sprites[selected] = assetStore.Get(filenames[selected], filenames[selected]);
                    notice = "Спрайт установлен: " + names[selected];
                }
                catch (Exception error)
                {
                    if (!(error is IOException || error is UnauthorizedAccessException || error is ArgumentException || error is OutOfMemoryException || error is System.Runtime.InteropServices.ExternalException))
                        throw;
                    notice = "Не удалось установить PNG: " + error.Message;
                }
                finally
                {
                    if (temporary != null && File.Exists(temporary))
                        try
                        {
                            File.Delete(temporary);
                        }
                        catch (IOException)
                        {
                        }
                        catch (UnauthorizedAccessException)
                        {
                        }
                }

                RefreshScene();
            }
        }

        void OnClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;
            if (mapScreen && (suppressMapClick || (mapDragging && mapDragMoved)))
            {
                suppressMapClick = false;
                return;
            }

            PointF point;
            if (!DrawingViewport.TryMap(e.Location, out point))
                return;
            for (int i = actions.Count - 1; i >= 0; i--)
            {
                if (!actions[i].Item1.Contains(point))
                    continue;
                Action action = actions[i].Item2;
                actions.Clear();
                action();
                RefreshScene();
                break;
            }
        }

        void Rect(Graphics g, float x, float y, float w, float h, Color c)
        {
            using (var b = new SolidBrush(c))
                g.FillRectangle(b, x, y, w, h);
        }

        void TextAt(Graphics g, string value, float x, float y, float size, Color color, float width, bool bold = false)
        {
            Font font = HudFont(size, bold);
            using (var brush = new SolidBrush(color))
                g.DrawString(value, font, brush, new RectangleF(x, y, width, 500));
        }

        void Stroke(Graphics g, float x, float y, float w, float h, Color c)
        {
            using (var p = new Pen(c, 1))
                g.DrawRectangle(p, x, y, w, h);
        }

        void Button(Graphics g, string label, float x, float y, float w, float h, Action action, bool primary = false)
        {
            Rect(g, x, y, w, h, primary ? colors[selected] : Color.FromArgb(40, 48, 58));
            TextAt(g, label, x + 16, y + (h - 22) / 2, 15, primary ? background : text, w - 25, true);
            actions.Add(Tuple.Create(new RectangleF(x, y, w, h), action));
        }

        void Portrait(Graphics g, int id, float x, float y, float width, float height)
        {
            if (sprites.ContainsKey(id))
            {
                var im = sprites[id];
                float r = Math.Min(width / im.Width, height / im.Height);
                g.DrawImage(im, x + (width - im.Width * r) / 2, y + height - im.Height * r, im.Width * r, im.Height * r);
                return;
            }

            float size = Math.Min(width, height);
            float xx = x + (width - size) / 2, yy = y + (height - size) / 2;
            using (var p = new Pen(colors[id], 2))
            {
                g.DrawEllipse(p, xx + size * .3f, yy + size * .1f, size * .4f, size * .4f);
                g.DrawArc(p, xx + size * .16f, yy + size * .52f, size * .68f, size * .6f, 180, 180);
                g.DrawLine(p, xx + size * .16f, yy + size * .82f, xx + size * .84f, yy + size * .82f);
            }

            TextAt(g, new[] { "П", "И", "Р", "Ш" }[id], xx + size * .40f, yy + size * .19f, size * .22f, colors[id], size * .4f, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.Clear(background);
            actions.Clear();
            var viewport = DrawingViewport;
            if (viewport.Scale <= 0)
                return;
            var saved = g.Save();
            try
            {
                g.TranslateTransform(viewport.OffsetX, viewport.OffsetY);
                g.ScaleTransform(viewport.Scale, viewport.Scale);
                g.SetClip(new RectangleF(0, 0, viewport.Width, viewport.Height));
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                Rect(g, 0, 0, 1280, 840, background);
                if (mapScreen)
                {
                    DrawActMap(g);
                    return;
                }

                if (checkout)
                {
                    DrawCheckout(g);
                    return;
                }

                TextAt(g, "С / Л", 36, 24, 24, gold, 100, true);
                TextAt(g, "СВОИ ЛЮДИ", 140, 31, 15, text, 320, true);
                TextAt(g, "СМОЛЕВИЧИ  /  " + GameVersion.Number, 1000, 32, 12, muted, 245);
                Rect(g, 36, 73, 1208, 1, Color.FromArgb(50, 58, 65));
                if (preview)
                {
                    TextAt(g, names[selected], 40, 102, 34, colors[selected], 700, true);
                    Portrait(g, selected, 240, 160, 800, 615);
                    Button(g, "← К выбору персонажа", 940, 102, 300, 50, () => preview = false);
                    TextAt(g, "← →  другой персонаж      Esc  назад", 40, 805, 12, muted, 1100);
                    return;
                }

                TextAt(g, "СВОИ ЛЮДИ. СВОИ ПРАВИЛА.", 36, 104, 12, gold, 800, true);
                TextAt(g, "Кто сегодня в пати?", 32, 129, 42, text, 1100, true);
                TextAt(g, "Четыре персонажа. Четыре способа устроить хаос.", 36, 191, 17, muted, 1160);
                for (int i = 0; i < 4; i++)
                {
                    int id = i;
                    float x = 36 + i * 188;
                    bool active = selected == i;
                    Rect(g, x, 246, 173, 390, active ? Color.FromArgb(35, 44, 54) : surface);
                    if (active)
                        Stroke(g, x, 246, 173, 390, colors[i]);
                    Rect(g, x, 246, 173, 3, colors[i]);
                    TextAt(g, "0" + (i + 1), x + 15, 260, 12, muted, 80);
                    Portrait(g, i, x + 5, 289, 163, 210);
                    TextAt(g, names[i], x + 15, 512, 25, active ? colors[i] : text, 150, true);
                    TextAt(g, summaries[i], x + 15, 555, 14, muted, 148);
                    actions.Add(Tuple.Create(new RectangleF(x, 246, 173, 390), (Action)(() =>
                    {
                        selected = id;
                        notice = "";
                    })));
                }

                Rect(g, 812, 246, 432, 482, surface);
                Rect(g, 812, 246, 432, 3, colors[selected]);
                TextAt(g, tags[selected], 836, 269, 11, colors[selected], 383, true);
                TextAt(g, names[selected], 832, 300, 34, text, 385, true);
                TextAt(g, descriptions[selected], 836, 355, 16, text, 380);
                var r = rules[selected];
                for (int j = 0; j < 3; j++)
                {
                    float yy = 501 + j * 66;
                    TextAt(g, r[j * 2], 836, yy, 10, muted, 380, true);
                    TextAt(g, r[j * 2 + 1], 836, yy + 20, 15, text, 380);
                }

                Button(g, "Выбрать: " + names[selected] + "   →", 36, 659, 360, 56, OpenCheckout, true);
                Button(g, "Загрузить спрайт PNG", 412, 659, 361, 56, Import);
                Button(g, "Рассмотреть спрайт", 812, 741, 432, 42, () => preview = true);
                TextAt(g, "Выберите персонажа и заберите личную реликвию на кассе.\nЗатем отправляйтесь по улицам Смолевичей.", 36, 737, 16, muted, 730);
                TextAt(g, notice.Length > 0 ? notice : "← →  выбор      Enter  к кассе      Esc  назад", 36, 805, 12, muted, 1190);
            }
            finally
            {
                try
                {
                    if (!mapScreen && inventoryView == "pause")
                        DrawRunInventory(g);
                }
                finally
                {
                    g.Restore(saved);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !resourcesDisposed)
            {
                resourcesDisposed = true;
                ResetCombat();
                mapRoutes.Dispose();
                howlSound.Dispose();
                deathVisuals.Dispose();
                if (hedgehogPoses != null) hedgehogPoses.Dispose();
                if (rabidHedgehogPoses != null) rabidHedgehogPoses.Dispose();
                if (sceneBackground != null)
                {
                    sceneBackground.Dispose();
                    sceneBackground = null;
                }
                if (combatTimer != null)
                    combatTimer.Dispose();
                Array.Clear(cardSheets, 0, cardSheets.Length);
                sprites.Clear();
                DisposeCheckoutArt();
                if (assetStore != null)
                    assetStore.Dispose();
                foreach (var font in fonts.Values)
                    font.Dispose();
                fonts.Clear();
                actions.Clear();
            }

            base.Dispose(disposing);
        }
    }
}
