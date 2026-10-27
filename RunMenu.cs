using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        Image menuIcons;
        string inventoryView = "";
        int deckPage;
        bool legendOpen;
        void ReturnToMainMenu()
        {
            menuMusicHero = -1;
            music.SetTrack("", true);
            debugConsoleOpen = false;
            debugCommand = "";
            debugReply = "shut_god — Шутен: бесконечное золото, атаки и защита ×3.";
            ClearMimicratorBackdrop();
            mimicratorPreview = false;
            mimicratorPaused = false;
            StopMapMotion();
            ResetCombat();
            mapRoutes.Clear();
            encounteredEnemies.Clear();
            testEnemyOverride = null;
            mapScreen = false;
            checkout = false;
            preview = false;
            SetInventory("");
            currentHero = null;
            mapRun = null;
            mapIntel.Clear();
            mapScroll = previousMapMaxScroll = 0;
            mapDragging = mapDragMoved = suppressMapClick = false;
            Capture = false;
            hoveredMapNode = -1;
            hoveredRelic = legendOpen = false;
            deckPage = buffCursor = 0;
            notice = "";
            RefreshScene();
        }

        int MaxDeckPage
        {
            get
            {
                return Math.Max(0, (currentHero.Deck.Count - 1) / 8);
            }
        }

        void DrawRunBar(Graphics g)
        {
            DrawHudBackground(g);
            DrawHeroPortrait(g);
            FitImage(g, healthIcon, new RectangleF(117, 10, 44, 44));
            TextAt(g, currentHero.Health + " / " + currentHero.MaxHealth, 170, 19, 20, text, 130, true);
            FitImage(g, coinIcon, new RectangleF(292, 12, 40, 40));
            TextAt(g, currentHero.MoneyDisplay, 340, 20, 20, gold, 150, true);
            DrawMenuIcon(g, false, new RectangleF(CanvasWidth - 160, 9, 60, 46), () =>
            {
                SetInventory("deck");
                deckPage = 0;
            });
            TextAt(g, currentHero.Deck.Count.ToString(), CanvasWidth - 111, 35, 12, text, 32, true);
            DrawMenuIcon(g, true, new RectangleF(CanvasWidth - 76, 9, 55, 46), () => SetInventory("pause"));
        }

        void DrawMapRelics(Graphics g)
        {
            var relic = currentHero.Effects.Chosen;
            string icon = relic.Icon == "placeholder" ? "mystery" : relic.Icon;
            FitImage(g, relicImages[icon], new RectangleF(22, 69, 49, 49));
            actions.Add(Tuple.Create(RelicHitArea, (Action)(() => SetInventory("relics"))));
        }

        void DrawMenuIcon(Graphics g, bool gear, RectangleF area, Action action)
        {
            float x = area.X + area.Width / 2, y = area.Y + area.Height / 2;
            float cell = menuIcons.Width / 2f;
            var source = new RectangleF(gear ? cell : 0, 0, cell, menuIcons.Height);
            g.DrawImage(menuIcons, new RectangleF(x - 30, y - 30, 60, 60), source, GraphicsUnit.Pixel);
            actions.Add(Tuple.Create(area, action));
        }

        void DrawRunInventory(Graphics g)
        {
            // A menu never advances a room or lets clicks reach the map underneath.
            actions.Clear();
            Rect(g, 0, 0, CanvasWidth, CanvasHeight, Color.FromArgb(225, 6, 12, 18));
            DrawCenteredOverlay(g, DrawRunInventoryContent);
        }

        string DeckCardDescription(CardData card)
        {
            string cost = currentHero.Hero == 3
                ? "Золото: " + currentHero.Effects.GoldPrice(card.GoldCost)
                : "Энергия: " + card.ManaCost;
            return cost + "   Урон: " + card.Damage + "   Защита: " + card.Block;
        }

        void DrawRunInventoryContent(Graphics g)
        {
            RelicFrame(g, 205, 174, 870, 570, true);
            bool isDeck = inventoryView == "deck";
            bool isPause = inventoryView == "pause";
            TextAt(g, isPause ? "Пауза" : isDeck ? "Колода · " + currentHero.Deck.Count : "Реликвии · 1", 237, 202, 29, gold, 650, true);
            Button(g, "Закрыть · Esc", 897, 195, 145, 39, () => SetInventory(""));
            if (isPause)
            {
                Button(g, "Продолжить", 430, 315, 420, 56, () => SetInventory(""));
                Button(g, "Полный экран: " + (isFullscreen ? "включён" : "выключен"), 430, 397, 420, 56, ToggleFullscreen);
                Button(g, "В главное меню", 430, 479, 420, 56, ReturnToMainMenu);
                Button(g, "Выйти из игры", 430, 561, 420, 56, Close);
            }
            else if (!isDeck)
            {
                var relic = currentHero.Effects.Chosen;
                string icon = relic.Icon == "placeholder" ? "mystery" : relic.Icon;
                FitImage(g, relicImages[icon], new RectangleF(244, 293, 210, 240));
                TextAt(g, relic.Name, 487, 302, 25, text, 535, true);
                TextAt(g, relic.Description, 487, 370, 18, text, 535);
            }
            else if (currentHero.Deck.Count == 0)
            {
                TextAt(g, "Колода пока пуста", 283, 345, 33, text, 680, true);
            }
            else
            {
                deckPage = Math.Min(deckPage, MaxDeckPage);
                int end = Math.Min(currentHero.Deck.Count, (deckPage + 1) * 8);
                for (int index = deckPage * 8; index < end; index++)
                {
                    var card = currentHero.Deck[index];
                    float y = 267 + (index % 8) * 48;
                    Rect(g, 235, y, 810, 43, Color.FromArgb(100, background));
                    TextAt(g, string.IsNullOrEmpty(card.Name) ? "Без названия" : card.Name, 250, y + 9, 16, text, 390, true);
                    TextAt(g, DeckCardDescription(card), 654, y + 10, 12, muted, 385);
                }

                if (deckPage > 0)
                    Button(g, "← Назад", 237, 687, 155, 34, () => deckPage--);
                TextAt(g, (deckPage + 1) + " / " + (MaxDeckPage + 1), 606, 695, 12, muted, 130);
                if (deckPage < MaxDeckPage)
                    Button(g, "Далее →", 887, 687, 155, 34, () => deckPage++);
            }
        }
    }
}
