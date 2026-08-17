using System.Collections;
using System.Collections.Generic;
using System.Text;
using Dev.CSU._02_Scripts.SceneTransition;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PortableEndingSystem
{
    [DisallowMultipleComponent]
    public sealed class EndingCreditsPlayer : MonoBehaviour
    {
        private const float EntryPadding = 48f;
        private const float ExitPadding = 12f;
        private const float ViewportEdgePadding = 24f;

        [Header("Data")]
        [SerializeField] private EndingCreditsData data;

        [Header("Credits UI")]
        [SerializeField] private RectTransform creditsViewport;
        [SerializeField] private RectTransform creditsContent;
        [SerializeField] private TMP_Text creditsText;
        [SerializeField] private RectTransform creditImagesRoot;
        [SerializeField] private Image photoTemplate;

        [Header("End UI")]
        [SerializeField] private CanvasGroup endActions;
        [SerializeField] private Button exitButton;
        [SerializeField] private TMP_Text cancelHintText;

        [Header("Transition and Audio")]
        [SerializeField] private CanvasGroup sceneFadeOverlay;
        [SerializeField] private AudioSource bgmSource;

#if ENABLE_INPUT_SYSTEM
        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string cancelActionPath = "Common/Cancel";
#endif

        private readonly List<Image> generatedPhotoImages = new List<Image>();
        private Coroutine creditsRoutine;
        private float startY;
        private float endY;
        private float creditsHeight;
        private bool isInitialized;
        private bool hasReachedEnd;
        private bool isRecalculatingLayout;
        private bool isExiting;
        private bool useCancelFallback;
        private bool hasWarnedAboutCancelFallback;

#if ENABLE_INPUT_SYSTEM
        private InputAction cancelAction;
        private bool cancelActionEnabledByThisComponent;
#endif

        public bool IsInitialized => isInitialized;
        public bool HasReachedEnd => hasReachedEnd;
        public bool IsExiting => isExiting;
        public float StartY => startY;
        public float EndY => endY;
        public float CurrentY => creditsContent == null ? 0f : creditsContent.anchoredPosition.y;

        private void Awake()
        {
            if (sceneFadeOverlay != null)
            {
                sceneFadeOverlay.gameObject.SetActive(true);
                sceneFadeOverlay.transform.SetAsLastSibling();
                sceneFadeOverlay.alpha = 1f;
                sceneFadeOverlay.interactable = false;
                sceneFadeOverlay.blocksRaycasts = true;
            }
        }

        private void OnEnable()
        {
            exitButton?.onClick.RemoveListener(RequestExit);
            exitButton?.onClick.AddListener(RequestExit);
            InitializeCancelInput();

            if (ValidateSetup(out string message) == false)
            {
                Debug.LogWarning(message, this);
                return;
            }

            ResetRuntimeState();
            creditsRoutine = StartCoroutine(PlayEnding());
        }

        private void OnDisable()
        {
            exitButton?.onClick.RemoveListener(RequestExit);
            ReleaseCancelInput();
            StopAllCoroutines();
            creditsRoutine = null;

            if (bgmSource != null)
            {
                bgmSource.Stop();
                bgmSource.clip = null;
            }
        }

        private void Update()
        {
            if (data != null && data.AllowEscapeExit && WasCancelPressed())
            {
                RequestExit();
            }
        }

        public void RequestExit()
        {
            if (isExiting || data == null)
            {
                return;
            }

            string sceneName = data.ExitSceneName;
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning($"{nameof(EndingCreditsData)} needs an Exit Scene Name.", data);
                return;
            }

            isExiting = true;
            if (!SceneTransitions.TryLoadScene(sceneName))
            {
                isExiting = false;
                Debug.LogError(
                    $"{nameof(EndingCreditsPlayer)} on '{name}' could not "
                    + $"request the shared fade transition to "
                    + $"'{sceneName}'.",
                    this);
                return;
            }

            if (creditsRoutine != null)
            {
                StopCoroutine(creditsRoutine);
                creditsRoutine = null;
            }
        }

        public bool ValidateSetup(out string message)
        {
            List<string> missing = new List<string>();
            AddIfMissing(data, nameof(data), missing);
            AddIfMissing(creditsViewport, nameof(creditsViewport), missing);
            AddIfMissing(creditsContent, nameof(creditsContent), missing);
            AddIfMissing(creditsText, nameof(creditsText), missing);
            AddIfMissing(creditImagesRoot, nameof(creditImagesRoot), missing);
            AddIfMissing(photoTemplate, nameof(photoTemplate), missing);
            AddIfMissing(endActions, nameof(endActions), missing);
            AddIfMissing(exitButton, nameof(exitButton), missing);
            AddIfMissing(sceneFadeOverlay, nameof(sceneFadeOverlay), missing);

            if (missing.Count == 0)
            {
                message = string.Empty;
                return true;
            }

            message =
                $"{nameof(EndingCreditsPlayer)} on '{name}' is missing Inspector reference(s): " +
                string.Join(", ", missing);
            return false;
        }

        private IEnumerator PlayEnding()
        {
            PrepareBgm();
            StartCoroutine(FadeScreenIn());
            StartCoroutine(FadeBgmIn());

            yield return null;
            RecalculateLayout(0f);

            float elapsed = 0f;
            while (elapsed < data.InitialDelay && isExiting == false)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            while (isExiting == false && creditsContent.anchoredPosition.y < endY)
            {
                Vector2 position = creditsContent.anchoredPosition;
                position.y = Mathf.MoveTowards(
                    position.y,
                    endY,
                    data.ScrollSpeed * Time.unscaledDeltaTime);
                creditsContent.anchoredPosition = position;
                yield return null;
            }

            if (isExiting)
            {
                yield break;
            }

            hasReachedEnd = true;
            elapsed = 0f;
            while (elapsed < data.EndHoldDuration && isExiting == false)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (isExiting == false)
            {
                yield return FadeInEndActions();
            }

            creditsRoutine = null;
        }

        private void ResetRuntimeState()
        {
            isInitialized = false;
            hasReachedEnd = false;
            isExiting = false;
            SetEndActionsState(0f, false);
        }

        private void PrepareBgm()
        {
            if (bgmSource == null || data.BgmClip == null)
            {
                return;
            }

            bgmSource.Stop();
            bgmSource.clip = data.BgmClip;
            bgmSource.loop = data.LoopBgm;
            bgmSource.playOnAwake = false;
            bgmSource.ignoreListenerPause = true;
            bgmSource.volume = data.BgmFadeInDuration > 0f ? 0f : data.BgmVolume;
            bgmSource.Play();
        }

        private IEnumerator FadeBgmIn()
        {
            if (bgmSource == null || data.BgmClip == null)
            {
                yield break;
            }

            float duration = data.BgmFadeInDuration;
            if (duration <= 0f)
            {
                bgmSource.volume = data.BgmVolume;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration && isExiting == false)
            {
                elapsed += Time.unscaledDeltaTime;
                bgmSource.volume = Mathf.Lerp(0f, data.BgmVolume, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            if (isExiting == false)
            {
                bgmSource.volume = data.BgmVolume;
            }
        }

        private IEnumerator FadeScreenIn()
        {
            if (sceneFadeOverlay == null)
            {
                yield break;
            }

            sceneFadeOverlay.gameObject.SetActive(true);
            sceneFadeOverlay.transform.SetAsLastSibling();
            sceneFadeOverlay.blocksRaycasts = true;
            sceneFadeOverlay.interactable = false;

            float duration = data.ScreenFadeInDuration;
            float startAlpha = sceneFadeOverlay.alpha;
            float elapsed = 0f;
            while (elapsed < duration && isExiting == false)
            {
                elapsed += Time.unscaledDeltaTime;
                sceneFadeOverlay.alpha = Mathf.Lerp(startAlpha, 0f, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            if (isExiting == false)
            {
                sceneFadeOverlay.alpha = 0f;
                sceneFadeOverlay.blocksRaycasts = false;
            }
        }

        private IEnumerator FadeInEndActions()
        {
            float duration = data.EndActionsFadeDuration;
            if (duration <= 0f)
            {
                SetEndActionsState(1f, true);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration && isExiting == false)
            {
                elapsed += Time.unscaledDeltaTime;
                endActions.alpha = Mathf.Clamp01(elapsed / duration);
                yield return null;
            }

            if (isExiting == false)
            {
                SetEndActionsState(1f, true);
            }
        }

        private void RecalculateLayout(float normalizedProgress)
        {
            if (isRecalculatingLayout || data == null)
            {
                return;
            }

            isRecalculatingLayout = true;
            try
            {
                creditsText.textWrappingMode = TextWrappingModes.PreserveWhitespaceNoWrap;
                creditsText.text = CreateStyledCredits();
                RebuildPhotoViews();
                float layoutScale = ApplyResponsiveHorizontalLayout();

                Canvas.ForceUpdateCanvases();
                float availableWidth = Mathf.Max(1f, creditsText.rectTransform.rect.width);
                creditsHeight = Mathf.Ceil(
                    creditsText.GetPreferredValues(creditsText.text, availableWidth, 0f).y);
                creditsContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, creditsHeight);

                Canvas.ForceUpdateCanvases();
                creditsText.ForceMeshUpdate(true, true);
                LayoutCreditPhotos(layoutScale);

                startY = creditsViewport.rect.yMin - EntryPadding;
                endY = creditsViewport.rect.yMax + creditsHeight + ExitPadding;

                Vector2 position = creditsContent.anchoredPosition;
                position.x = 0f;
                position.y = hasReachedEnd
                    ? endY
                    : Mathf.Lerp(startY, endY, Mathf.Clamp01(normalizedProgress));
                creditsContent.anchoredPosition = position;
                isInitialized = true;
            }
            finally
            {
                isRecalculatingLayout = false;
            }
        }

        private void OnRectTransformDimensionsChange()
        {
            if (Application.isPlaying == false ||
                isInitialized == false ||
                isRecalculatingLayout ||
                creditsContent == null)
            {
                return;
            }

            float progress = Mathf.InverseLerp(startY, endY, CurrentY);
            RecalculateLayout(progress);
        }

        private string CreateStyledCredits()
        {
            string normalized = data.CreditsTemplate
                .Replace("\r\n", "\n")
                .Replace('\r', '\n');

            string[] lines = normalized.Split('\n');
            string developer = data.DeveloperName;
            string title = data.GameTitle;
            string developerTag = CreateDeveloperNameTag(developer);
            StringBuilder builder = new StringBuilder(normalized.Length * 2);

            for (int index = 0; index < lines.Length; index++)
            {
                string sourceLine = lines[index];
                string classificationLine = sourceLine.Trim();
                if (classificationLine.Length > 0)
                {
                    bool containsDeveloperPlaceholder = classificationLine.Contains("{DEVELOPER_NAME}");
                    string resolvedLine = classificationLine
                        .Replace("{GAME_TITLE}", title)
                        .Replace("{DEVELOPER_NAME}", developer);
                    string styledLine = EscapeRichText(sourceLine)
                        .Replace("{GAME_TITLE}", EscapeRichText(title))
                        .Replace("{DEVELOPER_NAME}", developerTag);

                    if (resolvedLine == "THE END" || resolvedLine == "THANK YOU FOR PLAYING")
                    {
                        builder.Append("<size=76><b>").Append(styledLine).Append("</b></size>");
                    }
                    else if (resolvedLine == title)
                    {
                        builder.Append("<size=58><b>").Append(styledLine).Append("</b></size>");
                    }
                    else if (containsDeveloperPlaceholder && classificationLine == "{DEVELOPER_NAME}")
                    {
                        builder.Append("<size=40>").Append(styledLine).Append("</size>");
                    }
                    else if (IsUppercaseHeading(resolvedLine))
                    {
                        builder.Append("<size=32><b>").Append(styledLine).Append("</b></size>");
                    }
                    else
                    {
                        builder.Append("<size=27>").Append(styledLine).Append("</size>");
                    }
                }
                else if (sourceLine.Length > 0)
                {
                    builder.Append(sourceLine);
                }

                if (index < lines.Length - 1)
                {
                    builder.Append('\n');
                }
            }

            return builder.ToString();
        }

        private static string CreateDeveloperNameTag(string developer)
        {
            return $"<b>{EscapeRichText(developer)}</b>";
        }

        private void RebuildPhotoViews()
        {
            for (int index = 0; index < generatedPhotoImages.Count; index++)
            {
                if (generatedPhotoImages[index] != null)
                {
                    Destroy(generatedPhotoImages[index].gameObject);
                }
            }

            generatedPhotoImages.Clear();
            photoTemplate.gameObject.SetActive(false);

            IReadOnlyList<EndingCreditPhotoData> photos = data.Photos;
            for (int index = 0; photos != null && index < photos.Count; index++)
            {
                EndingCreditPhotoData photo = photos[index];
                if (photo == null)
                {
                    generatedPhotoImages.Add(null);
                    continue;
                }

                Image image = Instantiate(photoTemplate, creditImagesRoot);
                image.name = $"CreditPhoto_{index + 1:00}";
                image.sprite = photo.Sprite;
                image.preserveAspect = true;
                image.raycastTarget = false;
                image.gameObject.SetActive(photo.Sprite != null);
                generatedPhotoImages.Add(image);
            }
        }

        private float ApplyResponsiveHorizontalLayout()
        {
            float viewportWidth = Mathf.Max(1f, creditsViewport.rect.width);
            float widestPhoto = 0f;
            bool hasPhoto = false;
            IReadOnlyList<EndingCreditPhotoData> photos = data.Photos;

            for (int index = 0; photos != null && index < photos.Count; index++)
            {
                EndingCreditPhotoData entry = photos[index];
                if (entry == null || entry.Sprite == null || string.IsNullOrWhiteSpace(entry.AnchorText))
                {
                    continue;
                }

                hasPhoto = true;
                widestPhoto = Mathf.Max(widestPhoto, entry.DisplaySize.x);
            }

            float requiredWidth = data.CenterTextWidth + ViewportEdgePadding * 2f;
            if (hasPhoto)
            {
                requiredWidth += (data.PhotoGap + widestPhoto) * 2f;
            }

            float layoutScale = Mathf.Min(1f, viewportWidth / Mathf.Max(1f, requiredWidth));
            float textWidth = Mathf.Min(viewportWidth, data.CenterTextWidth * layoutScale);
            RectTransform textRect = creditsText.rectTransform;
            textRect.anchorMin = new Vector2(0.5f, 0f);
            textRect.anchorMax = new Vector2(0.5f, 1f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = new Vector2(textWidth, 0f);

            creditImagesRoot.anchorMin = Vector2.zero;
            creditImagesRoot.anchorMax = Vector2.one;
            creditImagesRoot.pivot = new Vector2(0.5f, 0.5f);
            creditImagesRoot.anchoredPosition = Vector2.zero;
            creditImagesRoot.sizeDelta = Vector2.zero;
            return layoutScale;
        }

        private void LayoutCreditPhotos(float layoutScale)
        {
            TMP_TextInfo textInfo = creditsText.textInfo;
            string renderedCharacters = CreateRenderedCharacterString(textInfo);
            float viewportWidth = Mathf.Max(1f, creditsViewport.rect.width);
            float textWidth = creditsText.rectTransform.rect.width;
            float scaledGap = data.PhotoGap * layoutScale;
            float sideWidth = Mathf.Max(
                0f,
                (viewportWidth - textWidth) * 0.5f -
                scaledGap -
                ViewportEdgePadding * layoutScale);

            IReadOnlyList<EndingCreditPhotoData> photos = data.Photos;
            for (int index = 0; photos != null && index < photos.Count; index++)
            {
                EndingCreditPhotoData entry = photos[index];
                Image image = index < generatedPhotoImages.Count ? generatedPhotoImages[index] : null;
                if (entry == null || image == null || image.sprite == null)
                {
                    continue;
                }

                string anchor = entry.AnchorText != null ? entry.AnchorText.Trim() : string.Empty;
                int characterIndex = FindAnchorCharacterIndex(
                    renderedCharacters,
                    anchor,
                    entry.AnchorOccurrence);

                if (characterIndex < 0 || characterIndex >= textInfo.characterCount)
                {
                    image.gameObject.SetActive(false);
                    Debug.LogWarning(
                        $"Credit photo #{index + 1} cannot find anchor '{anchor}' occurrence {entry.AnchorOccurrence}.",
                        data);
                    continue;
                }

                int lineNumber = textInfo.characterInfo[characterIndex].lineNumber;
                if (lineNumber < 0 || lineNumber >= textInfo.lineCount)
                {
                    image.gameObject.SetActive(false);
                    continue;
                }

                Vector2 desiredSize = entry.DisplaySize * layoutScale;
                float additionalScale = desiredSize.x <= 0f
                    ? 1f
                    : Mathf.Min(1f, sideWidth / desiredSize.x);
                Vector2 finalSize = desiredSize * additionalScale;

                RectTransform imageRect = image.rectTransform;
                imageRect.anchorMin = new Vector2(0.5f, 0.5f);
                imageRect.anchorMax = new Vector2(0.5f, 0.5f);
                imageRect.pivot = new Vector2(0.5f, 0.5f);
                imageRect.localScale = Vector3.one;
                imageRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, finalSize.x);
                imageRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, finalSize.y);

                TMP_LineInfo line = textInfo.lineInfo[lineNumber];
                float lineCenterY = (line.ascender + line.descender) * 0.5f;
                Vector3 worldLineCenter = creditsText.rectTransform.TransformPoint(
                    new Vector3(0f, lineCenterY, 0f));
                float localY = creditImagesRoot.InverseTransformPoint(worldLineCenter).y +
                               entry.VerticalOffset;
                float horizontalOffset =
                    textWidth * 0.5f + scaledGap + finalSize.x * 0.5f;
                float side = index % 2 == 0 ? -1f : 1f;
                imageRect.anchoredPosition = new Vector2(horizontalOffset * side, localY);
            }
        }

        private void SetEndActionsState(float alpha, bool acceptsInput)
        {
            if (endActions == null)
            {
                return;
            }

            endActions.alpha = alpha;
            endActions.interactable = acceptsInput;
            endActions.blocksRaycasts = acceptsInput;
        }

        private static string CreateRenderedCharacterString(TMP_TextInfo textInfo)
        {
            StringBuilder rendered = new StringBuilder(textInfo.characterCount);
            for (int index = 0; index < textInfo.characterCount; index++)
            {
                rendered.Append(textInfo.characterInfo[index].character);
            }

            return rendered.ToString();
        }

        private static int FindAnchorCharacterIndex(
            string renderedCharacters,
            string anchorText,
            int occurrence)
        {
            if (string.IsNullOrEmpty(anchorText))
            {
                return -1;
            }

            int searchStart = 0;
            for (int matchIndex = 1; matchIndex <= Mathf.Max(1, occurrence); matchIndex++)
            {
                int found = renderedCharacters.IndexOf(
                    anchorText,
                    searchStart,
                    System.StringComparison.Ordinal);
                if (found < 0)
                {
                    return -1;
                }

                if (matchIndex == occurrence)
                {
                    return found;
                }

                searchStart = found + anchorText.Length;
            }

            return -1;
        }

        private static bool IsUppercaseHeading(string value)
        {
            bool containsEnglishLetter = false;
            foreach (char character in value)
            {
                if (character >= 'a' && character <= 'z')
                {
                    return false;
                }

                if (character >= 'A' && character <= 'Z')
                {
                    containsEnglishLetter = true;
                }
            }

            return containsEnglishLetter;
        }

        private static string EscapeRichText(string value)
        {
            return (value ?? string.Empty)
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }

        private void InitializeCancelInput()
        {
            useCancelFallback = false;

#if ENABLE_INPUT_SYSTEM
            cancelAction = null;
            cancelActionEnabledByThisComponent = false;

            if (inputActions != null && string.IsNullOrWhiteSpace(cancelActionPath) == false)
            {
                cancelAction = inputActions.FindAction(cancelActionPath, false);
            }

            if (cancelAction != null)
            {
                if (cancelAction.enabled == false)
                {
                    cancelAction.Enable();
                    cancelActionEnabledByThisComponent = true;
                }

                RefreshCancelHint();
                return;
            }
#endif

            useCancelFallback = true;
            RefreshCancelHint();
            WarnAboutCancelFallback();
        }

        private void RefreshCancelHint()
        {
            if (cancelHintText == null)
            {
                return;
            }

            string bindingDisplay = "ESC";
#if ENABLE_INPUT_SYSTEM
            if (cancelAction != null)
            {
                string resolvedDisplay =
                    cancelAction.GetBindingDisplayString(
                        InputBinding.MaskByGroup("Keyboard&Mouse"),
                        InputBinding.DisplayStringOptions
                            .DontIncludeInteractions);
                if (string.IsNullOrWhiteSpace(resolvedDisplay) == false)
                {
                    bindingDisplay = resolvedDisplay;
                }
            }
#endif

            cancelHintText.SetText($"{bindingDisplay}: 엔딩 나가기");
        }

        private void ReleaseCancelInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (cancelActionEnabledByThisComponent &&
                cancelAction != null &&
                cancelAction.enabled)
            {
                cancelAction.Disable();
            }

            cancelAction = null;
            cancelActionEnabledByThisComponent = false;
#endif

            useCancelFallback = false;
        }

        private bool WasCancelPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (cancelAction != null)
            {
                return cancelAction.WasPressedThisFrame();
            }
#endif

            return useCancelFallback && WasFallbackEscapePressed();
        }

        private void WarnAboutCancelFallback()
        {
            if (hasWarnedAboutCancelFallback)
            {
                return;
            }

            hasWarnedAboutCancelFallback = true;

#if ENABLE_INPUT_SYSTEM
            string reason = inputActions == null
                ? $"no {nameof(InputActionAsset)} is assigned"
                : $"action '{cancelActionPath}' was not found in '{inputActions.name}'";
            Debug.LogWarning(
                $"{nameof(EndingCreditsPlayer)} on '{name}' has {reason}. " +
                "Ending cancel temporarily falls back to the Escape key.",
                this);
#else
            Debug.LogWarning(
                $"{nameof(EndingCreditsPlayer)} on '{name}' cannot use the Input System. " +
                "Ending cancel temporarily falls back to the Escape key.",
                this);
#endif
        }

        private static bool WasFallbackEscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#else
            return false;
#endif
        }

        private static void AddIfMissing(
            Object value,
            string fieldName,
            ICollection<string> missing)
        {
            if (value == null)
            {
                missing.Add(fieldName);
            }
        }
    }
}
