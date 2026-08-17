using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Dev.CSU._02_Scripts.RocketShooting
{
    internal static class RocketShootingSoundRuntimeInstaller
    {
        private const string RocketSceneName = "Rocket Shooting";

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneInstaller()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallAfterInitialSceneLoad()
        {
            InstallIntoActiveScene();
        }

        private static void HandleSceneLoaded(
            Scene scene,
            LoadSceneMode loadSceneMode)
        {
            InstallIntoActiveScene();
        }

        private static void InstallIntoActiveScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name != RocketSceneName)
            {
                return;
            }

            RocketShootingDirector director =
                Object.FindFirstObjectByType<RocketShootingDirector>(
                    FindObjectsInactive.Include);
            if (director == null
                || director.TryGetComponent(
                    out RocketShootingButtonSoundInstaller _))
            {
                return;
            }

            director.gameObject.AddComponent<
                RocketShootingButtonSoundInstaller>();
        }
    }

    [DisallowMultipleComponent]
    internal sealed class RocketShootingButtonSoundInstaller :
        MonoBehaviour
    {
        private IEnumerator Start()
        {
            InstallUiSounds();
            yield return null;
            InstallUiSounds();
        }

        private void InstallUiSounds()
        {
            Scene scene = gameObject.scene;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Button[] buttons =
                    root.GetComponentsInChildren<Button>(true);
                foreach (Button button in buttons)
                {
                    if (button.GetComponent<
                            RocketShootingUIButtonSound>()
                        == null)
                    {
                        button.gameObject.AddComponent<
                            RocketShootingUIButtonSound>();
                    }
                }

                InstallControlSounds<Slider>(root);
                InstallControlSounds<Toggle>(root);
                InstallControlSounds<TMP_Dropdown>(root);
            }
        }

        private static void InstallControlSounds<T>(GameObject root)
            where T : Selectable
        {
            T[] controls = root.GetComponentsInChildren<T>(true);
            foreach (T control in controls)
            {
                if (control.GetComponent<
                        RocketShootingUIControlSound>()
                    == null)
                {
                    control.gameObject.AddComponent<
                        RocketShootingUIControlSound>();
                }
            }
        }
    }

    [DisallowMultipleComponent]
    internal sealed class RocketShootingUIControlSound :
        MonoBehaviour,
        IPointerEnterHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        ISubmitHandler
    {
        private Selectable _control;

        private void Awake()
        {
            _control = GetComponent<Selectable>();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (CanPlay())
            {
                RocketShootingSoundPlayer.Play(
                    RocketShootingSoundCue.UIHover);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left
                || !CanPlay()
                || _control is Slider)
            {
                return;
            }

            PlayClick();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left
                || !CanPlay()
                || _control is not Slider)
            {
                return;
            }

            PlayClick();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (CanPlay())
            {
                PlayClick();
            }
        }

        private static void PlayClick()
        {
            RocketShootingSoundPlayer.Play(
                RocketShootingSoundCue.UIClick);
        }

        private bool CanPlay()
        {
            return isActiveAndEnabled
                && _control != null
                && _control.IsInteractable();
        }
    }
}
