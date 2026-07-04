namespace Memesploding.Game.Domain.StateMachine;

public static class MatchTransitionRules
{
    public static bool CanTransition(MatchPhase from, MatchPhase to)
    {
        return (from, to) switch
        {
            (MatchPhase.WaitingStart, MatchPhase.Dealing) => true,
            (MatchPhase.WaitingStart, MatchPhase.Playing) => true,
            (MatchPhase.Dealing, MatchPhase.Playing) => true,
            (MatchPhase.Playing, MatchPhase.Finished) => true,
            (MatchPhase.Finished, MatchPhase.Archived) => true,
            _ => false
        };
    }
}
