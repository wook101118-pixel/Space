using System;
using SpaceGame.CommonUI.Input;
using SpaceGame.CommonUI.Modal;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SpaceGame.CommonUI.Views
{
    [DisallowMultipleComponent]
    public sealed class InputBindingRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text displayNameText;
        [SerializeField] private TMP_Text bindingText;
        [SerializeField] private Button rebindButton;
        [SerializeField] private Button resetButton;

        private InputBindingDefinition definition;
        private InputBindingCatalog catalog;
        private ModalCancelRouter cancelRouter;
        private Action<string> statusChanged;
        private Action<InputBindingRowView> rebindStarted;
        private Action<InputBindingRowView> rebindEnded;
        private InputActionRebindingExtensions.RebindingOperation operation;
        private IDisposable cancelRegistration;
        private bool actionWasEnabled;

        public bool IsRebinding => operation != null;

        public void ConfigureView(
            TMP_Text nameLabel,
            TMP_Text bindingLabel,
            Button changeButton,
            Button defaultButton)
        {
            displayNameText = nameLabel;
            bindingText = bindingLabel;
            rebindButton = changeButton;
            resetButton = defaultButton;
        }

        public void Initialize(
            InputBindingDefinition bindingDefinition,
            InputBindingCatalog bindingCatalog,
            ModalCancelRouter router,
            Action<string> onStatusChanged,
            Action<InputBindingRowView> onRebindStarted = null,
            Action<InputBindingRowView> onRebindEnded = null)
        {
            definition = bindingDefinition;
            catalog = bindingCatalog;
            cancelRouter = router;
            statusChanged = onStatusChanged;
            rebindStarted = onRebindStarted;
            rebindEnded = onRebindEnded;
            displayNameText.text = definition.DisplayName;
            rebindButton.onClick.AddListener(BeginRebind);
            resetButton.onClick.AddListener(ResetBinding);
            Refresh();
        }

        public void Refresh()
        {
            if (definition != null && operation == null)
            {
                displayNameText.SetText(definition.DisplayName);
                bindingText.SetText(
                    $"현재 키: {definition.GetDisplayString()}");
                displayNameText.SetLayoutDirty();
                displayNameText.SetVerticesDirty();
                bindingText.SetLayoutDirty();
                bindingText.SetVerticesDirty();
                displayNameText.ForceMeshUpdate(true, true);
                bindingText.ForceMeshUpdate(true, true);
            }
        }

        public bool CancelRebind()
        {
            if (operation == null)
            {
                return false;
            }

            operation.Cancel();
            return true;
        }

        private void BeginRebind()
        {
            if (operation != null ||
                !definition.TryGetBindingIndex(out int bindingIndex))
            {
                return;
            }

            rebindStarted?.Invoke(this);

            InputAction action = definition.Action;
            actionWasEnabled = action.enabled;
            if (actionWasEnabled)
            {
                action.Disable();
            }

            bindingText.text = "입력을 기다리는 중…";
            statusChanged?.Invoke(
                $"{definition.DisplayName}: 새 키를 누르세요. ESC로 취소합니다.");

            cancelRegistration = cancelRouter.Push(CancelRebind, 1000);
            operation = action.PerformInteractiveRebinding(bindingIndex);
            string deviceLayout = InputControlPath.TryGetDeviceLayout(
                action.bindings[bindingIndex].path);
            if (!string.IsNullOrWhiteSpace(deviceLayout) &&
                deviceLayout != "*")
            {
                operation.WithControlsHavingToMatchPath(
                    $"<{deviceLayout}>");

                // WithTargetBinding also includes every device required by
                // the Keyboard&Mouse control scheme. Include paths are ORed,
                // so explicitly exclude the opposite device to keep keyboard
                // rows on the keyboard and the UI click row on the mouse.
                if (string.Equals(
                        deviceLayout,
                        "Keyboard",
                        StringComparison.OrdinalIgnoreCase))
                {
                    operation.WithControlsExcluding("<Mouse>");
                }
                else if (string.Equals(
                             deviceLayout,
                             "Mouse",
                             StringComparison.OrdinalIgnoreCase))
                {
                    operation.WithControlsExcluding("<Keyboard>");
                }
            }

            foreach (string forbiddenPath in catalog.ForbiddenControlPaths)
            {
                operation.WithControlsExcluding(forbiddenPath);
            }

            operation
                .OnMatchWaitForAnother(0f)
                .WithCancelingThrough("<Keyboard>/escape")
                .OnPotentialMatch(ValidatePotentialMatch)
                .OnCancel(OnRebindCancelled)
                .OnComplete(OnRebindCompleted)
                .Start();
        }

        private void ValidatePotentialMatch(
            InputActionRebindingExtensions.RebindingOperation currentOperation)
        {
            InputControl selectedControl = currentOperation.selectedControl;
            if (selectedControl == null)
            {
                return;
            }

            if (catalog.IsForbidden(selectedControl))
            {
                statusChanged?.Invoke("이 입력은 사용할 수 없습니다.");
                currentOperation.RemoveCandidate(selectedControl);
                return;
            }

            if (catalog.TryFindConflict(
                    definition,
                    selectedControl,
                    out _,
                    out string conflictDisplayName))
            {
                statusChanged?.Invoke(
                    $"이미 사용 중인 키입니다: {conflictDisplayName}");
                currentOperation.RemoveCandidate(selectedControl);
                return;
            }

            currentOperation.Complete();
        }

        private void OnRebindCancelled(
            InputActionRebindingExtensions.RebindingOperation completedOperation)
        {
            statusChanged?.Invoke("키 변경을 취소했습니다.");
            CleanupOperation();
            Refresh();
        }

        private void OnRebindCompleted(
            InputActionRebindingExtensions.RebindingOperation completedOperation)
        {
            CleanupOperation();
            catalog.NotifyBindingsChanged();
            statusChanged?.Invoke(
                $"{definition.DisplayName}: {definition.GetDisplayString()}");
            Refresh();
        }

        private void CleanupOperation()
        {
            InputAction action = definition.Action;
            operation?.Dispose();
            operation = null;
            cancelRegistration?.Dispose();
            cancelRegistration = null;
            if (actionWasEnabled && !action.enabled)
            {
                action.Enable();
            }

            actionWasEnabled = false;
            rebindEnded?.Invoke(this);
        }

        private void ResetBinding()
        {
            CancelRebind();

            if (!definition.TryGetBindingIndex(out int bindingIndex))
            {
                return;
            }

            definition.Action.RemoveBindingOverride(bindingIndex);
            catalog.NotifyBindingsChanged();
            statusChanged?.Invoke(
                $"{definition.DisplayName} 기본값을 복원했습니다.");
            Refresh();
        }

        private void OnDestroy()
        {
            CancelRebind();
            rebindButton?.onClick.RemoveListener(BeginRebind);
            resetButton?.onClick.RemoveListener(ResetBinding);
        }

        private void OnDisable()
        {
            CancelRebind();
        }
    }
}
