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
        HedgehogAtlas hedgehogPoses, rabidHedgehogPoses;
        Image dogBody, dogHeads;
        readonly DeathVisuals deathVisuals = new DeathVisuals();
        int actingEnemyIndex = -1, attackTarget;
        float guardVisualTime, guardAtActionStart;
        EnemyKind? testEnemyOverride;
        readonly Dictionary<int, EnemyKind> encounteredEnemies = new Dictionary<int, EnemyKind>();
        EnemyKind EnemyAtNode(int id)
        {
            if (mapRun.Map.Nodes[id].Kind == RoomKind.Elite) return EnemyKind.Mimicrator;
            EnemyKind enemy;
            return encounteredEnemies.TryGetValue(id, out enemy) ? enemy : EnemyCatalog.ForNode(mapRun.Map.Seed, id);
        }
        readonly HowlSound howlSound = new HowlSound();
        string combatNotice = "";
        bool HasLiveFight
        {
            get
            {
                return mapScreen && mapRun != null && mapRun.Pending != null && (mapRun.Pending.Kind == RoomKind.Fight || mapRun.Pending.Kind == RoomKind.Elite) && !mapRun.PendingIsRevisit;
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
            ratPoses = new PoseAtlas(assetStore.Get("enemies/rat-bite.png", "enemies.rat-bite.png"), 3, 1, actorArtCache);
            shutenPoses = new PoseAtlas(assetStore.Get("shuten-combat.png", "shuten-combat.png"), 2, 2, actorArtCache);
            hedgehogPoses = new HedgehogAtlas(assetStore.Get("enemies/hedgehog-curl.png", "enemies.hedgehog-curl.png"), false, actorArtCache);
            rabidHedgehogPoses = new HedgehogAtlas(assetStore.Get("enemies/hedgehog-bite.png", "enemies.hedgehog-bite.png"), true, actorArtCache);
            eliteCombatBackground = assetStore.Get("combat-mimicrator.png", "combat-mimicrator.png");
            ordinaryCombatBackground = assetStore.Get("combat-social.png", "combat-social.png");
            routeButtonArt = assetStore.Get("route-button.png", "route-button.png");
            combatIcons = assetStore.Get("combat-icons.png", "combat-icons.png");
            InitializeMotionTimer();
        }

        void ResetCombat()
        {
            ClearMimicratorBackdrop();
            mimicratorCombatSeconds = 0;
            mimicratorVisualForm = MimicratorForm.Fairy;
            cardArtCache.Clear();
            actorArtCache.Clear();
            livingSeconds = 0;
            howlSound.Stop();
            if (combatTimer != null && !MapMoving)
                combatTimer.Stop();
            combatHintAreas.Clear();
            combatPointer = null;
            routePressed = false;
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

        internal void StartTestHedgehog() { StartTestEncounter(EnemyKind.Hedgehogs); }
        internal void StartTestRabidHedgehog() { StartTestEncounter(EnemyKind.Hedgehogs); }
        internal void StartTestHedgehogs() { StartTestEncounter(EnemyKind.Hedgehogs); }

        internal void StartTestMimicrator() { StartTestEncounter(EnemyKind.Mimicrator); }

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
            if (enemy == EnemyKind.Mimicrator)
            {
                LoadMimicratorBattleArt();
                WakeMotion();
            }
            combatNotice = "Твой ход";
            SyncDeaths();
            WakeMotion();
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
            if (!CombatBusy || combat == null || GameplayPaused || !Motion.IsFinite(elapsed) || elapsed <= 0)
                return;
            // Split at impacts/completion so death starts at impact, and a queued
            // enemy receives the remainder of the frame instead of losing it.
            float remaining = elapsed;
            while (remaining > 0 && CombatBusy && combat != null)
            {
                float step = attackTimeline.IsRunning ? Math.Min(remaining, attackTimeline.SecondsToBoundary) : remaining;
                deathVisuals.Advance(step);
                guardVisualTime = Math.Max(0, guardVisualTime - step);
                attackTimeline.Advance(step);
                remaining = Math.Max(0, remaining - step);
                SyncDeaths();
                if (!CombatBusy && combat.Phase == CombatPhase.Enemy) StartNextEnemyAnimation();
            }
            if (!CombatBusy && !CombatAmbientMoving && !LivingScene && combatTimer != null)
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
                    SyncDeaths();
                    combatNotice = combat.Phase == CombatPhase.Victory ? "Победа!" : combat.Phase == CombatPhase.Defeat ? "Поражение" : combat.LastRetaliation > 0 ? "Попадание · Шипы: −" + combat.LastRetaliation + " HP" : "Попадание";
                });
            }
            else if (card.Block > 0)
            {
                StartCombatAnimation(AttackAnimation.HeroGuard, () =>
                {
                    combat.TryPlay(index);
                    SyncDeaths();
                    combatNotice = combat.Phase == CombatPhase.Defeat ? "Поражение" : selected == 3 ? "Защита кредиткой" : "Защита поднята";
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
            if (actor.Kind == EnemyKind.Mimicrator)
            {
                if (NeedsMimicratorTransform) StartMimicratorTransform();
                else
                {
                    if (!combat.EnemyActionPrepared) combat.PrepareNextEnemyAction();
                    StartMimicratorAction(actor);
                }
                return;
            }
            combatNotice = actor.Name + (howling ? " воет" : " атакует");
            if (howling && Visible && ContainsFocus) howlSound.Play();
            StartCombatAnimation(howling ? AttackAnimation.DogHowl : actor.Kind == EnemyKind.StrayDogs ? AttackAnimation.DogBite : actor.Kind == EnemyKind.Hedgehog ? AttackAnimation.HedgehogNeedles : actor.Kind == EnemyKind.RabidHedgehog ? AttackAnimation.HedgehogBite : AttackAnimation.RatBite, () =>
            {
                int block = combat.Block;
                bool wasRabid = combat.Rabies;
                combat.ResolveNextEnemyAction();
                if (combat.Block < block) guardVisualTime = .3f;
                SyncDeaths();
                combatNotice = !wasRabid && combat.Rabies ? "Бешенство наложено" : howling ? "Слабость: урон атак −25% на 1 ход" : combat.Phase == CombatPhase.Defeat ? "Поражение" : combat.LastDamage == 0 ? "Удар заблокирован" : "Получено урона: " + combat.LastDamage;
            });
        }

        void SyncDeaths()
        {
            if (combat == null) return;
            for (int i = 0; i < combat.Enemies.Count; i++)
            {
                int index = i;
                var enemy = combat.Enemies[index];
                if (!enemy.IsAlive && deathVisuals.Start(enemy.Id))
                    deathVisuals.Capture(enemy.Id, EnemyArea(index), canvas => PaintCombatEnemy(canvas, index));
            }
            if (currentHero.Health <= 0 && deathVisuals.Start(-1))
                deathVisuals.Capture(-1, HeroDeathArea, PaintCombatHero);
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

    }
}
