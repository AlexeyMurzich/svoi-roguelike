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
        DogAtlas dogPoses;
        readonly DeathVisuals deathVisuals = new DeathVisuals();
        int actingEnemyIndex = -1, attackTarget;
        float guardVisualTime, guardAtActionStart;
        EnemyKind? testEnemyOverride;
        readonly Dictionary<int, EnemyKind> encounteredEnemies = new Dictionary<int, EnemyKind>();
        EnemyKind EnemyAtNode(int id)
        {
            if (mapRun.Map.Nodes[id].Kind == RoomKind.Elite) return EnemyCatalog.ForEliteNode(mapRun.Map.Seed, id, mapRun.Map.Nodes[id].District);
            if (mapRun.Map.Nodes[id].Kind == RoomKind.Question) return QuestionEnemy;
            EnemyKind enemy;
            return encounteredEnemies.TryGetValue(id, out enemy) ? enemy : mapRun.OrdinaryEnemyAt(id);
        }
        readonly HowlSound howlSound = new HowlSound();
        string combatNotice = "";
        bool HasLiveFight
        {
            get
            {
                return mapScreen && mapRun != null && mapRun.Pending != null && (mapRun.Pending.Kind == RoomKind.Fight || mapRun.Pending.Kind == RoomKind.Elite || QuestionBattle) && !mapRun.PendingIsRevisit;
            }
        }

        bool CombatBusy
        {
            get
            {
                return attackTimeline.IsRunning || deathVisuals.IsRunning || FomatreshkaFleeing;
            }
        }

        void LoadCombatArt()
        {
            for (int i = 0; i < 4; i++)
                cardSheets[i] = assetStore.Get("cards/" + filenames[i], "cards." + filenames[i]);
            dogPoses = new DogAtlas(assetStore.Get("enemies/dogs-complete.png", "enemies.dogs-complete.png"), actorArtCache);
            ratPoses = new PoseAtlas(assetStore.Get("enemies/rat-bite.png", "enemies.rat-bite.png"), 3, 1, actorArtCache);
            shutenPoses = new PoseAtlas(assetStore.Get("shuten-combat.png", "shuten-combat.png"), 2, 2, actorArtCache);
            hedgehogPoses = new HedgehogAtlas(assetStore.Get("enemies/hedgehog-curl.png", "enemies.hedgehog-curl.png"), false, actorArtCache);
            rabidHedgehogPoses = new HedgehogAtlas(assetStore.Get("enemies/hedgehog-bite.png", "enemies.hedgehog-bite.png"), true, actorArtCache);
            eliteCombatBackground = assetStore.Get("combat-mimicrator.png", "combat-mimicrator.png");
            ordinaryCombatBackground = assetStore.Get("combat-social-stage.png", "combat-social-stage.png");
            routeButtonArt = assetStore.Get("route-button.png", "route-button.png");
            combatIcons = assetStore.Get("combat-icons.png", "combat-icons.png");
            combatDockIcons = assetStore.Get("combat-dock-icons.png", "combat-dock-icons.png");
            InitializeMotionTimer();
        }

        void ResetCombat()
        {
            fomatreshkaFlee = -1;
            fomatreshkaEntrance = FomatreshkaRules.EntranceDuration;
            cardSounds.Stop();
            audioPlayerTurn = -1;
            tohaSound.Stop();
            ClearDistrictBackdrop();
            ClearTohaBackdrop();
            tohaVisualDark = false;
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
            hoveredCombatCard = -1;
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

        internal void StartTestDoctors() { StartTestEncounter(EnemyKind.Doctors); }

        internal void StartTestBabai() { StartTestEncounter(EnemyKind.Babai); }

        internal void StartTestToha() { StartTestEncounter(EnemyKind.Toha); }

        internal void StartTestMimicrator() { StartTestEncounter(EnemyKind.Mimicrator); }

        internal void StartTestIllyaToha() { StartTestEncounter(EnemyKind.Toha, 1); }

        void StartTestEncounter(EnemyKind enemy) { StartTestEncounter(enemy, 3); }

        void StartTestEncounter(EnemyKind enemy, int hero)
        {
            ReturnToMainMenu();
            selected = hero;
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
            mapIntel[combatNode].Identity = EnemyCatalog.Name(enemy);
            combat = new CombatState(currentHero, new Random(mapRun.Map.Seed ^ combatNode ^ 0xA71), enemy);
            cardSounds.Prepare(selected);
            if (enemy == EnemyKind.Doctors) LoadDoctorArt();
            if (enemy == EnemyKind.Fomatreshka) LoadFomatreshkaArt();
            if (enemy == EnemyKind.Mimicrator)
            {
                LoadMimicratorBattleArt();
                WakeMotion();
            }
            if (enemy == EnemyKind.Toha)
            {
                if (sceneBackground != null) { sceneBackground.Dispose(); sceneBackground = null; }
                LoadTohaArt();
            }
            combatNotice = "Твой ход";
            SyncDeaths();
            TryStartTohaTransform();
            WakeMotion();
        }

        void StartCombatAnimation(AttackAnimation kind, Action impact)
        {
            attackTimeline.Start(kind, impact);
            if (kind == AttackAnimation.HeroStrike || kind == AttackAnimation.HeroGuard)
                StartCardSound(kind == AttackAnimation.HeroStrike);
            else cardSounds.SoftenPrevious();
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
            bool wasHit = attackTimeline.HitApplied;
            var oldKind = attackTimeline.Kind;
            float remaining = elapsed;
            while (remaining > 0 && CombatBusy && combat != null)
            {
                float step = attackTimeline.IsRunning ? Math.Min(remaining, attackTimeline.SecondsToBoundary) : remaining;
                deathVisuals.Advance(step);
                if (fomatreshkaFlee >= 0) fomatreshkaFlee = Math.Min(FomatreshkaRules.FleeDuration, fomatreshkaFlee + step);
                guardVisualTime = Math.Max(0, guardVisualTime - step);
                attackTimeline.Advance(step);
                remaining = Math.Max(0, remaining - step);
                SyncDeaths();
                if (!CombatBusy)
                {
                    if (TryStartTohaTransform()) continue;
                    if (combat.Phase == CombatPhase.Enemy) StartNextEnemyAnimation();
                }
            }
            if (!CombatBusy && !CombatAmbientMoving && !LivingScene && !AudioNeedsMotion && !presentationPending && combatTimer != null)
                combatTimer.Stop();
            SyncMusic();
            // Impacts and completion also change cards, gold, HP and button availability.
            if (!CombatBusy || wasHit != attackTimeline.HitApplied || oldKind != attackTimeline.Kind) Invalidate();
            else InvalidateMotionFrame();
        }

        void PlayCombatCard(int index)
        {
            if (CombatBusy || combat == null || !combat.CanPlay(index))
                return;
            hoveredCombatCard = -1;
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
            hoveredCombatCard = -1;
            StartNextEnemyAnimation();
        }

        void StartNextEnemyAnimation()
        {
            if (combat.Phase != CombatPhase.Enemy || combat.ActingEnemy < 0) return;
            actingEnemyIndex = combat.ActingEnemy;
            var actor = combat.Enemies[actingEnemyIndex];
            if (actor.Kind == EnemyKind.Fomatreshka || actor.Kind == EnemyKind.Fan) { StartFomatreshkaAction(actor); return; }
            if (actor.Kind == EnemyKind.Babai) { StartBabaiLasso(); return; }
            if (actor.Kind == EnemyKind.Toha)
            {
                if (!TryStartTohaTransform()) StartTohaRam(actor);
                return;
            }
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
                combatNotice = actor.Kind == EnemyKind.FemaleDoctor ? "Укол: 5 урона · яд +2" : actor.Kind == EnemyKind.MaleDoctor ? "Кровотечение +2" : !wasRabid && combat.Rabies ? "Бешенство наложено" : howling ? "Слабость: урон атак −25% на 1 ход" : combat.Phase == CombatPhase.Defeat ? "Поражение" : combat.LastDamage == 0 ? "Удар заблокирован" : "Получено урона: " + combat.LastDamage;
            });
        }

        void SyncDeaths()
        {
            if (combat == null) return;
            RewardDoctors();
            for (int i = 0; i < combat.Enemies.Count; i++)
            {
                int index = i;
                var enemy = combat.Enemies[index];
                if (enemy.Fled)
                {
                    if (fomatreshkaFlee < 0) fomatreshkaFlee = 0;
                    continue;
                }
                if (!enemy.IsAlive && deathVisuals.Start(enemy.Id))
                    deathVisuals.Capture(enemy.Id, EnemyArea(index), canvas => PaintCombatEnemy(canvas, index));
            }
            if (currentHero.Health <= 0 && !(combat.Babai != null && combat.Babai.Caught) && deathVisuals.Start(-1))
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
