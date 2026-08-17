using System;
using Dev.CSU._02_Scripts.Planet;
using Dev.CSU._02_Scripts.SceneTransition;
using Dev.NKY.Scripts.Health;
using SpaceGame.RunOutcome;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dev.CSU._02_Scripts.Stage
{
    [DefaultExecutionOrder(1100)]
    [DisallowMultipleComponent]
    public sealed class StageThemeController : MonoBehaviour
    {
        private const string DefaultEndingSceneName = "Ending";

        [Tooltip("Stage presentation controller that reports completion and starts the next configured stage.")]
        [SerializeField] private PlanetParallaxController planetController;

        [Header("Run Completion")]
        [Tooltip("Scene loaded through the shared fade transition after the final configured stage completes.")]
        [SerializeField] private string endingSceneName =
            DefaultEndingSceneName;

        [Tooltip("Player health owns the single terminal outcome and run reward.")]
        [SerializeField] private Health playerHealth;

        private bool _warnedMissingController;
        private bool _endingTransitionRequested;

        public int CurrentStageNumber { get; private set; }
        public bool EndingTransitionRequested =>
            _endingTransitionRequested;

        public event Action<int> StageBackgroundReady;

        private void Awake()
        {
            ResolvePlayerHealth();
        }

        private void OnEnable()
        {
            SubscribeEvents();
        }

        private void Start()
        {
            if (planetController == null)
            {
                WarnMissingControllerOnce();
                return;
            }

            CurrentStageNumber = Mathf.Max(1, planetController.CurrentStageNumber);
            ResolvePlayerHealth();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        private void SubscribeEvents()
        {
            UnsubscribeEvents();

            if (planetController != null)
            {
                planetController.StageCompleted += HandleStageCompleted;
            }
        }

        private void UnsubscribeEvents()
        {
            if (planetController != null)
            {
                planetController.StageCompleted -= HandleStageCompleted;
            }
        }

        private void HandleStageCompleted(int completedStageNumber)
        {
            if (planetController == null || completedStageNumber != CurrentStageNumber)
            {
                return;
            }

            int nextStageNumber = completedStageNumber + 1;
            if (!planetController.HasStage(nextStageNumber))
            {
                RequestEndingTransition(completedStageNumber);
                return;
            }

            CurrentStageNumber = nextStageNumber;
            StageBackgroundReady?.Invoke(nextStageNumber);
            planetController.StartStage(nextStageNumber);
        }

        private void RequestEndingTransition(int completedStageNumber)
        {
            if (_endingTransitionRequested)
            {
                return;
            }

            ResolvePlayerHealth();
            if (playerHealth == null)
            {
                Debug.LogError(
                    $"{nameof(StageThemeController)} on '{name}' cannot "
                    + $"complete final stage {completedStageNumber}: no "
                    + $"{nameof(Health)} component is available to commit "
                    + "the run reward.",
                    this);
                return;
            }

            playerHealth.TryResolveRunOutcome(
                RunTerminalOutcome.Success);
            if (playerHealth.CurrentRunOutcome
                != RunTerminalOutcome.Success)
            {
                Debug.Log(
                    $"[RunOutcome] Ignored Success for final stage "
                    + $"{completedStageNumber}; first terminal outcome was "
                    + $"{playerHealth.CurrentRunOutcome}.",
                    this);
                return;
            }

            if (!playerHealth.TryBeginSceneTransition(
                    RunTerminalOutcome.Success))
            {
                return;
            }

            if (SceneTransitions.TryLoadScene(endingSceneName))
            {
                _endingTransitionRequested = true;
                return;
            }

            if (SceneTransitions.TryGetService(
                    out ISceneTransitionService activeService)
                && activeService.IsTransitioning)
            {
                _endingTransitionRequested = true;
                Debug.Log(
                    $"{nameof(StageThemeController)} on '{name}' respected "
                    + "an already-running scene transition after final "
                    + $"stage {completedStageNumber}.",
                    this);
                return;
            }

            playerHealth.TryCancelSceneTransition(
                RunTerminalOutcome.Success);
            try
            {
                Debug.LogWarning(
                    $"{nameof(StageThemeController)} on '{name}' completed "
                    + $"final stage {completedStageNumber}, but the shared "
                    + "transition service was unavailable. Falling back to "
                    + $"a direct load of '{endingSceneName}'.",
                    this);
                _endingTransitionRequested = true;
                SceneManager.LoadScene(endingSceneName);
            }
            catch (Exception exception)
            {
                _endingTransitionRequested = false;
                Debug.LogError(
                    $"{nameof(StageThemeController)} on '{name}' could not "
                    + $"load '{endingSceneName}' after final stage "
                    + $"{completedStageNumber}. "
                    + $"{exception.GetType().Name}: "
                    + exception.Message,
                    this);
            }
        }

        private void ResolvePlayerHealth()
        {
            if (playerHealth == null)
            {
                playerHealth = FindFirstObjectByType<Health>();
            }
        }

        private void WarnMissingControllerOnce()
        {
            if (_warnedMissingController) return;

            Debug.LogWarning($"{nameof(StageThemeController)} on '{name}' is inactive: " + "Planet Parallax Controller is not assigned.", this);
            _warnedMissingController = true;
        }

        private void OnValidate()
        {
            endingSceneName = string.IsNullOrWhiteSpace(endingSceneName)
                ? DefaultEndingSceneName
                : endingSceneName.Trim();
        }
    }
}
