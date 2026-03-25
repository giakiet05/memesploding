using System;
using ScriptableObjects;
using UnityEngine;
using UnityEngine.UI;

namespace Card
{
    public class BaseCard : MonoBehaviour
    {
        public string Id { get; private set; }
        public CardData Data { get; set; }
        public RectTransform RectTransform { get; private set; }
        [SerializeField] protected Image cardImage;

        public Color normalColor = Color.white;
        public Color inactiveColor = new Color(0.6f, 0.6f, 0.6f, 1f);

        protected virtual void Awake()
        {
            RectTransform = GetComponent<RectTransform>();
        }

        public virtual void Initialize(CardData data)
        {
            Data = data;
            GenerateId();
            UpdateVisuals();
        }

        public void GenerateId()
        {
            Id = Guid.NewGuid().ToString();
        }

        protected virtual void UpdateVisuals()
        {
            if (Data == null)
                return;

            if (Data.artwork == null)
                return;

            cardImage.sprite = Data.artwork;
        }

        public void SetNewest(bool isNewest)
        {
            if (cardImage == null)
                return;

            cardImage.color = isNewest ? normalColor : inactiveColor;
        }
    }
}
