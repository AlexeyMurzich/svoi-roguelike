namespace ShpilSvoih
{
    public sealed partial class Game
    {
        readonly MusicPlayer music = new MusicPlayer();
        readonly CardSoundPlayer cardSounds = new CardSoundPlayer();
        readonly AudioMixEnvelope audioMix = new AudioMixEnvelope();
        int audioPlayerTurn = -1;
        bool AudioNeedsMotion { get { return cardSounds.ActiveVoices > 0 || !audioMix.IsSettled; } }
        int menuMusicHero = -1;
        string DesiredMusicTrack
        {
            get
            {
                if (mimicratorPreview) return "mimicrator";
                if (HasLiveFight && combat != null && combat.Enemy == EnemyKind.Mimicrator &&
                    (combat.Phase == CombatPhase.Player || combat.Phase == CombatPhase.Enemy || CombatBusy))
                    return "mimicrator";
                return MusicTracks.Hero(!mapScreen && !checkout && menuMusicHero >= 0 ? menuMusicHero : selected);
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
            music.SetVolume(audioMix.Gain);
            music.SetTrack(Visible ? DesiredMusicTrack : "", pause);
        }
        void AdvanceAudio(float seconds)
        {
            cardSounds.Advance(seconds);
            bool foreground = cardSounds.ActiveVoices > 0 || (attackTimeline.IsRunning &&
                (attackTimeline.Kind == AttackAnimation.DogHowl || attackTimeline.Kind == AttackAnimation.TohaRam ||
                 attackTimeline.Kind == AttackAnimation.TohaDarkRam));
            audioMix.Advance(seconds, foreground);
        }
        void StartCardSound(bool attack)
        {
            if (!Visible || !ContainsFocus || GameplayPaused) return;
            cardSounds.Play(selected, attack, false);
        }
    }
}
