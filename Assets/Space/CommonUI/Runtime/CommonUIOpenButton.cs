using UnityEngine;
using UnityEngine.UI;

namespace SpaceGame.CommonUI
{
    public enum CommonUIWindowTarget
    {
        Settings,
        KeyInfo,
        Tutorial
    }

    [DisallowMultipleComponent]
    public sealed class CommonUIOpenButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private CommonUIRoot commonUIRoot;
        [SerializeField] private CommonUIWindowTarget target;

        public CommonUIWindowTarget Target => target;

        public void Configure(
            Button targetButton,
            CommonUIRoot root,
            CommonUIWindowTarget windowTarget)
        {
            button = targetButton;
            commonUIRoot = root;
            target = windowTarget;
        }

        private void Awake()
        {
            if (target == CommonUIWindowTarget.Tutorial)
            {
                gameObject.SetActive(false);
                return;
            }

            button.onClick.AddListener(Open);
        }

        private void OnDestroy()
        {
            button?.onClick.RemoveListener(Open);
        }

        private void Open()
        {
            switch (target)
            {
                case CommonUIWindowTarget.Settings:
                    commonUIRoot.OpenSettings();
                    break;
                case CommonUIWindowTarget.KeyInfo:
                    commonUIRoot.OpenKeyInfo();
                    break;
                case CommonUIWindowTarget.Tutorial:
                    commonUIRoot.OpenTutorial();
                    break;
            }
        }
    }
}
