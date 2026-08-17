using System;
using System.Collections.Generic;
using System.Linq;
using Dev.CSU._02_Scripts.MainMenu;
using SpaceGame.CommonUI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Dev.CSU._02_Scripts.PauseMenu.Editor
{
    internal static class PauseMenuInstaller
    {
        private const string InGameScenePath =
            "Assets/Dev/CSU/01_Scenes/InGame.unity";
        private const string RocketShootingScenePath =
            "Assets/Dev/CSU/01_Scenes/Rocket Shooting.unity";
        private const string MainMenuScenePath =
            "Assets/Dev/CSU/01_Scenes/MainMenu.unity";
        private const string PrefabFolder =
            "Assets/Dev/CSU/03_Prefabs/UI";
        private const string PrefabPath =
            PrefabFolder + "/PauseMenuWindow.prefab";
        private const string FontPath =
            "Assets/Dev/CSU/06_Fonts/x10y12pxDenkiChipHangul SDF.asset";
        private const string ButtonSpritePath =
            "Assets/Dev/CSU/00_Assets/Btn.png";
        private const string HoverMaterialPath =
            "Assets/Dev/CSU/08_Material/UI/M_MainMenuHoverGlow.mat";

        private static readonly Color BackdropColor =
            new Color(0f, 0.01f, 0.025f, 0.82f);
        private static readonly Color PanelColor =
            new Color(0.025f, 0.055f, 0.075f, 0.985f);
        private static readonly Color ButtonColor =
            new Color(0.035f, 0.22f, 0.24f, 0.96f);
        private static readonly Color AccentColor =
            new Color(0.08f, 0.92f, 0.96f, 0.92f);
        private static readonly Color TextColor =
            new Color(0.93f, 0.99f, 1f, 1f);
        private static readonly Color MutedTextColor =
            new Color(0.58f, 0.78f, 0.82f, 1f);

        [MenuItem(
            "Tools/CSU/Pause Menu/Install InGame Pause Menu",
            false,
            210)]
        private static void InstallInGame()
        {
            Install(InGameScenePath);
        }

        [MenuItem(
            "Tools/CSU/Pause Menu/Install Rocket Shooting Pause Menu",
            false,
            211)]
        private static void InstallRocketShooting()
        {
            Install(RocketShootingScenePath);
        }

        private static void Install(string scenePath)
        {
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    scenePath))
            {
                Debug.LogError(
                    $"Pause menu installer could not find {scenePath}.");
                return;
            }

            GameObject prefab = GetOrCreatePauseMenuPrefab();
            if (prefab == null)
            {
                return;
            }

            Scene scene = OpenScene(scenePath);
            if (!scene.IsValid())
            {
                return;
            }

            CommonUIRoot commonUIRoot =
                FindSceneObjects<CommonUIRoot>(scene).FirstOrDefault();
            if (commonUIRoot == null)
            {
                Debug.LogError(
                    "Pause menu installer requires the existing "
                    + $"CommonUIRoot in {scene.name}.",
                    scene.GetRootGameObjects().FirstOrDefault());
                return;
            }

            PauseMenuWindow[] existing =
                FindSceneObjects<PauseMenuWindow>(scene);
            PauseMenuWindow pauseMenu;
            if (existing.Length == 0)
            {
                GameObject instance = (GameObject)
                    PrefabUtility.InstantiatePrefab(
                        prefab,
                        commonUIRoot.transform);
                Undo.RegisterCreatedObjectUndo(
                    instance,
                    $"Install {scene.name} Pause Menu");
                instance.name = "PauseMenuWindow";
                SetStretch(
                    instance.GetComponent<RectTransform>());
                pauseMenu = instance.GetComponent<PauseMenuWindow>();
            }
            else
            {
                pauseMenu = existing[0];
                if (existing.Length > 1)
                {
                    Debug.LogError(
                        "Multiple PauseMenuWindow instances already exist. "
                        + "No duplicates were created.",
                        pauseMenu);
                    return;
                }
            }

            pauseMenu.ConfigureSceneRouting(scene.name);
            pauseMenu.transform.SetAsLastSibling();
            commonUIRoot.transform.SetAsLastSibling();
            EnsureMainMenuBuildScene();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[Pause Menu] Installed one {scene.name} pause menu and ensured "
                + "MainMenu is available in Build Settings.",
                pauseMenu);
        }

        [MenuItem(
            "Tools/CSU/Pause Menu/Validate InGame Pause Menu",
            false,
            212)]
        private static void ValidateInGame()
        {
            Validate(InGameScenePath);
        }

        [MenuItem(
            "Tools/CSU/Pause Menu/Validate Rocket Shooting Pause Menu",
            false,
            213)]
        private static void ValidateRocketShooting()
        {
            Validate(RocketShootingScenePath);
        }

        private static void Validate(string expectedScenePath)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != expectedScenePath)
            {
                Debug.LogError(
                    $"Open {expectedScenePath} before validating the pause menu.");
                return;
            }

            var errors = new List<string>();
            PauseMenuWindow[] windows =
                FindSceneObjects<PauseMenuWindow>(scene);
            CommonUIRoot[] roots =
                FindSceneObjects<CommonUIRoot>(scene);

            if (windows.Length != 1)
            {
                errors.Add(
                    $"Expected one PauseMenuWindow, found {windows.Length}.");
            }

            if (roots.Length != 1)
            {
                errors.Add(
                    $"Expected one CommonUIRoot, found {roots.Length}.");
            }

            if (windows.Length == 1)
            {
                PauseMenuWindow window = windows[0];
                if (window.GetComponentInParent<CommonUIRoot>(true) == null)
                {
                    errors.Add(
                        "PauseMenuWindow is not a CommonUIRoot child.");
                }

                Button[] buttons =
                    window.GetComponentsInChildren<Button>(true);
                if (buttons.Length != 6)
                {
                    errors.Add(
                        "Pause menu should contain five actions and one "
                        + $"backdrop button; found {buttons.Length} buttons.");
                }

                MainMenuButtonHoverVisual[] hoverVisuals =
                    window.GetComponentsInChildren<
                        MainMenuButtonHoverVisual>(true);
                if (hoverVisuals.Length != 5)
                {
                    errors.Add(
                        "Expected the shared hover visual on all five "
                        + $"action buttons; found {hoverVisuals.Length}.");
                }
                else
                {
                    foreach (MainMenuButtonHoverVisual hoverVisual
                             in hoverVisuals)
                    {
                        if (hoverVisual.ShowOnSelection)
                        {
                            errors.Add(
                                $"Pause button '{hoverVisual.name}' must "
                                + "show its hover visual only for pointer "
                                + "hover, not EventSystem selection.");
                        }

                        Button hoverButton =
                            hoverVisual.GetComponent<Button>();
                        if (hoverButton == null)
                        {
                            errors.Add(
                                $"Pause hover visual '{hoverVisual.name}' "
                                + "has no Button component.");
                            continue;
                        }

                        ColorBlock colors = hoverButton.colors;
                        if (colors.selectedColor != colors.normalColor)
                        {
                            errors.Add(
                                $"Pause button '{hoverButton.name}' must "
                                + "use its Normal Color for Selected Color.");
                        }
                    }
                }

                TMP_Text[] labels =
                    window.GetComponentsInChildren<TMP_Text>(true);
                if (labels.Any(label => label.font == null))
                {
                    errors.Add("One or more pause menu labels has no font.");
                }
            }

            bool mainMenuInBuild = EditorBuildSettings.scenes.Any(
                buildScene =>
                    buildScene.enabled
                    && string.Equals(
                        buildScene.path,
                        MainMenuScenePath,
                        StringComparison.Ordinal));
            if (!mainMenuInBuild)
            {
                errors.Add("MainMenu is not enabled in Build Settings.");
            }

            if (errors.Count == 0)
            {
                Debug.Log(
                    "[Pause Menu Validator] PASS: one window, five actions, "
                    + "shared hover visuals, Korean TMP font, CommonUI "
                    + "parenting, and MainMenu Build Settings are valid.");
            }
            else
            {
                Debug.LogError(
                    "[Pause Menu Validator] FAIL\n- "
                    + string.Join("\n- ", errors));
            }
        }

        private static Scene OpenScene(string scenePath)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.path == scenePath)
            {
                return activeScene;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return default;
            }

            return EditorSceneManager.OpenScene(
                scenePath,
                OpenSceneMode.Single);
        }

        private static GameObject GetOrCreatePauseMenuPrefab()
        {
            EnsureFolder(PrefabFolder);
            GameObject existing =
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                return existing;
            }

            TMP_FontAsset font =
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            Sprite buttonSprite =
                AssetDatabase.LoadAssetAtPath<Sprite>(ButtonSpritePath);
            Material hoverMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(
                    HoverMaterialPath);
            if (font == null || buttonSprite == null)
            {
                Debug.LogError(
                    "Pause menu prefab creation requires the existing "
                    + "x10y12pxDenkiChipHangul SDF and MainMenu button sprite.");
                return null;
            }

            GameObject root = CreateUiObject(
                "PauseMenuWindow",
                null,
                typeof(CanvasGroup),
                typeof(PauseMenuWindow));
            SetStretch(root.GetComponent<RectTransform>());
            CanvasGroup rootGroup = root.GetComponent<CanvasGroup>();

            Button backdrop = CreateBackdrop(root.transform);
            GameObject mainPanel = CreatePanel(
                "MainPausePanel",
                root.transform,
                new Vector2(540f, 540f));
            CanvasGroup mainGroup =
                mainPanel.GetComponent<CanvasGroup>();
            CreateText(
                "Title",
                mainPanel.transform,
                font,
                "일시정지",
                44f,
                FontStyles.Bold,
                TextColor,
                new Vector2(0f, 190f),
                new Vector2(440f, 72f));
            CreateText(
                "Subtitle",
                mainPanel.transform,
                font,
                "PAUSED",
                16f,
                FontStyles.Normal,
                MutedTextColor,
                new Vector2(0f, 148f),
                new Vector2(440f, 32f));

            Button continueButton = CreateActionButton(
                "ContinueButton",
                mainPanel.transform,
                font,
                buttonSprite,
                hoverMaterial,
                "계속하기",
                new Vector2(0f, 62f));
            Button settingsButton = CreateActionButton(
                "SettingsButton",
                mainPanel.transform,
                font,
                buttonSprite,
                hoverMaterial,
                "설정",
                new Vector2(0f, -36f));
            Button exitButton = CreateActionButton(
                "ExitButton",
                mainPanel.transform,
                font,
                buttonSprite,
                hoverMaterial,
                "나가기",
                new Vector2(0f, -134f));
            ConfigureVerticalNavigation(
                continueButton,
                settingsButton,
                exitButton);
            CreateText(
                "CancelHint",
                mainPanel.transform,
                font,
                "ESC : 계속하기",
                18f,
                FontStyles.Normal,
                MutedTextColor,
                new Vector2(0f, -218f),
                new Vector2(420f, 34f));

            GameObject exitPanel = CreatePanel(
                "ExitChoicePanel",
                root.transform,
                new Vector2(540f, 430f));
            CanvasGroup exitGroup =
                exitPanel.GetComponent<CanvasGroup>();
            CreateText(
                "Title",
                exitPanel.transform,
                font,
                "나가기",
                42f,
                FontStyles.Bold,
                TextColor,
                new Vector2(0f, 142f),
                new Vector2(440f, 70f));
            Button returnToMainMenuButton = CreateActionButton(
                "ReturnToMainMenuButton",
                exitPanel.transform,
                font,
                buttonSprite,
                hoverMaterial,
                "메인 메뉴로 나가기",
                new Vector2(0f, 45f));
            Button quitGameButton = CreateActionButton(
                "QuitGameButton",
                exitPanel.transform,
                font,
                buttonSprite,
                hoverMaterial,
                "게임에서 나가기",
                new Vector2(0f, -53f));
            ConfigureVerticalNavigation(
                returnToMainMenuButton,
                quitGameButton);
            CreateText(
                "CancelHint",
                exitPanel.transform,
                font,
                "ESC : 뒤로",
                18f,
                FontStyles.Normal,
                MutedTextColor,
                new Vector2(0f, -128f),
                new Vector2(420f, 32f));
            TMP_Text statusText = CreateText(
                "StatusText",
                exitPanel.transform,
                font,
                string.Empty,
                16f,
                FontStyles.Normal,
                AccentColor,
                new Vector2(0f, -168f),
                new Vector2(440f, 30f));

            PauseMenuWindow window =
                root.GetComponent<PauseMenuWindow>();
            window.ConfigureModal(
                rootGroup,
                backdrop,
                0.2f,
                shouldPause: true,
                shouldBlockGameplay: true,
                shouldCloseOnBackdrop: false,
                priority: 50);
            window.ConfigureView(
                null,
                mainPanel,
                exitPanel,
                mainGroup,
                exitGroup,
                continueButton,
                settingsButton,
                exitButton,
                returnToMainMenuButton,
                quitGameButton,
                continueButton,
                returnToMainMenuButton,
                statusText,
                0.18f);

            GameObject prefab =
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static Button CreateBackdrop(Transform parent)
        {
            GameObject target = CreateUiObject(
                "Backdrop",
                parent,
                typeof(Image),
                typeof(Button));
            SetStretch(target.GetComponent<RectTransform>());
            Image image = target.GetComponent<Image>();
            image.color = BackdropColor;
            image.raycastTarget = true;

            Button button = target.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation
            {
                mode = Navigation.Mode.None
            };
            return button;
        }

        private static GameObject CreatePanel(
            string name,
            Transform parent,
            Vector2 size)
        {
            GameObject panel = CreateUiObject(
                name,
                parent,
                typeof(Image),
                typeof(CanvasGroup),
                typeof(Outline));
            RectTransform rect = panel.GetComponent<RectTransform>();
            SetCenteredRect(rect, Vector2.zero, size);

            Image image = panel.GetComponent<Image>();
            image.color = PanelColor;
            image.raycastTarget = true;

            Outline outline = panel.GetComponent<Outline>();
            outline.effectColor = AccentColor;
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;
            return panel;
        }

        private static Button CreateActionButton(
            string name,
            Transform parent,
            TMP_FontAsset font,
            Sprite sprite,
            Material hoverMaterial,
            string label,
            Vector2 position)
        {
            GameObject target = CreateUiObject(
                name,
                parent,
                typeof(Image),
                typeof(Button));
            RectTransform rect = target.GetComponent<RectTransform>();
            SetCenteredRect(
                rect,
                position,
                new Vector2(400f, 72f));

            Image image = target.GetComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.color = ButtonColor;

            Button button = target.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor =
                new Color(0.9f, 1f, 1f, 1f);
            colors.selectedColor = colors.normalColor;
            colors.pressedColor =
                new Color(0.72f, 0.95f, 0.98f, 1f);
            colors.disabledColor =
                new Color(0.35f, 0.42f, 0.44f, 0.55f);
            colors.fadeDuration = 0.12f;
            button.colors = colors;

            GameObject glow = CreateUiObject(
                "HoverGlow",
                target.transform,
                typeof(Image));
            glow.transform.SetAsFirstSibling();
            RectTransform glowRect =
                glow.GetComponent<RectTransform>();
            glowRect.anchorMin = Vector2.zero;
            glowRect.anchorMax = Vector2.one;
            glowRect.pivot = new Vector2(0.5f, 0.5f);
            glowRect.anchoredPosition = Vector2.zero;
            glowRect.offsetMin = new Vector2(-18f, -18f);
            glowRect.offsetMax = new Vector2(18f, 18f);
            Image glowImage = glow.GetComponent<Image>();
            glowImage.material = hoverMaterial;
            glowImage.color =
                new Color(
                    AccentColor.r,
                    AccentColor.g,
                    AccentColor.b,
                    0f);
            glowImage.raycastTarget = false;

            CreateText(
                "Label",
                target.transform,
                font,
                label,
                29f,
                FontStyles.Bold,
                TextColor,
                Vector2.zero,
                new Vector2(360f, 58f));

            MainMenuButtonHoverVisual hover =
                target.AddComponent<MainMenuButtonHoverVisual>();
            hover.Configure(
                button,
                glowImage,
                AccentColor,
                selectionShowsVisual: false);
            return button;
        }

        private static TMP_Text CreateText(
            string name,
            Transform parent,
            TMP_FontAsset font,
            string value,
            float fontSize,
            FontStyles style,
            Color color,
            Vector2 position,
            Vector2 size)
        {
            GameObject target = CreateUiObject(
                name,
                parent,
                typeof(TextMeshProUGUI));
            SetCenteredRect(
                target.GetComponent<RectTransform>(),
                position,
                size);
            TMP_Text text = target.GetComponent<TMP_Text>();
            text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }

        private static void ConfigureVerticalNavigation(
            params Button[] buttons)
        {
            for (int index = 0; index < buttons.Length; index++)
            {
                Button button = buttons[index];
                button.navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    wrapAround = true,
                    selectOnUp = buttons[
                        (index - 1 + buttons.Length) % buttons.Length],
                    selectOnDown = buttons[
                        (index + 1) % buttons.Length]
                };
            }
        }

        private static void EnsureMainMenuBuildScene()
        {
            List<EditorBuildSettingsScene> scenes =
                EditorBuildSettings.scenes.ToList();
            int existingIndex = scenes.FindIndex(scene =>
                string.Equals(
                    scene.path,
                    MainMenuScenePath,
                    StringComparison.Ordinal));
            if (existingIndex >= 0)
            {
                if (!scenes[existingIndex].enabled)
                {
                    scenes[existingIndex] =
                        new EditorBuildSettingsScene(
                            MainMenuScenePath,
                            true);
                    EditorBuildSettings.scenes = scenes.ToArray();
                }

                return;
            }

            scenes.Add(
                new EditorBuildSettingsScene(
                    MainMenuScenePath,
                    true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static T[] FindSceneObjects<T>(Scene scene)
            where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<T>(true))
                .ToArray();
        }

        private static GameObject CreateUiObject(
            string name,
            Transform parent,
            params Type[] components)
        {
            var componentTypes = new List<Type>
            {
                typeof(RectTransform)
            };
            componentTypes.AddRange(components);
            var target = new GameObject(
                name,
                componentTypes.ToArray());
            target.layer = LayerMask.NameToLayer("UI");
            if (parent != null)
            {
                target.transform.SetParent(parent, false);
            }

            return target;
        }

        private static void SetStretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static void SetCenteredRect(
            RectTransform rect,
            Vector2 position,
            Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            int separatorIndex = folderPath.LastIndexOf(
                "/",
                StringComparison.Ordinal);
            string parent = folderPath.Substring(0, separatorIndex);
            string name = folderPath.Substring(separatorIndex + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
