using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace ScriptableObjects
{
    [CreateAssetMenu(fileName = "CardData", menuName = "Cards/Card Data", order = 0)]
    public class CardData : ScriptableObject
    {
        [FormerlySerializedAs("cardName")] public string cardCode;
        public string description;
        public List<Sprite> artworks;
        public Sprite Artwork => artworks[Random.Range(0, artworks.Count)];
    }
}