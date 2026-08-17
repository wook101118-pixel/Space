using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpaceGame.CommonUI.Audio;
using SpaceGame.CommonUI.Display;
using SpaceGame.CommonUI.Input;
using SpaceGame.CommonUI.Modal;
using SpaceGame.CommonUI.Pause;
using SpaceGame.CommonUI.Settings;
using SpaceGame.CommonUI.Views;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace SpaceGame.CommonUI.Tests
{
    public sealed class CommonUISystemTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<InputDevice> devices = new List<InputDevice>();
        private float originalTimeScale;

        [SetUp]
        public void SetUp()
        {
            originalTimeScale = Time.timeScale;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject target in objects)
            {
                if (target != null)
                {
                    UnityEngine.Object.DestroyImmediate(target);
                }
            }

            foreach (InputDevice device in devices)
            {
                if (device != null && device.added)
                {
                    InputSystem.RemoveDevice(device);
                }
            }

            objects.Clear();
            devices.Clear();
            Time.timeScale = originalTimeScale;
        }

        [Test]
        public void PauseRequests_ReleaseOnlyTheirOwnLease()
        {
            Time.timeScale = 0.75f;
            PauseRequestService service =
                CreateComponent<PauseRequestService>("PauseService");

            IDisposable first = service.Acquire("settings");
            IDisposable second = service.Acquire("tutorial");
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(service.RequestCount, Is.EqualTo(2));

            first.Dispose();
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(service.RequestCount, Is.EqualTo(1));

            second.Dispose();
            Assert.That(Time.timeScale, Is.EqualTo(0.75f));
            Assert.That(service.RequestCount, Is.Zero);
        }

        [Test]
        public void CancelRouter_InvokesOnlyHighestPriorityHandler()
        {
            ModalCancelRouter router =
                CreateComponent<ModalCancelRouter>("CancelRouter");

            int lowCount = 0;
            int highCount = 0;
            using IDisposable low = router.Push(
                () =>
                {
                    lowCount++;
                    return true;
                },
                100);
            using IDisposable high = router.Push(
                () =>
                {
                    highCount++;
                    return true;
                },
                400);

            Assert.That(router.TryRouteCancel(), Is.True);

            Assert.That(highCount, Is.EqualTo(1));
            Assert.That(lowCount, Is.Zero);
        }

        [Test]
        public void InputGate_PreservesNestedActionMapState()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            InputActionMap gameplay = asset.AddActionMap("Gameplay");
            gameplay.AddAction("Jump", binding: "<Keyboard>/space");
            InputActionMap initiallyDisabled = asset.AddActionMap("Other");
            initiallyDisabled.AddAction("Use", binding: "<Keyboard>/e");
            gameplay.Enable();

            ModalInputGate gate =
                CreateComponent<ModalInputGate>("InputGate");
            gate.Configure(
                asset,
                new[] {gameplay.id.ToString(), initiallyDisabled.id.ToString()});

            IDisposable first = gate.Acquire();
            IDisposable second = gate.Acquire();
            Assert.That(gameplay.enabled, Is.False);
            Assert.That(initiallyDisabled.enabled, Is.False);

            first.Dispose();
            Assert.That(gameplay.enabled, Is.False);

            second.Dispose();
            Assert.That(gameplay.enabled, Is.True);
            Assert.That(initiallyDisabled.enabled, Is.False);
            UnityEngine.Object.DestroyImmediate(asset);
        }

        [Test]
        public void InputGate_RestoresIndividualActionStatesWithinMap()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            InputActionMap gameplay = asset.AddActionMap("Gameplay");
            InputAction enabledAction = gameplay.AddAction(
                "Move",
                binding: "<Keyboard>/a");
            InputAction disabledAction = gameplay.AddAction(
                "Rotate",
                binding: "<Keyboard>/r");
            enabledAction.Enable();

            Assert.That(enabledAction.enabled, Is.True);
            Assert.That(disabledAction.enabled, Is.False);

            ModalInputGate gate =
                CreateComponent<ModalInputGate>("InputGate");
            gate.Configure(asset, new[] {gameplay.id.ToString()});

            IDisposable lease = gate.Acquire();
            Assert.That(enabledAction.enabled, Is.False);
            Assert.That(disabledAction.enabled, Is.False);

            lease.Dispose();
            Assert.That(enabledAction.enabled, Is.True);
            Assert.That(disabledAction.enabled, Is.False);

            UnityEngine.Object.DestroyImmediate(asset);
        }

        [Test]
        public void BindingOverrides_RestoreAndNotifyImmediately()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            InputActionMap map = asset.AddActionMap("Player");
            InputAction action = map.AddAction(
                "Jump",
                binding: "<Keyboard>/space");
            var definition = new InputBindingDefinition();
            definition.Configure(
                "Jump",
                InputActionReference.Create(action),
                action.bindings[0].id.ToString(),
                "Keyboard&Mouse");
            var catalog = ScriptableObject.CreateInstance<InputBindingCatalog>();
            catalog.Configure(
                asset,
                null,
                new[] {definition},
                Array.Empty<string>(),
                new[] {map.id.ToString()});

            action.ApplyBindingOverride(0, "<Keyboard>/k");
            string json = asset.SaveBindingOverridesAsJson();
            action.RemoveBindingOverride(0);
            int notificationCount = 0;
            catalog.BindingsChanged += () => notificationCount++;

            InputBindingOverrideUtility.Restore(catalog, json);

            Assert.That(
                action.bindings[0].effectivePath,
                Is.EqualTo("<Keyboard>/k"));
            Assert.That(definition.GetDisplayString(), Is.Not.Empty);
            Assert.That(notificationCount, Is.EqualTo(1));
            UnityEngine.Object.DestroyImmediate(catalog);
            UnityEngine.Object.DestroyImmediate(asset);
        }

        [Test]
        public void BindingOverrides_RestoreFiltersRemovedBindings()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            InputActionMap map = asset.AddActionMap("Player");
            InputAction action = map.AddAction(
                "Move",
                binding: "<Keyboard>/a");
            action.AddBinding("<Keyboard>/leftArrow");
            InputActionReference actionReference =
                InputActionReference.Create(action);
            var definition = new InputBindingDefinition();
            definition.Configure(
                "Move",
                actionReference,
                action.bindings[0].id.ToString(),
                "Keyboard&Mouse");

            action.ApplyBindingOverride(0, "<Keyboard>/k");
            action.ApplyBindingOverride(1, "<Keyboard>/l");
            Guid retainedBindingId = action.bindings[0].id;
            Guid removedBindingId = action.bindings[1].id;
            string jsonWithRemovedBinding =
                asset.SaveBindingOverridesAsJson();
            action.ChangeBinding(1).Erase();
            action.RemoveBindingOverride(0);

            var catalog = ScriptableObject.CreateInstance<InputBindingCatalog>();
            catalog.Configure(
                asset,
                null,
                new[] {definition},
                Array.Empty<string>(),
                new[] {map.id.ToString()});
            int notificationCount = 0;
            catalog.BindingsChanged += () => notificationCount++;

            string normalizedJson =
                InputBindingOverrideUtility.RestoreAndNormalize(
                catalog,
                jsonWithRemovedBinding);

            Assert.That(action.bindings.Count, Is.EqualTo(1));
            Assert.That(
                action.bindings[0].effectivePath,
                Is.EqualTo("<Keyboard>/k"));
            Assert.That(notificationCount, Is.EqualTo(1));
            Assert.That(
                normalizedJson,
                Does.Contain(retainedBindingId.ToString()));
            Assert.That(
                normalizedJson,
                Does.Not.Contain(removedBindingId.ToString()));
            string secondPassJson =
                InputBindingOverrideUtility.RestoreAndNormalize(
                    catalog,
                    normalizedJson);
            Assert.That(secondPassJson, Is.EqualTo(normalizedJson));
            LogAssert.NoUnexpectedReceived();

            UnityEngine.Object.DestroyImmediate(catalog);
            UnityEngine.Object.DestroyImmediate(actionReference);
            UnityEngine.Object.DestroyImmediate(asset);
        }

        [Test]
        public void BindingCatalog_RejectsForbiddenAndDuplicateControls()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            devices.Add(keyboard);
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            InputActionMap map = asset.AddActionMap("Player");
            InputAction first = map.AddAction(
                "First",
                binding: "<Keyboard>/a");
            InputAction second = map.AddAction(
                "Second",
                binding: "<Keyboard>/b");
            InputActionReference firstReference =
                InputActionReference.Create(first);
            InputActionReference secondReference =
                InputActionReference.Create(second);
            var firstDefinition = new InputBindingDefinition();
            firstDefinition.Configure(
                "First",
                firstReference,
                first.bindings[0].id.ToString(),
                "Keyboard&Mouse");
            var secondDefinition = new InputBindingDefinition();
            secondDefinition.Configure(
                "Second",
                secondReference,
                second.bindings[0].id.ToString(),
                "Keyboard&Mouse");
            var catalog = ScriptableObject.CreateInstance<InputBindingCatalog>();
            catalog.Configure(
                asset,
                null,
                new[] {firstDefinition, secondDefinition},
                new[] {"<Keyboard>/escape"},
                new[] {map.id.ToString()});

            Assert.That(catalog.IsForbidden(keyboard.escapeKey), Is.True);
            Assert.That(
                catalog.HasDuplicate(
                    firstDefinition,
                    keyboard.bKey,
                    out InputBindingDefinition duplicate),
                Is.True);
            Assert.That(duplicate, Is.SameAs(secondDefinition));

            UnityEngine.Object.DestroyImmediate(catalog);
            UnityEngine.Object.DestroyImmediate(firstReference);
            UnityEngine.Object.DestroyImmediate(secondReference);
            UnityEngine.Object.DestroyImmediate(asset);
        }

        [Test]
        public void BindingCatalog_AllowsSameControlAcrossExplicitConflictGroups()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            devices.Add(keyboard);
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            InputActionMap map = asset.AddActionMap("Player");
            InputAction gameplay = map.AddAction(
                "GameplayLeft",
                binding: "<Keyboard>/a");
            InputAction pointerCommand = map.AddAction(
                "PointerCommand",
                binding: "<Keyboard>/leftArrow");
            InputActionReference gameplayReference =
                InputActionReference.Create(gameplay);
            InputActionReference pointerReference =
                InputActionReference.Create(pointerCommand);
            var gameplayDefinition = new InputBindingDefinition();
            gameplayDefinition.Configure(
                "Gameplay Left",
                gameplayReference,
                gameplay.bindings[0].id.ToString(),
                "Keyboard&Mouse",
                "Gameplay");
            var pointerDefinition = new InputBindingDefinition();
            pointerDefinition.Configure(
                "Pointer Command",
                pointerReference,
                pointerCommand.bindings[0].id.ToString(),
                "Keyboard&Mouse",
                "UIPointer");
            var catalog = ScriptableObject.CreateInstance<InputBindingCatalog>();
            catalog.Configure(
                asset,
                null,
                new[] {gameplayDefinition, pointerDefinition},
                Array.Empty<string>(),
                new[] {map.id.ToString()});

            Assert.That(
                catalog.TryFindConflict(
                    gameplayDefinition,
                    keyboard.leftArrowKey,
                    out _,
                    out _),
                Is.False);

            UnityEngine.Object.DestroyImmediate(catalog);
            UnityEngine.Object.DestroyImmediate(gameplayReference);
            UnityEngine.Object.DestroyImmediate(pointerReference);
            UnityEngine.Object.DestroyImmediate(asset);
        }

        [Test]
        public void BindingCatalog_RejectsSameControlWithinExplicitConflictGroup()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            devices.Add(keyboard);
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            InputActionMap map = asset.AddActionMap("Player");
            InputAction left = map.AddAction(
                "MoveLeft",
                binding: "<Keyboard>/a");
            InputAction right = map.AddAction(
                "MoveRight",
                binding: "<Keyboard>/d");
            InputActionReference leftReference =
                InputActionReference.Create(left);
            InputActionReference rightReference =
                InputActionReference.Create(right);
            var leftDefinition = new InputBindingDefinition();
            leftDefinition.Configure(
                "Move Left",
                leftReference,
                left.bindings[0].id.ToString(),
                "Keyboard&Mouse",
                "Gameplay");
            var rightDefinition = new InputBindingDefinition();
            rightDefinition.Configure(
                "Move Right",
                rightReference,
                right.bindings[0].id.ToString(),
                "Keyboard&Mouse",
                "Gameplay");
            var catalog = ScriptableObject.CreateInstance<InputBindingCatalog>();
            catalog.Configure(
                asset,
                null,
                new[] {leftDefinition, rightDefinition},
                Array.Empty<string>(),
                new[] {map.id.ToString()});

            Assert.That(
                catalog.TryFindConflict(
                    leftDefinition,
                    keyboard.dKey,
                    out InputBindingDefinition duplicate,
                    out string conflictDisplayName),
                Is.True);
            Assert.That(duplicate, Is.SameAs(rightDefinition));
            Assert.That(
                conflictDisplayName,
                Is.EqualTo(rightDefinition.DisplayName));

            UnityEngine.Object.DestroyImmediate(catalog);
            UnityEngine.Object.DestroyImmediate(leftReference);
            UnityEngine.Object.DestroyImmediate(rightReference);
            UnityEngine.Object.DestroyImmediate(asset);
        }

        [Test]
        public void BindingCatalog_ReservedCancelRejectsSameControlAcrossGroups()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            devices.Add(keyboard);
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            InputActionMap map = asset.AddActionMap("Common");
            InputAction gameplay = map.AddAction(
                "GameplayAction",
                binding: "<Keyboard>/a");
            InputAction cancel = map.AddAction(
                "Cancel",
                binding: "<Keyboard>/escape");
            InputActionReference gameplayReference =
                InputActionReference.Create(gameplay);
            InputActionReference cancelReference =
                InputActionReference.Create(cancel);
            var gameplayDefinition = new InputBindingDefinition();
            gameplayDefinition.Configure(
                "Gameplay Action",
                gameplayReference,
                gameplay.bindings[0].id.ToString(),
                "Keyboard&Mouse",
                "Gameplay");
            var cancelDefinition = new InputBindingDefinition();
            cancelDefinition.Configure(
                "Common Cancel",
                cancelReference,
                cancel.bindings[0].id.ToString(),
                "Keyboard&Mouse",
                "CommonUI",
                "Common/Cancel",
                true);
            var catalog = ScriptableObject.CreateInstance<InputBindingCatalog>();
            catalog.Configure(
                asset,
                cancelReference,
                new[] {gameplayDefinition, cancelDefinition},
                Array.Empty<string>(),
                new[] {map.id.ToString()});

            Assert.That(
                catalog.TryFindConflict(
                    gameplayDefinition,
                    keyboard.escapeKey,
                    out InputBindingDefinition duplicate,
                    out string conflictDisplayName),
                Is.True);
            Assert.That(duplicate, Is.SameAs(cancelDefinition));
            Assert.That(
                conflictDisplayName,
                Is.EqualTo(cancelDefinition.DisplayName));

            UnityEngine.Object.DestroyImmediate(catalog);
            UnityEngine.Object.DestroyImmediate(gameplayReference);
            UnityEngine.Object.DestroyImmediate(cancelReference);
            UnityEngine.Object.DestroyImmediate(asset);
        }

        [Test]
        public void BindingDefinition_ResolvesActionFromStoredActionPath()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            InputActionMap map = asset.AddActionMap("Player");
            InputAction action = map.AddAction(
                "RotateBlock",
                binding: "<Keyboard>/r");
            InputActionReference actionReference =
                InputActionReference.Create(action);
            var definition = new InputBindingDefinition();
            definition.Configure(
                "Rotate Block",
                actionReference,
                action.bindings[0].id.ToString(),
                "Keyboard&Mouse",
                "Gameplay");
            UnityEngine.Object.DestroyImmediate(actionReference);

            var catalog = ScriptableObject.CreateInstance<InputBindingCatalog>();
            catalog.Configure(
                asset,
                null,
                new[] {definition},
                Array.Empty<string>(),
                new[] {map.id.ToString()});

            Assert.That(definition.ActionPath, Is.EqualTo("Player/RotateBlock"));
            Assert.That(definition.Action, Is.SameAs(action));
            Assert.That(definition.TryGetBindingIndex(out int index), Is.True);
            Assert.That(index, Is.Zero);

            UnityEngine.Object.DestroyImmediate(catalog);
            UnityEngine.Object.DestroyImmediate(asset);
        }

        [Test]
        public void RebindRow_KeyboardTargetExcludesMouseCandidates()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            devices.Add(keyboard);
            devices.Add(mouse);
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.AddControlScheme("Keyboard&Mouse")
                .WithRequiredDevice("<Keyboard>")
                .WithRequiredDevice("<Mouse>");
            InputActionMap map = asset.AddActionMap("Player");
            InputAction action = map.AddAction(
                "MoveLeft",
                InputActionType.Button);
            action.AddBinding(
                "<Keyboard>/a",
                groups: "Keyboard&Mouse");
            InputActionReference actionReference =
                InputActionReference.Create(action);
            var definition = new InputBindingDefinition();
            definition.Configure(
                "Move Left",
                actionReference,
                action.bindings[0].id.ToString(),
                "Keyboard&Mouse",
                "Gameplay");
            var catalog = ScriptableObject.CreateInstance<InputBindingCatalog>();
            catalog.Configure(
                asset,
                null,
                new[] {definition},
                Array.Empty<string>(),
                new[] {map.id.ToString()});
            ModalCancelRouter router =
                CreateComponent<ModalCancelRouter>("CancelRouter");
            router.Configure(null, null);
            InputBindingRowView row = CreateInteractiveRebindRow();
            row.Initialize(definition, catalog, router, null);

            row.SendMessage(
                "BeginRebind",
                SendMessageOptions.RequireReceiver);
            Assert.That(row.IsRebinding, Is.True);

            InputSystem.QueueStateEvent(
                mouse,
                new MouseState().WithButton(MouseButton.Right));
            InputSystem.Update();
            Assert.That(row.IsRebinding, Is.True);
            Assert.That(
                definition.GetEffectivePath(),
                Is.EqualTo("<Keyboard>/a"));

            InputSystem.QueueStateEvent(mouse, new MouseState());
            InputSystem.Update();
            InputSystem.QueueStateEvent(
                keyboard,
                new KeyboardState(Key.B));
            InputSystem.Update();
            Assert.That(row.IsRebinding, Is.False);
            Assert.That(
                definition.GetEffectivePath(),
                Is.EqualTo("<Keyboard>/b"));

            UnityEngine.Object.DestroyImmediate(catalog);
            UnityEngine.Object.DestroyImmediate(actionReference);
            UnityEngine.Object.DestroyImmediate(asset);
        }

        [Test]
        public void RebindRow_MouseTargetExcludesKeyboardCandidates()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            devices.Add(keyboard);
            devices.Add(mouse);
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.AddControlScheme("Keyboard&Mouse")
                .WithRequiredDevice("<Keyboard>")
                .WithRequiredDevice("<Mouse>");
            InputActionMap map = asset.AddActionMap("UI");
            InputAction action = map.AddAction(
                "Click",
                InputActionType.Button);
            action.AddBinding(
                "<Mouse>/leftButton",
                groups: "Keyboard&Mouse");
            InputActionReference actionReference =
                InputActionReference.Create(action);
            var definition = new InputBindingDefinition();
            definition.Configure(
                "UI Click",
                actionReference,
                action.bindings[0].id.ToString(),
                "Keyboard&Mouse",
                "UIPointer");
            var catalog = ScriptableObject.CreateInstance<InputBindingCatalog>();
            catalog.Configure(
                asset,
                null,
                new[] {definition},
                Array.Empty<string>(),
                Array.Empty<string>());
            ModalCancelRouter router =
                CreateComponent<ModalCancelRouter>("CancelRouter");
            router.Configure(null, null);
            InputBindingRowView row = CreateInteractiveRebindRow();
            row.Initialize(definition, catalog, router, null);

            row.SendMessage(
                "BeginRebind",
                SendMessageOptions.RequireReceiver);
            Assert.That(row.IsRebinding, Is.True);

            InputSystem.QueueStateEvent(
                keyboard,
                new KeyboardState(Key.B));
            InputSystem.Update();
            Assert.That(row.IsRebinding, Is.True);
            Assert.That(
                definition.GetEffectivePath(),
                Is.EqualTo("<Mouse>/leftButton"));

            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            InputSystem.QueueStateEvent(
                mouse,
                new MouseState().WithButton(MouseButton.Right));
            InputSystem.Update();
            Assert.That(row.IsRebinding, Is.False);
            Assert.That(
                definition.GetEffectivePath(),
                Is.EqualTo("<Mouse>/rightButton"));

            UnityEngine.Object.DestroyImmediate(catalog);
            UnityEngine.Object.DestroyImmediate(actionReference);
            UnityEngine.Object.DestroyImmediate(asset);
        }

        [Test]
        public void BindingCatalog_RejectsUnlistedAlternativeConflicts()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            devices.Add(keyboard);
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            InputActionMap map = asset.AddActionMap("Player");
            InputAction move = map.AddAction("Move");
            move.AddBinding(
                    "<Keyboard>/a",
                    groups: "Keyboard&Mouse")
                .WithName("left");
            move.AddBinding(
                    "<Keyboard>/leftArrow",
                    groups: "Keyboard&Mouse")
                .WithName("left");
            move.AddBinding(
                    "<Keyboard>/d",
                    groups: "Keyboard&Mouse")
                .WithName("right");
            move.AddBinding(
                    "<Keyboard>/rightArrow",
                    groups: "Keyboard&Mouse")
                .WithName("right");

            InputActionReference moveReference =
                InputActionReference.Create(move);
            var leftDefinition = new InputBindingDefinition();
            leftDefinition.Configure(
                "왼쪽 이동",
                moveReference,
                move.bindings[0].id.ToString(),
                "Keyboard&Mouse");
            var rightDefinition = new InputBindingDefinition();
            rightDefinition.Configure(
                "오른쪽 이동",
                moveReference,
                move.bindings[2].id.ToString(),
                "Keyboard&Mouse");
            var catalog = ScriptableObject.CreateInstance<InputBindingCatalog>();
            catalog.Configure(
                asset,
                null,
                new[] {leftDefinition, rightDefinition},
                Array.Empty<string>(),
                new[] {map.id.ToString()});

            Assert.That(
                catalog.TryFindConflict(
                    leftDefinition,
                    keyboard.rightArrowKey,
                    out InputBindingDefinition duplicate,
                    out string conflictDisplayName),
                Is.True);
            Assert.That(duplicate, Is.SameAs(rightDefinition));
            Assert.That(
                conflictDisplayName,
                Is.EqualTo(rightDefinition.DisplayName));
            Assert.That(
                catalog.TryFindConflict(
                    leftDefinition,
                    keyboard.leftArrowKey,
                    out _,
                    out _),
                Is.False);

            UnityEngine.Object.DestroyImmediate(catalog);
            UnityEngine.Object.DestroyImmediate(moveReference);
            UnityEngine.Object.DestroyImmediate(asset);
        }

        [Test]
        public void SettingsCoordinator_PreviewsCommitsAndRestores()
        {
            var repository = new MemoryRepository();
            var audio = new RecordingAudioAdapter();
            var screen = new RecordingScreenApplier();
            var coordinator =
                new SettingsCoordinator(repository, audio, screen);
            coordinator.LoadAndApply();

            GameSettingsData snapshot = coordinator.BeginEdit();
            GameSettingsData working = snapshot.Clone();
            working.masterVolume = 0.25f;
            coordinator.PreviewAudio(working);
            Assert.That(
                audio.LastApplied.masterVolume,
                Is.EqualTo(0.25f));

            coordinator.RestorePreview(snapshot);
            Assert.That(
                audio.LastApplied.masterVolume,
                Is.EqualTo(snapshot.masterVolume));

            coordinator.Commit(working);
            Assert.That(repository.SaveCount, Is.EqualTo(1));
            Assert.That(
                repository.Stored.masterVolume,
                Is.EqualTo(0.25f));
            Assert.That(
                screen.LastApplied.masterVolume,
                Is.EqualTo(0.25f));
        }

        [Test]
        public void GameSettingsDefaults_Are1920By1080Fullscreen()
        {
            GameSettingsData defaults = GameSettingsData.CreateDefault();

            Assert.That(defaults.fullscreen, Is.True);
            Assert.That(defaults.resolutionWidth, Is.EqualTo(1920));
            Assert.That(defaults.resolutionHeight, Is.EqualTo(1080));
        }

        private T CreateComponent<T>(string name)
            where T : Component
        {
            var target = new GameObject(name);
            objects.Add(target);
            return target.AddComponent<T>();
        }

        private InputBindingRowView CreateInteractiveRebindRow()
        {
            InputBindingRowView row =
                CreateComponent<InputBindingRowView>("InputBindingRow");
            Type textType = Type.GetType(
                "TMPro.TextMeshProUGUI, Unity.TextMeshPro");
            Type buttonType = Type.GetType(
                "UnityEngine.UI.Button, UnityEngine.UI");
            Assert.That(textType, Is.Not.Null);
            Assert.That(buttonType, Is.Not.Null);

            Component nameLabel = CreateComponent(
                "BindingName",
                textType);
            Component bindingLabel = CreateComponent(
                "BindingValue",
                textType);
            Component rebindButton = CreateComponent(
                "RebindButton",
                buttonType);
            Component resetButton = CreateComponent(
                "ResetButton",
                buttonType);
            var configureView = typeof(InputBindingRowView).GetMethod(
                nameof(InputBindingRowView.ConfigureView));
            Assert.That(configureView, Is.Not.Null);
            configureView.Invoke(
                row,
                new object[]
                {
                    nameLabel,
                    bindingLabel,
                    rebindButton,
                    resetButton
                });
            return row;
        }

        private Component CreateComponent(string name, Type type)
        {
            var target = new GameObject(name);
            objects.Add(target);
            return target.AddComponent(type);
        }

        private sealed class MemoryRepository : ISettingsRepository
        {
            public int SaveCount { get; private set; }
            public GameSettingsData Stored { get; private set; }

            public bool TryLoad(out GameSettingsData settings)
            {
                settings = GameSettingsData.CreateDefault();
                return true;
            }

            public void Save(GameSettingsData settings)
            {
                SaveCount++;
                Stored = settings.Clone();
            }
        }

        private sealed class RecordingAudioAdapter : IAudioSettingsAdapter
        {
            public GameSettingsData LastApplied { get; private set; }

            public void Apply(GameSettingsData settings)
            {
                LastApplied = settings.Clone();
            }
        }

        private sealed class RecordingScreenApplier :
            IScreenSettingsApplier
        {
            public GameSettingsData LastApplied { get; private set; }

            public IReadOnlyList<ResolutionOption>
                GetAvailableResolutions()
            {
                return Array.Empty<ResolutionOption>();
            }

            public void Apply(GameSettingsData settings)
            {
                LastApplied = settings.Clone();
            }
        }
    }
}
