using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaceGame.CommonUI.Input
{
    [Serializable]
    public sealed class InputBindingDefinition
    {
        [SerializeField] private string displayName;
        [SerializeField] private InputActionReference action;
        [SerializeField] private string actionPath;
        [SerializeField] private string bindingId;
        [SerializeField] private string controlScheme;
        [SerializeField] private string conflictGroup;
        [SerializeField] private string logicalBinding;
        [SerializeField] private bool reserved;

        [NonSerialized] private InputActionAsset actionAsset;

        public string DisplayName => displayName;
        public InputActionReference ActionReference => action;
        public InputAction Action
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(actionPath))
                {
                    InputAction resolved =
                        actionAsset?.FindAction(actionPath, false);
                    if (resolved != null)
                    {
                        return resolved;
                    }
                }

                return action?.action;
            }
        }

        public string ActionPath => actionPath;
        public string BindingId => bindingId;
        public string ControlScheme => controlScheme;
        public string ConflictGroup => conflictGroup;
        public string LogicalBinding => logicalBinding;
        public bool IsReserved => reserved;

        public void Configure(
            string name,
            InputActionReference actionReference,
            string id,
            string scheme,
            string group = "",
            string logicalId = "",
            bool isReserved = false)
        {
            displayName = name;
            action = actionReference;
            InputAction inputAction = actionReference?.action;
            actionPath = inputAction?.actionMap == null
                ? inputAction?.name ?? string.Empty
                : $"{inputAction.actionMap.name}/{inputAction.name}";
            bindingId = id;
            controlScheme = scheme;
            conflictGroup = group ?? string.Empty;
            logicalBinding = logicalId ?? string.Empty;
            reserved = isReserved;
        }

        internal void ResolveAgainst(InputActionAsset asset)
        {
            actionAsset = asset;
        }

        public bool TryGetBindingIndex(out int bindingIndex)
        {
            bindingIndex = -1;
            InputAction inputAction = Action;
            if (inputAction == null ||
                !Guid.TryParse(bindingId, out Guid parsedId))
            {
                return false;
            }

            bindingIndex = inputAction.bindings.IndexOf(binding =>
                binding.id == parsedId);
            return bindingIndex >= 0;
        }

        public string GetEffectivePath()
        {
            if (!TryGetBindingIndex(out int index))
            {
                return string.Empty;
            }

            return Action.bindings[index].effectivePath;
        }

        public string GetDisplayString()
        {
            if (!TryGetBindingIndex(out int index))
            {
                return "미지정";
            }

            string value = Action.GetBindingDisplayString(
                index,
                InputBinding.DisplayStringOptions.DontIncludeInteractions);
            return string.IsNullOrWhiteSpace(value) ? "미지정" : value;
        }
    }

    [CreateAssetMenu(
        fileName = "InputBindingCatalog",
        menuName = "Space/Common UI/Input Binding Catalog")]
    public sealed class InputBindingCatalog : ScriptableObject
    {
        [SerializeField] private InputActionAsset actionAsset;
        [SerializeField] private InputActionReference cancelAction;
        [SerializeField] private string cancelActionPath = "Common/Cancel";
        [SerializeField] private List<InputBindingDefinition> bindings =
            new List<InputBindingDefinition>();
        [SerializeField] private List<string> forbiddenControlPaths =
            new List<string>();
        [SerializeField] private List<string> gameplayActionMapIds =
            new List<string>();

        public event Action BindingsChanged;

        public InputActionAsset ActionAsset => actionAsset;
        public InputAction CancelAction
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(cancelActionPath))
                {
                    InputAction resolved =
                        actionAsset?.FindAction(cancelActionPath, false);
                    if (resolved != null)
                    {
                        return resolved;
                    }
                }

                return cancelAction?.action;
            }
        }

        public IReadOnlyList<InputBindingDefinition> Bindings
        {
            get
            {
                ResolveDefinitions();
                return bindings;
            }
        }
        public IReadOnlyList<string> ForbiddenControlPaths =>
            forbiddenControlPaths;
        public IReadOnlyList<string> GameplayActionMapIds =>
            gameplayActionMapIds;

        public void Configure(
            InputActionAsset asset,
            InputActionReference modalCancelAction,
            IEnumerable<InputBindingDefinition> definitions,
            IEnumerable<string> forbiddenPaths,
            IEnumerable<string> gameplayMapIds,
            string modalCancelActionPath = null)
        {
            actionAsset = asset;
            cancelAction = modalCancelAction;
            cancelActionPath = modalCancelActionPath
                ?? GetActionPath(modalCancelAction?.action);
            bindings = definitions == null
                ? new List<InputBindingDefinition>()
                : new List<InputBindingDefinition>(definitions);
            forbiddenControlPaths = forbiddenPaths == null
                ? new List<string>()
                : new List<string>(forbiddenPaths);
            gameplayActionMapIds = gameplayMapIds == null
                ? new List<string>()
                : new List<string>(gameplayMapIds);
            ResolveDefinitions();
        }

        public bool IsForbidden(InputControl control)
        {
            if (control == null)
            {
                return true;
            }

            foreach (string forbiddenPath in forbiddenControlPaths)
            {
                if (InputControlPath.Matches(forbiddenPath, control))
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasDuplicate(
            InputBindingDefinition source,
            InputControl control,
            out InputBindingDefinition duplicate)
        {
            return TryFindConflict(
                source,
                control,
                out duplicate,
                out _);
        }

        public bool TryFindConflict(
            InputBindingDefinition source,
            InputControl control,
            out InputBindingDefinition duplicate,
            out string conflictDisplayName)
        {
            duplicate = null;
            conflictDisplayName = string.Empty;
            if (source == null || control == null)
            {
                return false;
            }

            ResolveDefinitions();
            foreach (InputBindingDefinition candidate in bindings)
            {
                if (ReferenceEquals(candidate, source))
                {
                    continue;
                }

                if (!string.Equals(
                        candidate.ControlScheme,
                        source.ControlScheme,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!ShouldCheckConflict(source, candidate))
                {
                    continue;
                }

                string effectivePath = candidate.GetEffectivePath();
                if (!string.IsNullOrWhiteSpace(effectivePath) &&
                    InputControlPath.Matches(effectivePath, control))
                {
                    duplicate = candidate;
                    conflictDisplayName = candidate.DisplayName;
                    return true;
                }
            }

            // Definitions with an explicit conflict group are intentionally
            // scoped. This allows separate input domains to reuse a control
            // while still rejecting duplicates inside each group.
            if (!string.IsNullOrWhiteSpace(source.ConflictGroup))
            {
                return false;
            }

            if (actionAsset == null ||
                !Guid.TryParse(source.BindingId, out Guid sourceBindingId))
            {
                return false;
            }

            foreach (InputActionMap map in actionAsset.actionMaps)
            {
                if (!IsGameplayMap(map))
                {
                    continue;
                }

                foreach (InputAction action in map.actions)
                {
                    foreach (InputBinding binding in action.bindings)
                    {
                        if (binding.isComposite ||
                            binding.id == sourceBindingId ||
                            !MatchesControlScheme(
                                binding,
                                source.ControlScheme))
                        {
                            continue;
                        }

                        string effectivePath = binding.effectivePath;
                        if (string.IsNullOrWhiteSpace(effectivePath) ||
                            !InputControlPath.Matches(
                                effectivePath,
                                control))
                        {
                            continue;
                        }

                        duplicate = FindOwningDefinition(action, binding);
                        if (ReferenceEquals(duplicate, source))
                        {
                            // A fixed alternative for the same logical
                            // direction is redundant but not contradictory.
                            duplicate = null;
                            continue;
                        }

                        conflictDisplayName = duplicate != null
                            ? duplicate.DisplayName
                            : $"다른 게임 입력 ({action.name})";
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsGameplayMap(InputActionMap map)
        {
            if (map == null)
            {
                return false;
            }

            string mapId = map.id.ToString();
            foreach (string gameplayMapId in gameplayActionMapIds)
            {
                if (string.Equals(
                        gameplayMapId,
                        mapId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private InputBindingDefinition FindOwningDefinition(
            InputAction action,
            InputBinding targetBinding)
        {
            foreach (InputBindingDefinition candidate in bindings)
            {
                if (candidate?.Action != action ||
                    !candidate.TryGetBindingIndex(out int candidateIndex))
                {
                    continue;
                }

                InputBinding candidateBinding =
                    action.bindings[candidateIndex];
                if (candidateBinding.id == targetBinding.id)
                {
                    return candidate;
                }

                if (!string.IsNullOrWhiteSpace(targetBinding.name) &&
                    string.Equals(
                        candidateBinding.name,
                        targetBinding.name,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool MatchesControlScheme(
            InputBinding binding,
            string controlScheme)
        {
            return string.IsNullOrWhiteSpace(controlScheme) ||
                   InputBinding.MaskByGroup(controlScheme).Matches(binding);
        }

        private static bool ShouldCheckConflict(
            InputBindingDefinition source,
            InputBindingDefinition candidate)
        {
            if (source.IsReserved || candidate.IsReserved)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(source.ConflictGroup) ||
                string.IsNullOrWhiteSpace(candidate.ConflictGroup))
            {
                return true;
            }

            return string.Equals(
                source.ConflictGroup,
                candidate.ConflictGroup,
                StringComparison.OrdinalIgnoreCase);
        }

        private void ResolveDefinitions()
        {
            foreach (InputBindingDefinition definition in bindings)
            {
                definition?.ResolveAgainst(actionAsset);
            }
        }

        private static string GetActionPath(InputAction action)
        {
            if (action == null)
            {
                return string.Empty;
            }

            return action.actionMap == null
                ? action.name
                : $"{action.actionMap.name}/{action.name}";
        }

        private void OnEnable()
        {
            ResolveDefinitions();
        }

        public void RemoveAllOverrides()
        {
            actionAsset?.RemoveAllBindingOverrides();
            NotifyBindingsChanged();
        }

        public void NotifyBindingsChanged()
        {
            BindingsChanged?.Invoke();
        }
    }
}
