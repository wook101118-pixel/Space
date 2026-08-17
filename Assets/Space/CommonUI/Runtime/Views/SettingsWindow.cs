using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SpaceGame.CommonUI.Display;
using SpaceGame.CommonUI.Input;
using SpaceGame.CommonUI.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SpaceGame.CommonUI.Views
{
    [DisallowMultipleComponent]
    public sealed class SettingsWindow : ModalWindowBase
    {
        // TMP_Dropdown creates its popup canvas at sorting order 30000.
        // Keep the modal above normal UI while leaving its option list visible.
        private const int TopmostSortingOrder = 29000;

        [Header("Audio")]
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Slider uiSlider;
        [SerializeField] private Slider ambienceSlider;

        [Header("Display")]
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private TMP_Dropdown resolutionDropdown;

        [Header("Bindings")]
        [SerializeField] private RectTransform bindingContent;
        [SerializeField] private InputBindingRowView bindingRowPrefab;
        [SerializeField] private Button resetAllBindingsButton;
        [SerializeField] private TMP_Text statusText;

        [Header("Actions")]
        [SerializeField] private Button applyButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button restoreDefaultsButton;
        [SerializeField] private Button closeButton;

        private readonly List<InputBindingRowView> bindingRows =
            new List<InputBindingRowView>();
        private readonly List<ResolutionOption> resolutions =
            new List<ResolutionOption>();
        private GameSettingsData snapshot;
        private GameSettingsData workingCopy;
        private string bindingSnapshotJson;
        private bool suppressControlEvents;
        private Coroutine bindingRefreshRoutine;
        private InputBindingRowView activeRebindRow;

        public void ConfigureView(
            Slider master,
            Slider bgm,
            Slider sfx,
            Slider ui,
            Slider ambience,
            Toggle fullscreen,
            TMP_Dropdown resolution,
            RectTransform bindingsRoot,
            InputBindingRowView rowPrefab,
            Button resetBindings,
            TMP_Text status,
            Button apply,
            Button cancel,
            Button defaults,
            Button close)
        {
            masterSlider = master;
            bgmSlider = bgm;
            sfxSlider = sfx;
            uiSlider = ui;
            ambienceSlider = ambience;
            fullscreenToggle = fullscreen;
            resolutionDropdown = resolution;
            bindingContent = bindingsRoot;
            bindingRowPrefab = rowPrefab;
            resetAllBindingsButton = resetBindings;
            statusText = status;
            applyButton = apply;
            cancelButton = cancel;
            restoreDefaultsButton = defaults;
            closeButton = close;
        }

        protected override void OnInitialized()
        {
            EnsureTopmostLayer();
            masterSlider.onValueChanged.AddListener(OnAudioChanged);
            bgmSlider.onValueChanged.AddListener(OnAudioChanged);
            sfxSlider.onValueChanged.AddListener(OnAudioChanged);
            uiSlider.onValueChanged.AddListener(OnAudioChanged);
            ambienceSlider.onValueChanged.AddListener(OnAudioChanged);
            fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
            resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
            applyButton.onClick.AddListener(ApplyAndClose);
            cancelButton.onClick.AddListener(RequestClose);
            closeButton.onClick.AddListener(RequestClose);
            restoreDefaultsButton.onClick.AddListener(RestoreDefaults);
            resetAllBindingsButton.onClick.AddListener(RestoreAllBindings);
            BuildBindingRows();
        }

        protected override void OnOpened()
        {
            EnsureTopmostLayer();
            snapshot = Context.Settings.BeginEdit();
            CaptureCurrentDisplay(snapshot);
            workingCopy = snapshot.Clone();
            bindingSnapshotJson =
                Context.BindingCatalog.ActionAsset.SaveBindingOverridesAsJson();
            PopulateResolutionOptions();
            PushDataToControls();
            RefreshBindingRows();
            SetStatus(string.Empty);
        }

        private void EnsureTopmostLayer()
        {
            transform.SetAsLastSibling();

            Canvas topmostCanvas = GetComponent<Canvas>();
            if (topmostCanvas == null)
            {
                topmostCanvas = gameObject.AddComponent<Canvas>();
            }

            topmostCanvas.overrideSorting = true;
            topmostCanvas.sortingOrder = TopmostSortingOrder;

            if (!TryGetComponent<GraphicRaycaster>(out _))
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        protected override void OnCloseRequested()
        {
            CancelRebindIfNeeded();
            Context.Settings.RestorePreview(snapshot);
            InputBindingOverrideUtility.Restore(
                Context.BindingCatalog,
                bindingSnapshotJson);
            CloseDirect();
        }

        private void ApplyAndClose()
        {
            if (CancelRebindIfNeeded())
            {
                SetStatus("진행 중인 키 변경을 먼저 취소했습니다.");
                return;
            }

            PullControlsToData();
            Context.Settings.Commit(workingCopy);
            string json =
                Context.BindingCatalog.ActionAsset.SaveBindingOverridesAsJson();
            Context.BindingRepository.SaveJson(json);
            CloseDirect();
        }

        private void RestoreDefaults()
        {
            CancelRebindIfNeeded();
            workingCopy = GameSettingsData.CreateDefault();
            PushDataToControls();
            Context.Settings.PreviewAudio(workingCopy);
            Context.BindingCatalog.RemoveAllOverrides();
            RefreshBindingRows();
            SetStatus(
                "화면, 사운드와 모든 키를 기본값으로 복원했습니다. " +
                "적용을 눌러 저장하세요.");
        }

        private void RestoreAllBindings()
        {
            CancelRebindIfNeeded();
            workingCopy = GameSettingsData.CreateDefault();
            PushDataToControls();
            Context.Settings.PreviewAudio(workingCopy);
            Context.BindingCatalog.RemoveAllOverrides();
            RefreshBindingRows();
            SetStatus(
                "화면, 사운드와 모든 키를 초기화했습니다. " +
                "적용을 눌러 저장하세요.");
        }

        private void BuildBindingRows()
        {
            if (bindingRowPrefab == null || bindingContent == null)
            {
                return;
            }

            foreach (InputBindingDefinition definition in
                     Context.BindingCatalog.Bindings)
            {
                InputBindingRowView row = Instantiate(
                    bindingRowPrefab,
                    bindingContent);
                row.gameObject.SetActive(true);
                row.Initialize(
                    definition,
                    Context.BindingCatalog,
                    Context.CancelRouter,
                    SetStatus,
                    HandleRebindStarting,
                    HandleRebindEnded);
                bindingRows.Add(row);
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(bindingContent);
            bindingContent.anchoredPosition =
                new Vector2(bindingContent.anchoredPosition.x, 0f);
            ScheduleBindingRefresh();
        }

        private void PopulateResolutionOptions()
        {
            resolutions.Clear();
            resolutions.AddRange(
                Context.ScreenApplier.GetAvailableResolutions());
            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(
                resolutions.Select(option => option.ToString()).ToList());
        }

        private void PushDataToControls()
        {
            suppressControlEvents = true;
            masterSlider.SetValueWithoutNotify(workingCopy.masterVolume);
            bgmSlider.SetValueWithoutNotify(workingCopy.bgmVolume);
            sfxSlider.SetValueWithoutNotify(workingCopy.sfxVolume);
            uiSlider.SetValueWithoutNotify(workingCopy.uiVolume);
            ambienceSlider.SetValueWithoutNotify(workingCopy.ambienceVolume);
            fullscreenToggle.SetIsOnWithoutNotify(workingCopy.fullscreen);

            int resolutionIndex = FindClosestResolutionIndex(workingCopy);
            resolutionDropdown.SetValueWithoutNotify(resolutionIndex);
            ApplyResolutionToWorkingCopy(resolutionIndex);
            suppressControlEvents = false;
        }

        private void PullControlsToData()
        {
            workingCopy.masterVolume = masterSlider.value;
            workingCopy.bgmVolume = bgmSlider.value;
            workingCopy.sfxVolume = sfxSlider.value;
            workingCopy.uiVolume = uiSlider.value;
            workingCopy.ambienceVolume = ambienceSlider.value;
            workingCopy.fullscreen = fullscreenToggle.isOn;
            ApplyResolutionToWorkingCopy(resolutionDropdown.value);
        }

        private void OnAudioChanged(float ignored)
        {
            if (suppressControlEvents || workingCopy == null)
            {
                return;
            }

            PullControlsToData();
            Context.Settings.PreviewAudio(workingCopy);
        }

        private void OnFullscreenChanged(bool value)
        {
            if (!suppressControlEvents && workingCopy != null)
            {
                workingCopy.fullscreen = value;
            }
        }

        private void OnResolutionChanged(int index)
        {
            if (!suppressControlEvents && workingCopy != null)
            {
                ApplyResolutionToWorkingCopy(index);
            }
        }

        private void ApplyResolutionToWorkingCopy(int index)
        {
            if (resolutions.Count == 0)
            {
                return;
            }

            ResolutionOption option =
                resolutions[Mathf.Clamp(index, 0, resolutions.Count - 1)];
            workingCopy.resolutionWidth = option.width;
            workingCopy.resolutionHeight = option.height;
            workingCopy.refreshRateNumerator =
                option.refreshRateNumerator;
            workingCopy.refreshRateDenominator =
                option.refreshRateDenominator;
        }

        private int FindClosestResolutionIndex(GameSettingsData settings)
        {
            if (resolutions.Count == 0)
            {
                return 0;
            }

            int exact = resolutions.FindIndex(option =>
                option.width == settings.resolutionWidth &&
                option.height == settings.resolutionHeight &&
                (settings.refreshRateNumerator == 0 ||
                 option.refreshRateNumerator ==
                 settings.refreshRateNumerator));
            if (exact >= 0)
            {
                return exact;
            }

            int bestIndex = 0;
            long bestDistance = long.MaxValue;
            for (int index = 0; index < resolutions.Count; index++)
            {
                long widthDelta =
                    resolutions[index].width - settings.resolutionWidth;
                long heightDelta =
                    resolutions[index].height - settings.resolutionHeight;
                long distance =
                    widthDelta * widthDelta + heightDelta * heightDelta;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = index;
                }
            }

            return bestIndex;
        }

        private bool CancelRebindIfNeeded()
        {
            bool cancelledAny = false;
            foreach (InputBindingRowView row in bindingRows)
            {
                if (row.CancelRebind())
                {
                    cancelledAny = true;
                }
            }

            return cancelledAny;
        }

        private void HandleRebindStarting(InputBindingRowView requester)
        {
            if (activeRebindRow == requester)
            {
                return;
            }

            activeRebindRow?.CancelRebind();
            activeRebindRow = requester;
        }

        private void HandleRebindEnded(InputBindingRowView sender)
        {
            if (activeRebindRow == sender)
            {
                activeRebindRow = null;
            }
        }

        private void RefreshBindingRows()
        {
            foreach (InputBindingRowView row in bindingRows)
            {
                row.Refresh();
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(bindingContent);
            ScheduleBindingRefresh();
        }

        private void ScheduleBindingRefresh()
        {
            if (bindingRefreshRoutine != null)
            {
                StopCoroutine(bindingRefreshRoutine);
            }

            if (isActiveAndEnabled)
            {
                bindingRefreshRoutine =
                    StartCoroutine(RefreshBindingsAfterLayout());
            }
        }

        private IEnumerator RefreshBindingsAfterLayout()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(bindingContent);
            foreach (InputBindingRowView row in bindingRows)
            {
                row.Refresh();
            }

            Canvas.ForceUpdateCanvases();
            bindingRefreshRoutine = null;
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message ?? string.Empty;
            }
        }

        private static void CaptureCurrentDisplay(GameSettingsData settings)
        {
            settings.fullscreen = Screen.fullScreen;
            CaptureCurrentResolution(settings);
        }

        private static void CaptureCurrentResolution(GameSettingsData settings)
        {
            settings.resolutionWidth = Mathf.Max(320, Screen.width);
            settings.resolutionHeight = Mathf.Max(200, Screen.height);
            Resolution current = Screen.currentResolution;
            settings.refreshRateNumerator =
                (int)current.refreshRateRatio.numerator;
            settings.refreshRateDenominator =
                Mathf.Max(1, (int)current.refreshRateRatio.denominator);
        }

        protected override void OnDisable()
        {
            if (bindingRefreshRoutine != null)
            {
                StopCoroutine(bindingRefreshRoutine);
                bindingRefreshRoutine = null;
            }

            CancelRebindIfNeeded();
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            masterSlider?.onValueChanged.RemoveListener(OnAudioChanged);
            bgmSlider?.onValueChanged.RemoveListener(OnAudioChanged);
            sfxSlider?.onValueChanged.RemoveListener(OnAudioChanged);
            uiSlider?.onValueChanged.RemoveListener(OnAudioChanged);
            ambienceSlider?.onValueChanged.RemoveListener(OnAudioChanged);
            fullscreenToggle?.onValueChanged.RemoveListener(
                OnFullscreenChanged);
            resolutionDropdown?.onValueChanged.RemoveListener(
                OnResolutionChanged);
            applyButton?.onClick.RemoveListener(ApplyAndClose);
            cancelButton?.onClick.RemoveListener(RequestClose);
            closeButton?.onClick.RemoveListener(RequestClose);
            restoreDefaultsButton?.onClick.RemoveListener(RestoreDefaults);
            resetAllBindingsButton?.onClick.RemoveListener(
                RestoreAllBindings);
            base.OnDestroy();
        }
    }

}
