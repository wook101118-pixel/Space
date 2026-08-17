using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaceGame.CommonUI.Modal
{
    [DisallowMultipleComponent]
    public sealed class ModalCancelRouter : MonoBehaviour
    {
        private readonly List<Entry> entries = new List<Entry>();
        private InputAction cancelAction;
        private InputAction fallbackCancelAction;
        private long nextSequence;
        private bool enabledActionLocally;
        private int lastHandledFrame = -1;

        public void Configure(
            InputAction action,
            string fallbackControlPath)
        {
            Unsubscribe();
            fallbackCancelAction?.Dispose();
            cancelAction = action;
            fallbackCancelAction = null;
            if (cancelAction == null &&
                !string.IsNullOrWhiteSpace(fallbackControlPath))
            {
                fallbackCancelAction = new InputAction(
                    "Modal Cancel Fallback",
                    InputActionType.Button,
                    fallbackControlPath);
                Debug.LogWarning(
                    $"[{nameof(ModalCancelRouter)}] The configured cancel "
                    + "action is missing. A fixed fallback is active at "
                    + $"'{fallbackControlPath}'.",
                    this);
            }

            Subscribe();
        }

        public IDisposable Push(Func<bool> cancelHandler, int priority)
        {
            if (cancelHandler == null)
            {
                throw new ArgumentNullException(nameof(cancelHandler));
            }

            var entry = new Entry
            {
                cancelHandler = cancelHandler,
                priority = priority,
                sequence = ++nextSequence
            };
            entries.Add(entry);
            return new Registration(this, entry);
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            entries.Clear();
        }

        private void Subscribe()
        {
            InputAction action = cancelAction;
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (action != null)
            {
                action.performed -= OnCancelPerformed;
                action.performed += OnCancelPerformed;
                if (!action.enabled)
                {
                    action.Enable();
                    enabledActionLocally = true;
                }
            }

            if (fallbackCancelAction != null)
            {
                fallbackCancelAction.performed -= OnCancelPerformed;
                fallbackCancelAction.performed += OnCancelPerformed;
                fallbackCancelAction.Enable();
            }
        }

        private void Unsubscribe()
        {
            InputAction action = cancelAction;
            if (action != null)
            {
                action.performed -= OnCancelPerformed;
                if (enabledActionLocally && action.enabled)
                {
                    action.Disable();
                }
            }

            if (fallbackCancelAction != null)
            {
                fallbackCancelAction.performed -= OnCancelPerformed;
                fallbackCancelAction.Disable();
            }

            enabledActionLocally = false;
        }

        private void OnCancelPerformed(InputAction.CallbackContext context)
        {
            TryRouteCancel();
        }

        public bool TryRouteCancel()
        {
            if (lastHandledFrame == Time.frameCount)
            {
                return false;
            }

            lastHandledFrame = Time.frameCount;
            Entry top = null;
            foreach (Entry entry in entries)
            {
                if (top == null ||
                    entry.priority > top.priority ||
                    entry.priority == top.priority &&
                    entry.sequence > top.sequence)
                {
                    top = entry;
                }
            }

            return top != null && top.cancelHandler.Invoke();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            fallbackCancelAction?.Dispose();
            fallbackCancelAction = null;
        }

        private void Remove(Entry entry)
        {
            entries.Remove(entry);
        }

        private sealed class Entry
        {
            public Func<bool> cancelHandler;
            public int priority;
            public long sequence;
        }

        private sealed class Registration : IDisposable
        {
            private ModalCancelRouter router;
            private readonly Entry entry;

            public Registration(ModalCancelRouter router, Entry entry)
            {
                this.router = router;
                this.entry = entry;
            }

            public void Dispose()
            {
                if (router == null)
                {
                    return;
                }

                router.Remove(entry);
                router = null;
            }
        }
    }
}
