using UnityEngine;
using UnityEngine.UI;

namespace Dev.CSU._02_Scripts.MainMenu
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class MainMenuQuitButton : MonoBehaviour
    {
        [SerializeField] private Button quitButton;

        private bool _quitRequested;

        private void Awake()
        {
            ResolveButton();
        }

        private void OnEnable()
        {
            ResolveButton();
            quitButton.onClick.RemoveListener(Quit);
            quitButton.onClick.AddListener(Quit);
        }

        private void OnDisable()
        {
            if (quitButton != null)
            {
                quitButton.onClick.RemoveListener(Quit);
            }
        }

        public void Quit()
        {
            if (_quitRequested)
            {
                return;
            }

            _quitRequested = true;
            quitButton.interactable = false;

#if UNITY_EDITOR
            Debug.Log("Application.Quit() was requested by the MainMenu ExitButton.", this);
            _quitRequested = false;
            quitButton.interactable = true;
#else
            Application.Quit();
#endif
        }

        private void ResolveButton()
        {
            if (quitButton == null)
            {
                quitButton = GetComponent<Button>();
            }
        }

        private void Reset()
        {
            quitButton = GetComponent<Button>();
        }

        private void OnValidate()
        {
            ResolveButton();
        }
    }
}
