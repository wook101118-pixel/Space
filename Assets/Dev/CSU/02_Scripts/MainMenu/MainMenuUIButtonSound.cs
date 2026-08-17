using System.Collections;
using Dev.NKY.Scripts;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Dev.CSU._02_Scripts.MainMenu
{
    [DefaultExecutionOrder(-1000)]
    [RequireComponent(typeof(Button))]
    [DisallowMultipleComponent]
    public sealed class MainMenuUIButtonSound :
        MonoBehaviour,
        IPointerEnterHandler,
        IPointerDownHandler,
        IPointerUpHandler
    {
        private const string InGameSceneName = "InGame";
        private const string EndingSceneName = "Ending";
        private const string BackdropButtonName = "Backdrop";
        private const string HoverClipPath =
            "SFX/RocketShooting/UIHover";
        private const string ClickClipPath =
            "SFX/RocketShooting/UIClick";
        private const float HoverVolume = 0.35f;
        private const float ClickVolume = 0.55f;

        private static AudioClip _hoverClip;
        private static AudioClip _clickClip;
        private static bool _clipsLoaded;
        private static bool _hoverClipWarningLogged;
        private static bool _clickClipWarningLogged;
        private static bool _soundManagerWarningLogged;

        private Button _button;
        private int _lastClickFrame = -1;
        private bool _pointerClickSoundPlayed;
        private int _pressedPointerId = int.MinValue;
        private Coroutine _pointerResetRoutine;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _hoverClip = null;
            _clickClip = null;
            _clipsLoaded = false;
            _hoverClipWarningLogged = false;
            _clickClipWarningLogged = false;
            _soundManagerWarningLogged = false;
        }

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
            InstallIntoScene(SceneManager.GetActiveScene());
        }

        private void Awake()
        {
            ResolveButton();
            LoadClips();
            LogMissingClipOnce(
                _hoverClip,
                HoverClipPath,
                ref _hoverClipWarningLogged);
            LogMissingClipOnce(
                _clickClip,
                ClickClipPath,
                ref _clickClipWarningLogged);
        }

        private void OnEnable()
        {
            ResolveButton();
            _lastClickFrame = -1;
            ResetPointerPressState();
            if (_button == null)
            {
                Debug.LogWarning(
                    $"[{nameof(MainMenuUIButtonSound)}] "
                    + $"'{name}' has no Button component.",
                    this);
                return;
            }

            // Keep the component self-contained for buttons that do not have
            // an Inspector listener. MainMenu wires PlayClick as the first
            // persistent listener so its audio starts before each action.
            _button.onClick.RemoveListener(PlayClick);
            _button.onClick.AddListener(PlayClick);
        }

        private void OnDisable()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(PlayClick);
            }

            ResetPointerPressState();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!CanPlayHover())
            {
                return;
            }

            LoadClips();
            PlayClip(
                _hoverClip,
                HoverVolume,
                HoverClipPath,
                ref _hoverClipWarningLogged);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!CanPlayHover()
                || eventData == null
                || eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            StopPointerResetRoutine();
            _pointerClickSoundPlayed = true;
            _pressedPointerId = eventData.pointerId;
            PlayClickClip();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_pointerClickSoundPlayed
                || eventData == null
                || eventData.pointerId != _pressedPointerId)
            {
                return;
            }

            StopPointerResetRoutine();
            _pointerResetRoutine = StartCoroutine(
                ClearPointerPressAfterClickDispatch());
        }

        public void PlayClick()
        {
            if (!isActiveAndEnabled
                || _button == null
                || !_button.IsInteractable())
            {
                return;
            }

            // Pointer input already played on press so scene-changing
            // actions cannot destroy the Button before its click sound.
            // Button.onClick remains the keyboard/Submit fallback.
            if (_pointerClickSoundPlayed)
            {
                return;
            }

            PlayClickClip();
        }

        private void PlayClickClip()
        {
            if (!isActiveAndEnabled
                || _button == null
                || !_button.IsInteractable())
            {
                return;
            }

            // A serialized MainMenu Button can invoke both its persistent
            // listener and this runtime fallback in the same UnityEvent.
            if (_lastClickFrame == Time.frameCount)
            {
                return;
            }

            _lastClickFrame = Time.frameCount;

            // Button.onClick covers keyboard Submit and programmatic clicks;
            // pointer input is already handled on press above.
            LoadClips();
            PlayClip(
                _clickClip,
                ClickVolume,
                ClickClipPath,
                ref _clickClipWarningLogged);
        }

        private IEnumerator ClearPointerPressAfterClickDispatch()
        {
            // PointerUp and Button.onClick are dispatched in the same event
            // cycle. Clear one frame later so that cycle cannot double-play.
            yield return null;
            _pointerResetRoutine = null;
            _pointerClickSoundPlayed = false;
            _pressedPointerId = int.MinValue;
        }

        private void ResetPointerPressState()
        {
            StopPointerResetRoutine();
            _pointerClickSoundPlayed = false;
            _pressedPointerId = int.MinValue;
        }

        private void StopPointerResetRoutine()
        {
            if (_pointerResetRoutine == null)
            {
                return;
            }

            StopCoroutine(_pointerResetRoutine);
            _pointerResetRoutine = null;
        }

        private bool CanPlayHover()
        {
            return isActiveAndEnabled
                && _button != null
                && _button.IsInteractable();
        }

        private void ResolveButton()
        {
            if (_button == null)
            {
                _button = GetComponent<Button>();
            }
        }

        private static void HandleSceneLoaded(
            Scene scene,
            LoadSceneMode loadMode)
        {
            InstallIntoScene(scene);
        }

        private static int InstallIntoScene(Scene scene)
        {
            if (!scene.IsValid()
                || !scene.isLoaded
                || (scene.name != InGameSceneName
                    && scene.name != EndingSceneName))
            {
                return 0;
            }

            int installedCount = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Button[] buttons =
                    root.GetComponentsInChildren<Button>(true);
                foreach (Button button in buttons)
                {
                    if (!ShouldInstall(button))
                    {
                        continue;
                    }

                    button.gameObject.AddComponent<
                        MainMenuUIButtonSound>();
                    installedCount++;
                }
            }

            return installedCount;
        }

        private static bool ShouldInstall(Button button)
        {
            if (button == null
                || string.Equals(
                    button.gameObject.name,
                    BackdropButtonName,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return button.GetComponent<MainMenuUIButtonSound>() == null
                && button.GetComponent<UISoundHandler>() == null;
        }

        private static void LoadClips()
        {
            if (_clipsLoaded)
            {
                return;
            }

            _clipsLoaded = true;
            _hoverClip = Resources.Load<AudioClip>(HoverClipPath);
            _clickClip = Resources.Load<AudioClip>(ClickClipPath);
        }

        private static void PlayClip(
            AudioClip clip,
            float volume,
            string resourcePath,
            ref bool clipWarningLogged)
        {
            if (clip == null)
            {
                LogMissingClipOnce(
                    clip,
                    resourcePath,
                    ref clipWarningLogged);
                return;
            }

            SoundManager.EnsureRuntimeInstance();
            SoundManager soundManager = SoundManager.Instance;
            if (soundManager == null)
            {
                if (!_soundManagerWarningLogged)
                {
                    _soundManagerWarningLogged = true;
                    Debug.LogWarning(
                        $"[{nameof(MainMenuUIButtonSound)}] "
                        + "SoundManager is unavailable; UI sound was not "
                        + "played.");
                }

                return;
            }

            soundManager.PlayUI(clip, volume);
        }

        private static void LogMissingClipOnce(
            AudioClip clip,
            string resourcePath,
            ref bool warningLogged)
        {
            if (clip != null || warningLogged)
            {
                return;
            }

            warningLogged = true;
            Debug.LogWarning(
                $"[{nameof(MainMenuUIButtonSound)}] "
                + "Could not load AudioClip from "
                + $"Resources/{resourcePath}.");
        }
    }
}
