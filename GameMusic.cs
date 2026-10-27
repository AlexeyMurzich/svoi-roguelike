namespace ShpilSvoih
{
    public sealed partial class Game
    {
        readonly MusicPlayer music = new MusicPlayer();
        int menuMusicHero = -1;
        string DesiredMusicTrack
        {
            get
            {
                if (mimicratorPreview || checkout) return "";
                if (HasLiveFight && combat != null && combat.Enemy == EnemyKind.Mimicrator &&
                    (combat.Phase == CombatPhase.Player || combat.Phase == CombatPhase.Enemy || CombatBusy))
                    return "mimicrator";
                return !mapScreen && menuMusicHero >= 0 ? MusicTracks.Hero(menuMusicHero) : "";
            }
        }
        void ChooseMenuHero(int hero)
        {
            selected = hero;
            menuMusicHero = hero;
            SyncMusic();
        }
        void SyncMusic()
        {
            if (resourcesDisposed) return;
            // Offscreen regression windows never open an audio device.
            music.SetTrack(Visible ? DesiredMusicTrack : "", GameplayPaused || !ContainsFocus);
        }
    }
}
