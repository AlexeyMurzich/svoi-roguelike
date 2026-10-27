using System.Drawing;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        Image tohaLight, tohaDark, tohaDay, tohaNight;
        bool tohaVisualDark;

        bool IsTohaRamming
        {
            get { return attackTimeline.IsRunning && (attackTimeline.Kind == AttackAnimation.TohaRam || attackTimeline.Kind == AttackAnimation.TohaDarkRam); }
        }

        void LoadTohaArt()
        {
            if (tohaLight != null) return;
            tohaLight = assetStore.Get("enemies/toha-light.png", "enemies.toha-light.png");
            tohaDark = assetStore.Get("enemies/toha-dark.png", "enemies.toha-dark.png");
            tohaDay = assetStore.Get("combat-toha-light.png", "combat-toha-light.png");
            tohaNight = assetStore.Get("combat-toha-dark.png", "combat-toha-dark.png");
        }

        bool TryStartTohaTransform()
        {
            if (combat == null || combat.Enemy != EnemyKind.Toha || CombatBusy || tohaVisualDark ||
                !combat.Enemies[0].IsAlive || !combat.Enemies[0].IsDarkToha || currentHero.Health <= 0) return false;
            combatNotice = "Тоха показывает тёмную сторону";
            StartCombatAnimation(AttackAnimation.TohaTransform, () =>
            {
                tohaVisualDark = true;
                combatNotice = "Тоха · тёмная сторона";
            });
            return true;
        }

        void StartTohaRam(CombatEnemy actor)
        {
            combat.PrepareNextEnemyAction();
            combatNotice = actor.IsDarkToha ? "Тоха · тёмный наезд" : "Тоха разгоняется";
            StartCombatAnimation(actor.IsDarkToha ? AttackAnimation.TohaDarkRam : AttackAnimation.TohaRam, () =>
            {
                int block = combat.Block;
                combat.ResolveNextEnemyImpact();
                if (combat.Block < block) guardVisualTime = .3f;
                SyncDeaths();
                combatNotice = combat.Phase == CombatPhase.Defeat ? "Поражение" : combat.LastDamage == 0 ?
                    "Наезд заблокирован" : "Наезд · получено урона: " + combat.LastDamage;
            });
        }

        void ApplyTohaHeroRecoil(Graphics graphics)
        {
            if (!IsTohaRamming || !attackTimeline.HitApplied) return;
            float p = (attackTimeline.Time - attackTimeline.HitTime) / .3f;
            if (p < 0 || p > 1) return;
            float kick = (attackTimeline.Kind == AttackAnimation.TohaDarkRam ? 22 : 12) * (float)System.Math.Sin(p * System.Math.PI);
            graphics.TranslateTransform(-kick, -kick * .15f);
        }
    }
}
