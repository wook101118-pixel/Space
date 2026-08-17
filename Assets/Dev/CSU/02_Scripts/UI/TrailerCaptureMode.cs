using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Dev.CSU._02_Scripts.RocketShooting;
using Dev.CSU._02_Scripts.SpaceShip;
using Dev.NKY.Scripts.Health;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Dev.CSU._02_Scripts.UI
{
    /// <summary>
    /// Opt-in capture helper for clean trailer footage. This component is
    /// never installed unless the player is launched with
    /// --trailer-capture.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class TrailerCaptureMode : MonoBehaviour
    {
        private const string CommandLineFlag = "--trailer-capture";
        private const string DirectInGameFlag = "--trailer-direct-ingame";
        private const string StraightFlightFlag =
            "--trailer-straight-flight";
        private const string CinematicSpeedPrefix = "--trailer-speed=";
        private const string EditingCaptureProductName =
            "Space_Edit_Capture";
        private const string RestartRequestFileName =
            "capture-restart.request";
        private const string InGameSceneName = "InGame";
        private const string RocketShootingSceneName = "Rocket Shooting";
        private const string CommonUiRootName = "CommonUIRoot";
        private const string FuelBarName = "FuelBar";
        private const string DistanceTextName = "DistanceText";
        private const int CaptureWidth = 1920;
        private const int CaptureHeight = 1080;

        private readonly List<CanvasGroupState> _hudStates = new();
        private readonly List<CanvasEnabledState> _rocketUiStates = new();
        private readonly List<CanvasEnabledState> _inGameUiStates = new();
        private bool _hudHidden = true;
        private bool _rocketUiHidden;
        private bool _missingHudWarningLogged;
        private Coroutine _resolutionRoutine;
        private bool _directInGame;
        private bool _directInGameRequested;
        private float _cinematicSpeed;
        private bool _editingCaptureBuild;
        private bool _straightFlight;
        private Coroutine _inGamePreparationRoutine;
        private RocketTurnInput _lockedTurnInput;
        private bool _turnInputWasEnabled;
        private bool _cursorVisibleBeforeCapture;
        private CursorLockMode _cursorLockModeBeforeCapture;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallForCapturePlayer()
        {
            bool editingCaptureBuild = string.Equals(
                Application.productName,
                EditingCaptureProductName,
                StringComparison.Ordinal);
            if (!editingCaptureBuild
                && !HasCommandLineFlag(CommandLineFlag))
            {
                return;
            }

            DamageTask.SetDamageSystemEnabled(false);

            GameObject root = new GameObject(nameof(TrailerCaptureMode));
            root.hideFlags = HideFlags.DontSave;
            TrailerCaptureMode captureMode =
                root.AddComponent<TrailerCaptureMode>();
            captureMode._editingCaptureBuild = editingCaptureBuild;
            captureMode._directInGame = editingCaptureBuild
                || HasCommandLineFlag(DirectInGameFlag);
            captureMode._straightFlight = editingCaptureBuild
                || HasCommandLineFlag(StraightFlightFlag);
            captureMode._cinematicSpeed = editingCaptureBuild
                ? 0f
                : ReadPositiveFloatArgument(CinematicSpeedPrefix);
            DontDestroyOnLoad(root);

            Debug.Log(
                "[TrailerCapture] Capture mode enabled. " +
                "Player damage and death are disabled. " +
                (editingCaptureBuild
                    ? "All InGame screen UI is hidden. "
                    : "The InGame HUD starts hidden; press F10 to toggle it. ") +
                $"EditingBuild={editingCaptureBuild}, " +
                $"DirectInGame={captureMode._directInGame}, " +
                $"StraightFlight={captureMode._straightFlight}, " +
                $"CinematicSpeed={captureMode._cinematicSpeed:F1}.");
        }

        private static bool HasCommandLineFlag(string expectedFlag)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            foreach (string argument in arguments)
            {
                if (string.Equals(
                    argument,
                    expectedFlag,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static float ReadPositiveFloatArgument(string prefix)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            foreach (string argument in arguments)
            {
                if (!argument.StartsWith(
                        prefix,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string value = argument.Substring(prefix.Length);
                if (float.TryParse(
                        value,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out float parsed))
                {
                    return Mathf.Max(0f, parsed);
                }
            }

            return 0f;
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void Start()
        {
            _cursorVisibleBeforeCapture = Cursor.visible;
            _cursorLockModeBeforeCapture = Cursor.lockState;
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.None;
            RestartCaptureResolution();
            ApplyToActiveScene();
            ApplySceneCaptureOptions(SceneManager.GetActiveScene());
        }

        private void RestartCaptureResolution()
        {
            if (_resolutionRoutine != null)
            {
                StopCoroutine(_resolutionRoutine);
            }

            _resolutionRoutine = StartCoroutine(
                ApplyCaptureResolution());
        }

        private IEnumerator ApplyCaptureResolution()
        {
            const int applicationPasses = 3;
            const float delayBetweenPasses = 0.5f;

            for (int pass = 0; pass < applicationPasses; pass++)
            {
                Screen.SetResolution(
                    CaptureWidth,
                    CaptureHeight,
                    FullScreenMode.Windowed,
                    new RefreshRate
                    {
                        numerator = 60,
                        denominator = 1
                    });
                yield return new WaitForSecondsRealtime(
                    delayBetweenPasses);
            }

            Debug.Log(
                $"[TrailerCapture] Capture resolution set to " +
                $"{CaptureWidth}x{CaptureHeight} windowed.");
            _resolutionRoutine = null;
        }

        private void Update()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name == RocketShootingSceneName)
            {
                UpdateRocketShootingUi(scene);
                return;
            }

            if (scene.name != InGameSceneName)
            {
                return;
            }

            if (_editingCaptureBuild)
            {
                if (TryConsumeEditingRestartRequest())
                {
                    return;
                }

                HideAllInGameUi(scene);
                ApplyStraightFlightLock();
                Cursor.visible = false;
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f10Key.wasPressedThisFrame)
            {
                SetHudHidden(!_hudHidden);
            }
        }

        private static bool TryConsumeEditingRestartRequest()
        {
            string requestPath = Path.GetFullPath(
                Path.Combine(
                    Application.dataPath,
                    "..",
                    RestartRequestFileName));
            if (!File.Exists(requestPath))
            {
                return false;
            }

            try
            {
                File.Delete(requestPath);
                Debug.Log(
                    "[TrailerCapture] Restarting InGame for a fresh " +
                    "capture take.");
                SceneManager.LoadScene(InGameSceneName);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[TrailerCapture] Could not consume the capture " +
                    $"restart request: {exception.Message}");
                return false;
            }
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            if (_inGamePreparationRoutine != null)
            {
                StopCoroutine(_inGamePreparationRoutine);
                _inGamePreparationRoutine = null;
            }

            RestoreHud();
            RestoreRocketShootingUi();
            RestoreInGameUi();
            RestoreStraightFlight();
            DamageTask.SetDamageSystemEnabled(true);
            Cursor.visible = _cursorVisibleBeforeCapture;
            Cursor.lockState = _cursorLockModeBeforeCapture;
        }

        private void HandleSceneLoaded(
            Scene scene,
            LoadSceneMode loadSceneMode)
        {
            RestoreHud();
            RestoreRocketShootingUi();
            RestoreInGameUi();
            RestoreStraightFlight();
            _hudStates.Clear();
            _rocketUiStates.Clear();
            _inGameUiStates.Clear();
            _rocketUiHidden = false;
            _missingHudWarningLogged = false;
            RestartCaptureResolution();

            if (scene.name == InGameSceneName
                && _editingCaptureBuild)
            {
                HideAllInGameUi(scene);
            }
            else if (scene.name == InGameSceneName && _hudHidden)
            {
                HideHud(scene);
            }

            ApplySceneCaptureOptions(scene);
        }

        private void ApplySceneCaptureOptions(Scene scene)
        {
            if (scene.name == "MainMenu"
                && _directInGame
                && !_directInGameRequested)
            {
                _directInGameRequested = true;
                StartCoroutine(LoadDirectInGame());
                return;
            }

            if (scene.name == InGameSceneName
                && !_editingCaptureBuild
                && _cinematicSpeed > 0f)
            {
                StartCoroutine(ApplyCinematicSpeed());
            }

            if (scene.name == InGameSceneName && _editingCaptureBuild)
            {
                if (_inGamePreparationRoutine != null)
                {
                    StopCoroutine(_inGamePreparationRoutine);
                }

                _inGamePreparationRoutine = StartCoroutine(
                    PrepareEditingCaptureScene(scene));
            }
        }

        private IEnumerator PrepareEditingCaptureScene(Scene scene)
        {
            const int applicationPasses = 8;
            const float delayBetweenPasses = 0.25f;

            for (int pass = 0; pass < applicationPasses; pass++)
            {
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    yield break;
                }

                HideAllInGameUi(scene);
                ApplyStraightFlightLock();
                Cursor.visible = false;
                yield return new WaitForSecondsRealtime(
                    delayBetweenPasses);
            }

            RocketMovement movement =
                FindFirstObjectByType<RocketMovement>(
                    FindObjectsInactive.Include);
            Debug.Log(
                "[TrailerCapture] Editing capture InGame ready. " +
                "All screen UI hidden, steering input disabled, " +
                "damage disabled, original stats preserved, " +
                $"Speed={movement?.Speed ?? 0f:F2}, " +
                $"TimeScale={Time.timeScale:F2}.");
            _inGamePreparationRoutine = null;
        }

        private static IEnumerator LoadDirectInGame()
        {
            yield return new WaitForSecondsRealtime(0.35f);
            SceneManager.LoadScene(InGameSceneName);
        }

        private IEnumerator ApplyCinematicSpeed()
        {
            const int applicationPasses = 4;
            const float delayBetweenPasses = 0.25f;

            for (int pass = 0; pass < applicationPasses; pass++)
            {
                RocketMovement movement =
                    FindFirstObjectByType<RocketMovement>(
                        FindObjectsInactive.Include);
                if (movement != null)
                {
                    movement.ChangeSpeed(_cinematicSpeed);
                }

                yield return new WaitForSecondsRealtime(
                    delayBetweenPasses);
            }

            Debug.Log(
                $"[TrailerCapture] InGame cinematic speed set to " +
                $"{_cinematicSpeed:F1}.");
        }

        private void UpdateRocketShootingUi(Scene scene)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f10Key.wasPressedThisFrame)
            {
                SetRocketShootingUiHidden(!_rocketUiHidden, scene);
                return;
            }

            if (_rocketUiHidden)
            {
                return;
            }

            RocketShootingDirector director =
                FindFirstObjectByType<RocketShootingDirector>(
                    FindObjectsInactive.Include);
            if (director != null && director.Phase != LaunchPhase.Idle)
            {
                SetRocketShootingUiHidden(true, scene);
            }
        }

        private void SetRocketShootingUiHidden(bool hidden, Scene scene)
        {
            _rocketUiHidden = hidden;
            if (hidden)
            {
                HideRocketShootingUi(scene);
            }
            else
            {
                RestoreRocketShootingUi();
            }

            Debug.Log(
                $"[TrailerCapture] Rocket Shooting UI " +
                $"{(hidden ? "hidden" : "shown")}.");
        }

        private void HideRocketShootingUi(Scene scene)
        {
            if (_rocketUiStates.Count == 0)
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    Canvas[] canvases =
                        root.GetComponentsInChildren<Canvas>(true);
                    foreach (Canvas canvas in canvases)
                    {
                        if (!canvas.isRootCanvas
                            || canvas.renderMode == RenderMode.WorldSpace)
                        {
                            continue;
                        }

                        _rocketUiStates.Add(
                            new CanvasEnabledState(
                                canvas,
                                canvas.enabled));
                    }
                }
            }

            foreach (CanvasEnabledState state in _rocketUiStates)
            {
                if (state.Canvas != null)
                {
                    state.Canvas.enabled = false;
                }
            }
        }

        private void RestoreRocketShootingUi()
        {
            foreach (CanvasEnabledState state in _rocketUiStates)
            {
                if (state.Canvas != null)
                {
                    state.Canvas.enabled = state.Enabled;
                }
            }
        }

        private void HideAllInGameUi(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Canvas[] canvases =
                    root.GetComponentsInChildren<Canvas>(true);
                foreach (Canvas canvas in canvases)
                {
                    if (canvas.renderMode == RenderMode.WorldSpace
                        || ContainsCanvas(_inGameUiStates, canvas))
                    {
                        continue;
                    }

                    _inGameUiStates.Add(
                        new CanvasEnabledState(
                            canvas,
                            canvas.enabled));
                }
            }

            foreach (CanvasEnabledState state in _inGameUiStates)
            {
                if (state.Canvas != null)
                {
                    state.Canvas.enabled = false;
                }
            }
        }

        private void RestoreInGameUi()
        {
            foreach (CanvasEnabledState state in _inGameUiStates)
            {
                if (state.Canvas != null)
                {
                    state.Canvas.enabled = state.Enabled;
                }
            }
        }

        private static bool ContainsCanvas(
            List<CanvasEnabledState> states,
            Canvas candidate)
        {
            foreach (CanvasEnabledState state in states)
            {
                if (state.Canvas == candidate)
                {
                    return true;
                }
            }

            return false;
        }

        private void ApplyStraightFlightLock()
        {
            if (!_straightFlight)
            {
                return;
            }

            if (_lockedTurnInput == null)
            {
                _lockedTurnInput =
                    FindFirstObjectByType<RocketTurnInput>(
                        FindObjectsInactive.Include);
                if (_lockedTurnInput != null)
                {
                    _turnInputWasEnabled = _lockedTurnInput.enabled;
                    _lockedTurnInput.enabled = false;
                }
            }

        }

        private void RestoreStraightFlight()
        {
            if (_lockedTurnInput != null)
            {
                _lockedTurnInput.enabled = _turnInputWasEnabled;
            }

            _lockedTurnInput = null;
        }

        private void ApplyToActiveScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name == InGameSceneName && _hudHidden)
            {
                HideHud(scene);
            }
        }

        private void SetHudHidden(bool hidden)
        {
            _hudHidden = hidden;
            if (hidden)
            {
                HideHud(SceneManager.GetActiveScene());
            }
            else
            {
                RestoreHud();
            }

            Debug.Log(
                $"[TrailerCapture] InGame HUD " +
                $"{(hidden ? "hidden" : "shown")}.");
        }

        private void HideHud(Scene scene)
        {
            if (_hudStates.Count > 0)
            {
                ApplyHiddenValues();
                return;
            }

            Canvas gameplayCanvas = FindGameplayCanvas(scene);
            if (gameplayCanvas == null)
            {
                WarnMissingHudOnce("gameplay Canvas");
                return;
            }

            AddHudTarget(gameplayCanvas.transform.Find(FuelBarName));
            AddHudTarget(gameplayCanvas.transform.Find(DistanceTextName));

            if (_hudStates.Count != 2)
            {
                WarnMissingHudOnce(
                    $"{FuelBarName} or {DistanceTextName}");
            }

            ApplyHiddenValues();
        }

        private void AddHudTarget(Transform target)
        {
            if (target == null)
            {
                return;
            }

            CanvasGroup canvasGroup =
                target.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = target.gameObject.AddComponent<CanvasGroup>();
            }

            _hudStates.Add(new CanvasGroupState(
                canvasGroup,
                canvasGroup.alpha,
                canvasGroup.interactable,
                canvasGroup.blocksRaycasts));
        }

        private void ApplyHiddenValues()
        {
            foreach (CanvasGroupState state in _hudStates)
            {
                if (state.CanvasGroup == null)
                {
                    continue;
                }

                state.CanvasGroup.alpha = 0f;
                state.CanvasGroup.interactable = false;
                state.CanvasGroup.blocksRaycasts = false;
            }
        }

        private void RestoreHud()
        {
            foreach (CanvasGroupState state in _hudStates)
            {
                if (state.CanvasGroup == null)
                {
                    continue;
                }

                state.CanvasGroup.alpha = state.Alpha;
                state.CanvasGroup.interactable = state.Interactable;
                state.CanvasGroup.blocksRaycasts = state.BlocksRaycasts;
            }
        }

        private static Canvas FindGameplayCanvas(Scene scene)
        {
            Canvas fallback = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Canvas[] canvases =
                    root.GetComponentsInChildren<Canvas>(true);
                foreach (Canvas canvas in canvases)
                {
                    if (!canvas.isRootCanvas
                        || canvas.renderMode == RenderMode.WorldSpace)
                    {
                        continue;
                    }

                    if (canvas.transform.Find(CommonUiRootName) != null)
                    {
                        return canvas;
                    }

                    fallback ??= canvas;
                }
            }

            return fallback;
        }

        private void WarnMissingHudOnce(string missingTarget)
        {
            if (_missingHudWarningLogged)
            {
                return;
            }

            _missingHudWarningLogged = true;
            Debug.LogWarning(
                $"[TrailerCapture] Could not hide {missingTarget} " +
                "in the InGame scene.",
                this);
        }

        private readonly struct CanvasGroupState
        {
            public CanvasGroupState(
                CanvasGroup canvasGroup,
                float alpha,
                bool interactable,
                bool blocksRaycasts)
            {
                CanvasGroup = canvasGroup;
                Alpha = alpha;
                Interactable = interactable;
                BlocksRaycasts = blocksRaycasts;
            }

            public CanvasGroup CanvasGroup { get; }
            public float Alpha { get; }
            public bool Interactable { get; }
            public bool BlocksRaycasts { get; }
        }

        private readonly struct CanvasEnabledState
        {
            public CanvasEnabledState(Canvas canvas, bool enabled)
            {
                Canvas = canvas;
                Enabled = enabled;
            }

            public Canvas Canvas { get; }
            public bool Enabled { get; }
        }
    }
}
