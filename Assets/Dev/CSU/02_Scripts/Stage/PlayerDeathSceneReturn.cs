using System.Collections;
using Dev.CSU._02_Scripts.SceneTransition;
using Dev.NKY.Scripts.Health;
using SpaceGame.CommonUI.Pause;
using SpaceGame.RunOutcome;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dev.CSU._02_Scripts.Stage
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class PlayerDeathSceneReturn : MonoBehaviour
    {
        [SerializeField] private Health health;
        [SerializeField] private PauseRequestService pauseService;
        [SerializeField] private string targetSceneName = "Rocket Shooting";
        [SerializeField, Min(0f)] private float delaySeconds = 2f;

        private Coroutine _returnRoutine;
        private bool _transitionRequested;
        private System.IDisposable _deathPauseLease;
        private float _fallbackTimeScale = 1f;
        private bool _fallbackPauseActive;

        private void Awake()
        {
            ResolveHealth();
        }

        private void OnEnable()
        {
            ResolveHealth();
            if (health != null)
            {
                health.DeadEvent += HandlePlayerDeath;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.DeadEvent -= HandlePlayerDeath;
            }

            if (_returnRoutine != null)
            {
                StopCoroutine(_returnRoutine);
                _returnRoutine = null;
            }

            ReleaseDeathPause();
        }

        private void ResolveHealth()
        {
            if (health == null)
            {
                health = GetComponent<Health>();
            }

            if (pauseService == null)
            {
                pauseService = FindFirstObjectByType<PauseRequestService>();
            }
        }

        private void HandlePlayerDeath()
        {
            if (_transitionRequested || _returnRoutine != null)
            {
                return;
            }

            health.TryResolveRunOutcome(RunTerminalOutcome.Death);
            if (health.CurrentRunOutcome != RunTerminalOutcome.Death)
            {
                Debug.Log(
                    "[RunOutcome] Ignored Death scene return because the "
                    + $"first terminal outcome was {health.CurrentRunOutcome}.",
                    this);
                return;
            }

            PauseGameplay();
            _returnRoutine = StartCoroutine(ReturnAfterDelay());
        }

        private IEnumerator ReturnAfterDelay()
        {
            if (delaySeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(delaySeconds);
            }

            _returnRoutine = null;
            if (health == null
                || health.CurrentRunOutcome
                != RunTerminalOutcome.Death)
            {
                ReleaseDeathPause();
                yield break;
            }

            if (!health.TryBeginSceneTransition(
                    RunTerminalOutcome.Death))
            {
                ReleaseDeathPause();
                Debug.LogError(
                    "[PlayerDeathSceneReturn] Death won the terminal "
                    + "outcome, but the shared transition gate could not "
                    + "start. Gameplay was resumed to avoid a permanent "
                    + "pause.",
                    this);
                yield break;
            }

            _transitionRequested = true;

            if (SceneTransitions.TryLoadScene(targetSceneName))
            {
                yield break;
            }

            if (SceneTransitions.TryGetService(
                    out ISceneTransitionService activeService)
                && activeService.IsTransitioning)
            {
                Debug.Log(
                    "[PlayerDeathSceneReturn] An existing scene "
                    + "transition already owns the scene request. The "
                    + "death pause will be released when this scene "
                    + "unloads.",
                    this);
                yield break;
            }

            health.TryCancelSceneTransition(
                RunTerminalOutcome.Death);
            _transitionRequested = false;
            ReleaseDeathPause();

            try
            {
                Debug.LogWarning(
                    "[PlayerDeathSceneReturn] The shared transition "
                    + $"service could not load '{targetSceneName}'. "
                    + "Falling back to a direct scene load.",
                    this);
                _transitionRequested = true;
                SceneManager.LoadScene(targetSceneName);
            }
            catch (System.Exception exception)
            {
                _transitionRequested = false;
                Debug.LogError(
                    $"[PlayerDeathSceneReturn] Failed to load "
                    + $"'{targetSceneName}' after player death. Gameplay "
                    + "was resumed to avoid a permanent pause. "
                    + $"{exception.GetType().Name}: "
                    + exception.Message,
                    this);
            }
        }

        private void PauseGameplay()
        {
            if (_deathPauseLease != null || _fallbackPauseActive)
            {
                return;
            }

            if (pauseService != null)
            {
                _deathPauseLease = pauseService.Acquire(this);
                return;
            }

            _fallbackTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            _fallbackPauseActive = true;
            Debug.LogWarning(
                "[PlayerDeathSceneReturn] PauseRequestService was not "
                + "available; using a local time-scale pause until the "
                + "death scene transition completes.",
                this);
        }

        private void ReleaseDeathPause()
        {
            _deathPauseLease?.Dispose();
            _deathPauseLease = null;

            if (!_fallbackPauseActive)
            {
                return;
            }

            Time.timeScale = _fallbackTimeScale;
            _fallbackPauseActive = false;
        }
    }
}
