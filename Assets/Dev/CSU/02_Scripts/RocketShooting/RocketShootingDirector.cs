using System;
using Dev.NKY.Scripts;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Dev.CSU._02_Scripts.RocketShooting
{
    public enum LaunchPhase
    {
        Idle,
        Ignition,
        LiftOff,
        Cruise
    }

    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class RocketShootingDirector : MonoBehaviour
    {
        private const int MaxPhaseStepsPerFrame = 4;
        private const float MaxAccelerationIntegrationStep = 0.25f;

        [Header("References")]
        [Tooltip("Rocket presentation controlled by this launch sequence.")]
        [SerializeField] private RocketView rocketView;

        [Tooltip("Vertical world scroller driven by the launch speed.")]
        [SerializeField] private VerticalBackgroundScroller backgroundScroller;

        [Tooltip("Existing upgrade/inventory panel closed when a launch begins.")]
        [SerializeField] private InventoryPanelController upgradePanel;

        [Tooltip("Upgrade/inventory visual root hidden immediately when a launch begins.")]
        [SerializeField] private GameObject upgradePanelRoot;

        [Header("Sequence")]
        [Tooltip("Starts the launch automatically when the scene begins.")]
        [SerializeField] private bool autoStart;

        [Tooltip("Time spent playing the ignition presentation before lift-off.")]
        [Min(0f)]
        [SerializeField] private float ignitionDuration = 0.75f;

        [Tooltip("Time taken for the rocket to move from Launch Start to Cruise Anchor.")]
        [Min(0f)]
        [SerializeField] private float liftOffDuration = 2f;

        [Header("Scroll Acceleration")]
        [Tooltip("Background speed, in world units per second, when lift-off begins.")]
        [Min(0f)]
        [SerializeField] private float startScrollSpeed = 6f;

        [Tooltip("Maximum background speed reached after the acceleration duration.")]
        [Min(0f)]
        [FormerlySerializedAs("cruiseSpeed")]
        [SerializeField] private float maximumScrollSpeed = 18f;

        [Tooltip("Seconds of active LiftOff and Cruise time required to reach maximum speed.")]
        [Min(0.01f)]
        [SerializeField] private float accelerationDuration = 12f;

        [Tooltip("Normalized scroll-speed progression from the start speed to the maximum speed.")]
        [FormerlySerializedAs("speedCurve")]
        [SerializeField] private AnimationCurve accelerationCurve =
            CreateDefaultAccelerationCurve();

        [Tooltip("Restarts acceleration when changing between active scrolling phases. Keep disabled for seamless LiftOff-to-Cruise acceleration.")]
        [SerializeField] private bool resetAccelerationOnPhaseChange;

        [Header("Rocket Motion")]
        [Tooltip("Normalized vertical rocket movement during lift-off.")]
        [SerializeField] private AnimationCurve liftCurve =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private float _phaseElapsed;
        private float _accelerationElapsed;
        private float _currentScrollSpeed;
        private bool _launchStarted;
        private bool _warnedInvalidConfiguration;

        public LaunchPhase Phase { get; private set; } = LaunchPhase.Idle;
        public double Altitude { get; private set; }
        public float CurrentScrollSpeed => _currentScrollSpeed;
        public float AccelerationElapsed => _accelerationElapsed;
        public float AccelerationProgress =>
            Mathf.Clamp01(
                _accelerationElapsed
                / Mathf.Max(0.01f, accelerationDuration));
        public float StartScrollSpeed => startScrollSpeed;
        public float MaximumScrollSpeed => maximumScrollSpeed;
        public float AccelerationDuration => accelerationDuration;

        public event Action<LaunchPhase> PhaseChanged;

        public float EvaluateConfiguredScrollSpeed(
            float normalizedProgress)
        {
            float curveValue = EvaluateNormalizedCurve(
                accelerationCurve,
                Mathf.Clamp01(normalizedProgress));
            return Mathf.Clamp(
                Mathf.Lerp(
                    startScrollSpeed,
                    maximumScrollSpeed,
                    curveValue),
                0f,
                maximumScrollSpeed);
        }

        private void Awake()
        {
            ResolveUpgradePanel();

            Phase = LaunchPhase.Idle;
            Altitude = 0d;
            _phaseElapsed = 0f;
            _accelerationElapsed = 0f;
            _currentScrollSpeed = 0f;
            _launchStarted = false;

            if (rocketView != null)
            {
                rocketView.ResetToIdle();
            }

            if (backgroundScroller != null)
            {
                backgroundScroller.SetScrollSpeed(0f);
            }
        }

        private void Start()
        {
            if (autoStart)
            {
                BeginLaunch();
            }
        }

        private void Update()
        {
            // 임시 함수
            if(Keyboard.current.eKey.wasPressedThisFrame) BeginLaunch();

            float deltaTime = Mathf.Max(0f, Time.deltaTime);
            double frameDistance = AdvanceSequence(deltaTime);

            if (backgroundScroller != null)
            {
                if (deltaTime > 0f)
                {
                    _currentScrollSpeed = Mathf.Clamp(
                        (float)(frameDistance / deltaTime),
                        0f,
                        maximumScrollSpeed);
                }

                backgroundScroller.SetScrollSpeed(
                    deltaTime > 0f ? _currentScrollSpeed : 0f);
            }

            Altitude += frameDistance;
        }

        private void OnDisable()
        {
            if (backgroundScroller != null)
            {
                backgroundScroller.SetScrollSpeed(0f);
            }
        }

        public void BeginLaunch()
        {
            if (_launchStarted || Phase != LaunchPhase.Idle)
            {
                return;
            }

            if (!HasValidConfiguration())
            {
                WarnInvalidConfigurationOnce();
                return;
            }

            CloseUpgradePanel();

            _warnedInvalidConfiguration = false;
            _launchStarted = true;
            Altitude = 0d;
            _accelerationElapsed = 0f;
            _currentScrollSpeed = 0f;

            backgroundScroller.ResetPassedBackgroundCount();
            rocketView.ResetToIdle();
            TransitionTo(LaunchPhase.Ignition);
        }

        private double AdvanceSequence(float deltaTime)
        {
            float remainingDelta = deltaTime;
            double frameDistance = 0d;

            for (int step = 0;
                 step < MaxPhaseStepsPerFrame && remainingDelta > 0f;
                 step++)
            {
                switch (Phase)
                {
                    case LaunchPhase.Idle:
                        _currentScrollSpeed = 0f;
                        remainingDelta = 0f;
                        break;

                    case LaunchPhase.Ignition:
                        ConsumeIgnitionTime(ref remainingDelta);
                        break;

                    case LaunchPhase.LiftOff:
                        frameDistance +=
                            ConsumeLiftOffTime(ref remainingDelta);
                        break;

                    case LaunchPhase.Cruise:
                        frameDistance += ConsumeCruiseTime(remainingDelta);
                        remainingDelta = 0f;
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            return Math.Max(0d, frameDistance);
        }

        private void ConsumeIgnitionTime(ref float remainingDelta)
        {
            _currentScrollSpeed = 0f;

            if (ignitionDuration <= 0f)
            {
                EnterLiftOff();
                return;
            }

            float phaseRemaining =
                Mathf.Max(0f, ignitionDuration - _phaseElapsed);
            if (phaseRemaining <= 0f)
            {
                EnterLiftOff();
                return;
            }

            float consumedDelta = Mathf.Min(remainingDelta, phaseRemaining);
            _phaseElapsed += consumedDelta;
            remainingDelta =
                Mathf.Max(0f, remainingDelta - consumedDelta);

            if (consumedDelta >= phaseRemaining)
            {
                EnterLiftOff();
            }
        }

        private void EnterLiftOff()
        {
            _accelerationElapsed = 0f;
            TransitionTo(LaunchPhase.LiftOff);
            rocketView.SetLiftProgress(0f, liftCurve);
            _currentScrollSpeed = EvaluateScrollSpeed(0f);
        }

        private double ConsumeLiftOffTime(ref float remainingDelta)
        {
            if (liftOffDuration <= 0f)
            {
                rocketView.SetLiftProgress(1f, liftCurve);
                rocketView.SnapToCruiseAnchor();
                TransitionTo(LaunchPhase.Cruise);
                return 0d;
            }

            float phaseRemaining =
                Mathf.Max(0f, liftOffDuration - _phaseElapsed);
            if (phaseRemaining <= 0f)
            {
                rocketView.SnapToCruiseAnchor();
                TransitionTo(LaunchPhase.Cruise);
                return 0d;
            }

            float consumedDelta = Mathf.Min(remainingDelta, phaseRemaining);
            float startNormalizedTime =
                Mathf.Clamp01(_phaseElapsed / liftOffDuration);
            float endElapsed = Mathf.Min(
                liftOffDuration,
                _phaseElapsed + consumedDelta);
            float endNormalizedTime =
                Mathf.Clamp01(endElapsed / liftOffDuration);

            double travelledDistance =
                ConsumeAccelerationTime(consumedDelta);

            _phaseElapsed = endElapsed;
            remainingDelta =
                Mathf.Max(0f, remainingDelta - consumedDelta);
            rocketView.SetLiftProgress(endNormalizedTime, liftCurve);

            if (consumedDelta >= phaseRemaining)
            {
                rocketView.SnapToCruiseAnchor();
                TransitionTo(LaunchPhase.Cruise);
            }

            return travelledDistance;
        }

        private double ConsumeCruiseTime(float deltaTime)
        {
            rocketView.SnapToCruiseAnchor();
            return ConsumeAccelerationTime(deltaTime);
        }

        private double ConsumeAccelerationTime(float deltaTime)
        {
            float remainingDeltaTime = Mathf.Max(0f, deltaTime);
            if (remainingDeltaTime <= 0f)
            {
                _currentScrollSpeed =
                    EvaluateScrollSpeed(_accelerationElapsed);
                return 0d;
            }

            float safeAccelerationDuration =
                Mathf.Max(0.01f, accelerationDuration);
            double travelledDistance = 0d;

            while (remainingDeltaTime > 0f
                   && _accelerationElapsed
                       < safeAccelerationDuration)
            {
                float accelerationTimeRemaining =
                    safeAccelerationDuration - _accelerationElapsed;
                float step = Mathf.Min(
                    remainingDeltaTime,
                    Mathf.Min(
                        accelerationTimeRemaining,
                        MaxAccelerationIntegrationStep));
                float startSpeed =
                    EvaluateScrollSpeed(_accelerationElapsed);
                _accelerationElapsed += step;
                float endSpeed =
                    EvaluateScrollSpeed(_accelerationElapsed);

                travelledDistance +=
                    (double)(startSpeed + endSpeed)
                    * 0.5d
                    * step;
                remainingDeltaTime =
                    Mathf.Max(0f, remainingDeltaTime - step);
            }

            if (remainingDeltaTime > 0f)
            {
                _accelerationElapsed = safeAccelerationDuration;
                travelledDistance +=
                    (double)maximumScrollSpeed
                    * remainingDeltaTime;
            }

            _currentScrollSpeed =
                EvaluateScrollSpeed(_accelerationElapsed);
            return travelledDistance;
        }

        private float EvaluateScrollSpeed(float elapsedTime)
        {
            float normalizedTime = Mathf.Clamp01(
                elapsedTime
                / Mathf.Max(0.01f, accelerationDuration));
            return EvaluateConfiguredScrollSpeed(normalizedTime);
        }

        private void TransitionTo(LaunchPhase nextPhase)
        {
            if (Phase == nextPhase)
            {
                return;
            }

            LaunchPhase previousPhase = Phase;
            Phase = nextPhase;
            _phaseElapsed = 0f;

            if (resetAccelerationOnPhaseChange
                && IsScrollingPhase(previousPhase)
                && IsScrollingPhase(nextPhase))
            {
                _accelerationElapsed = 0f;
                _currentScrollSpeed = EvaluateScrollSpeed(0f);
            }

            rocketView.SetPresentationPhase(nextPhase);
            PlayPhaseSound(nextPhase);
            PhaseChanged?.Invoke(nextPhase);
        }

        private static void PlayPhaseSound(LaunchPhase phase)
        {
            switch (phase)
            {
                case LaunchPhase.Ignition:
                    RocketShootingSoundPlayer.Play(
                        RocketShootingSoundCue.RocketPreLaunch);
                    break;
                case LaunchPhase.LiftOff:
                    RocketShootingSoundPlayer.Play(
                        RocketShootingSoundCue.RocketLaunch);
                    break;
            }
        }

        private static bool IsScrollingPhase(LaunchPhase phase)
        {
            return phase == LaunchPhase.LiftOff
                || phase == LaunchPhase.Cruise;
        }

        private bool HasValidConfiguration()
        {
            return rocketView != null
                && rocketView.isActiveAndEnabled
                && rocketView.IsConfigured
                && backgroundScroller != null
                && backgroundScroller.isActiveAndEnabled
                && backgroundScroller.IsConfigured;
        }

        private InventoryPanelController ResolveUpgradePanel()
        {
            if (upgradePanel == null)
            {
                upgradePanel =
                    FindFirstObjectByType<InventoryPanelController>(
                        FindObjectsInactive.Include);
            }

            return upgradePanel;
        }

        private void CloseUpgradePanel()
        {
            ResolveUpgradePanel()?.HideUI();

            if (upgradePanelRoot != null)
            {
                upgradePanelRoot.SetActive(false);
            }
        }

        private void WarnInvalidConfigurationOnce()
        {
            if (_warnedInvalidConfiguration)
            {
                return;
            }

            Debug.LogWarning(
                $"{nameof(RocketShootingDirector)} on '{name}' could not begin "
                + "the launch because Rocket View or Background Scroller is not "
                + "fully configured.",
                this);
            _warnedInvalidConfiguration = true;
        }

        private static float EvaluateNormalizedCurve(
            AnimationCurve curve,
            float normalizedTime)
        {
            float value = curve != null
                ? curve.Evaluate(Mathf.Clamp01(normalizedTime))
                : normalizedTime;
            return Mathf.Clamp01(value);
        }

        private static AnimationCurve CreateDefaultAccelerationCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 0f),
                new Keyframe(0.3f, 0.05f, 0.35f, 0.35f),
                new Keyframe(0.75f, 0.62f, 1.55f, 1.55f),
                new Keyframe(1f, 1f, 0f, 0f));
        }

        private void OnValidate()
        {
            ignitionDuration = Mathf.Max(0f, ignitionDuration);
            liftOffDuration = Mathf.Max(0f, liftOffDuration);
            startScrollSpeed = Mathf.Max(0f, startScrollSpeed);
            maximumScrollSpeed = Mathf.Max(
                startScrollSpeed,
                maximumScrollSpeed);
            accelerationDuration =
                Mathf.Max(0.01f, accelerationDuration);

            if (liftCurve == null || liftCurve.length == 0)
            {
                liftCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            }

            if (accelerationCurve == null
                || accelerationCurve.length == 0)
            {
                accelerationCurve = CreateDefaultAccelerationCurve();
            }
        }
    }
}
