using System.Drawing;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        Image babaiBodies, babaiBed;
        RectangleF babaiBedSource;
        int babaiDistanceAtStart = BabaiRules.InitialDistance;
        bool IsBabaiLasso { get { return attackTimeline.Kind == AttackAnimation.BabaiLasso; } }
        float BabaiDisplayDistance
        {
            get { return combat == null || combat.Babai == null ? BabaiRules.InitialDistance :
                BabaiMotion.DisplayDistance(combat.Babai.Distance, babaiDistanceAtStart, IsBabaiLasso, attackTimeline.HitApplied, attackTimeline.Time); }
        }
        float HeroSpriteCenter
        {
            get
            {
                if (combat == null || combat.Babai == null) return HeroCenter;
                float pulled = (BabaiRules.InitialDistance - BabaiDisplayDistance) / BabaiRules.InitialDistance;
                return HeroCenter + (CanvasWidth * .64f - HeroCenter) * pulled;
            }
        }
        void LoadBabaiArt()
        {
            if (babaiBodies != null) return;
            babaiBodies = assetStore.Get("enemies/babai-poses.png", "enemies.babai-poses.png");
            babaiBed = assetStore.Get("enemies/babai-bed.png", "enemies.babai-bed.png");
            babaiBedSource = BabaiStageLayout.FootCrop((Bitmap)babaiBed);
        }
        void StartBabaiLasso()
        {
            babaiDistanceAtStart = combat.Babai.Distance;
            combat.PrepareNextEnemyAction();
            combatNotice = "Бабай набрасывает лассо";
            StartCombatAnimation(AttackAnimation.BabaiLasso, () =>
            {
                int block = combat.Block;
                combat.ResolveNextEnemyImpact();
                if (combat.Block < block) guardVisualTime = .3f;
                combatNotice = combat.Babai.Caught ? "Бабай утащил под кровать" : combat.Phase == CombatPhase.Defeat ?
                    "Поражение" : "До кровати: " + combat.Babai.Distance + " · Бабай притянул на 1";
                SyncDeaths();
            });
        }
    }
}
