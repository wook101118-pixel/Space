using SpaceGame.CommonUI.Settings;
using SpaceGame.CommonUI.Tutorial;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceGame.CommonUI.Views
{
    [DisallowMultipleComponent]
    public sealed class TutorialPanel : ModalWindowBase
    {
        private const string SeenKeyPrefix =
            PlayerPrefsSettingsRepository.Prefix + "TutorialSeen.";

        [Header("Content")]
        [SerializeField] private TutorialSequenceData sequence;
        [SerializeField] private Image pageImage;
        [SerializeField] private GameObject placeholder;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private TMP_Text pageCounterText;

        [Header("Actions")]
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button startButton;
        [SerializeField] private Button closeButton;

        [Header("Start policy")]
        [SerializeField] private bool showOnStart;
        [SerializeField] private bool showOnlyFirstRun;
        [SerializeField] private bool escapeCloses = true;

        private int pageIndex;

        protected override bool HandlesCancel => escapeCloses;

        public void ConfigureView(
            TutorialSequenceData tutorialSequence,
            Image image,
            GameObject missingImagePlaceholder,
            TMP_Text title,
            TMP_Text body,
            TMP_Text counter,
            Button previous,
            Button next,
            Button start,
            Button close,
            bool openOnStart,
            bool firstRunOnly,
            bool allowEscape)
        {
            sequence = tutorialSequence;
            pageImage = image;
            placeholder = missingImagePlaceholder;
            titleText = title;
            bodyText = body;
            pageCounterText = counter;
            previousButton = previous;
            nextButton = next;
            startButton = start;
            closeButton = close;
            showOnStart = openOnStart;
            showOnlyFirstRun = firstRunOnly;
            escapeCloses = allowEscape;
        }

        public void TryOpenOnStart()
        {
            if (!showOnStart)
            {
                return;
            }

            if (showOnlyFirstRun &&
                PlayerPrefs.GetInt(GetSeenKey(), 0) != 0)
            {
                return;
            }

            Open();
        }

        public override void Open()
        {
            pageIndex = 0;
            base.Open();
        }

        protected override bool CanOpen()
        {
            return sequence != null && sequence.Pages.Count > 0;
        }

        protected override void OnInitialized()
        {
            previousButton.onClick.AddListener(ShowPrevious);
            nextButton.onClick.AddListener(ShowNext);
            startButton.onClick.AddListener(RequestClose);
            closeButton.onClick.AddListener(RequestClose);
        }

        protected override void OnOpened()
        {
            RenderPage();
        }

        protected override void OnClosed()
        {
            if (showOnlyFirstRun)
            {
                PlayerPrefs.SetInt(GetSeenKey(), 1);
                PlayerPrefs.Save();
            }
        }

        private void ShowPrevious()
        {
            pageIndex = Mathf.Max(0, pageIndex - 1);
            RenderPage();
        }

        private void ShowNext()
        {
            pageIndex = Mathf.Min(sequence.Pages.Count - 1, pageIndex + 1);
            RenderPage();
        }

        private void RenderPage()
        {
            int pageCount = sequence.Pages.Count;
            pageIndex = Mathf.Clamp(pageIndex, 0, pageCount - 1);
            TutorialPageData page = sequence.Pages[pageIndex];

            pageImage.sprite = page.Image;
            pageImage.enabled = page.Image != null;
            placeholder.SetActive(page.Image == null);
            if (page.Image == null)
            {
                TMP_Text visualHint = placeholder.GetComponentInChildren<TMP_Text>(true);
                if (visualHint != null)
                {
                    visualHint.text = string.IsNullOrWhiteSpace(page.VisualHint)
                        ? page.Title
                        : page.VisualHint;
                }
            }
            titleText.text = page.Title;
            bodyText.text = page.Body;
            pageCounterText.text = $"{pageIndex + 1} / {pageCount}";
            previousButton.gameObject.SetActive(pageIndex > 0);

            bool isLast = pageIndex == pageCount - 1;
            nextButton.gameObject.SetActive(!isLast);
            startButton.gameObject.SetActive(isLast);
        }

        private string GetSeenKey()
        {
            string sequenceId = sequence == null
                ? "missing"
                : sequence.SequenceId;
            return SeenKeyPrefix + sequenceId;
        }
    }
}
