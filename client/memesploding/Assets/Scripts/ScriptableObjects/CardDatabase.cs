using System.Collections.Generic;
using UnityEngine;

namespace ScriptableObjects
{
    [CreateAssetMenu(menuName = "Cards/Card Database")]
    public class CardDatabase : ScriptableObject
    {
        [SerializeField] private List<CardData> cards;

        private Dictionary<string, CardData> _lookup;

        public void Initialize()
        {
            _lookup = new Dictionary<string, CardData>();

            foreach (var card in cards)
                _lookup[card.cardName] = card;
        }

        public CardData Get(string cardName)
        {
            if (_lookup == null)
                Initialize();

            _lookup.TryGetValue(cardName, out var data);
            return data;
        }
    }
}