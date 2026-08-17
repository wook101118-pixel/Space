using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Dev.CSU._02_Scripts.MainMenu;
using SpaceGame.CommonUI;
using SpaceGame.CommonUI.Modal;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Dev.CSU._02_Scripts.PauseMenu.Editor
{
    internal static class PauseMenuPlayModeVerifier
    {
        private const string MenuPath =
            "Tools/CSU/Pause Menu/Run Play Mode Flow Verification";

        private static bool _isRunning;

        [MenuItem(MenuPath, false, 212)]
        private static async void Run()
        {
            if (_isRunning)
            {
                Debug.LogWarning(
                    "[Pause Menu Play Mode] Verification is already running.");
                return;
            }

            if (!EditorApplication.isPlaying)
            {
                Debug.LogError(
                    "[Pause Menu Play Mode] Enter Play Mode in InGame first.");
                return;
            }

            _isRunning = true;
            PauseMenuWindow window =
                Object.FindFirstObjectByType<PauseMenuWindow>(
                    FindObjectsInactive.Include);
            CommonUIRoot root =
                Object.FindFirstObjectByType<CommonUIRoot>(
                    FindObjectsInactive.Include);
            if (window == null || root == null)
            {
                Debug.LogError(
                    "[Pause Menu Play Mode] PauseMenuWindow or CommonUIRoot "
                    + "was not found.");
                _isRunning = false;
                return;
            }

            float initialTimeScale = Time.timeScale;
            List<ActionMapState> actionMapStates =
                CaptureGameplayActionMapStates(root);
            var quitRecorder = new RecordingQuitService();

            try
            {
                Ensure(!window.IsOpen, "Pause menu should start closed.");
                Ensure(
                    root.Context.PauseService.RequestCount == 0,
                    "Pause lease count should start at zero.");
                Ensure(
                    GetCancelHandlerCount(root.Context.CancelRouter) == 1,
                    "Exactly one permanent cancel handler should be active.");

                // Let the Editor menu click that launched this verifier finish
                // before testing runtime selection and pointer-independent ESC.
                await WaitForUi();
                await WaitForPlayerFrames(2);
                await PressEscape();
                Ensure(window.IsOpen, "ESC should open the pause menu.");
                Ensure(
                    Time.timeScale == 0f,
                    "Opening pause should stop scaled gameplay time.");
                Ensure(
                    root.Context.PauseService.RequestCount == 1,
                    "Pause menu should own exactly one pause lease.");
                EnsureGameplayMapsBlocked(actionMapStates);
                Ensure(
                    GetCancelHandlerCount(root.Context.CancelRouter) == 2,
                    "Open pause should add one high-priority cancel handler.");

                EnsureDefaultSelections(window);
                EnsureUiInputBindings();
                EnsureNavigationGraph(window);
                EnsureSelectionVisualPolicy(window);
                await WaitForCurrentSelection(window, "ContinueButton");
                await EnsurePointerHoverBehavior(
                    window,
                    "ContinueButton");

                FindButton(window, "SettingsButton").onClick.Invoke();
                await WaitForUi();
                Ensure(
                    root.SettingsWindow.IsOpen,
                    "Settings button should open the existing SettingsWindow.");
                Ensure(
                    window.IsOpen && window.IsWaitingForSettings,
                    "Pause menu should remain open behind settings.");
                Ensure(
                    root.Context.PauseService.RequestCount == 2,
                    "Settings should add a nested lease without releasing "
                    + "the pause-menu lease.");
                Ensure(
                    GetCancelHandlerCount(root.Context.CancelRouter) == 3,
                    "Settings should own the top cancel handler.");

                await PressEscape();
                await WaitForUi();
                Ensure(
                    !root.SettingsWindow.IsOpen,
                    "ESC should close settings first.");
                Ensure(
                    window.IsOpen && !window.IsWaitingForSettings,
                    "Closing settings should return to the pause menu.");
                Ensure(
                    root.Context.PauseService.RequestCount == 1,
                    "Closing settings should leave the pause lease active.");
                await WaitForCurrentSelection(window, "SettingsButton");

                FindButton(window, "ExitButton").onClick.Invoke();
                await WaitForUi();
                Ensure(
                    window.IsShowingExitChoices,
                    "Exit should show the two-choice sub-screen.");
                await WaitForCurrentSelection(
                    window,
                    "ReturnToMainMenuButton");

                await PressEscape();
                await WaitForUi();
                Ensure(
                    window.IsOpen && !window.IsShowingExitChoices,
                    "ESC in exit choices should return to the main pause "
                    + "screen instead of resuming.");
                await WaitForCurrentSelection(window, "ExitButton");

                await PressEscape();
                await WaitForUi();
                Ensure(
                    !window.IsOpen,
                    "ESC on the main pause screen should resume.");
                Ensure(
                    root.Context.PauseService.RequestCount == 0,
                    "Resume should release the pause lease exactly once.");
                Ensure(
                    Mathf.Approximately(Time.timeScale, initialTimeScale),
                    "Resume should restore the exact prior time scale.");
                EnsureGameplayMapsRestored(actionMapStates);
                Ensure(
                    GetCancelHandlerCount(root.Context.CancelRouter) == 1,
                    "Closing pause should leave one permanent handler.");

                await PressEscape();
                Ensure(
                    window.IsOpen
                    && root.Context.PauseService.RequestCount == 1,
                    "A second pause cycle should still acquire one lease.");
                await WaitForCurrentSelection(window, "ContinueButton");
                await PressSubmit();
                await WaitForUi();
                Ensure(
                    !window.IsOpen
                    && root.Context.PauseService.RequestCount == 0,
                    "Submit on the selected Continue button should close "
                    + "and release exactly once.");

                await PressEscape();
                FindButton(window, "ExitButton").onClick.Invoke();
                await WaitForUi();
                window.SetQuitServiceForTests(quitRecorder);
                Button quitButton = FindButton(window, "QuitGameButton");
                quitButton.onClick.Invoke();
                quitButton.onClick.Invoke();
                Ensure(
                    quitRecorder.RequestCount == 1,
                    "Repeated quit clicks should produce one quit request.");
                window.RequestClose();
                await WaitForUi();
                window.SetQuitServiceForTests(
                    new ApplicationGameQuitService());
                Ensure(
                    !window.IsOpen
                    && root.Context.PauseService.RequestCount == 0,
                    "Verifier cleanup should leave gameplay resumed.");
                EnsureGameplayMapsRestored(actionMapStates);

                Debug.Log(
                    "[Pause Menu Play Mode] PASS: cancel priority, nested "
                    + "settings, selection-neutral hover visuals, retained "
                    + "Submit, exit back-navigation, exact time-scale and "
                    + "action-map restoration, repeat entry, and idempotent "
                    + "quit request all passed.");
            }
            catch (Exception exception)
            {
                if (root.SettingsWindow.IsOpen)
                {
                    root.SettingsWindow.RequestClose();
                }

                if (window.IsOpen)
                {
                    window.RequestClose();
                }

                Debug.LogError(
                    "[Pause Menu Play Mode] FAIL: " + exception.Message);
                Debug.LogException(exception);
            }
            finally
            {
                _isRunning = false;
            }
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateRun()
        {
            return EditorApplication.isPlaying && !_isRunning;
        }

        [MenuItem(
            "Tools/CSU/Pause Menu/Open For Manual Play Check",
            false,
            213)]
        private static void OpenForManualPlayCheck()
        {
            PauseMenuWindow window =
                Object.FindFirstObjectByType<PauseMenuWindow>(
                    FindObjectsInactive.Include);
            if (!EditorApplication.isPlaying || window == null)
            {
                Debug.LogError(
                    "[Pause Menu Play Mode] Enter InGame Play Mode first.");
                return;
            }

            window.Open();
        }

        [MenuItem(
            "Tools/CSU/Pause Menu/Simulate One Cancel Input",
            false,
            214)]
        private static async void SimulateOneCancelInput()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogError(
                    "[Pause Menu Play Mode] Enter Play Mode first.");
                return;
            }

            await PressEscape();
        }

        private static async Task PressEscape()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                throw new InvalidOperationException(
                    "No keyboard is available to verify Cancel routing.");
            }

            InputSystem.QueueStateEvent(
                keyboard,
                new KeyboardState(Key.Escape));
            await Task.Delay(100);
            InputSystem.QueueStateEvent(
                keyboard,
                new KeyboardState());
            await Task.Delay(180);
        }

        private static async Task PressSubmit()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                throw new InvalidOperationException(
                    "No keyboard is available to verify Submit routing.");
            }

            InputSystem.QueueStateEvent(
                keyboard,
                new KeyboardState(Key.Enter));
            await Task.Delay(100);
            InputSystem.QueueStateEvent(
                keyboard,
                new KeyboardState());
            await Task.Delay(180);
        }

        private static void EnsureNavigationGraph(PauseMenuWindow window)
        {
            Button continueButton = FindButton(window, "ContinueButton");
            Button settingsButton = FindButton(window, "SettingsButton");
            Button exitButton = FindButton(window, "ExitButton");
            Button mainMenuButton =
                FindButton(window, "ReturnToMainMenuButton");
            Button quitButton = FindButton(window, "QuitGameButton");

            EnsureNavigationLink(
                continueButton,
                settingsButton,
                exitButton);
            EnsureNavigationLink(
                settingsButton,
                exitButton,
                continueButton);
            EnsureNavigationLink(
                exitButton,
                continueButton,
                settingsButton);
            EnsureNavigationLink(
                mainMenuButton,
                quitButton,
                quitButton);
            EnsureNavigationLink(
                quitButton,
                mainMenuButton,
                mainMenuButton);
        }

        private static void EnsureNavigationLink(
            Button button,
            Selectable expectedDown,
            Selectable expectedUp)
        {
            Navigation navigation = button.navigation;
            Ensure(
                navigation.mode == Navigation.Mode.Explicit
                && navigation.selectOnDown == expectedDown
                && navigation.selectOnUp == expectedUp,
                $"Button '{button.name}' has an invalid explicit "
                + "keyboard/gamepad navigation link.");
        }

        private static void EnsureDefaultSelections(
            PauseMenuWindow window)
        {
            EnsurePrivateButton(
                window,
                "mainDefaultSelection",
                "ContinueButton");
            EnsurePrivateButton(
                window,
                "exitDefaultSelection",
                "ReturnToMainMenuButton");
        }

        private static void EnsureSelectionVisualPolicy(
            PauseMenuWindow window)
        {
            MainMenuButtonHoverVisual[] hoverVisuals = window
                .GetComponentsInChildren<MainMenuButtonHoverVisual>(true);
            Ensure(
                hoverVisuals.Length == 5,
                "Pause menu should have five pointer hover visuals.");

            string[] buttonNames =
            {
                "ContinueButton",
                "SettingsButton",
                "ExitButton",
                "ReturnToMainMenuButton",
                "QuitGameButton"
            };
            foreach (string buttonName in buttonNames)
            {
                Button button = FindButton(window, buttonName);
                MainMenuButtonHoverVisual hoverVisual =
                    button.GetComponent<MainMenuButtonHoverVisual>();
                Ensure(
                    hoverVisual != null,
                    $"Pause button '{buttonName}' has no pointer hover "
                    + "visual.");
                Ensure(
                    !hoverVisual.ShowOnSelection,
                    $"Pause button '{buttonName}' must not show its "
                    + "hover visual for EventSystem selection.");

                Ensure(
                    ColorsApproximately(
                        button.colors.selectedColor,
                        button.colors.normalColor),
                    $"Pause button '{button.name}' must use its Normal "
                    + "Color for Selected Color.");
            }
        }

        private static async Task WaitForCurrentSelection(
            PauseMenuWindow window,
            string expectedButtonName)
        {
            Button button = FindButton(window, expectedButtonName);
            float timeoutAt = Time.realtimeSinceStartup + 2f;
            while ((EventSystem.current == null
                    || EventSystem.current.currentSelectedGameObject
                        != button.gameObject)
                   && Time.realtimeSinceStartup < timeoutAt)
            {
                await Task.Yield();
            }

            // The menu selection is scheduled for the next player frame. Keep
            // observing briefly so a delayed Editor menu click cannot create a
            // false pass before deselectOnBackgroundClick is processed.
            await WaitForPlayerFrames(2);

            EventSystem eventSystem = EventSystem.current;
            GameObject actualSelection = eventSystem != null
                ? eventSystem.currentSelectedGameObject
                : null;
            string actualName = actualSelection != null
                ? actualSelection.name
                : "<none>";
            string eventSystemName = eventSystem != null
                ? $"{eventSystem.name} ({eventSystem.GetInstanceID()})"
                : "<none>";
            Ensure(
                eventSystem != null
                && actualSelection == button.gameObject,
                $"'{expectedButtonName}' should remain the active Submit "
                + $"selection. Actual: '{actualName}', EventSystem: "
                + $"'{eventSystemName}', frame: {Time.frameCount}.");
        }

        private static async Task EnsurePointerHoverBehavior(
            PauseMenuWindow window,
            string buttonName)
        {
            Button button = FindButton(window, buttonName);
            MainMenuButtonHoverVisual hoverVisual =
                button.GetComponent<MainMenuButtonHoverVisual>();
            Transform glowTransform = button.transform.Find("HoverGlow");
            Image glowImage = glowTransform != null
                ? glowTransform.GetComponent<Image>()
                : null;
            Ensure(
                hoverVisual != null && glowImage != null,
                $"'{buttonName}' is missing its pointer hover visual.");
            Ensure(
                EventSystem.current != null,
                "EventSystem is required to verify pointer hover.");

            var pointerData = new PointerEventData(EventSystem.current);
            hoverVisual.OnPointerExit(pointerData);
            await WaitForUi();
            Vector3 baseScale = button.transform.localScale;
            Ensure(
                glowImage.color.a <= 0.001f,
                $"'{buttonName}' should start with its hover glow hidden.");

            hoverVisual.OnPointerEnter(pointerData);
            await WaitForUi();
            Ensure(
                glowImage.color.a > 0.001f,
                $"'{buttonName}' pointer enter should reveal the hover glow.");
            Ensure(
                Vector3.Distance(
                    button.transform.localScale,
                    baseScale) > 0.0001f,
                $"'{buttonName}' pointer enter should apply hover scale.");

            hoverVisual.OnPointerExit(pointerData);
            await WaitForUi();
            Ensure(
                glowImage.color.a <= 0.001f,
                $"'{buttonName}' pointer exit should hide the hover glow.");
            Ensure(
                Vector3.Distance(
                    button.transform.localScale,
                    baseScale) <= 0.0001f,
                $"'{buttonName}' pointer exit should restore base scale.");
        }

        private static bool ColorsApproximately(Color left, Color right)
        {
            return Mathf.Approximately(left.r, right.r)
                && Mathf.Approximately(left.g, right.g)
                && Mathf.Approximately(left.b, right.b)
                && Mathf.Approximately(left.a, right.a);
        }

        private static void EnsurePrivateButton(
            PauseMenuWindow window,
            string fieldName,
            string expectedName)
        {
            FieldInfo field = typeof(PauseMenuWindow).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Button button = field?.GetValue(window) as Button;
            Ensure(
                button != null && button.name == expectedName,
                $"Pause menu field '{fieldName}' should reference "
                + $"'{expectedName}'.");
        }

        private static void EnsureUiInputBindings()
        {
            InputSystemUIInputModule module =
                Object.FindFirstObjectByType<InputSystemUIInputModule>();
            Ensure(
                module != null,
                "InputSystemUIInputModule was not found.");

            InputAction moveAction = module.move?.action;
            Ensure(
                moveAction == null || moveAction.bindings.Count == 0,
                "UI navigation must remain disabled for pointer-only UI.");
            EnsureBinding(
                module.submit?.action,
                "{Submit}",
                "Submit usage (keyboard/gamepad)");
            EnsureBinding(
                module.cancel?.action,
                "{Cancel}",
                "Cancel usage (keyboard/gamepad)");
        }

        private static void EnsureBinding(
            InputAction action,
            string pathFragment,
            string label)
        {
            bool found =
                action != null
                && action.bindings.Any(binding =>
                {
                    string path =
                        string.IsNullOrEmpty(binding.effectivePath)
                            ? binding.path
                            : binding.effectivePath;
                    return !string.IsNullOrEmpty(path)
                           && path.IndexOf(
                               pathFragment,
                               StringComparison.OrdinalIgnoreCase) >= 0;
                });
            Ensure(
                found,
                $"UI input action is missing the '{label}' binding.");
        }

        private static async Task WaitForUi()
        {
            await Task.Delay(280);
        }

        private static async Task WaitForPlayerFrames(int frameCount)
        {
            int targetFrame = Time.frameCount + Mathf.Max(1, frameCount);
            while (Time.frameCount < targetFrame)
            {
                await Task.Yield();
            }
        }

        private static Button FindButton(
            PauseMenuWindow window,
            string buttonName)
        {
            Button button = window
                .GetComponentsInChildren<Button>(true)
                .FirstOrDefault(candidate =>
                    candidate.name == buttonName);
            if (button == null)
            {
                throw new InvalidOperationException(
                    $"Pause button '{buttonName}' was not found.");
            }

            return button;
        }

        private static List<ActionMapState>
            CaptureGameplayActionMapStates(CommonUIRoot root)
        {
            var states = new List<ActionMapState>();
            InputActionAsset asset =
                root.Context.BindingCatalog.ActionAsset;
            foreach (string mapId in
                     root.Context.BindingCatalog.GameplayActionMapIds)
            {
                InputActionMap map = asset.FindActionMap(
                    mapId,
                    throwIfNotFound: false);
                if (map != null)
                {
                    states.Add(new ActionMapState(map, map.enabled));
                }
            }

            return states;
        }

        private static void EnsureGameplayMapsBlocked(
            IEnumerable<ActionMapState> states)
        {
            Ensure(
                states.All(state => !state.map.enabled),
                "All configured gameplay action maps should be blocked.");
        }

        private static void EnsureGameplayMapsRestored(
            IEnumerable<ActionMapState> states)
        {
            Ensure(
                states.All(state =>
                    state.map.enabled == state.wasEnabled),
                "Gameplay action maps should return to their exact prior "
                + "enabled state.");
        }

        private static int GetCancelHandlerCount(
            ModalCancelRouter router)
        {
            FieldInfo entriesField =
                typeof(ModalCancelRouter).GetField(
                    "entries",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            if (entriesField?.GetValue(router) is ICollection entries)
            {
                return entries.Count;
            }

            throw new InvalidOperationException(
                "Could not inspect ModalCancelRouter registrations.");
        }

        private static void Ensure(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private readonly struct ActionMapState
        {
            public readonly InputActionMap map;
            public readonly bool wasEnabled;

            public ActionMapState(
                InputActionMap actionMap,
                bool enabled)
            {
                map = actionMap;
                wasEnabled = enabled;
            }
        }

        private sealed class RecordingQuitService : IGameQuitService
        {
            public int RequestCount { get; private set; }

            public void RequestQuit()
            {
                RequestCount++;
            }
        }
    }
}
