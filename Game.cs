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
            "Илля всегда в замешательстве: при доборе стоимость каждой карты случайно меняется от 0 до 4 энергии. Добирает на две карты больше. В начале хода две случайные карты разыгрываются бесплатно.",
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
        readonly ScaledImageCache actorArtCache = new ScaledImageCache(24 * 1024 * 1024, 48, 4);
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
                music.Dispose();
                tohaSound.Dispose();
                actorArtCache.Dispose();
                interfaceArtCache.Dispose();
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
            KeyPress += OnDebugConsoleKeyPress;
        }

        private void RefreshScene()
        {
            SyncMusic();
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

            if (HandleDebugConsoleKey(e)) return;
            if (HandleMimicratorKey(e)) return;

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
                    ChooseMenuHero((selected + 3) % 4);
                }

                if (e.KeyCode == Keys.Right)
                {
                    ChooseMenuHero((selected + 1) % 4);
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
            combatPointer = null;
            routePressed = false;
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
            if (debugConsoleOpen || e.Button != MouseButtons.Left)
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

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (nativePaint) PaintScene(e.Graphics);
            else PaintFrame(e.Graphics);
        }

        void PaintScene(Graphics g)
        {
            Rect(g, 0, 0, RenderPixels.Width, RenderPixels.Height, background);
            actions.Clear();
            var viewport = RenderViewport;
            actorArtCache.SetScale(viewport.Scale);
            interfaceArtCache.SetScale(viewport.Scale);
            if (viewport.Scale <= 0)
                return;
            var saved = g.Save();
            try
            {
                g.TranslateTransform(viewport.OffsetX, viewport.OffsetY);
                g.ScaleTransform(viewport.Scale, viewport.Scale);
                g.SetClip(new RectangleF(0, 0, viewport.Width, viewport.Height), CombineMode.Intersect);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                if (mimicratorPreview)
                {
                    DrawMimicratorPreview(g);
                    return;
                }
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

                DrawMainMenu(g);
            }
            finally
            {
                try
                {
                    if (!mapScreen && inventoryView == "pause")
                        DrawRunInventory(g);
                    DrawDebugConsole(g);
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
                ReleasePresenter();
                ClearRenderSurface();
                music.Dispose();
                tohaSound.Dispose();
                ResetCombat();
                ClearMimicratorBackdrop();
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
                actorArtCache.Dispose();
                interfaceArtCache.Dispose();
                if (assetStore != null) assetStore.Dispose();
                foreach (var font in fonts.Values)
                    font.Dispose();
                fonts.Clear();
                actions.Clear();
            }

            base.Dispose(disposing);
        }
    }
}
