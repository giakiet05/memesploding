using Memesploding.Shared.Enums;

namespace Memesploding.Game.Infrastructure.Runtime;

using Memesploding.Game.Domain.MatchRuntime;

public class DeckComposer : IDeckComposer
{
    private static readonly Guid OriginalSetId = Guid.Parse("550e8400-e29b-41d4-a716-446655440001");
    private static readonly Guid ImplodingSetId = Guid.Parse("550e8400-e29b-41d4-a716-446655440002");
    private static readonly Guid BarkingSetId = Guid.Parse("550e8400-e29b-41d4-a716-446655440003");
    private static readonly Guid StreakingSetId = Guid.Parse("550e8400-e29b-41d4-a716-446655440004");

    private readonly Random _random = new();

    public void InitializeHandsAndDeck(MatchRuntimeState state, IReadOnlyCollection<Guid> cardSetIds)
    {
        var selectedSetIds = cardSetIds.Count == 0 ? [OriginalSetId] : cardSetIds.ToList();
        var pool = BuildCardPool(selectedSetIds, state.Players.Count);

        // Deal 7 cards and 1 defuse to each player.
        foreach (var idx in Enumerable.Range(0, state.Players.Count))
        {
            var hand = new List<string>();
            for (var i = 0; i < 7; i++)
            {
                hand.Add(DrawRandom(pool));
            }

            hand.Add(CardCode.Defuse.ToString());
            state.Players[idx] = state.Players[idx] with { Hand = hand };
        }

        // Add bombs to draw pile after dealing.
        for (var i = 0; i < Math.Max(1, state.Players.Count - 1); i++)
        {
            pool.Add(CardCode.ExplodingKitten.ToString());
        }

        if (selectedSetIds.Contains(ImplodingSetId))
        {
            pool.Add(CardCode.ImplodingKitten.ToString());
        }

        // Add remaining defuses in draw pile.
        for (var i = 0; i < GetExtraDefuseCount(state.Players.Count); i++)
        {
            pool.Add(CardCode.Defuse.ToString());
        }

        Shuffle(pool);
        state.DrawPile = pool;
        state.DiscardPile = [];
    }

    private static List<string> BuildCardPool(IReadOnlyCollection<Guid> setIds, int playerCount)
    {
        var cards = new List<string>();

        if (setIds.Contains(OriginalSetId))
        {
            AddOriginalSet(cards, GetOriginalSetExtraCopies(playerCount));
        }

        if (setIds.Contains(ImplodingSetId))
        {
            Add(cards, CardCode.AlterTheFuture, 4);
            Add(cards, CardCode.TargetedAttack, 3);
            Add(cards, CardCode.DrawFromBottom, 3);
            Add(cards, CardCode.Reverse, 4);
            Add(cards, CardCode.FeralCat, 4);
        }

        if (setIds.Contains(BarkingSetId))
        {
            Add(cards, CardCode.BarkingKitten, 2);
            Add(cards, CardCode.AlterTheFutureNow, 3);
            Add(cards, CardCode.Bury, 3);
            Add(cards, CardCode.PersonalAttack, 3);
            Add(cards, CardCode.IllTakeThat, 3);
            Add(cards, CardCode.TowerMask, 2);
        }

        if (setIds.Contains(StreakingSetId))
        {
            Add(cards, CardCode.StreakingKitten, 1);
            Add(cards, CardCode.SuperSkip, 3);
            Add(cards, CardCode.AlterTheFuture5, 2);
            Add(cards, CardCode.SwapTopAndBottom, 2);
            Add(cards, CardCode.GarbageCollection, 2);
            Add(cards, CardCode.CatomicBomb, 1);
            Add(cards, CardCode.Mark, 3);
            Add(cards, CardCode.CurseOfTheCatButt, 2);
        }

        if (cards.Count == 0)
        {
            AddOriginalSet(cards, GetOriginalSetExtraCopies(playerCount));
        }

        return cards;
    }

    private static int GetOriginalSetExtraCopies(int playerCount)
    {
        return Math.Clamp(playerCount - 2, 0, 4);
    }

    private static int GetExtraDefuseCount(int playerCount)
    {
        return playerCount >= 5 ? 3 : 2;
    }

    private static void AddOriginalSet(List<string> cards, int extraCopies)
    {
        Add(cards, CardCode.Attack, 4 + extraCopies);
        Add(cards, CardCode.Skip, 4 + extraCopies);
        Add(cards, CardCode.Favor, 4 + extraCopies);
        Add(cards, CardCode.Shuffle, 4 + extraCopies);
        Add(cards, CardCode.SeeTheFuture, 5 + extraCopies);
        Add(cards, CardCode.Nope, 5 + extraCopies);
        Add(cards, CardCode.Cat1, 4 + extraCopies);
        Add(cards, CardCode.Cat2, 4 + extraCopies);
        Add(cards, CardCode.Cat3, 4 + extraCopies);
        Add(cards, CardCode.Cat4, 4 + extraCopies);
        Add(cards, CardCode.Cat5, 4 + extraCopies);
    }

    private static void Add(List<string> cards, CardCode code, int count)
    {
        for (var i = 0; i < count; i++)
        {
            cards.Add(code.ToString());
        }
    }

    private string DrawRandom(List<string> cards)
    {
        if (cards.Count == 0)
        {
            return CardCode.Cat1.ToString();
        }

        var index = _random.Next(cards.Count);
        var card = cards[index];
        cards.RemoveAt(index);
        return card;
    }

    private void Shuffle(List<string> cards)
    {
        for (var i = cards.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }
    }
}
