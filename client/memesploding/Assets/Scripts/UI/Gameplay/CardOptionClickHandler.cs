using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UI.Gameplay
{
    public sealed class CardOptionClickHandler : MonoBehaviour, IPointerClickHandler
    {
        public Action OnClicked;

        public void OnPointerClick(PointerEventData eventData)
        {
            OnClicked?.Invoke();
        }
    }
}
