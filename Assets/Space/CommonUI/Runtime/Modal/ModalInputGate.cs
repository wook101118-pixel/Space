using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaceGame.CommonUI.Modal
{
    [DisallowMultipleComponent]
    public sealed class ModalInputGate : MonoBehaviour
    {
        [SerializeField] private InputActionAsset actionAsset;
        [SerializeField] private List<string> gameplayActionMapIds =
            new List<string>();

        private readonly Dictionary<InputAction, bool> previousActionStates =
            new Dictionary<InputAction, bool>();
        private int requestCount;

        public void Configure(
            InputActionAsset asset,
            IEnumerable<string> actionMapIds)
        {
            actionAsset = asset;
            gameplayActionMapIds = actionMapIds == null
                ? new List<string>()
                : new List<string>(actionMapIds);
        }

        public IDisposable Acquire()
        {
            requestCount++;
            if (requestCount == 1)
            {
                BlockGameplay();
            }

            return new GateLease(this);
        }

        private void Release()
        {
            requestCount = Mathf.Max(0, requestCount - 1);
            if (requestCount == 0)
            {
                RestoreGameplay();
            }
        }

        private void BlockGameplay()
        {
            previousActionStates.Clear();
            if (actionAsset == null)
            {
                return;
            }

            foreach (string mapId in gameplayActionMapIds)
            {
                InputActionMap map = actionAsset.FindActionMap(mapId, false);
                if (map == null)
                {
                    continue;
                }

                bool hasEnabledAction = false;
                foreach (InputAction action in map.actions)
                {
                    bool wasEnabled = action.enabled;
                    previousActionStates[action] = wasEnabled;
                    hasEnabledAction |= wasEnabled;
                }

                if (hasEnabledAction)
                {
                    map.Disable();
                }
            }
        }

        private void RestoreGameplay()
        {
            foreach (KeyValuePair<InputAction, bool> pair in
                     previousActionStates)
            {
                InputAction action = pair.Key;
                if (action == null || action.enabled == pair.Value)
                {
                    continue;
                }

                if (pair.Value)
                {
                    action.Enable();
                }
                else
                {
                    action.Disable();
                }
            }

            previousActionStates.Clear();
        }

        private void OnDisable()
        {
            requestCount = 0;
            RestoreGameplay();
        }

        private sealed class GateLease : IDisposable
        {
            private ModalInputGate gate;

            public GateLease(ModalInputGate gate)
            {
                this.gate = gate;
            }

            public void Dispose()
            {
                if (gate == null)
                {
                    return;
                }

                gate.Release();
                gate = null;
            }
        }
    }
}
