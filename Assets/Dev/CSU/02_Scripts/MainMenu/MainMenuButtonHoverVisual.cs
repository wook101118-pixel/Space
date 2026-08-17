using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Dev.CSU._02_Scripts.MainMenu
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class MainMenuButtonHoverVisual :
        MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        ISelectHandler,
        IDeselectHandler
    {
        [Header("References")]
        [SerializeField] private Button targetButton;
        [SerializeField] private Image glowImage;

        [Header("Interaction")]
        [SerializeField] private bool showOnSelection = true;

        [Header("Appearance")]
        [SerializeField] private Color glowColor =
            new Color(0.1f, 0.92f, 0.96f, 0.78f);
        [Min(1f)]
        [SerializeField] private float buttonHoverScale = 1.015f;
        [Range(0f, 0.08f)]
        [SerializeField] private float pulseScale = 0.018f;
        [Range(0f, 0.35f)]
        [SerializeField] private float pulseBrightness = 0.12f;

        [Header("Timing (Unscaled)")]
        [Min(0.01f)]
        [SerializeField] private float enterDuration = 0.2f;
        [Min(0.01f)]
        [SerializeField] private float exitDuration = 0.2f;
        [Min(0.1f)]
        [SerializeField] private float pulsePeriod = 1.5f;

        private RectTransform _buttonRect;
        private RectTransform _glowRect;
        private Vector3 _buttonBaseScale = Vector3.one;
        private Vector3 _glowBaseScale = Vector3.one;
        private bool _pointerInside;
        private bool _navigationSelected;
        private bool _hasCapturedScales;
        private float _visibility;
        private float _pulseTime;

        public Color GlowColor => glowColor;
        public float EnterDuration => enterDuration;
        public float ExitDuration => exitDuration;
        public float PulsePeriod => pulsePeriod;
        public bool ShowOnSelection => showOnSelection;

        public void Configure(
            Button button,
            Image image,
            Color color,
            bool selectionShowsVisual = true)
        {
            targetButton = button;
            glowImage = image;
            glowColor = color;
            showOnSelection = selectionShowsVisual;
            if (!showOnSelection)
            {
                _navigationSelected = false;
            }

            CacheReferences();

            if (!Application.isPlaying)
            {
                ApplyImmediateHiddenState();
            }
        }

        private void Awake()
        {
            CacheReferences();
            CaptureBaseScales();
            ApplyImmediateHiddenState();
        }

        private void OnEnable()
        {
            CacheReferences();
            CaptureBaseScales();
            _pointerInside = false;
            _navigationSelected = false;
            _visibility = 0f;
            _pulseTime = 0f;
            ApplyVisuals(0f, 0f);
        }

        private void Update()
        {
            bool shouldShow = IsInteractionActive();
            float targetVisibility = shouldShow ? 1f : 0f;
            float duration = shouldShow
                ? enterDuration
                : exitDuration;
            float step = Time.unscaledDeltaTime
                / Mathf.Max(0.01f, duration);
            _visibility = Mathf.MoveTowards(
                _visibility,
                targetVisibility,
                step);

            if (shouldShow)
            {
                _pulseTime += Time.unscaledDeltaTime;
            }
            else if (_visibility <= 0f)
            {
                _pulseTime = 0f;
            }

            float easedVisibility = SmoothStep01(_visibility);
            float pulse = CalculatePulse(shouldShow);
            ApplyVisuals(easedVisibility, pulse);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _pointerInside = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _pointerInside = false;
        }

        public void OnSelect(BaseEventData eventData)
        {
            _navigationSelected =
                showOnSelection
                && !(eventData is PointerEventData);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _navigationSelected = false;
        }

        private void OnDisable()
        {
            _pointerInside = false;
            _navigationSelected = false;
            _visibility = 0f;
            _pulseTime = 0f;
            RestoreBaseScales();
            ApplyImmediateHiddenState();
        }

        private void OnDestroy()
        {
            RestoreBaseScales();
        }

        private bool IsInteractionActive()
        {
            return targetButton != null
                && targetButton.isActiveAndEnabled
                && targetButton.IsInteractable()
                && (_pointerInside
                    || (showOnSelection && _navigationSelected));
        }

        private float CalculatePulse(bool shouldShow)
        {
            if (!shouldShow || _visibility <= 0f)
            {
                return 0f;
            }

            float normalizedTime =
                _pulseTime / Mathf.Max(0.1f, pulsePeriod);
            return 0.5f
                + 0.5f * Mathf.Sin(
                    normalizedTime * Mathf.PI * 2f);
        }

        private void ApplyVisuals(
            float easedVisibility,
            float pulse)
        {
            if (glowImage != null)
            {
                float brightnessMultiplier =
                    1f + pulse * pulseBrightness;
                Color animatedColor = glowColor;
                animatedColor.r *= brightnessMultiplier;
                animatedColor.g *= brightnessMultiplier;
                animatedColor.b *= brightnessMultiplier;
                animatedColor.a =
                    glowColor.a * easedVisibility;
                glowImage.color = animatedColor;
            }

            if (_buttonRect != null)
            {
                float scale = Mathf.Lerp(
                    1f,
                    buttonHoverScale,
                    easedVisibility);
                _buttonRect.localScale =
                    Vector3.Scale(
                        _buttonBaseScale,
                        Vector3.one * scale);
            }

            if (_glowRect != null)
            {
                float scale = 1f
                    + pulse
                    * pulseScale
                    * easedVisibility;
                _glowRect.localScale =
                    Vector3.Scale(
                        _glowBaseScale,
                        Vector3.one * scale);
            }
        }

        private void CacheReferences()
        {
            if (targetButton == null)
            {
                targetButton = GetComponent<Button>();
            }

            _buttonRect = transform as RectTransform;
            _glowRect = glowImage != null
                ? glowImage.rectTransform
                : null;
        }

        private void CaptureBaseScales()
        {
            if (_hasCapturedScales)
            {
                return;
            }

            if (_buttonRect != null)
            {
                _buttonBaseScale = _buttonRect.localScale;
            }

            if (_glowRect != null)
            {
                _glowBaseScale = _glowRect.localScale;
            }

            _hasCapturedScales = true;
        }

        private void RestoreBaseScales()
        {
            if (!_hasCapturedScales)
            {
                return;
            }

            if (_buttonRect != null)
            {
                _buttonRect.localScale = _buttonBaseScale;
            }

            if (_glowRect != null)
            {
                _glowRect.localScale = _glowBaseScale;
            }
        }

        private void ApplyImmediateHiddenState()
        {
            if (glowImage == null)
            {
                return;
            }

            Color hiddenColor = glowColor;
            hiddenColor.a = 0f;
            glowImage.color = hiddenColor;
        }

        private static float SmoothStep01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private void OnValidate()
        {
            buttonHoverScale = Mathf.Max(1f, buttonHoverScale);
            pulseScale = Mathf.Clamp(pulseScale, 0f, 0.08f);
            pulseBrightness =
                Mathf.Clamp(pulseBrightness, 0f, 0.35f);
            enterDuration = Mathf.Max(0.01f, enterDuration);
            exitDuration = Mathf.Max(0.01f, exitDuration);
            pulsePeriod = Mathf.Max(0.1f, pulsePeriod);
            if (!showOnSelection)
            {
                _navigationSelected = false;
            }

            CacheReferences();

            if (!Application.isPlaying)
            {
                ApplyImmediateHiddenState();
            }
        }
    }
}
