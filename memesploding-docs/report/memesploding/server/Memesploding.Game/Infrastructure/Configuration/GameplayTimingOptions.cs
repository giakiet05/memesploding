namespace Memesploding.Game.Infrastructure.Configuration;

public class GameplayTimingOptions
{
    public const string SectionName = "GameplayTiming";

    public int DefaultTurnSeconds { get; set; } = 15;
    public int NopeWindowSeconds { get; set; } = 5;
    public int DefuseDecisionSeconds { get; set; } = 5;
    public int BombReinsertSeconds { get; set; } = 10;
    public int FavorDecisionSeconds { get; set; } = 10;
    public int ReconnectGraceSeconds { get; set; } = 120;
    public int EffectResolutionDelayMs { get; set; } = 400;
    public int TurnTransitionDelayMs { get; set; } = 2000;
    public int BotActionDelayMs { get; set; } = 2000;
}
