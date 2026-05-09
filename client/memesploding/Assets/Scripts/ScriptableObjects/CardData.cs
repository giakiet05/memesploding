using UnityEngine;
using UnityEngine.Serialization;

namespace ScriptableObjects
{
    [CreateAssetMenu(fileName = "CardData", menuName = "Cards/Card Data", order = 0)]
    public class CardData : ScriptableObject
    {
        [FormerlySerializedAs("cardName")] public string cardCode;
        public string description;
        public Sprite artwork;
    }
}