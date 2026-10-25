using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        CombatState combat;
        int combatNode = -1;
        readonly Image[] cardSheets = new Image[4];
        readonly AttackTimeline attackTimeline = new AttackTimeline();
        PoseAtlas ratPoses, shutenPoses;
        Image dogBody, dogHeads, shutenDefense;
        readonly DeathVisuals deathVisuals = new DeathVisuals();
        int actingEnemyIndex = -1, attackTarget;
        float guardVisualTime, guardAtActionStart;
        EnemyKind? testEnemyOverride;
        readonly Dictionary<int, EnemyKind> encounteredEnemies = new Dictionary<int, EnemyKind>();
        EnemyKind EnemyAtNode(int id)
        {
            EnemyKind enemy;
            return encounteredEnemies.TryGetValue(id, out enemy) ? enemy : EnemyCatalog.ForNode(mapRun.Map.Seed, id);
        }
        readonly HowlSound howlSound = new HowlSound();
        string combatNotice = "";
        bool HasLiveFight
        {
            get
            {
                return mapScreen && mapRun != null && mapRun.Pending != null && mapRun.Pending.Kind == RoomKind.Fight && !mapRun.PendingIsRevisit;
            }
        }

        bool CombatBusy
        {
            get
            {
                return attackTimeline.IsRunning || deathVisuals.IsRunning;
            }
        }

        void LoadCombatArt()
        {
            for (int i = 0; i < 4; i++)
                cardSheets[i] = assetStore.Get("cards/" + filenames[i], "cards." + filenames[i]);
            dogBody = assetStore.Get("enemies/dog-body.png", "enemies.dog-body.png");
            dogHeads = assetStore.Get("enemies/stray-dogs.png", "enemies.stray-dogs.png");
            shutenDefense = assetStore.Get("shuten-defense.png", "shuten-defense.png");
            ratPoses = new PoseAtlas(assetStore.Get("enemies/rat-bite.png", "enemies.rat-bite.png"));
            shutenPoses = new PoseAtlas(assetStore.Get("shuten-attack.png", "shuten-attack.png"));
            InitializeMotionTimer();
        }

        void ResetCombat()
        {
            howlSound.Stop();
            if (combatTimer != null && !MapMoving)
                combatTimer.Stop();
            combat = null;
            combatNode = -1;
            attackTimeline.Cancel();
            deathVisuals.Clear();
            actingEnemyIndex = -1;
            guardVisualTime = guardAtActionStart = 0;
            combatNotice = "";
        }

        internal void StartTestBattle()
        {
            StartTestEncounter(EnemyKind.Rat);
        }

        internal void StartTestDogs()
        {
            StartTestEncounter(EnemyKind.StrayDogs);
        }

        void StartTestEncounter(EnemyKind enemy)
        {
            ReturnToMainMenu();
            selected = 3;
            OpenCheckout();
            TakeBuff();
            OpenActMap();
            testEnemyOverride = enemy;
            EnterMapNode(mapRun.Current.Exits[0]);
            RefreshScene();
        }

        void EnterMapNode(int id)
        {
            StopMapMotion();
            if (!mapRun.Enter(id))
                return;
            if (HasLiveFight)
                EnsureCombat();
        }

        void EnsureCombat()
        {
            if (!HasLiveFight || (combat != null && combatNode == mapRun.Pending.Id))
                return;
            EnemyKind enemy = testEnemyOverride ?? EnemyAtNode(mapRun.Pending.Id);
            testEnemyOverride = null;
            ResetCombat();
            combatNode = mapRun.Pending.Id;
            encounteredEnemies[combatNode] = enemy;
            combat = new CombatState(currentHero, new Random(mapRun.Map.Seed ^ combatNode ^ 0xA71), enemy);
            combatNotice = "Твой ход";
            SyncDeaths();
        }

        void StartCombatAnimation(AttackAnimation kind, Action impact)
        {
            attackTimeline.Start(kind, impact);
            ResetMotionClock();
            WakeMotion();
            RefreshScene();
        }

        void AdvanceCombatAnimation(float elapsed)
        {
            if (!CombatBusy || inventoryView != "")
                return;
            deathVisuals.Advance(elapsed);
            guardVisualTime = Math.Max(0, guardVisualTime - Math.Max(0, elapsed));
            attackTimeline.Advance(elapsed);
            SyncDeaths();
            if (!CombatBusy && combat.Phase == CombatPhase.Enemy) StartNextEnemyAnimation();
            if (!CombatBusy && combatTimer != null)
                combatTimer.Stop();
            // The displayed frame's hit targets remain valid until the next paint.
            Invalidate();
        }

        void PlayCombatCard(int index)
        {
            if (CombatBusy || combat == null || !combat.CanPlay(index))
                return;
            var card = combat.Hand[index];
            guardAtActionStart = combat.Block > 0 ? 1 : Motion.Ease(guardVisualTime / .3f);
            if (card.Attack)
            {
                attackTarget = combat.SelectedTarget;
                StartCombatAnimation(AttackAnimation.HeroStrike, () =>
                {
                    combat.TryPlay(index);
                    combatNotice = combat.Phase == CombatPhase.Victory ? "Победа!" : combat.Phase == CombatPhase.Defeat ? "Поражение" : "Попадание";
                });
            }
            else if (selected == 3 && card.Block > 0)
            {
                StartCombatAnimation(AttackAnimation.HeroGuard, () =>
                {
                    combat.TryPlay(index);
                    combatNotice = combat.Phase == CombatPhase.Defeat ? "Поражение" : "Защита кредиткой";
                });
            }
            else
            {
                combat.TryPlay(index);
                combatNotice = "Защита поднята";
                RefreshScene();
            }
        }

        void EndCombatTurn()
        {
            if (CombatBusy || combat == null || !combat.EndPlayerTurn())
                return;
            StartNextEnemyAnimation();
        }

        void StartNextEnemyAnimation()
        {
            if (combat.Phase != CombatPhase.Enemy || combat.ActingEnemy < 0) return;
            actingEnemyIndex = combat.ActingEnemy;
            var actor = combat.Enemies[actingEnemyIndex];
            bool howling = actor.WillHowl;
            combatNotice = actor.Name + (howling ? " воет" : " атакует");
            if (howling && Visible && ContainsFocus) howlSound.Play();
            StartCombatAnimation(howling ? AttackAnimation.DogHowl : actor.Kind == EnemyKind.StrayDogs ? AttackAnimation.DogBite : AttackAnimation.RatBite, () =>
            {
                int block = combat.Block;
                combat.ResolveNextEnemyAction();
                if (combat.Block < block) guardVisualTime = .3f;
                combatNotice = howling ? "Слабость: урон атак −25% на 1 ход" : combat.Phase == CombatPhase.Defeat ? "Поражение" : combat.LastDamage == 0 ? "Удар заблокирован" : "Получено урона: " + combat.LastDamage;
            });
        }

        void SyncDeaths()
        {
            if (combat == null) return;
            foreach (var enemy in combat.Enemies)
                if (!enemy.IsAlive) deathVisuals.Start(enemy.Id);
            if (currentHero.Health <= 0) deathVisuals.Start(-1);
            if (deathVisuals.IsRunning) WakeMotion();
        }

        void SelectCombatTarget(int index)
        {
            if (!CombatBusy && combat.SelectTarget(index)) RefreshScene();
        }

        void HandleCombatKey(KeyEventArgs e)
        {
            if (e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D6)
                PlayCombatCard(e.KeyCode - Keys.D1);
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                if (CombatBusy)
                    return;
                if (combat.Phase == CombatPhase.Victory)
                    CompleteMapRoom();
                else if (combat.Phase == CombatPhase.Defeat)
                    ReturnToMainMenu();
                else
                    EndCombatTurn();
            }
        }

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
                g.DrawImage(cardSheets[selected], area, source, GraphicsUnit.Pixel);
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
            var price = combat.Cost(card).ToString("0.#");
            var font = HudFont(21, true);
            var size = g.MeasureString(price, font);
            using (var brush = new SolidBrush(text))
                g.DrawString(price, font, brush, cx - size.Width / 2, cy - size.Height / 2);
            float rulesY = area.Y + area.Height * .765f;
            Rect(g, area.X + 13, rulesY, area.Width - 26, area.Height * .125f, Color.FromArgb(255, 17, 24, 25));
            string rules = card.Attack ? combat.DamageFor(card) + " урона" : card.Block + " блока";
            if (card.Attack && card.Block > 0)
                rules += ", " + card.Block + " блока";
            TextAt(g, rules, area.X + 19, rulesY + 5, 16, text, area.Width - 32, true);
            TextAt(g, (index + 1).ToString(), area.Right - 17, area.Bottom - 22, 12, gold, 20, true);
            if (usable)
            {
                int choice = index;
                actions.Add(Tuple.Create(slot, (Action)(() => PlayCombatCard(choice))));
            }
            else
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
            SyncDeaths();
            g.InterpolationMode = InterpolationMode.Bilinear;
            DrawSceneBackground(g, true);
            float heroX = CanvasWidth * .24f;
            float floor = Math.Min(CanvasHeight - 295, (160 + CanvasHeight - 285) / 2 + 160);
            DrawCombatActors(g, heroX, floor);
            TextAt(g, combatNotice, CanvasWidth / 2 - 150, floor + 34, 16, gold, 360, true);
            float cardsY = CanvasHeight - 260;
            if (combat.Phase == CombatPhase.Player || CombatBusy)
            {
                for (int i = 0; i < combat.Hand.Count; i++)
                    DrawCombatCard(g, combat.Hand[i], i, CombatLayout.CardSlot(CanvasWidth, CanvasHeight, combat.Hand.Count, i));
            }

            TextAt(g, "Колода · " + combat.DrawCount + "\nСброс · " + combat.DiscardCount, 25, cardsY + 90, 16, text, 190, true);
            if (selected != 3)
                TextAt(g, "Энергия · " + combat.Energy, 25, cardsY + 35, 18, gold, 210, true);
            if (!CombatBusy)
            {
                if (combat.Phase == CombatPhase.Victory)
                    Button(g, "Победа · На карту", CanvasWidth - 235, CanvasHeight - 80, 210, 54, CompleteMapRoom, true);
                else if (combat.Phase == CombatPhase.Defeat)
                    Button(g, "Поражение · В меню", CanvasWidth - 235, CanvasHeight - 80, 210, 54, ReturnToMainMenu);
                else
                    Button(g, "Завершить ход", CanvasWidth - 235, CanvasHeight - 80, 210, 54, EndCombatTurn, true);
            }

            DrawRunBar(g);
            DrawMapRelics(g);
            if (inventoryView != "")
                DrawRunInventory(g);
            else if (hoveredRelic)
                DrawRelicTooltip(g);
        }
    }
}
