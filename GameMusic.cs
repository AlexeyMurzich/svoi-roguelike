namespace ShpilSvoih
{
    public sealed partial class Game
    {
        readonly MusicPlayer music = new MusicPlayer();
        readonly CardSoundPlayer cardSounds = new CardSoundPlayer();
        int audioPlayerTurn = -1;
        bool AudioNeedsMotion { get { return cardSounds.ActiveVoices > 0; } }
        double DesiredMusicGain { get { return AudioLevels.Music; } }
        int menuMusicHero = -1;
        string DesiredMusicTrack
        {
            get
            {
                if (mimicratorPreview || checkout) return "";
                if (HasLiveFight && combat != null && combat.Enemy == EnemyKind.Mimicrator)
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
            // Long menu songs can reserve more audio; battle effects retain a shorter queue.
            LoopingMusicVoice.SetMenuBuffering(!checkout && !mapScreen && !mimicratorPreview && menuMusicHero >= 0);
            bool pause = GameplayPaused || !ContainsFocus || !Visible || (mimicratorPreview && mimicratorPaused);
            tohaSound.SetPaused(pause);
            cardSounds.SetPaused(pause);
            howlSound.SetPaused(pause);
            if (!pause && combat != null && combat.Turn != audioPlayerTurn)
            {
                audioPlayerTurn = combat.Turn;
                // Simultaneous automatic cards get one accent, not a stack of attacks.
                if (combat.AutomaticAttacksThisTurn > 0 || combat.AutomaticBlocksThisTurn > 0)
                {
                    cardSounds.Play(selected, combat.AutomaticAttacksThisTurn > 0, false, true);
                    WakeMotion();
                }
            }
            // Offscreen regression windows never open an audio device.
            music.SetVolume(DesiredMusicGain);
            music.SetTrack(Visible ? DesiredMusicTrack : "", pause);
        }
        void AdvanceAudio(float seconds)
        {
            cardSounds.Advance(seconds);
        }
        void StartCardSound(bool attack)
        {
            if (!Visible || !ContainsFocus || GameplayPaused) return;
            cardSounds.Play(selected, attack, false);
        }
    }
}
