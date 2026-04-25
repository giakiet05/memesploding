using Memesploding.Game.Domain.MatchRuntime;

namespace Memesploding.Game.Infrastructure.Runtime;

public interface IDeckComposer
{
    void InitializeHandsAndDeck(MatchRuntimeState state, IReadOnlyCollection<Guid> cardSetIds);
}
