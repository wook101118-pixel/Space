using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SpaceGame.CommonUI.Audio;
using SpaceGame.CommonUI.Display;
using SpaceGame.CommonUI.Input;
using SpaceGame.CommonUI.Modal;
using SpaceGame.CommonUI.Pause;
using SpaceGame.CommonUI.Tutorial;
using SpaceGame.CommonUI.Views;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SpaceGame.CommonUI.Editor
{
    public static class CommonUIBuilder
    {
        private const string RootFolder = "Assets/Space/CommonUI";
        private const string DataFolder = RootFolder + "/Data";
        private const string PrefabFolder = RootFolder + "/Prefabs";
        private const string InputCatalogPath =
            DataFolder + "/InputBindingCatalog.asset";
        private const string TutorialSequencePath =
            DataFolder + "/MainTutorialSequence.asset";
        private const string FontAssetPath =
            "Assets/Dev/CSU/06_Fonts/x10y12pxDenkiChipHangul SDF.asset";
        private const string BindingRowPath =
            PrefabFolder + "/InputBindingRow.prefab";
        private const string AudioMixerPath =
            "Assets/Resources/Audio.mixer";
        private const string KeyInfoRowPath =
            PrefabFolder + "/KeyInfoRow.prefab";
        private const string SettingsWindowPath =
            PrefabFolder + "/SettingsWindow.prefab";
        private const string KeyInfoWindowPath =
            PrefabFolder + "/KeyInfoWindow.prefab";
        private const string TutorialPanelPath =
            PrefabFolder + "/TutorialPanel.prefab";
        private const string CommonRootPath =
            PrefabFolder + "/CommonUIRoot.prefab";
        private const string OpenButtonsPath =
            PrefabFolder + "/CommonUIButtons.prefab";

        private static readonly Color BackdropColor =
            new Color(0.01f, 0.02f, 0.05f, 0.82f);
        private static readonly Color PanelColor =
            new Color(0.055f, 0.075f, 0.13f, 0.985f);
        private static readonly Color SurfaceColor =
            new Color(0.1f, 0.13f, 0.21f, 1f);
        private static readonly Color AccentColor =
            new Color(0.18f, 0.68f, 0.92f, 1f);
        private static readonly Color TextColor =
            new Color(0.94f, 0.97f, 1f, 1f);
        private static readonly Color MutedTextColor =
            new Color(0.68f, 0.75f, 0.84f, 1f);

        [MenuItem("Tools/Space/Common UI/Build Assets")]
        public static void BuildAssets()
        {
            EnsureFolders();
            TMP_FontAsset font = GetOrCreateFont();
            InputBindingCatalog catalog = GetOrCreateInputCatalog();
            TutorialSequenceData tutorial = GetOrCreateTutorialSequence();
            InputBindingRowView bindingRow =
                GetOrCreateBindingRow(font);
            KeyInfoRowView keyInfoRow =
                GetOrCreateKeyInfoRow(font);
            SettingsWindow settingsWindow = GetOrCreateSettingsWindow(
                font,
                bindingRow);
            KeyInfoWindow keyInfoWindow = GetOrCreateKeyInfoWindow(
                font,
                keyInfoRow);
            RepairFlexibleScrollLayout(SettingsWindowPath, "Panel/Body");
            RepairFlexibleScrollLayout(
                KeyInfoWindowPath,
                "Panel/Bindings");
            TutorialPanel tutorialPanel = GetOrCreateTutorialPanel(
                font,
                tutorial);
            GetOrCreateCommonRoot(
                catalog,
                tutorial,
                settingsWindow,
                keyInfoWindow,
                tutorialPanel);
            GetOrCreateOpenButtons(font);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "[Common UI] Assets are ready. Existing assets were preserved.");
        }

        [MenuItem("Tools/Space/Common UI/Build And Install Build Scenes")]
        public static void BuildAndInstallBuildScenes()
        {
            BuildAssets();

            SceneSetup[] previousSetup =
                EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.SaveOpenScenes();

            string[] scenePaths = EditorBuildSettings.scenes
                .Where(scene => scene.enabled &&
                                !string.IsNullOrWhiteSpace(scene.path))
                .Select(scene => scene.path)
                .ToArray();

            try
            {
                foreach (string scenePath in scenePaths)
                {
                    InstallIntoScene(scenePath);
                }
            }
            finally
            {
                if (previousSetup.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"[Common UI] Installed into {scenePaths.Length} enabled " +
                "Build Settings scene(s).");
        }

        [MenuItem("Tools/Space/Common UI/Verify Assets And Build Scenes")]
        public static void Verify()
        {
            var errors = new List<string>();
            string[] requiredAssets =
            {
                InputCatalogPath,
                TutorialSequencePath,
                FontAssetPath,
                BindingRowPath,
                KeyInfoRowPath,
                SettingsWindowPath,
                KeyInfoWindowPath,
                TutorialPanelPath,
                CommonRootPath,
                OpenButtonsPath
            };

            foreach (string path in requiredAssets)
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) == null)
                {
                    errors.Add($"Missing asset: {path}");
                }
            }

            SceneSetup[] previousSetup =
                EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (EditorBuildSettingsScene buildScene in
                         EditorBuildSettings.scenes.Where(scene =>
                             scene.enabled))
                {
                    Scene scene = EditorSceneManager.OpenScene(
                        buildScene.path,
                        OpenSceneMode.Single);
                    Canvas[] canvases = FindSceneObjects<Canvas>(scene);
                    EventSystem[] eventSystems =
                        FindSceneObjects<EventSystem>(scene);
                    CommonUIRoot[] roots =
                        FindSceneObjects<CommonUIRoot>(scene);
                    CommonUIOpenButton[] buttons =
                        FindSceneObjects<CommonUIOpenButton>(scene);

                    if (canvases.Length != 1)
                    {
                        errors.Add(
                            $"{buildScene.path}: expected one Canvas, " +
                            $"found {canvases.Length}.");
                    }

                    if (eventSystems.Length != 1)
                    {
                        errors.Add(
                            $"{buildScene.path}: expected one EventSystem, " +
                            $"found {eventSystems.Length}.");
                    }

                    if (roots.Length != 1)
                    {
                        errors.Add(
                            $"{buildScene.path}: expected one CommonUIRoot, " +
                            $"found {roots.Length}.");
                    }

                    if (buttons.Length != 3)
                    {
                        errors.Add(
                            $"{buildScene.path}: expected three explicit open " +
                            $"buttons, found {buttons.Length}.");
                    }
                }
            }
            finally
            {
                if (previousSetup.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
                }
            }

            if (errors.Count == 0)
            {
                Debug.Log(
                    "[Common UI Verifier] PASS: assets, scene roots, Canvas, " +
                    "EventSystem, and explicit buttons are connected.");
            }
            else
            {
                Debug.LogError(
                    "[Common UI Verifier] FAIL\n- " +
                    string.Join("\n- ", errors));
            }
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "Space");
            EnsureFolder("Assets/Space", "CommonUI");
            EnsureFolder(RootFolder, "Data");
            EnsureFolder(RootFolder, "Prefabs");
            EnsureFolder(RootFolder, "Fonts");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string combined = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(combined))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static TMP_FontAsset GetOrCreateFont()
        {
            TMP_FontAsset existing =
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null)
            {
                return existing;
            }

            string[] fontGuids = AssetDatabase.FindAssets(
                "x10y12pxDenkiChipHangul t:Font");
            Font sourceFont = fontGuids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<Font>)
                .FirstOrDefault(font => font != null);
            if (sourceFont == null)
            {
                throw new InvalidOperationException(
                    "x10y12pxDenkiChipHangul Font was not found in the project.");
            }

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                39,
                2,
                GlyphRenderMode.SDFAA,
                2048,
                2048,
                AtlasPopulationMode.Dynamic,
                true);
            fontAsset.name = "x10y12pxDenkiChipHangul SDF";
            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

            foreach (Texture2D texture in fontAsset.atlasTextures)
            {
                if (texture != null && !AssetDatabase.Contains(texture))
                {
                    texture.name = fontAsset.name + " Atlas";
                    AssetDatabase.AddObjectToAsset(texture, fontAsset);
                }
            }

            if (fontAsset.material != null &&
                !AssetDatabase.Contains(fontAsset.material))
            {
                fontAsset.material.name = fontAsset.name + " Material";
                AssetDatabase.AddObjectToAsset(
                    fontAsset.material,
                    fontAsset);
            }

            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            return fontAsset;
        }

        private static InputBindingCatalog GetOrCreateInputCatalog()
        {
            InputBindingCatalog existing =
                AssetDatabase.LoadAssetAtPath<InputBindingCatalog>(
                    InputCatalogPath);
            if (existing != null)
            {
                return existing;
            }

            string[] actionAssetPaths = AssetDatabase.FindAssets(
                    "t:InputActionAsset")
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            if (actionAssetPaths.Length == 0)
            {
                throw new InvalidOperationException(
                    "No InputActionAsset exists in the project.");
            }

            string assetPath = actionAssetPaths[0];
            InputActionAsset actionAsset =
                AssetDatabase.LoadAssetAtPath<InputActionAsset>(assetPath);
            InputActionReference[] references =
                AssetDatabase.LoadAllAssetsAtPath(assetPath)
                    .OfType<InputActionReference>()
                    .ToArray();

            InputAction cancelAction =
                actionAsset.FindAction("Common/Cancel", false);
            InputActionReference cancelReference =
                cancelAction == null
                    ? null
                    : references.FirstOrDefault(reference =>
                        reference.action != null &&
                        reference.action.id == cancelAction.id);
            if (cancelReference == null)
            {
                throw new InvalidOperationException(
                    "The InputActionAsset has no persistent Common/Cancel "
                    + "action reference.");
            }

            var definitions = new List<InputBindingDefinition>();
            InputAction moveAction =
                actionAsset.FindAction("Player/Move", false);
            if (moveAction == null)
            {
                throw new InvalidOperationException(
                    "Player/Move Input Action이 없습니다.");
            }

            AddCatalogBinding(
                definitions,
                actionAsset,
                references,
                "우주선 왼쪽 조작 - 기본 키",
                "Player/Move",
                "351f2ccd-1f9f-44bf-9bec-d62ac5c5f408",
                "d2581a9b-1d11-4566-b27d-b92aff5fabbc",
                "<Keyboard>/a",
                "Gameplay",
                "MoveLeft");
            AddCatalogBinding(
                definitions,
                actionAsset,
                references,
                "우주선 왼쪽 조작 - 보조 키",
                "Player/Move",
                "351f2ccd-1f9f-44bf-9bec-d62ac5c5f408",
                "2e46982e-44cc-431b-9f0b-c11910bf467a",
                "<Keyboard>/leftArrow",
                "Gameplay",
                "MoveLeft");
            AddCatalogBinding(
                definitions,
                actionAsset,
                references,
                "우주선 오른쪽 조작 - 기본 키",
                "Player/Move",
                "351f2ccd-1f9f-44bf-9bec-d62ac5c5f408",
                "fcfe95b8-67b9-4526-84b5-5d0bc98d6400",
                "<Keyboard>/d",
                "Gameplay",
                "MoveRight");
            AddCatalogBinding(
                definitions,
                actionAsset,
                references,
                "우주선 오른쪽 조작 - 보조 키",
                "Player/Move",
                "351f2ccd-1f9f-44bf-9bec-d62ac5c5f408",
                "77bff152-3580-4b21-b6de-dcd0c7e41164",
                "<Keyboard>/rightArrow",
                "Gameplay",
                "MoveRight");
            AddCatalogBinding(
                definitions,
                actionAsset,
                references,
                "기계 부품 회전",
                "Player/RotateBlock",
                "d885a2c2-2ff4-4b58-a3ad-cdf05dc28f6d",
                "3d26de7f-cecf-445d-ad13-976140bb1d4c",
                "<Keyboard>/r",
                "Gameplay",
                "RotateBlock");
            AddCatalogBinding(
                definitions,
                actionAsset,
                references,
                "공통 취소 / 닫기",
                "Common/Cancel",
                "d2db7f26-9567-4f5e-8736-1f3c79d7cff1",
                "bfd8db9d-e9ca-4ffd-8f64-66a4601e599b",
                "<Keyboard>/escape",
                "CommonUI",
                "Cancel",
                true);
            AddCatalogBinding(
                definitions,
                actionAsset,
                references,
                "UI 클릭",
                "UI/Click",
                "3c7022bf-7922-4f7c-a998-c437916075ad",
                "4faf7dc9-b979-4210-aa8c-e808e1ef89f5",
                "<Mouse>/leftButton",
                "UIPointer",
                "Click");

            if (definitions.Count != 7)
            {
                throw new InvalidOperationException(
                    "7개의 키 설정 정의를 생성하지 못했습니다.");
            }

            string[] gameplayMapIds =
            {
                moveAction.actionMap.id.ToString()
            };

            var catalog =
                ScriptableObject.CreateInstance<InputBindingCatalog>();
            catalog.Configure(
                actionAsset,
                cancelReference,
                definitions,
                new[]
                {
                    "<Keyboard>/escape",
                    "<Keyboard>/printScreen",
                    "<Keyboard>/pause"
                },
                gameplayMapIds,
                "Common/Cancel");
            AssetDatabase.CreateAsset(catalog, InputCatalogPath);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        private static void AddCatalogBinding(
            ICollection<InputBindingDefinition> definitions,
            InputActionAsset actionAsset,
            IEnumerable<InputActionReference> references,
            string displayName,
            string actionPath,
            string expectedActionId,
            string expectedBindingId,
            string expectedControlPath,
            string conflictGroup,
            string logicalBinding,
            bool reserved = false)
        {
            InputAction action = actionAsset.FindAction(actionPath, false);
            Guid actionId = Guid.Parse(expectedActionId);
            if (action == null || action.id != actionId)
            {
                throw new InvalidOperationException(
                    $"{actionPath} Input Action 또는 GUID가 일치하지 않습니다.");
            }

            InputActionReference actionReference = references.FirstOrDefault(
                reference => reference.action != null &&
                             reference.action.id == actionId);
            if (actionReference == null)
            {
                throw new InvalidOperationException(
                    $"{actionPath} 영구 Action Reference가 없습니다.");
            }

            Guid bindingId = Guid.Parse(expectedBindingId);
            int bindingIndex = action.bindings.IndexOf(binding =>
                binding.id == bindingId);
            if (bindingIndex < 0 ||
                !string.Equals(
                    action.bindings[bindingIndex].path,
                    expectedControlPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"{actionPath}의 {expectedControlPath} Binding 또는 "
                    + "GUID가 일치하지 않습니다.");
            }

            var definition = new InputBindingDefinition();
            definition.Configure(
                displayName,
                actionReference,
                bindingId.ToString(),
                "Keyboard&Mouse",
                conflictGroup,
                logicalBinding,
                reserved);
            definitions.Add(definition);
        }

        private static TutorialSequenceData GetOrCreateTutorialSequence()
        {
            TutorialSequenceData existing =
                AssetDatabase.LoadAssetAtPath<TutorialSequenceData>(
                    TutorialSequencePath);
            if (existing != null)
            {
                return existing;
            }

            var pages = new List<TutorialPageData>();
            pages.Add(CreateTutorialPage(
                "TutorialPage_01.asset",
                "로켓 준비",
                "자원으로 엔진·연료·선체·드릴을 강화하고 부품을 4×4 " +
                "보드에 배치하세요. 부품을 드래그하는 동안 R 키로 " +
                "회전하고 Esc 키로 취소할 수 있습니다."));
            pages.Add(CreateTutorialPage(
                "TutorialPage_02.asset",
                "비행 조작",
                "A/D 또는 ←/→ 키로 로켓을 좌우로 움직여 장애물을 " +
                "피하세요. 연료와 선체 상태를 확인하면서 더 먼 거리를 " +
                "목표로 하세요."));
            pages.Add(CreateTutorialPage(
                "TutorialPage_03.asset",
                "일시정지와 설정",
                "비행 중 Esc 키를 누르면 일시정지 메뉴가 열립니다. " +
                "설정에서 좌우 이동 키와 음량, 화면 옵션을 바꿀 수 " +
                "있습니다."));

            var sequence =
                ScriptableObject.CreateInstance<TutorialSequenceData>();
            sequence.Configure("main", pages);
            AssetDatabase.CreateAsset(sequence, TutorialSequencePath);
            EditorUtility.SetDirty(sequence);
            AssetDatabase.SaveAssets();
            return sequence;
        }

        private static TutorialPageData CreateTutorialPage(
            string fileName,
            string title,
            string body)
        {
            string path = DataFolder + "/" + fileName;
            TutorialPageData existing =
                AssetDatabase.LoadAssetAtPath<TutorialPageData>(path);
            if (existing != null)
            {
                return existing;
            }

            var page = ScriptableObject.CreateInstance<TutorialPageData>();
            page.Configure(null, title, body);
            AssetDatabase.CreateAsset(page, path);
            return page;
        }

        private static InputBindingRowView GetOrCreateBindingRow(
            TMP_FontAsset font)
        {
            InputBindingRowView existing =
                AssetDatabase.LoadAssetAtPath<InputBindingRowView>(
                    BindingRowPath);
            if (existing != null)
            {
                return existing;
            }

            GameObject root = CreateUiObject("InputBindingRow");
            AddLayoutElement(root, 48f);
            HorizontalLayoutGroup layout = root.AddComponent<
                HorizontalLayoutGroup>();
            ConfigureHorizontalLayout(layout, 8, 8, 8f);

            TextMeshProUGUI name = CreateText(
                "DisplayName",
                root.transform,
                font,
                "Action",
                18,
                TextAlignmentOptions.MidlineLeft,
                TextColor);
            name.margin = new Vector4(13.5f, 0f, 0f, 0f);
            AddLayoutElement(name.gameObject, 48f, 260f, 1f);

            TextMeshProUGUI binding = CreateText(
                "Binding",
                root.transform,
                font,
                "미지정",
                18,
                TextAlignmentOptions.Center,
                AccentColor);
            AddLayoutElement(binding.gameObject, 48f, 160f, 1f);

            Button change = CreateButton(
                "ChangeButton",
                root.transform,
                font,
                "키 설정",
                SurfaceColor);
            AddLayoutElement(change.gameObject, 44f, 88f);

            Button reset = CreateButton(
                "ResetButton",
                root.transform,
                font,
                "초기화",
                SurfaceColor);
            AddLayoutElement(reset.gameObject, 44f, 88f);

            InputBindingRowView view =
                root.AddComponent<InputBindingRowView>();
            view.ConfigureView(name, binding, change, reset);
            GameObject prefab =
                PrefabUtility.SaveAsPrefabAsset(root, BindingRowPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<InputBindingRowView>();
        }

        private static KeyInfoRowView GetOrCreateKeyInfoRow(
            TMP_FontAsset font)
        {
            KeyInfoRowView existing =
                AssetDatabase.LoadAssetAtPath<KeyInfoRowView>(KeyInfoRowPath);
            if (existing != null)
            {
                return existing;
            }

            GameObject root = CreateUiObject("KeyInfoRow");
            AddLayoutElement(root, 44f);
            HorizontalLayoutGroup layout = root.AddComponent<
                HorizontalLayoutGroup>();
            ConfigureHorizontalLayout(layout, 10, 10, 8f);

            TextMeshProUGUI name = CreateText(
                "DisplayName",
                root.transform,
                font,
                "Action",
                18,
                TextAlignmentOptions.MidlineLeft,
                TextColor);
            AddLayoutElement(name.gameObject, 44f, 240f, 1f);

            TextMeshProUGUI binding = CreateText(
                "Binding",
                root.transform,
                font,
                "미지정",
                18,
                TextAlignmentOptions.MidlineRight,
                AccentColor);
            AddLayoutElement(binding.gameObject, 44f, 150f);

            KeyInfoRowView view = root.AddComponent<KeyInfoRowView>();
            view.ConfigureView(name, binding);
            GameObject prefab =
                PrefabUtility.SaveAsPrefabAsset(root, KeyInfoRowPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<KeyInfoRowView>();
        }

        private static SettingsWindow GetOrCreateSettingsWindow(
            TMP_FontAsset font,
            InputBindingRowView bindingRowPrefab)
        {
            SettingsWindow existing =
                AssetDatabase.LoadAssetAtPath<SettingsWindow>(
                    SettingsWindowPath);
            if (existing != null)
            {
                return existing;
            }

            GameObject root = CreateModalRoot(
                "SettingsWindow",
                out CanvasGroup group,
                out Button backdrop,
                out RectTransform panel);
            SettingsWindow view = root.AddComponent<SettingsWindow>();
            view.ConfigureModal(
                group,
                backdrop,
                0.16f,
                true,
                true,
                true,
                400);

            VerticalLayoutGroup panelLayout =
                panel.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureVerticalLayout(panelLayout, 24, 24, 12f);

            RectTransform header = CreateHorizontalContainer(
                "Header",
                panel,
                64f);
            TextMeshProUGUI title = CreateText(
                "Title",
                header,
                font,
                "설정",
                30,
                TextAlignmentOptions.MidlineLeft,
                TextColor);
            AddLayoutElement(title.gameObject, 64f, 200f, 1f);
            Button close = CreateButton(
                "CloseButton",
                header,
                font,
                "닫기",
                SurfaceColor);
            AddLayoutElement(close.gameObject, 48f, 92f);

            ScrollRect bodyScroll = CreateVerticalScrollView(
                "Body",
                panel,
                out RectTransform bodyContent);
            AddLayoutElement(
                bodyScroll.gameObject,
                200f,
                -1f,
                0f,
                1f);

            CreateSectionLabel(bodyContent, font, "오디오");
            Slider master = CreateSliderRow(
                bodyContent,
                font,
                "전체 음량");
            Slider bgm = CreateSliderRow(bodyContent, font, "BGM 음량");
            Slider sfx = CreateSliderRow(bodyContent, font, "SFX 음량");
            Slider ui = CreateSliderRow(bodyContent, font, "UI 음량");
            Slider ambience = CreateSliderRow(
                bodyContent,
                font,
                "환경음");

            CreateSectionLabel(bodyContent, font, "화면");
            Toggle fullscreen = CreateToggleRow(
                bodyContent,
                font,
                "전체 화면");
            TMP_Dropdown resolution = CreateDropdownRow(
                bodyContent,
                font,
                "해상도");

            CreateSectionLabel(bodyContent, font, "키 설정");
            RectTransform bindingHeader = CreateHorizontalContainer(
                "BindingHeader",
                bodyContent,
                52f);
            TextMeshProUGUI bindingTitle = CreateText(
                "BindingTitle",
                bindingHeader,
                font,
                "이동 키 설정 (A / D)",
                22,
                TextAlignmentOptions.MidlineLeft,
                TextColor);
            AddLayoutElement(
                bindingTitle.gameObject,
                52f,
                260f,
                1f);
            Button resetBindings = CreateButton(
                "ResetAllBindingsButton",
                bindingHeader,
                font,
                "전체 초기화",
                SurfaceColor);
            AddLayoutElement(resetBindings.gameObject, 44f, 126f);

            RectTransform bindingsRoot =
                CreateUiObject("BindingRows", bodyContent)
                    .GetComponent<RectTransform>();
            VerticalLayoutGroup bindingsLayout =
                bindingsRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureVerticalLayout(bindingsLayout, 0, 0, 4f);
            ContentSizeFitter bindingFitter =
                bindingsRoot.gameObject.AddComponent<ContentSizeFitter>();
            bindingFitter.verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            TextMeshProUGUI status = CreateText(
                "Status",
                bodyContent,
                font,
                string.Empty,
                16,
                TextAlignmentOptions.MidlineLeft,
                MutedTextColor);
            AddLayoutElement(status.gameObject, 44f);

            RectTransform footer = CreateHorizontalContainer(
                "Footer",
                panel,
                64f);
            HorizontalLayoutGroup footerLayout =
                footer.GetComponent<HorizontalLayoutGroup>();
            footerLayout.childAlignment = TextAnchor.MiddleRight;
            Button defaults = CreateButton(
                "RestoreDefaultsButton",
                footer,
                font,
                "기본값 복원",
                SurfaceColor);
            AddLayoutElement(defaults.gameObject, 48f, 140f);
            Button cancel = CreateButton(
                "CancelButton",
                footer,
                font,
                "취소",
                SurfaceColor);
            AddLayoutElement(cancel.gameObject, 48f, 100f);
            Button apply = CreateButton(
                "ApplyButton",
                footer,
                font,
                "적용",
                AccentColor);
            AddLayoutElement(apply.gameObject, 48f, 100f);

            view.ConfigureView(
                master,
                bgm,
                sfx,
                ui,
                ambience,
                fullscreen,
                resolution,
                bindingsRoot,
                bindingRowPrefab,
                resetBindings,
                status,
                apply,
                cancel,
                defaults,
                close);

            GameObject prefab =
                PrefabUtility.SaveAsPrefabAsset(root, SettingsWindowPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<SettingsWindow>();
        }

        private static KeyInfoWindow GetOrCreateKeyInfoWindow(
            TMP_FontAsset font,
            KeyInfoRowView rowPrefab)
        {
            KeyInfoWindow existing =
                AssetDatabase.LoadAssetAtPath<KeyInfoWindow>(
                    KeyInfoWindowPath);
            if (existing != null)
            {
                return existing;
            }

            GameObject root = CreateModalRoot(
                "KeyInfoWindow",
                out CanvasGroup group,
                out Button backdrop,
                out RectTransform panel);
            KeyInfoWindow view = root.AddComponent<KeyInfoWindow>();
            view.ConfigureModal(
                group,
                backdrop,
                0.16f,
                false,
                true,
                true,
                300);

            VerticalLayoutGroup layout =
                panel.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureVerticalLayout(layout, 24, 24, 12f);

            RectTransform header = CreateHorizontalContainer(
                "Header",
                panel,
                64f);
            TextMeshProUGUI title = CreateText(
                "Title",
                header,
                font,
                "단축키 안내",
                30,
                TextAlignmentOptions.MidlineLeft,
                TextColor);
            AddLayoutElement(title.gameObject, 64f, 200f, 1f);
            Button close = CreateButton(
                "CloseButton",
                header,
                font,
                "닫기",
                SurfaceColor);
            AddLayoutElement(close.gameObject, 48f, 92f);

            ScrollRect scroll = CreateVerticalScrollView(
                "Bindings",
                panel,
                out RectTransform content);
            AddLayoutElement(
                scroll.gameObject,
                240f,
                -1f,
                0f,
                1f);
            TextMeshProUGUI emptyText = CreateText(
                "EmptyText",
                content,
                font,
                "표시할 단축키가 없습니다.",
                20,
                TextAlignmentOptions.Center,
                MutedTextColor);
            AddLayoutElement(emptyText.gameObject, 80f);

            view.ConfigureView(content, rowPrefab, close, emptyText);
            GameObject prefab =
                PrefabUtility.SaveAsPrefabAsset(root, KeyInfoWindowPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<KeyInfoWindow>();
        }

        private static TutorialPanel GetOrCreateTutorialPanel(
            TMP_FontAsset font,
            TutorialSequenceData sequence)
        {
            TutorialPanel existing =
                AssetDatabase.LoadAssetAtPath<TutorialPanel>(
                    TutorialPanelPath);
            if (existing != null)
            {
                return existing;
            }

            GameObject root = CreateModalRoot(
                "TutorialPanel",
                out CanvasGroup group,
                out Button backdrop,
                out RectTransform panel);
            TutorialPanel view = root.AddComponent<TutorialPanel>();
            view.ConfigureModal(
                group,
                backdrop,
                0.2f,
                true,
                true,
                false,
                200);

            VerticalLayoutGroup layout =
                panel.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureVerticalLayout(layout, 24, 24, 12f);

            RectTransform header = CreateHorizontalContainer(
                "Header",
                panel,
                60f);
            TextMeshProUGUI headerTitle = CreateText(
                "HeaderTitle",
                header,
                font,
                "튜토리얼",
                26,
                TextAlignmentOptions.MidlineLeft,
                TextColor);
            AddLayoutElement(headerTitle.gameObject, 60f, 180f, 1f);
            Button close = CreateButton(
                "CloseButton",
                header,
                font,
                "닫기",
                SurfaceColor);
            AddLayoutElement(close.gameObject, 46f, 92f);

            GameObject imageFrame = CreateUiObject(
                "ImageFrame",
                panel,
                typeof(Image));
            imageFrame.GetComponent<Image>().color = SurfaceColor;
            AddLayoutElement(imageFrame, 220f);
            Image pageImage = CreateUiObject(
                    "PageImage",
                    imageFrame.transform,
                    typeof(Image))
                .GetComponent<Image>();
            SetStretch(pageImage.rectTransform, 10f);
            pageImage.preserveAspect = true;
            GameObject placeholder = CreateUiObject(
                "Placeholder",
                imageFrame.transform,
                typeof(Image));
            SetStretch(placeholder.GetComponent<RectTransform>(), 10f);
            placeholder.GetComponent<Image>().color =
                new Color(0.16f, 0.2f, 0.29f, 1f);
            TextMeshProUGUI placeholderText = CreateText(
                "Text",
                placeholder.transform,
                font,
                "이미지 없음",
                22,
                TextAlignmentOptions.Center,
                MutedTextColor);
            SetStretch(placeholderText.rectTransform, 0f);

            TextMeshProUGUI pageTitle = CreateText(
                "PageTitle",
                panel,
                font,
                "제목",
                28,
                TextAlignmentOptions.Center,
                TextColor);
            AddLayoutElement(pageTitle.gameObject, 52f);
            TextMeshProUGUI body = CreateText(
                "Body",
                panel,
                font,
                "본문",
                19,
                TextAlignmentOptions.TopLeft,
                TextColor);
            body.textWrappingMode = TextWrappingModes.Normal;
            AddLayoutElement(body.gameObject, 100f, -1f, 1f);

            RectTransform footer = CreateHorizontalContainer(
                "Footer",
                panel,
                64f);
            Button previous = CreateButton(
                "PreviousButton",
                footer,
                font,
                "이전",
                SurfaceColor);
            AddLayoutElement(previous.gameObject, 48f, 100f);
            TextMeshProUGUI counter = CreateText(
                "PageCounter",
                footer,
                font,
                "1 / 1",
                18,
                TextAlignmentOptions.Center,
                MutedTextColor);
            AddLayoutElement(counter.gameObject, 48f, 100f, 1f);
            Button next = CreateButton(
                "NextButton",
                footer,
                font,
                "다음",
                AccentColor);
            AddLayoutElement(next.gameObject, 48f, 100f);
            Button start = CreateButton(
                "StartButton",
                footer,
                font,
                "시작",
                AccentColor);
            AddLayoutElement(start.gameObject, 48f, 100f);

            view.ConfigureView(
                sequence,
                pageImage,
                placeholder,
                pageTitle,
                body,
                counter,
                previous,
                next,
                start,
                close,
                false,
                false,
                true);

            GameObject prefab =
                PrefabUtility.SaveAsPrefabAsset(root, TutorialPanelPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<TutorialPanel>();
        }

        private static CommonUIRoot GetOrCreateCommonRoot(
            InputBindingCatalog catalog,
            TutorialSequenceData sequence,
            SettingsWindow settingsPrefab,
            KeyInfoWindow keyInfoPrefab,
            TutorialPanel tutorialPrefab)
        {
            CommonUIRoot existing =
                AssetDatabase.LoadAssetAtPath<CommonUIRoot>(CommonRootPath);
            if (existing != null)
            {
                return existing;
            }

            GameObject root = CreateUiObject("CommonUIRoot");
            SetStretch(root.GetComponent<RectTransform>(), 0f);
            CommonUIRoot commonRoot = root.AddComponent<CommonUIRoot>();
            AudioMixerSettingsAdapter audio =
                root.AddComponent<AudioMixerSettingsAdapter>();
            AudioMixer mixer =
                AssetDatabase.LoadAssetAtPath<AudioMixer>(AudioMixerPath);
            audio.Configure(
                mixer,
                "Master",
                "BGM",
                "SFX",
                "UI",
                "Ambience");
            ScreenSettingsApplier screen =
                root.AddComponent<ScreenSettingsApplier>();
            screen.Configure(
                true,
                new[]
                {
                    new ResolutionOption(1280, 720, 0, 1),
                    new ResolutionOption(1600, 900, 0, 1),
                    new ResolutionOption(1920, 1080, 0, 1)
                });
            PauseRequestService pause =
                root.AddComponent<PauseRequestService>();
            ModalCancelRouter router =
                root.AddComponent<ModalCancelRouter>();
            ModalInputGate gate = root.AddComponent<ModalInputGate>();

            GameObject settingsInstance = (GameObject)
                PrefabUtility.InstantiatePrefab(
                    settingsPrefab.gameObject,
                    root.transform);
            GameObject keyInfoInstance = (GameObject)
                PrefabUtility.InstantiatePrefab(
                    keyInfoPrefab.gameObject,
                    root.transform);
            GameObject tutorialInstance = (GameObject)
                PrefabUtility.InstantiatePrefab(
                    tutorialPrefab.gameObject,
                    root.transform);
            SetStretch(
                settingsInstance.GetComponent<RectTransform>(),
                0f);
            SetStretch(
                keyInfoInstance.GetComponent<RectTransform>(),
                0f);
            SetStretch(
                tutorialInstance.GetComponent<RectTransform>(),
                0f);

            commonRoot.Configure(
                catalog,
                sequence,
                audio,
                screen,
                pause,
                router,
                gate,
                settingsInstance.GetComponent<SettingsWindow>(),
                keyInfoInstance.GetComponent<KeyInfoWindow>(),
                tutorialInstance.GetComponent<TutorialPanel>());

            GameObject prefab =
                PrefabUtility.SaveAsPrefabAsset(root, CommonRootPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<CommonUIRoot>();
        }

        private static GameObject GetOrCreateOpenButtons(
            TMP_FontAsset font)
        {
            GameObject existing =
                AssetDatabase.LoadAssetAtPath<GameObject>(OpenButtonsPath);
            if (existing != null)
            {
                return existing;
            }

            GameObject root = CreateUiObject("CommonUIButtons");
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-20f, -20f);
            rect.sizeDelta = new Vector2(180f, 148f);
            VerticalLayoutGroup layout =
                root.AddComponent<VerticalLayoutGroup>();
            ConfigureVerticalLayout(layout, 0, 0, 6f);

            CreateOpenButton(
                root.transform,
                font,
                "설정",
                CommonUIWindowTarget.Settings);
            CreateOpenButton(
                root.transform,
                font,
                "단축키",
                CommonUIWindowTarget.KeyInfo);
            CreateOpenButton(
                root.transform,
                font,
                "튜토리얼",
                CommonUIWindowTarget.Tutorial);

            GameObject prefab =
                PrefabUtility.SaveAsPrefabAsset(root, OpenButtonsPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void CreateOpenButton(
            Transform parent,
            TMP_FontAsset font,
            string label,
            CommonUIWindowTarget target)
        {
            Button button = CreateButton(
                target + "Button",
                parent,
                font,
                label,
                SurfaceColor);
            AddLayoutElement(button.gameObject, 44f);
            CommonUIOpenButton opener =
                button.gameObject.AddComponent<CommonUIOpenButton>();
            opener.Configure(button, null, target);
        }

        [MenuItem("Tools/Space/Common UI/Install Current Loaded Scene")]
        public static void InstallCurrentLoadedScene()
        {
            BuildAssets();
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.path))
            {
                throw new InvalidOperationException(
                    "The active scene must be saved before installation.");
            }

            InstallIntoLoadedScene(scene);
            Debug.Log(
                $"[Common UI] Installed current scene: {scene.path}");
        }

        private static void InstallIntoScene(string scenePath)
        {
            Scene scene = EditorSceneManager.OpenScene(
                scenePath,
                OpenSceneMode.Single);
            InstallIntoLoadedScene(scene);
        }

        private static void InstallIntoLoadedScene(Scene scene)
        {
            Canvas canvas = FindSceneObjects<Canvas>(scene)
                .FirstOrDefault(candidate =>
                    candidate.renderMode == RenderMode.ScreenSpaceOverlay)
                ?? FindSceneObjects<Canvas>(scene).FirstOrDefault();
            if (canvas == null)
            {
                GameObject canvasObject = CreateUiObject(
                    "Canvas",
                    null,
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(GraphicRaycaster));
                Undo.RegisterCreatedObjectUndo(
                    canvasObject,
                    "Create Common UI Canvas");
                canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasScaler scaler =
                    canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode =
                    CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode =
                    CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }

            EventSystem eventSystem =
                FindSceneObjects<EventSystem>(scene).FirstOrDefault();
            if (eventSystem == null)
            {
                GameObject eventObject = new GameObject(
                    "EventSystem",
                    typeof(EventSystem),
                    typeof(InputSystemUIInputModule));
                Undo.RegisterCreatedObjectUndo(
                    eventObject,
                    "Create Common UI EventSystem");
                eventSystem = eventObject.GetComponent<EventSystem>();
                ConfigureInputSystemModule(
                    eventObject.GetComponent<InputSystemUIInputModule>());
            }

            CommonUIRoot root =
                FindSceneObjects<CommonUIRoot>(scene).FirstOrDefault();
            if (root == null)
            {
                CommonUIRoot rootPrefab =
                    AssetDatabase.LoadAssetAtPath<CommonUIRoot>(
                        CommonRootPath);
                GameObject instance = (GameObject)
                    PrefabUtility.InstantiatePrefab(
                        rootPrefab.gameObject,
                        canvas.transform);
                Undo.RegisterCreatedObjectUndo(
                    instance,
                    "Install Common UI Root");
                instance.name = "CommonUIRoot";
                SetStretch(instance.GetComponent<RectTransform>(), 0f);
                root = instance.GetComponent<CommonUIRoot>();
            }

            CommonUIOpenButton[] openers =
                FindSceneObjects<CommonUIOpenButton>(scene);
            if (openers.Length == 0)
            {
                GameObject buttonsPrefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        OpenButtonsPath);
                GameObject buttons = (GameObject)
                    PrefabUtility.InstantiatePrefab(
                        buttonsPrefab,
                        canvas.transform);
                Undo.RegisterCreatedObjectUndo(
                    buttons,
                    "Install Common UI Buttons");
                openers = buttons.GetComponentsInChildren<
                    CommonUIOpenButton>(true);
            }

            foreach (CommonUIOpenButton opener in openers)
            {
                Button button = opener.GetComponent<Button>();
                Undo.RecordObject(
                    opener,
                    "Connect Common UI Button");
                opener.Configure(button, root, opener.Target);
                EditorUtility.SetDirty(opener);
            }

            Transform buttonsTransform = openers.Length > 0
                ? openers[0].transform.parent
                : null;
            buttonsTransform?.SetAsLastSibling();
            root.transform.SetAsLastSibling();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void ConfigureInputSystemModule(
            InputSystemUIInputModule module)
        {
            InputBindingCatalog catalog =
                AssetDatabase.LoadAssetAtPath<InputBindingCatalog>(
                    InputCatalogPath);
            InputActionAsset actionAsset = catalog.ActionAsset;
            string actionAssetPath =
                AssetDatabase.GetAssetPath(actionAsset);
            InputActionReference[] references =
                AssetDatabase.LoadAllAssetsAtPath(actionAssetPath)
                    .OfType<InputActionReference>()
                    .ToArray();
            InputActionMap uiMap = actionAsset.FindActionMap("UI", false);
            if (uiMap == null)
            {
                throw new InvalidOperationException(
                    "The InputActionAsset has no UI action map.");
            }

            Guid uiMapId = uiMap.id;

            InputActionReference FindReference(
                Func<InputBinding, bool> bindingPredicate)
            {
                InputAction action = actionAsset.actionMaps
                    .Where(map => map.id == uiMapId)
                    .SelectMany(map => map.actions)
                    .FirstOrDefault(candidate =>
                        candidate.bindings.Any(bindingPredicate));
                return action == null
                    ? null
                    : references.FirstOrDefault(reference =>
                        reference.action != null &&
                        reference.action.id == action.id);
            }

            module.actionsAsset = actionAsset;
            module.point = FindReference(binding =>
                (binding.path ?? string.Empty)
                .Contains("<Mouse>/position"));
            InputAction navigateAction = uiMap.FindAction(
                "Navigate",
                false);
            module.move = navigateAction == null
                ? null
                : references.FirstOrDefault(reference =>
                    reference.action != null
                    && reference.action.id == navigateAction.id);
            module.submit = FindReference(binding =>
                (binding.path ?? string.Empty).Contains("{Submit}"));
            module.cancel = FindReference(binding =>
                (binding.path ?? string.Empty).Contains("{Cancel}"));
            module.leftClick = FindReference(binding =>
                (binding.path ?? string.Empty)
                .Contains("<Mouse>/leftButton"));
            module.rightClick = FindReference(binding =>
                (binding.path ?? string.Empty)
                .Contains("<Mouse>/rightButton"));
            module.middleClick = FindReference(binding =>
                (binding.path ?? string.Empty)
                .Contains("<Mouse>/middleButton"));
            module.scrollWheel = FindReference(binding =>
                (binding.path ?? string.Empty)
                .Contains("<Mouse>/scroll"));
            module.trackedDevicePosition = FindReference(binding =>
                (binding.path ?? string.Empty)
                .Contains("<XRController>/devicePosition"));
            module.trackedDeviceOrientation = FindReference(binding =>
                (binding.path ?? string.Empty)
                .Contains("<XRController>/deviceRotation"));
            EditorUtility.SetDirty(module);
        }

        private static T[] FindSceneObjects<T>(Scene scene)
            where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
        }

        private static GameObject CreateModalRoot(
            string name,
            out CanvasGroup group,
            out Button backdrop,
            out RectTransform panel)
        {
            GameObject root = CreateUiObject(name);
            SetStretch(root.GetComponent<RectTransform>(), 0f);
            group = root.AddComponent<CanvasGroup>();

            GameObject backdropObject = CreateUiObject(
                "Backdrop",
                root.transform,
                typeof(Image),
                typeof(Button));
            SetStretch(
                backdropObject.GetComponent<RectTransform>(),
                0f);
            backdropObject.GetComponent<Image>().color = BackdropColor;
            backdrop = backdropObject.GetComponent<Button>();
            backdrop.transition = Selectable.Transition.None;

            GameObject panelObject = CreateUiObject(
                "Panel",
                root.transform,
                typeof(Image));
            panel = panelObject.GetComponent<RectTransform>();
            panel.anchorMin = new Vector2(0.08f, 0.06f);
            panel.anchorMax = new Vector2(0.92f, 0.94f);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            panelObject.GetComponent<Image>().color = PanelColor;
            return root;
        }

        private static RectTransform CreateHorizontalContainer(
            string name,
            Transform parent,
            float height)
        {
            GameObject root = CreateUiObject(name, parent);
            AddLayoutElement(root, height);
            HorizontalLayoutGroup layout =
                root.AddComponent<HorizontalLayoutGroup>();
            ConfigureHorizontalLayout(layout, 0, 0, 10f);
            return root.GetComponent<RectTransform>();
        }

        private static ScrollRect CreateVerticalScrollView(
            string name,
            Transform parent,
            out RectTransform content)
        {
            GameObject root = CreateUiObject(
                name,
                parent,
                typeof(Image),
                typeof(ScrollRect));
            root.GetComponent<Image>().color =
                new Color(0f, 0f, 0f, 0.08f);
            ScrollRect scroll = root.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            GameObject viewport = CreateUiObject(
                "Viewport",
                root.transform,
                typeof(RectMask2D));
            RectTransform viewportRect =
                viewport.GetComponent<RectTransform>();
            SetStretch(viewportRect, 4f);

            GameObject contentObject = CreateUiObject(
                "Content",
                viewport.transform);
            content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            VerticalLayoutGroup contentLayout =
                contentObject.AddComponent<VerticalLayoutGroup>();
            ConfigureVerticalLayout(contentLayout, 8, 8, 8f);
            ContentSizeFitter fitter =
                contentObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewportRect;
            scroll.content = content;
            return scroll;
        }

        private static void CreateSectionLabel(
            Transform parent,
            TMP_FontAsset font,
            string label)
        {
            TextMeshProUGUI text = CreateText(
                label + "Section",
                parent,
                font,
                label,
                22,
                TextAlignmentOptions.MidlineLeft,
                AccentColor);
            AddLayoutElement(text.gameObject, 44f);
        }

        private static Slider CreateSliderRow(
            Transform parent,
            TMP_FontAsset font,
            string label)
        {
            RectTransform row = CreateHorizontalContainer(
                label + "Row",
                parent,
                48f);
            TextMeshProUGUI labelText = CreateText(
                "Label",
                row,
                font,
                label,
                18,
                TextAlignmentOptions.MidlineLeft,
                TextColor);
            AddLayoutElement(labelText.gameObject, 48f, 180f);

            GameObject sliderObject = CreateUiObject(
                "Slider",
                row,
                typeof(Slider));
            AddLayoutElement(sliderObject, 44f, 180f, 1f);
            Slider slider = sliderObject.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0.8f;

            Image background = CreateUiObject(
                    "Background",
                    sliderObject.transform,
                    typeof(Image))
                .GetComponent<Image>();
            RectTransform backgroundRect = background.rectTransform;
            backgroundRect.anchorMin = new Vector2(0f, 0.5f);
            backgroundRect.anchorMax = new Vector2(1f, 0.5f);
            backgroundRect.sizeDelta = new Vector2(-16f, 8f);
            background.color = new Color(0.17f, 0.21f, 0.3f, 1f);

            GameObject fillArea = CreateUiObject(
                "Fill Area",
                sliderObject.transform);
            RectTransform fillAreaRect =
                fillArea.GetComponent<RectTransform>();
            SetStretch(fillAreaRect, 8f);
            Image fill = CreateUiObject(
                    "Fill",
                    fillArea.transform,
                    typeof(Image))
                .GetComponent<Image>();
            SetStretch(fill.rectTransform, 0f);
            fill.color = AccentColor;

            GameObject handleArea = CreateUiObject(
                "Handle Slide Area",
                sliderObject.transform);
            RectTransform handleAreaRect =
                handleArea.GetComponent<RectTransform>();
            SetStretch(handleAreaRect, 9f);
            Image handle = CreateUiObject(
                    "Handle",
                    handleArea.transform,
                    typeof(Image))
                .GetComponent<Image>();
            handle.rectTransform.sizeDelta = new Vector2(22f, 22f);
            handle.color = Color.white;

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            return slider;
        }

        private static Toggle CreateToggleRow(
            Transform parent,
            TMP_FontAsset font,
            string label)
        {
            RectTransform row = CreateHorizontalContainer(
                label + "Row",
                parent,
                48f);
            TextMeshProUGUI labelText = CreateText(
                "Label",
                row,
                font,
                label,
                18,
                TextAlignmentOptions.MidlineLeft,
                TextColor);
            AddLayoutElement(labelText.gameObject, 48f, 180f, 1f);

            GameObject toggleObject = CreateUiObject(
                "Toggle",
                row,
                typeof(Image),
                typeof(Toggle));
            AddLayoutElement(toggleObject, 36f, 36f);
            Image background = toggleObject.GetComponent<Image>();
            background.color = SurfaceColor;
            Toggle toggle = toggleObject.GetComponent<Toggle>();
            TextMeshProUGUI checkmark = CreateText(
                "Checkmark",
                toggleObject.transform,
                font,
                "■",
                24,
                TextAlignmentOptions.Center,
                AccentColor);
            SetStretch(checkmark.rectTransform, 0f);
            toggle.targetGraphic = background;
            toggle.graphic = checkmark;
            toggle.isOn = true;
            return toggle;
        }

        private static TMP_Dropdown CreateDropdownRow(
            Transform parent,
            TMP_FontAsset font,
            string label)
        {
            RectTransform row = CreateHorizontalContainer(
                label + "Row",
                parent,
                52f);
            TextMeshProUGUI labelText = CreateText(
                "Label",
                row,
                font,
                label,
                18,
                TextAlignmentOptions.MidlineLeft,
                TextColor);
            AddLayoutElement(labelText.gameObject, 48f, 180f);

            GameObject dropdownObject = CreateUiObject(
                "Dropdown",
                row,
                typeof(Image),
                typeof(TMP_Dropdown));
            AddLayoutElement(dropdownObject, 46f, 240f, 1f);
            Image background = dropdownObject.GetComponent<Image>();
            background.color = SurfaceColor;
            TMP_Dropdown dropdown =
                dropdownObject.GetComponent<TMP_Dropdown>();
            dropdown.targetGraphic = background;

            TextMeshProUGUI caption = CreateText(
                "Label",
                dropdownObject.transform,
                font,
                "해상도",
                17,
                TextAlignmentOptions.MidlineLeft,
                TextColor);
            SetStretch(caption.rectTransform, 12f, 36f, 6f, 6f);
            TextMeshProUGUI arrow = CreateText(
                "Arrow",
                dropdownObject.transform,
                font,
                "▼",
                16,
                TextAlignmentOptions.Center,
                AccentColor);
            RectTransform arrowRect = arrow.rectTransform;
            arrowRect.anchorMin = new Vector2(1f, 0f);
            arrowRect.anchorMax = Vector2.one;
            arrowRect.pivot = new Vector2(1f, 0.5f);
            arrowRect.sizeDelta = new Vector2(32f, 0f);

            GameObject templateObject = CreateUiObject(
                "Template",
                dropdownObject.transform,
                typeof(Image),
                typeof(ScrollRect));
            RectTransform template =
                templateObject.GetComponent<RectTransform>();
            template.anchorMin = new Vector2(0f, 0f);
            template.anchorMax = new Vector2(1f, 0f);
            template.pivot = new Vector2(0.5f, 1f);
            template.anchoredPosition = new Vector2(0f, -2f);
            template.sizeDelta = new Vector2(0f, 220f);
            templateObject.GetComponent<Image>().color = PanelColor;

            GameObject viewportObject = CreateUiObject(
                "Viewport",
                templateObject.transform,
                typeof(Image),
                typeof(Mask));
            RectTransform viewport =
                viewportObject.GetComponent<RectTransform>();
            SetStretch(viewport, 4f);
            viewportObject.GetComponent<Image>().color =
                new Color(1f, 1f, 1f, 0.02f);
            viewportObject.GetComponent<Mask>().showMaskGraphic = false;

            GameObject contentObject = CreateUiObject(
                "Content",
                viewportObject.transform);
            RectTransform content =
                contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;

            GameObject itemObject = CreateUiObject(
                "Item",
                contentObject.transform,
                typeof(Toggle));
            RectTransform itemRect =
                itemObject.GetComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0f, 0.5f);
            itemRect.anchorMax = new Vector2(1f, 0.5f);
            itemRect.sizeDelta = new Vector2(0f, 34f);
            Image itemBackground = CreateUiObject(
                    "Item Background",
                    itemObject.transform,
                    typeof(Image))
                .GetComponent<Image>();
            SetStretch(itemBackground.rectTransform, 0f);
            itemBackground.color =
                new Color(0.18f, 0.3f, 0.43f, 0.7f);
            TextMeshProUGUI itemLabel = CreateText(
                "Item Label",
                itemObject.transform,
                font,
                "Option",
                16,
                TextAlignmentOptions.MidlineLeft,
                TextColor);
            SetStretch(itemLabel.rectTransform, 10f, 10f, 2f, 2f);
            Toggle itemToggle = itemObject.GetComponent<Toggle>();
            itemToggle.targetGraphic = itemBackground;
            itemToggle.graphic = itemBackground;

            ScrollRect templateScroll =
                templateObject.GetComponent<ScrollRect>();
            templateScroll.content = content;
            templateScroll.viewport = viewport;
            templateScroll.horizontal = false;
            templateScroll.vertical = true;

            dropdown.template = template;
            dropdown.captionText = caption;
            dropdown.itemText = itemLabel;
            templateObject.SetActive(false);
            return dropdown;
        }

        private static Button CreateButton(
            string name,
            Transform parent,
            TMP_FontAsset font,
            string label,
            Color color)
        {
            GameObject root = CreateUiObject(
                name,
                parent,
                typeof(Image),
                typeof(Button));
            Image image = root.GetComponent<Image>();
            image.color = color;
            Button button = root.GetComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            colors.pressedColor = new Color(0.75f, 0.82f, 0.9f, 1f);
            button.colors = colors;
            TextMeshProUGUI text = CreateText(
                "Label",
                root.transform,
                font,
                label,
                17,
                TextAlignmentOptions.Center,
                TextColor);
            SetStretch(text.rectTransform, 6f);
            return button;
        }

        private static TextMeshProUGUI CreateText(
            string name,
            Transform parent,
            TMP_FontAsset font,
            string value,
            float size,
            TextAlignmentOptions alignment,
            Color color)
        {
            GameObject root = CreateUiObject(
                name,
                parent,
                typeof(TextMeshProUGUI));
            TextMeshProUGUI text =
                root.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private static GameObject CreateUiObject(
            string name,
            Transform parent = null,
            params Type[] components)
        {
            GameObject root = new GameObject(
                name,
                typeof(RectTransform));
            root.layer = LayerMask.NameToLayer("UI");
            if (parent != null)
            {
                root.transform.SetParent(parent, false);
            }

            foreach (Type component in components)
            {
                if (component != typeof(RectTransform))
                {
                    root.AddComponent(component);
                }
            }

            return root;
        }

        private static void ConfigureVerticalLayout(
            VerticalLayoutGroup layout,
            int horizontalPadding,
            int verticalPadding,
            float spacing)
        {
            layout.padding = new RectOffset(
                horizontalPadding,
                horizontalPadding,
                verticalPadding,
                verticalPadding);
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        private static void ConfigureHorizontalLayout(
            HorizontalLayoutGroup layout,
            int horizontalPadding,
            int verticalPadding,
            float spacing)
        {
            layout.padding = new RectOffset(
                horizontalPadding,
                horizontalPadding,
                verticalPadding,
                verticalPadding);
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        private static void AddLayoutElement(
            GameObject target,
            float preferredHeight,
            float preferredWidth = -1f,
            float flexibleWidth = 0f,
            float flexibleHeight = 0f)
        {
            LayoutElement element =
                target.GetComponent<LayoutElement>() ??
                target.AddComponent<LayoutElement>();
            element.preferredHeight = preferredHeight;
            if (preferredWidth >= 0f)
            {
                element.preferredWidth = preferredWidth;
            }

            element.flexibleWidth = flexibleWidth;
            element.flexibleHeight = flexibleHeight;
        }

        private static void RepairFlexibleScrollLayout(
            string prefabPath,
            string scrollPath)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Transform scroll = root.transform.Find(scrollPath);
                if (scroll == null)
                {
                    throw new InvalidOperationException(
                        $"{prefabPath} is missing {scrollPath}.");
                }

                LayoutElement element =
                    scroll.GetComponent<LayoutElement>() ??
                    scroll.gameObject.AddComponent<LayoutElement>();
                element.flexibleWidth = 0f;
                element.flexibleHeight = 1f;
                EditorUtility.SetDirty(element);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void SetStretch(
            RectTransform rect,
            float inset)
        {
            SetStretch(rect, inset, inset, inset, inset);
        }

        private static void SetStretch(
            RectTransform rect,
            float left,
            float right,
            float bottom,
            float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }
    }
}
