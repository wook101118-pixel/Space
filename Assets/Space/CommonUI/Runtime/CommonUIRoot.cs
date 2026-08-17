using SpaceGame.CommonUI.Audio;
using SpaceGame.CommonUI.Display;
using SpaceGame.CommonUI.Input;
using SpaceGame.CommonUI.Modal;
using SpaceGame.CommonUI.Pause;
using SpaceGame.CommonUI.Settings;
using SpaceGame.CommonUI.Tutorial;
using SpaceGame.CommonUI.Views;
using UnityEngine;

namespace SpaceGame.CommonUI
{
    [DefaultExecutionOrder(-32000)]
    [DisallowMultipleComponent]
    public sealed class CommonUIRoot : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private InputBindingCatalog inputBindingCatalog;
        [SerializeField] private TutorialSequenceData tutorialSequence;

        [Header("Services")]
        [SerializeField] private AudioMixerSettingsAdapter audioAdapter;
        [SerializeField] private ScreenSettingsApplier screenApplier;
        [SerializeField] private PauseRequestService pauseService;
        [SerializeField] private ModalCancelRouter cancelRouter;
        [SerializeField] private ModalInputGate inputGate;

        [Header("Views")]
        [SerializeField] private SettingsWindow settingsWindow;
        [SerializeField] private KeyInfoWindow keyInfoWindow;
        [SerializeField] private TutorialPanel tutorialPanel;

        private CommonUIContext context;

        public CommonUIContext Context => context;
        public SettingsWindow SettingsWindow => settingsWindow;
        public KeyInfoWindow KeyInfoWindow => keyInfoWindow;
        public TutorialPanel TutorialPanel => tutorialPanel;

        public void Configure(
            InputBindingCatalog bindingCatalog,
            TutorialSequenceData sequence,
            AudioMixerSettingsAdapter audio,
            ScreenSettingsApplier screen,
            PauseRequestService pause,
            ModalCancelRouter router,
            ModalInputGate gate,
            SettingsWindow settings,
            KeyInfoWindow keyInfo,
            TutorialPanel tutorial)
        {
            inputBindingCatalog = bindingCatalog;
            tutorialSequence = sequence;
            audioAdapter = audio;
            screenApplier = screen;
            pauseService = pause;
            cancelRouter = router;
            inputGate = gate;
            settingsWindow = settings;
            keyInfoWindow = keyInfo;
            tutorialPanel = tutorial;
        }

        private void Awake()
        {
            transform.SetAsLastSibling();
            string cancelFallbackPath =
                inputBindingCatalog.ForbiddenControlPaths.Count > 0
                    ? inputBindingCatalog.ForbiddenControlPaths[0]
                    : string.Empty;
            cancelRouter.Configure(
                inputBindingCatalog.CancelAction,
                cancelFallbackPath);
            inputGate.Configure(
                inputBindingCatalog.ActionAsset,
                inputBindingCatalog.GameplayActionMapIds);

            var bindingRepository =
                new PlayerPrefsInputBindingOverrideRepository();
            string storedBindingOverrides = bindingRepository.LoadJson();
            string normalizedBindingOverrides =
                InputBindingOverrideUtility.RestoreAndNormalize(
                inputBindingCatalog,
                storedBindingOverrides);
            if (storedBindingOverrides != normalizedBindingOverrides)
            {
                bindingRepository.SaveJson(normalizedBindingOverrides);
            }

            var settingsCoordinator = new SettingsCoordinator(
                new PlayerPrefsSettingsRepository(),
                audioAdapter,
                screenApplier);
            settingsCoordinator.LoadAndApply();

            context = new CommonUIContext(
                settingsCoordinator,
                bindingRepository,
                inputBindingCatalog,
                screenApplier,
                pauseService,
                cancelRouter,
                inputGate);

            settingsWindow.Initialize(context);
            keyInfoWindow.Initialize(context);
            DisableTutorialFeature();
        }

        public void OpenSettings()
        {
            settingsWindow.Open();
        }

        public void OpenKeyInfo()
        {
            keyInfoWindow.Open();
        }

        public void OpenTutorial()
        {
            DisableTutorialFeature();
        }

        private void DisableTutorialFeature()
        {
            if (tutorialPanel != null)
            {
                tutorialPanel.gameObject.SetActive(false);
            }
        }
    }
}
