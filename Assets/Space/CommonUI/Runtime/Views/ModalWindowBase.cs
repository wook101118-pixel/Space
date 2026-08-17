using System;
using System.Collections;
using SpaceGame.CommonUI.Modal;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceGame.CommonUI.Views
{
    public abstract class ModalWindowBase : MonoBehaviour
    {
        [Header("Modal")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Button backdropButton;
        [SerializeField, Min(0f)] private float fadeDuration = 0.16f;
        [SerializeField] private bool pauseWhileOpen = true;
        [SerializeField] private bool blockGameplayInput = true;
        [SerializeField] private bool closeOnBackdrop = true;
        [SerializeField] private int cancelPriority = 100;

        private CommonUIContext context;
        private IDisposable pauseLease;
        private IDisposable inputGateLease;
        private IDisposable cancelRegistration;
        private Coroutine fadeRoutine;

        public event Action OpenTransitionStarted;
        public event Action CloseTransitionStarted;

        public bool IsOpen { get; private set; }
        protected CommonUIContext Context => context;

        public void Initialize(CommonUIContext commonContext)
        {
            context = commonContext;
            SetInstantVisibility(false);
            OnInitialized();
        }

        public virtual void Open()
        {
            if (IsOpen || context == null || !CanOpen())
            {
                return;
            }

            IsOpen = true;
            if (pauseWhileOpen)
            {
                pauseLease = context.PauseService.Acquire(this);
            }

            if (blockGameplayInput)
            {
                inputGateLease = context.InputGate.Acquire();
            }

            if (HandlesCancel)
            {
                cancelRegistration = context.CancelRouter.Push(
                    HandleCancel,
                    cancelPriority);
            }

            if (backdropButton != null && closeOnBackdrop)
            {
                backdropButton.onClick.AddListener(RequestClose);
            }

            OnOpened();
            FadeTo(1f);
            OpenTransitionStarted?.Invoke();
        }

        public void RequestClose()
        {
            if (IsOpen)
            {
                OnCloseRequested();
            }
        }

        protected void CloseDirect()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            if (backdropButton != null)
            {
                backdropButton.onClick.RemoveListener(RequestClose);
            }

            cancelRegistration?.Dispose();
            cancelRegistration = null;
            inputGateLease?.Dispose();
            inputGateLease = null;
            pauseLease?.Dispose();
            pauseLease = null;
            OnClosed();
            FadeTo(0f);
            CloseTransitionStarted?.Invoke();
        }

        protected virtual bool CanOpen()
        {
            return true;
        }

        protected virtual bool HandlesCancel => true;

        protected virtual bool HandleCancel()
        {
            RequestClose();
            return true;
        }

        protected virtual void OnInitialized()
        {
        }

        protected virtual void OnOpened()
        {
        }

        protected virtual void OnCloseRequested()
        {
            CloseDirect();
        }

        protected virtual void OnClosed()
        {
        }

        protected void SetModalOptions(
            bool shouldPause,
            bool shouldBlockGameplay,
            bool shouldCloseOnBackdrop,
            int priority)
        {
            pauseWhileOpen = shouldPause;
            blockGameplayInput = shouldBlockGameplay;
            closeOnBackdrop = shouldCloseOnBackdrop;
            cancelPriority = priority;
        }

        public void ConfigureModal(
            CanvasGroup group,
            Button backdrop,
            float duration,
            bool shouldPause,
            bool shouldBlockGameplay,
            bool shouldCloseOnBackdrop,
            int priority)
        {
            canvasGroup = group;
            backdropButton = backdrop;
            fadeDuration = duration;
            SetModalOptions(
                shouldPause,
                shouldBlockGameplay,
                shouldCloseOnBackdrop,
                priority);
        }

        private void FadeTo(float target)
        {
            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
            }

            canvasGroup.interactable = target > 0f;
            canvasGroup.blocksRaycasts = target > 0f;
            if (!isActiveAndEnabled)
            {
                canvasGroup.alpha = target;
                fadeRoutine = null;
                return;
            }

            fadeRoutine = StartCoroutine(FadeRoutine(target));
        }

        private IEnumerator FadeRoutine(float target)
        {
            float initial = canvasGroup.alpha;
            if (fadeDuration <= 0f)
            {
                canvasGroup.alpha = target;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(
                    initial,
                    target,
                    Mathf.Clamp01(elapsed / fadeDuration));
                yield return null;
            }

            canvasGroup.alpha = target;
            fadeRoutine = null;
        }

        private void SetInstantVisibility(bool visible)
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        protected virtual void OnDisable()
        {
            if (IsOpen)
            {
                RequestClose();
            }
        }

        protected virtual void OnDestroy()
        {
            if (IsOpen)
            {
                RequestClose();
            }
        }
    }
}
