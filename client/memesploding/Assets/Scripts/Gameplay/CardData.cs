using UnityEngine;

namespace Gameplay
{
    [CreateAssetMenu(fileName = "CardData", menuName = "Gameplay/Card Data", order = 0)]
    public class CardData : ScriptableObject
    {
        public string cardName;
        public string description;
        public Sprite artwork;
    }
}