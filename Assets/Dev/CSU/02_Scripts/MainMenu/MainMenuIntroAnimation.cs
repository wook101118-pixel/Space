using System.Collections;
using Dev.CSU._02_Scripts.SceneTransition;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Dev.CSU._02_Scripts.MainMenu
{
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class MainMenuIntroAnimation : MonoBehaviour
    {
        private const int TargetCount = 4;

        private static readonly string[] TargetNames =
        {
            nameof(logoBox),
            nameof(startButton),
            nameof(settingButton),
            nameof(exitButton)
        };

        [Header("Targets (Play Order)")]
        [SerializeField] private RectTransform logoBox;
        [SerializeField] private RectTransform startButton;
        [SerializeField] private RectTransform settingButton;
        [SerializeField] private RectTransform exitButton;

        [Header("Animation")]
        [Min(0f)]
        [SerializeField] private float rightStartDistance = 800f;
        [Min(0.01f)]
        [SerializeField] private float moveDuration = 0.5f;
        [Min(0f)]
        [SerializeField] private float itemInterval = 0.12f;
        [SerializeField] private Ease moveEase = Ease.OutCubic;

        private readonly RectTransform[] _targets =
            new RectTransform[TargetCount];
        private readonly Vector2[] _originalPositions =
            new Vector2[TargetCount];

        private Sequence _introSequence;
        private Coroutine _pendingPlayRoutine;
        private bool _hasCapturedOriginalPositions;
        private bool _hasStarted;
        private bool _isWaitingForCanvasRender;

        public void PlayIntro()
        {
            if (!Application.isPlaying || !isActiveAndEnabled)
            {
                return;
            }

            QueueIntro();
        }

        private void Awake()
        {
            RefreshTargets();
            WarnAboutMissingTargets();
        }

        private void OnEnable()
        {
            if (Application.isPlaying && _hasStarted)
            {
                QueueIntro();
            }
        }

        private void Start()
        {
            _hasStarted = true;
            QueueIntro();
        }

        private void OnDisable()
        {
            CancelPendingPlay();
            CancelCanvasRenderStart();
            KillIntroSequence();
            RestoreOriginalPositions();
        }

        private void OnDestroy()
        {
            CancelPendingPlay();
            CancelCanvasRenderStart();
            KillIntroSequence();
        }

        private void QueueIntro()
        {
            CancelPendingPlay();
            CancelCanvasRenderStart();
            KillIntroSequence();

            EnsureOriginalPositionsCaptured();
            PlaceTargetsAtStart();
            _pendingPlayRoutine = StartCoroutine(
                PlayWhenMenuIsVisible());
        }

        private IEnumerator PlayWhenMenuIsVisible()
        {
            while (SceneTransitions.IsTransitioning)
            {
                yield return null;
            }

            _pendingPlayRoutine = null;

            if (!isActiveAndEnabled)
            {
                yield break;
            }

            ScheduleStartBeforeCanvasRender();
        }

        private void ScheduleStartBeforeCanvasRender()
        {
            CancelCanvasRenderStart();
            _isWaitingForCanvasRender = true;
            Canvas.willRenderCanvases += StartAfterLayoutUpdate;
        }

        private void StartAfterLayoutUpdate()
        {
            CancelCanvasRenderStart();

            if (!isActiveAndEnabled)
            {
                return;
            }

            // VerticalLayoutGroup can queue a rebuild when the menu is
            // re-enabled. Rebuild once more immediately before rendering,
            // then apply the tween start positions after layout has settled.
            ForceButtonLayoutUpdate();
            PlaceTargetsAtStart();
            CreateAndPlaySequence();
        }

        private void CreateAndPlaySequence()
        {
            Sequence sequence = DOTween.Sequence()
                .SetUpdate(true)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable);

            _introSequence = sequence;
            bool hasTween = false;
            int playIndex = 0;

            for (int index = 0; index < TargetCount; index++)
            {
                RectTransform target = _targets[index];
                if (target == null)
                {
                    continue;
                }

                Tween moveTween = target
                    .DOAnchorPos(
                        _originalPositions[index],
                        Mathf.Max(0.01f, moveDuration))
                    .SetEase(moveEase);

                sequence.Insert(
                    playIndex * Mathf.Max(0f, itemInterval),
                    moveTween);
                hasTween = true;
                playIndex++;
            }

            if (!hasTween)
            {
                sequence.Kill(false);
                _introSequence = null;
                return;
            }

            sequence.OnComplete(() =>
            {
                if (_introSequence == sequence)
                {
                    RestoreOriginalPositions();
                }
            });
            sequence.OnKill(() =>
            {
                if (_introSequence == sequence)
                {
                    _introSequence = null;
                }
            });
        }

        private void EnsureOriginalPositionsCaptured()
        {
            if (_hasCapturedOriginalPositions)
            {
                return;
            }

            RefreshTargets();
            ForceTargetLayoutUpdate();

            for (int index = 0; index < TargetCount; index++)
            {
                RectTransform target = _targets[index];
                if (target != null)
                {
                    _originalPositions[index] = target.anchoredPosition;
                }
            }

            _hasCapturedOriginalPositions = true;
        }

        private void ForceTargetLayoutUpdate()
        {
            Canvas.ForceUpdateCanvases();
            ForceButtonLayoutUpdate();
            Canvas.ForceUpdateCanvases();
        }

        private void ForceButtonLayoutUpdate()
        {
            RectTransform buttonLayoutRoot = GetButtonLayoutRoot();
            if (buttonLayoutRoot != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(
                    buttonLayoutRoot);
            }
        }

        private RectTransform GetButtonLayoutRoot()
        {
            if (startButton != null)
            {
                return startButton.parent as RectTransform;
            }

            if (settingButton != null)
            {
                return settingButton.parent as RectTransform;
            }

            return exitButton != null
                ? exitButton.parent as RectTransform
                : null;
        }

        private void PlaceTargetsAtStart()
        {
            if (!_hasCapturedOriginalPositions)
            {
                return;
            }

            float startDistance = Mathf.Max(0f, rightStartDistance);
            Vector2 offset = Vector2.right * startDistance;

            for (int index = 0; index < TargetCount; index++)
            {
                RectTransform target = _targets[index];
                if (target != null)
                {
                    target.anchoredPosition =
                        _originalPositions[index] + offset;
                }
            }
        }

        private void RestoreOriginalPositions()
        {
            if (!_hasCapturedOriginalPositions)
            {
                return;
            }

            for (int index = 0; index < TargetCount; index++)
            {
                RectTransform target = _targets[index];
                if (target != null)
                {
                    target.anchoredPosition =
                        _originalPositions[index];
                }
            }
        }

        private void RefreshTargets()
        {
            _targets[0] = logoBox;
            _targets[1] = startButton;
            _targets[2] = settingButton;
            _targets[3] = exitButton;
        }

        private void WarnAboutMissingTargets()
        {
            for (int index = 0; index < TargetCount; index++)
            {
                if (_targets[index] == null)
                {
                    Debug.LogWarning(
                        $"{nameof(MainMenuIntroAnimation)} on '{name}' has "
                        + $"no '{TargetNames[index]}' reference. That UI "
                        + "will be skipped while the remaining targets play.",
                        this);
                }
            }
        }

        private void CancelPendingPlay()
        {
            if (_pendingPlayRoutine == null)
            {
                return;
            }

            StopCoroutine(_pendingPlayRoutine);
            _pendingPlayRoutine = null;
        }

        private void CancelCanvasRenderStart()
        {
            if (!_isWaitingForCanvasRender)
            {
                return;
            }

            Canvas.willRenderCanvases -= StartAfterLayoutUpdate;
            _isWaitingForCanvasRender = false;
        }

        private void KillIntroSequence()
        {
            Sequence sequence = _introSequence;
            _introSequence = null;

            if (sequence != null && sequence.IsActive())
            {
                sequence.Kill(false);
            }
        }

        private void OnValidate()
        {
            rightStartDistance = Mathf.Max(0f, rightStartDistance);
            moveDuration = Mathf.Max(0.01f, moveDuration);
            itemInterval = Mathf.Max(0f, itemInterval);
        }
    }
}
