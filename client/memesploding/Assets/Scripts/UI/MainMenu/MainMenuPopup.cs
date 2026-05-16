using UnityEngine;

namespace UI.MainMenu
{
    public class MainMenuPopup : MonoBehaviour
    {
        public bool IsVisible => gameObject.activeSelf;

        public virtual void Show()
        {
            gameObject.SetActive(true);
        }

        public virtual void Hide()
        {
            gameObject.SetActive(false);
        }

        public virtual void Toggle()
        {
            gameObject.SetActive(!gameObject.activeSelf);
        }
    }
}
