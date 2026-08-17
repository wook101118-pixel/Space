#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace PortableEndingSystem.Editor
{
    public static class PortableEndingSceneBuilder
    {
        [MenuItem("Tools/Portable Ending/Create Ending Scene")]
        public static void CreateEndingScene()
        {
            string scenePath = EditorUtility.SaveFilePanelInProject(
                "Create Portable Ending Scene",
                "Ending",
                "unity",
                "Choose where to save the generated ending scene.");

            if (string.IsNullOrWhiteSpace(scenePath))
            {
                return;
            }

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);

            CreateCamera();
            CreateEventSystem();

            GameObject canvasObject = new GameObject(
                "Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            Image background = CreateImage(
                "Background",
                canvasObject.transform,
                Color.black);
            Stretch(background.rectTransform);
            background.raycastTarget = false;

            GameObject root = CreateUiObject("PortableEndingRoot", canvasObject.transform);
            Stretch(root.GetComponent<RectTransform>());
            EndingCreditsPlayer player = root.AddComponent<EndingCreditsPlayer>();

            GameObject viewportObject = CreateUiObject("CreditsViewport", root.transform);
            RectTransform viewport = viewportObject.GetComponent<RectTransform>();
            Stretch(viewport);
            viewportObject.AddComponent<RectMask2D>();

            GameObject contentObject = CreateUiObject("CreditsContent", viewportObject.transform);
            RectTransform content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 0.5f);
            content.anchorMax = new Vector2(1f, 0.5f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = new Vector2(0f, -588f);
            content.sizeDelta = new Vector2(0f, 1000f);

            TMP_Text creditsText = CreateText(
                "CreditsText",
                content.transform,
                EndingCreditsData.DefaultCreditsTemplate,
                27f,
                TextAlignmentOptions.Top);
            Stretch(creditsText.rectTransform);
#if UNITY_2023_2_OR_NEWER
            creditsText.textWrappingMode = TextWrappingModes.Normal;
#else
            creditsText.enableWordWrapping = true;
#endif
            creditsText.overflowMode = TextOverflowModes.Overflow;
            creditsText.richText = true;

            GameObject photoRootObject = CreateUiObject("CreditImagesRoot", content.transform);
            RectTransform photoRoot = photoRootObject.GetComponent<RectTransform>();
            Stretch(photoRoot);

            Image photoTemplate = CreateImage(
                "PhotoTemplate",
                photoRoot,
                Color.white);
            photoTemplate.preserveAspect = true;
            photoTemplate.raycastTarget = false;
            photoTemplate.gameObject.SetActive(false);

            GameObject actionsObject = CreateUiObject("EndActions", root.transform);
            RectTransform actionsRect = actionsObject.GetComponent<RectTransform>();
            SetRect(
                actionsRect,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(440f, 120f));
            CanvasGroup actionsGroup = actionsObject.AddComponent<CanvasGroup>();
            actionsGroup.alpha = 0f;
            actionsGroup.interactable = false;
            actionsGroup.blocksRaycasts = false;

            Button exitButton = CreateButton(
                "ExitButton",
                actionsObject.transform,
                "메인 메뉴",
                new Vector2(360f, 72f));

            Image fadeImage = CreateImage(
                "SceneFadeOverlay",
                canvasObject.transform,
                Color.black);
            Stretch(fadeImage.rectTransform);
            CanvasGroup fadeGroup = fadeImage.gameObject.AddComponent<CanvasGroup>();
            fadeGroup.alpha = 1f;
            fadeGroup.interactable = false;
            fadeGroup.blocksRaycasts = true;

            TMP_Text escapeHint = CreateText(
                "EscapeHint",
                canvasObject.transform,
                "ESC: 엔딩 나가기",
                22f,
                TextAlignmentOptions.BottomRight);
            SetAnchoredRect(
                escapeHint.rectTransform,
                new Vector2(1f, 0f),
                new Vector2(-30f, 24f),
                new Vector2(420f, 50f));
            escapeHint.color = new Color(1f, 1f, 1f, 0.65f);

            AudioSource bgmSource = root.AddComponent<AudioSource>();
            bgmSource.playOnAwake = false;
            bgmSource.loop = true;

            EndingCreditsData data = CreateDataAsset(scenePath);
            SerializedObject serialized = new SerializedObject(player);
            SetObject(serialized, "data", data);
            SetObject(serialized, "creditsViewport", viewport);
            SetObject(serialized, "creditsContent", content);
            SetObject(serialized, "creditsText", creditsText);
            SetObject(serialized, "creditImagesRoot", photoRoot);
            SetObject(serialized, "photoTemplate", photoTemplate);
            SetObject(serialized, "endActions", actionsGroup);
            SetObject(serialized, "exitButton", exitButton);
            SetObject(serialized, "sceneFadeOverlay", fadeGroup);
            SetObject(serialized, "bgmSource", bgmSource);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            background.transform.SetSiblingIndex(0);
            root.transform.SetSiblingIndex(1);
            escapeHint.transform.SetSiblingIndex(2);
            fadeImage.transform.SetAsLastSibling();

            EditorSceneManager.MarkSceneDirty(scene);
            if (EditorSceneManager.SaveScene(scene, scenePath) == false)
            {
                throw new InvalidOperationException($"Failed to save scene at '{scenePath}'.");
            }

            AddSceneToBuildSettings(scenePath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = player;

            if (TMP_Settings.defaultFontAsset == null)
            {
                Debug.LogWarning(
                    "Portable Ending scene was created, but TMP has no Default Font Asset. " +
                    "Import TMP Essentials and assign a font that contains every language used by the credits.");
            }

            Debug.Log(
                $"PORTABLE_ENDING_SCENE_CREATED scene='{scenePath}' data='{AssetDatabase.GetAssetPath(data)}'");
        }

        [MenuItem("Tools/Portable Ending/Validate Open Ending Scene")]
        public static void ValidateOpenEndingScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            EndingCreditsPlayer[] players = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<EndingCreditsPlayer>(true))
                .ToArray();

            if (players.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Open scene must contain exactly one EndingCreditsPlayer; found {players.Length}.");
            }

            if (players[0].ValidateSetup(out string message) == false)
            {
                throw new InvalidOperationException(message);
            }

            int missingScripts = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Sum(transform => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject));

            if (missingScripts != 0)
            {
                throw new InvalidOperationException(
                    $"Open ending scene contains {missingScripts} missing script component(s).");
            }

            Debug.Log("PORTABLE_ENDING_SCENE_VALIDATION_PASSED");
        }

        private static EndingCreditsData CreateDataAsset(string scenePath)
        {
            string folder = Path.GetDirectoryName(scenePath)?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(folder))
            {
                folder = "Assets";
            }

            string dataPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{folder}/Ending Credits Data.asset");
            EndingCreditsData data = ScriptableObject.CreateInstance<EndingCreditsData>();
            AssetDatabase.CreateAsset(data, dataPath);
            return data;
        }

        private static void CreateCamera()
        {
            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        }

        private static void CreateEventSystem()
        {
            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            InputSystemUIInputModule inputModule =
                eventSystemObject.AddComponent<InputSystemUIInputModule>();
            inputModule.move = null;
#else
            eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
        }

        private static Button CreateButton(
            string name,
            Transform parent,
            string label,
            Vector2 size)
        {
            Image image = CreateImage(
                name,
                parent,
                new Color(0.12f, 0.24f, 0.36f, 0.98f));
            SetRect(
                image.rectTransform,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                size);

            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            TMP_Text text = CreateText(
                "Label",
                image.transform,
                label,
                28f,
                TextAlignmentOptions.Center);
            Stretch(text.rectTransform, 8f);
            return button;
        }

        private static TMP_Text CreateText(
            string name,
            Transform parent,
            string value,
            float fontSize,
            TextAlignmentOptions alignment)
        {
            GameObject textObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textObject.layer = LayerMask.NameToLayer("UI");
            textObject.transform.SetParent(parent, false);

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            if (TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }

            return text;
        }

        private static Image CreateImage(
            string name,
            Transform parent,
            Color color)
        {
            GameObject imageObject = CreateUiObject(name, parent);
            Image image = imageObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            GameObject result = new GameObject(name, typeof(RectTransform));
            result.layer = LayerMask.NameToLayer("UI");
            if (parent != null)
            {
                result.transform.SetParent(parent, false);
            }

            return result;
        }

        private static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static void SetRect(
            RectTransform rect,
            Vector2 anchor,
            Vector2 position,
            Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetAnchoredRect(
            RectTransform rect,
            Vector2 anchor,
            Vector2 position,
            Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetObject(
            SerializedObject serialized,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"Missing serialized property '{propertyName}' on {serialized.targetObject.GetType().Name}.");
            }

            property.objectReferenceValue = value;
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            EditorBuildSettingsScene[] existing = EditorBuildSettings.scenes
                .Where(scene => scene.path != scenePath)
                .ToArray();
            EditorBuildSettings.scenes = existing
                .Concat(new[] { new EditorBuildSettingsScene(scenePath, true) })
                .ToArray();
        }
    }
}
#endif
