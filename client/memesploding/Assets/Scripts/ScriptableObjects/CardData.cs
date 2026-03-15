using UnityEngine;

namespace ScriptableObjects
{
    [CreateAssetMenu(fileName = "CardData", menuName = "Cards/Card Data", order = 0)]
    public class CardData : ScriptableObject
    {
        public string cardName;
        public string description;
        public Sprite artwork;
    }
}