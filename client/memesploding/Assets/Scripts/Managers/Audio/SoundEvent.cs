namespace Managers.Audio
{
    /// <summary>
    /// Every named audio moment in Memesploding.
    /// Add new entries here, then wire them to an AudioCueSO in the SoundMap asset.
    /// </summary>
    public enum SoundEvent
    {
        // ── UI ──────────────────────────────────────────────────────────────────
        UiButtonClick,
        UiModalOpen,
        UiModalClose,
        UiError,
        UiSuccess,
        UiCountdownTick,

        // ── Card Interaction ─────────────────────────────────────────────────────
        CardHover,
        CardSelect,
        CardDeselect,
        CardDraw,
        CardPlay,
        CardCombo,
        CardShuffle,

        // ── Turn ─────────────────────────────────────────────────────────────────
        TurnStartSelf,
        TurnStartOther,
        TurnTimeout,

        // ── Reaction ─────────────────────────────────────────────────────────────
        ReactionWindowOpen,
        NopePlayed,

        // ── Bomb / Defuse ────────────────────────────────────────────────────────
        ExplosionTriggered,
        DefuseUsed,
        BombReinserted,

        // ── Player Status ────────────────────────────────────────────────────────
        PlayerEliminatedSelf,
        PlayerEliminatedOther,
        MatchVictory,
        MatchDefeat,

        // ── Special Card Effects ─────────────────────────────────────────────────
        FavorRequested,
        FavorResolved,
        AttackApplied,
        FuturePeeked,

        // ── BGM ──────────────────────────────────────────────────────────────────
        BgmMainMenu,
        BgmGameplay,
    }
}
