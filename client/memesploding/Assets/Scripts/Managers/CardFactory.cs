using Gameplay.Card;
using ScriptableObjects;
using UnityEngine;

namespace Managers
{
    public class CardFactory : MonoBehaviour
    {
        [SerializeField] private CardDatabase cardDatabase;
        [SerializeField] private DisplayCard displayCardPrefab;
        [SerializeField] private PlayableCard playableCardPrefab;

        public void SetCardDatabase(CardDatabase database)
        {
            cardDatabase = database;
        }

        public PlayableCard CreatePlayable(string cardName, Transform parent)
        {
            var data = cardDatabase.Get(cardName);
            if (data == null) return null;

            var card = Instantiate(playableCardPrefab, parent, false);
            card.Initialize(data);
            return card;
        }

        public DisplayCard CreateDisplay(string cardName, Transform parent)
        {
            var data = cardDatabase.Get(cardName);
            if (data == null) return null;

            var card = Instantiate(displayCardPrefab, parent, false);
            card.Initialize(data);
            return card;
        }
    }
}