using Memesploding.Game.Domain.StateMachine;

namespace Memesploding.Game.Domain.MatchRuntime;

public class MatchRuntimeState
{
    public Guid MatchId { get; set; }
    public string RoomCode { get; set; } = string.Empty;
    public MatchPhase Phase { get; set; } = MatchPhase.WaitingStart;
    public long StateVersion { get; set; } = 0;
    public int TurnTimerSeconds { get; set; } = 15;
    public int NopeWindowSeconds { get; set; } = 3;
    public int DefuseDecisionSeconds { get; set; } = 5;
    public int BombReinsertSeconds { get; set; } = 10;
    public int EffectResolutionDelayMs { get; set; } = 400;
    public int ReconnectGraceSeconds { get; set; } = 120;
    public int TurnDirection { get; set; } = 1;
    public int TurnIndex { get; set; } = 0;
    public int TurnCounter { get; set; } = 0;
    public DateTime? TurnEndsAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public List<string> DrawPile { get; set; } = [];
    public List<string> DiscardPile { get; set; } = [];
    public List<MatchRuntimeEvent> EventLog { get; set; } = [];
    public string? PendingReactionAction { get; set; }
    public Guid? PendingReactionUserId { get; set; }
    public string? PendingReactionPayload { get; set; }
    public int PendingNopeCount { get; set; }
    public DateTime? ReactionWindowEndsAt { get; set; }
    public DateTime? ReactionResolveAt { get; set; }
    public Guid? PendingDefuseUserId { get; set; }
    public DateTime? DefuseWindowEndsAt { get; set; }
    public DateTime? BombReinsertWindowEndsAt { get; set; }
    public Guid? PendingBombOwnerUserId { get; set; }
    public string? PendingBombCardCode { get; set; }
    public bool ImplodingKittenFaceUpInDeck { get; set; }
    public bool MatchEndedPublished { get; set; }
    public Guid? WinnerUserId { get; set; }
    public List<Guid> EliminationOrder { get; set; } = [];
    public List<MatchRuntimePlayerState> Players { get; set; } = [];
}
