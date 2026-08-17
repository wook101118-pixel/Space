using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dev.CSU._02_Scripts.UI
{
    /// <summary>
    /// Keeps the normal game and all of its UI intact while forcing a
    /// deterministic 16:9 capture surface for the dedicated UI capture build.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class UiCaptureResolutionMode : MonoBehaviour
    {
        private const string UiCaptureProductName = "Space_UI_Capture";
        private const string RocketShootingSceneName = "Rocket Shooting";
        private const string PrimaryUiCanvasName = "Canvas";
        private const string SecondaryUiCanvasName = "Canvas (1)";
        private const int CaptureWidth = 1920;
        private const int CaptureHeight = 1080;

        private Coroutine _resolutionRoutine;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallForUiCapturePlayer()
        {
            if (!string.Equals(
                    Application.productName,
                    UiCaptureProductName,
                    StringComparison.Ordinal))
            {
                return;
            }

            GameObject root = new GameObject(
                nameof(UiCaptureResolutionMode));
            root.hideFlags = HideFlags.DontSave;
            root.AddComponent<UiCaptureResolutionMode>();
            DontDestroyOnLoad(root);

            Debug.Log(
                "[UiCapture] Full-UI capture mode enabled. " +
                "Gameplay, cursor, controls, and every screen UI remain " +
                "unchanged.");
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void Start()
        {
            EnsureRocketShootingUiVisible(
                SceneManager.GetActiveScene());
            RestartCaptureResolution();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            if (_resolutionRoutine != null)
            {
                StopCoroutine(_resolutionRoutine);
                _resolutionRoutine = null;
            }
        }

        private void HandleSceneLoaded(
            Scene scene,
            LoadSceneMode loadSceneMode)
        {
            EnsureRocketShootingUiVisible(scene);
            RestartCaptureResolution();
        }

        private static void EnsureRocketShootingUiVisible(Scene scene)
        {
            if (scene.name != RocketShootingSceneName)
            {
                return;
            }

            int activatedCanvasCount = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != PrimaryUiCanvasName
                    && root.name != SecondaryUiCanvasName)
                {
                    continue;
                }

                if (!root.activeSelf)
                {
                    root.SetActive(true);
                    activatedCanvasCount++;
                }
            }

            Debug.Log(
                $"[UiCapture] Rocket Shooting full UI ready; " +
                $"activated {activatedCanvasCount} root canvas object(s).");
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
                $"[UiCapture] Capture resolution set to " +
                $"{CaptureWidth}x{CaptureHeight} windowed; UI unchanged.");
            _resolutionRoutine = null;
        }
    }
}
