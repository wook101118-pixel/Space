using System;
using System.Linq;
using System.Threading.Tasks;
using SpaceGame.CommonUI;
using SpaceGame.CommonUI.Views;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Dev.CSU._02_Scripts.CommonUI.Editor
{
    internal static class SettingsWindowSlidePlayModeVerifier
    {
        private const string MenuPath =
            "Tools/CSU/Common UI/Run Settings Slide Verification";
        private const float PositionTolerance = 1.5f;
        private const float AnimationTimeout = 1.5f;

        private static bool _isRunning;

        [MenuItem(MenuPath, false, 220)]
        private static async void Run()
        {
            if (_isRunning)
            {
                Debug.LogWarning(
                    "[Settings Slide Play Mode] Verification is already "
                    + "running.");
                return;
            }

            if (!EditorApplication.isPlaying)
            {
                Debug.LogError(
                    "[Settings Slide Play Mode] Enter Play Mode first.");
                return;
            }

            _isRunning = true;
            CommonUIRoot root = Object.FindFirstObjectByType<CommonUIRoot>(
                FindObjectsInactive.Include);
            SettingsWindow window = root != null
                ? root.SettingsWindow
                : null;
            SettingsWindowSlideAnimation slide = window != null
                ? window.GetComponent<SettingsWindowSlideAnimation>()
                : null;
            IDisposable externalPauseLease = null;
            Action onOpen = null;
            Action onClose = null;
            float initialTimeScale = Time.timeScale;
            int initialPauseRequestCount = root != null
                && root.Context != null
                ? root.Context.PauseService.RequestCount
                : 0;

            try
            {
                Ensure(root != null, "CommonUIRoot was not found.");
                Ensure(root.Context != null,
                    "CommonUIRoot has not initialized its context.");
                Ensure(window != null, "SettingsWindow was not found.");
                Ensure(slide != null,
                    "SettingsWindowSlideAnimation was not found.");

                SettingsWindowSlideAnimation[] slides =
                    Object.FindObjectsByType<SettingsWindowSlideAnimation>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None);
                Ensure(slides.Length == 1,
                    $"Expected one slide component, found {slides.Length}.");
                Ensure(slide.SettingsWindow == window,
                    "The slide SettingsWindow reference is invalid.");
                Ensure(slide.TargetRectTransform != null,
                    "The slide target RectTransform is missing.");
                Ensure(slide.TargetRectTransform.name == "Panel",
                    "The slide target must be SettingsWindow/Panel.");
                Ensure(
                    Mathf.Approximately(slide.RightStartDistance, 800f)
                    && Mathf.Approximately(slide.MoveDuration, 0.24f)
                    && slide.MoveEase == DG.Tweening.Ease.OutCubic
                    && Mathf.Approximately(slide.RightExitDistance, 800f)
                    && Mathf.Approximately(slide.ExitDuration, 0.24f)
                    && slide.ExitEase == DG.Tweening.Ease.InCubic,
                    "The shared slide Inspector defaults are invalid.");
                Ensure(!window.IsOpen,
                    "SettingsWindow must be closed before verification.");

                await WaitForPlayerFrames(2);

                RectTransform target = slide.TargetRectTransform;
                CanvasGroup canvasGroup = window.GetComponent<CanvasGroup>();
                Ensure(canvasGroup != null,
                    "SettingsWindow CanvasGroup was not found.");
                Vector2 home = slide.OriginalPosition;
                Vector2 openStart = home
                    + Vector2.right * slide.RightStartDistance;
                Vector2 closeEnd = home
                    + Vector2.right * slide.RightExitDistance;
                Ensure(IsNear(target.anchoredPosition, home),
                    "Panel did not start at its saved original position.");

                int openEventCount = 0;
                int closeEventCount = 0;
                onOpen = () => openEventCount++;
                onClose = () => closeEventCount++;
                window.OpenTransitionStarted += onOpen;
                window.CloseTransitionStarted += onClose;

                externalPauseLease = root.Context.PauseService.Acquire(slide);
                Ensure(Time.timeScale == 0f,
                    "The external pause lease did not set timeScale to zero.");

                window.Open();
                Ensure(window.IsOpen && openEventCount == 1,
                    "Open should start exactly one transition.");
                Ensure(IsNear(target.anchoredPosition, openStart),
                    "Open did not place Panel at the right start position.");
                Ensure(canvasGroup.alpha < 1f,
                    "Fade-in should start in the same frame as the slide.");

                window.Open();
                Ensure(openEventCount == 1,
                    "Repeated Open created a duplicate transition.");

                await WaitForPlayerFrames(1);
                Ensure(Time.timeScale == 0f,
                    "timeScale changed while the unscaled slide was running.");
                Ensure(target.anchoredPosition.x < openStart.x,
                    "Panel did not move while timeScale was zero.");
                await WaitUntil(
                    () => IsNear(target.anchoredPosition, home)
                        && canvasGroup.alpha >= 0.999f,
                    "Open slide and fade did not finish together.");

                Button cancelButton = FindButton(window, "CancelButton");
                cancelButton.onClick.Invoke();
                cancelButton.onClick.Invoke();
                Ensure(!window.IsOpen && closeEventCount == 1,
                    "Cancel should start one close transition only.");
                await WaitForPlayerFrames(1);
                Ensure(target.anchoredPosition.x > home.x,
                    "Close slide did not move right while timeScale was zero.");
                Ensure(canvasGroup.alpha < 1f,
                    "Fade-out did not start with the close slide.");
                await WaitUntil(
                    () => IsNear(target.anchoredPosition, closeEnd)
                        && canvasGroup.alpha <= 0.001f,
                    "Cancel slide and fade did not finish together.");

                window.Open();
                Ensure(openEventCount == 2,
                    "The second open transition did not start once.");
                await WaitUntil(
                    () => IsNear(target.anchoredPosition, home)
                        && canvasGroup.alpha >= 0.999f,
                    "The second open transition did not finish.");

                cancelButton.onClick.Invoke();
                Ensure(closeEventCount == 2,
                    "The rapid-close transition did not start once.");
                await WaitUntil(
                    () => target.anchoredPosition.x > home.x + 10f
                        && target.anchoredPosition.x < closeEnd.x - 10f,
                    "The close transition had no observable middle state.");
                Vector2 positionBeforeReopen = target.anchoredPosition;
                window.Open();
                Ensure(openEventCount == 3,
                    "Reopening during close did not start once.");
                Ensure(
                    Vector2.Distance(
                        target.anchoredPosition,
                        positionBeforeReopen) <= PositionTolerance,
                    "Reopening during close snapped Panel to a new position.");
                window.Open();
                Ensure(openEventCount == 3,
                    "Rapid repeated Open created a duplicate tween.");
                await WaitUntil(
                    () => IsNear(target.anchoredPosition, home)
                        && canvasGroup.alpha >= 0.999f,
                    "Reversed open transition did not return to home.");

                Button applyButton = FindButton(window, "ApplyButton");
                applyButton.onClick.Invoke();
                Ensure(!window.IsOpen && closeEventCount == 3,
                    "Apply should preserve one normal close transition.");
                await WaitUntil(
                    () => IsNear(target.anchoredPosition, closeEnd)
                        && canvasGroup.alpha <= 0.001f,
                    "Apply slide and fade did not finish together.");
                Ensure(Time.timeScale == 0f,
                    "Unscaled close did not preserve the external pause.");
                Ensure(
                    root.Context.PauseService.RequestCount
                        == initialPauseRequestCount + 1,
                    "SettingsWindow did not release its own pause lease.");

                Debug.Log(
                    $"[Settings Slide Play Mode] PASS ({gameObjectScene()}): "
                    + "single shared component, open/Cancel/Apply, paired "
                    + "fade timing, timeScale=0, duplicate input, and rapid "
                    + "close-to-open reversal all passed.");
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[Settings Slide Play Mode] FAIL: "
                    + exception.Message);
                Debug.LogException(exception);
            }
            finally
            {
                if (window != null)
                {
                    if (onOpen != null)
                    {
                        window.OpenTransitionStarted -= onOpen;
                    }

                    if (onClose != null)
                    {
                        window.CloseTransitionStarted -= onClose;
                    }

                    if (window.IsOpen)
                    {
                        window.RequestClose();
                    }
                }

                externalPauseLease?.Dispose();
                if (root != null && root.Context != null)
                {
                    EnsureCleanup(
                        root.Context.PauseService.RequestCount
                            == initialPauseRequestCount,
                        "Pause request count was not restored.");
                }

                EnsureCleanup(
                    Mathf.Approximately(Time.timeScale, initialTimeScale),
                    "Time.timeScale was not restored.");
                _isRunning = false;
            }

            string gameObjectScene()
            {
                return root != null
                    ? root.gameObject.scene.name
                    : "Unknown Scene";
            }
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateRun()
        {
            return EditorApplication.isPlaying && !_isRunning;
        }

        private static async Task WaitUntil(
            Func<bool> predicate,
            string timeoutMessage)
        {
            float timeoutAt = Time.realtimeSinceStartup + AnimationTimeout;
            while (!predicate()
                   && Time.realtimeSinceStartup < timeoutAt
                   && EditorApplication.isPlaying)
            {
                await Task.Yield();
            }

            Ensure(predicate(), timeoutMessage);
        }

        private static async Task WaitForPlayerFrames(int frameCount)
        {
            int targetFrame = Time.frameCount + Mathf.Max(1, frameCount);
            float timeoutAt = Time.realtimeSinceStartup + AnimationTimeout;
            while (Time.frameCount < targetFrame
                   && Time.realtimeSinceStartup < timeoutAt
                   && EditorApplication.isPlaying)
            {
                await Task.Yield();
            }

            Ensure(Time.frameCount >= targetFrame,
                "Timed out while waiting for a player frame.");
        }

        private static Button FindButton(
            SettingsWindow window,
            string buttonName)
        {
            Button button = window
                .GetComponentsInChildren<Button>(true)
                .FirstOrDefault(candidate => candidate.name == buttonName);
            if (button == null)
            {
                throw new InvalidOperationException(
                    $"Settings button '{buttonName}' was not found.");
            }

            return button;
        }

        private static bool IsNear(Vector2 actual, Vector2 expected)
        {
            return Vector2.Distance(actual, expected) <= PositionTolerance;
        }

        private static void Ensure(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void EnsureCleanup(bool condition, string message)
        {
            if (!condition)
            {
                Debug.LogError(
                    "[Settings Slide Play Mode] Cleanup failed: " + message);
            }
        }
    }
}
