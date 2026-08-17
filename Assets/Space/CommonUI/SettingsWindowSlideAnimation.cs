using DG.Tweening;
using UnityEngine;

namespace SpaceGame.CommonUI.Views
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SettingsWindow))]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SettingsWindowSlideAnimation : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private SettingsWindow settingsWindow;
        [SerializeField] private RectTransform targetRectTransform;

        [Header("Open Animation")]
        [Min(0f)]
        [SerializeField] private float rightStartDistance = 800f;
        [Min(0.01f)]
        [SerializeField] private float moveDuration = 0.24f;
        [SerializeField] private Ease moveEase = Ease.OutCubic;

        [Header("Close Animation")]
        [Min(0f)]
        [SerializeField] private float rightExitDistance = 800f;
        [Min(0.01f)]
        [SerializeField] private float exitDuration = 0.24f;
        [SerializeField] private Ease exitEase = Ease.InCubic;

        private Tween _moveTween;
        private Vector2 _originalPosition;
        private bool _hasCapturedOriginalPosition;
        private bool _isExitAnimationPlaying;
        private bool _hasWarnedMissingSettingsWindow;
        private bool _hasWarnedMissingRectTransform;

        public SettingsWindow SettingsWindow => settingsWindow;
        public RectTransform TargetRectTransform => targetRectTransform;
        public Vector2 OriginalPosition => _originalPosition;
        public float RightStartDistance => rightStartDistance;
        public float MoveDuration => moveDuration;
        public Ease MoveEase => moveEase;
        public float RightExitDistance => rightExitDistance;
        public float ExitDuration => exitDuration;
        public Ease ExitEase => exitEase;

        private void Awake()
        {
            ResolveReferences();
            CaptureOriginalPosition();
            WarnAboutMissingReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            CaptureOriginalPosition();
            SubscribeToTransitions();
            WarnAboutMissingReferences();
        }

        private void OnDisable()
        {
            UnsubscribeFromTransitions();
            KillMoveTween();
            RestoreOriginalPosition();
        }

        private void OnDestroy()
        {
            UnsubscribeFromTransitions();
            KillMoveTween();
            RestoreOriginalPosition();
        }

        private void HandleOpenTransitionStarted()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            ResolveReferences();
            CaptureOriginalPosition();
            if (targetRectTransform == null)
            {
                WarnAboutMissingReferences();
                return;
            }

            bool wasExitAnimationPlaying = _isExitAnimationPlaying;
            KillMoveTween();

            Vector2 startPosition = _originalPosition
                + Vector2.right * Mathf.Max(0f, rightStartDistance);
            if (!wasExitAnimationPlaying)
            {
                targetRectTransform.anchoredPosition = startPosition;
            }

            Tween moveTween = targetRectTransform
                .DOAnchorPos(
                    _originalPosition,
                    Mathf.Max(0.01f, moveDuration))
                .SetEase(moveEase)
                .SetUpdate(true)
                .SetTarget(targetRectTransform)
                .SetId(this);

            _moveTween = moveTween;
            _isExitAnimationPlaying = false;
            moveTween.OnComplete(() =>
            {
                if (_moveTween == moveTween
                    && targetRectTransform != null)
                {
                    targetRectTransform.anchoredPosition =
                        _originalPosition;
                }
            });
            RegisterTweenCleanup(moveTween);
        }

        private void HandleCloseTransitionStarted()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            ResolveReferences();
            CaptureOriginalPosition();
            if (targetRectTransform == null)
            {
                WarnAboutMissingReferences();
                return;
            }

            KillMoveTween();

            Vector2 endPosition = _originalPosition
                + Vector2.right * Mathf.Max(0f, rightExitDistance);
            Tween moveTween = targetRectTransform
                .DOAnchorPos(
                    endPosition,
                    Mathf.Max(0.01f, exitDuration))
                .SetEase(exitEase)
                .SetUpdate(true)
                .SetTarget(targetRectTransform)
                .SetId(this);

            _moveTween = moveTween;
            _isExitAnimationPlaying = true;
            moveTween.OnComplete(() =>
            {
                if (_moveTween == moveTween
                    && targetRectTransform != null)
                {
                    targetRectTransform.anchoredPosition = endPosition;
                }
            });
            RegisterTweenCleanup(moveTween);
        }

        private void RegisterTweenCleanup(Tween moveTween)
        {
            moveTween.OnKill(() =>
            {
                if (_moveTween == moveTween)
                {
                    _moveTween = null;
                    _isExitAnimationPlaying = false;
                }
            });
        }

        private void ResolveReferences()
        {
            if (settingsWindow == null)
            {
                settingsWindow = GetComponent<SettingsWindow>();
            }

            if (targetRectTransform == null)
            {
                targetRectTransform = transform.Find("Panel")
                    as RectTransform;
            }
        }

        private void SubscribeToTransitions()
        {
            if (settingsWindow == null)
            {
                return;
            }

            settingsWindow.OpenTransitionStarted -=
                HandleOpenTransitionStarted;
            settingsWindow.OpenTransitionStarted +=
                HandleOpenTransitionStarted;
            settingsWindow.CloseTransitionStarted -=
                HandleCloseTransitionStarted;
            settingsWindow.CloseTransitionStarted +=
                HandleCloseTransitionStarted;
        }

        private void UnsubscribeFromTransitions()
        {
            if (settingsWindow == null)
            {
                return;
            }

            settingsWindow.OpenTransitionStarted -=
                HandleOpenTransitionStarted;
            settingsWindow.CloseTransitionStarted -=
                HandleCloseTransitionStarted;
        }

        private void CaptureOriginalPosition()
        {
            if (_hasCapturedOriginalPosition
                || targetRectTransform == null)
            {
                return;
            }

            _originalPosition = targetRectTransform.anchoredPosition;
            _hasCapturedOriginalPosition = true;
        }

        private void RestoreOriginalPosition()
        {
            if (_hasCapturedOriginalPosition
                && targetRectTransform != null)
            {
                targetRectTransform.anchoredPosition = _originalPosition;
            }
        }

        private void KillMoveTween()
        {
            Tween moveTween = _moveTween;
            _moveTween = null;
            _isExitAnimationPlaying = false;

            if (moveTween != null && moveTween.IsActive())
            {
                moveTween.Kill(false);
            }
        }

        private void WarnAboutMissingReferences()
        {
            if (settingsWindow == null
                && !_hasWarnedMissingSettingsWindow)
            {
                _hasWarnedMissingSettingsWindow = true;
                Debug.LogWarning(
                    $"[{nameof(SettingsWindowSlideAnimation)}] "
                    + $"'{name}' has no SettingsWindow reference. "
                    + "The settings slide transition cannot be triggered.",
                    this);
            }

            if (targetRectTransform == null
                && !_hasWarnedMissingRectTransform)
            {
                _hasWarnedMissingRectTransform = true;
                Debug.LogWarning(
                    $"[{nameof(SettingsWindowSlideAnimation)}] "
                    + $"'{name}' has no target RectTransform. "
                    + "The settings slide transition cannot move its target.",
                    this);
            }
        }

        private void OnValidate()
        {
            rightStartDistance = Mathf.Max(0f, rightStartDistance);
            moveDuration = Mathf.Max(0.01f, moveDuration);
            rightExitDistance = Mathf.Max(0f, rightExitDistance);
            exitDuration = Mathf.Max(0.01f, exitDuration);
        }
    }
}
