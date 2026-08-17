#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Dev.NKY.Scripts.EditorTests
{
    public sealed class PlayerProgressPersistenceTests
    {
        [Test]
        public void LegacyMigration_IsIdempotentAndPreservesValues()
        {
            var store = new MemoryProgressStore();
            store.SetInt("SpaceGame.PlayerStats.Initialized", 1);
            store.SetFloat("SpaceGame.PlayerStats.Engine", 125f);
            store.SetFloat("SpaceGame.PlayerStats.Fuel", 230f);
            store.SetFloat("SpaceGame.PlayerStats.Armor", 145f);
            store.SetFloat("SpaceGame.PlayerStats.Drill", 160f);
            var persistence = new PlayerProgressPersistence(store);
            Dictionary<StatType, float> defaults =
                CreateDefaults(100f);

            Dictionary<StatType, float> first =
                persistence.LoadBaseStats(defaults);
            int savesAfterFirstLoad = store.SaveCount;
            int writesAfterFirstLoad = store.WriteCount;
            Dictionary<StatType, float> second =
                persistence.LoadBaseStats(defaults);

            Assert.That(first[StatType.Engine], Is.EqualTo(125f));
            Assert.That(first[StatType.Fuel], Is.EqualTo(230f));
            Assert.That(first[StatType.Armor], Is.EqualTo(145f));
            Assert.That(first[StatType.Drill], Is.EqualTo(160f));
            Assert.That(second, Is.EquivalentTo(first));
            Assert.That(
                store.GetFloat(
                    "SpaceGame.PlayerStats.LegacyBackup.Engine",
                    -1f),
                Is.EqualTo(125f));
            Assert.That(
                store.GetFloat(
                    "SpaceGame.PlayerStats.LegacyBackup.Fuel",
                    -1f),
                Is.EqualTo(230f));
            Assert.That(store.SaveCount, Is.EqualTo(savesAfterFirstLoad));
            Assert.That(store.WriteCount, Is.EqualTo(writesAfterFirstLoad));
        }

        [Test]
        public void VersionedSave_WithMissingBaseKey_RepairsFallback()
        {
            var store = new MemoryProgressStore();
            store.SetInt("SpaceGame.PlayerStats.SchemaVersion", 2);
            store.SetFloat("SpaceGame.PlayerStats.Base.Engine", 120f);
            store.SetFloat("SpaceGame.PlayerStats.Base.Fuel", 130f);
            store.SetFloat("SpaceGame.PlayerStats.Base.Armor", 140f);
            int writesBeforeLoad = store.WriteCount;
            var persistence = new PlayerProgressPersistence(store);

            Dictionary<StatType, float> loaded =
                persistence.LoadBaseStats(CreateDefaults(100f));

            Assert.That(loaded[StatType.Drill], Is.EqualTo(100f));
            Assert.That(
                store.GetFloat(
                    "SpaceGame.PlayerStats.Base.Drill",
                    -1f),
                Is.EqualTo(100f));
            Assert.That(store.WriteCount, Is.EqualTo(writesBeforeLoad + 2));
            Assert.That(store.SaveCount, Is.EqualTo(1));
        }

        [Test]
        public void VersionTwoSave_ClampsUnsafeStatsAndPreservesBackups()
        {
            var store = new MemoryProgressStore();
            store.SetInt("SpaceGame.PlayerStats.SchemaVersion", 2);
            store.SetFloat("SpaceGame.PlayerStats.Base.Engine", 688.6f);
            store.SetFloat("SpaceGame.PlayerStats.Base.Fuel", 280.1f);
            store.SetFloat("SpaceGame.PlayerStats.Base.Armor", 170f);
            store.SetFloat("SpaceGame.PlayerStats.Base.Drill", 1061.2f);
            var persistence = new PlayerProgressPersistence(store);

            Dictionary<StatType, float> loaded =
                persistence.LoadBaseStats(CreateDefaults(100f));

            Assert.That(
                loaded[StatType.Engine],
                Is.EqualTo(PlayerStats.MaximumPermanentEngineStat));
            Assert.That(loaded[StatType.Fuel], Is.EqualTo(280.1f));
            Assert.That(loaded[StatType.Armor], Is.EqualTo(170f));
            Assert.That(
                loaded[StatType.Drill],
                Is.EqualTo(
                    SpaceGame.RunOutcome.RunRewardCalculator
                        .MaximumPermanentDrillStat));
            Assert.That(
                store.GetFloat(
                    "SpaceGame.PlayerStats.SafetyBackupV3.Engine",
                    -1f),
                Is.EqualTo(688.6f));
            Assert.That(
                store.GetFloat(
                    "SpaceGame.PlayerStats.SafetyBackupV3.Drill",
                    -1f),
                Is.EqualTo(1061.2f));
            Assert.That(
                store.GetInt("SpaceGame.PlayerStats.SchemaVersion", 0),
                Is.EqualTo(3));
        }

        [Test]
        public void UpgradeCost_PersistsAcrossRepositoryInstances()
        {
            var store = new MemoryProgressStore();
            var first = new PlayerProgressPersistence(store);

            Assert.That(
                first.LoadOrCreateNextUpgradeCost(
                    StatType.Engine,
                    100),
                Is.EqualTo(100));

            first.SaveNextUpgradeCost(StatType.Engine, 200);
            var reloaded = new PlayerProgressPersistence(store);

            Assert.That(
                reloaded.LoadOrCreateNextUpgradeCost(
                    StatType.Engine,
                    100),
                Is.EqualTo(200));
        }

        [Test]
        public void EquippedModifier_ChangesFlightSnapshotButNotSavedBase()
        {
            var store = new MemoryProgressStore();
            var statsObject = new GameObject("PlayerStats");
            var parts = ScriptableObject.CreateInstance<MachinePartsDataSo>();
            var blockData = ScriptableObject.CreateInstance<BlockData>();

            try
            {
                using (PlayerProgressPersistence.OverrideDefaultForTests(
                           store))
                {
                    PlayerStats stats =
                        statsObject.AddComponent<PlayerStats>();
                    stats.InitializeForTests();
                    parts.statData = new List<StatModifier>
                    {
                        new StatModifier
                        {
                            type = StatType.Engine,
                            modifierType = ModifierType.Flat,
                            value = 25f
                        }
                    };
                    int writesBeforeModifier = store.WriteCount;

                    stats.ApplyModifiers(
                        new BlockInstance(
                            blockData,
                            parts,
                            Vector2Int.zero));

                    Assert.That(
                        stats.GetStat(StatType.Engine),
                        Is.EqualTo(125f));
                    Assert.That(
                        PlayerStats.GetSavedStats()[StatType.Engine],
                        Is.EqualTo(125f));
                    Assert.That(
                        new PlayerProgressPersistence(store)
                            .LoadBaseStats(CreateDefaults(100f))[
                                StatType.Engine],
                        Is.EqualTo(100f));
                    Assert.That(
                        store.WriteCount,
                        Is.EqualTo(writesBeforeModifier));
                }
            }
            finally
            {
                PlayerStats.ResetSessionFlightStatsForTests();
                Object.DestroyImmediate(blockData);
                Object.DestroyImmediate(parts);
                Object.DestroyImmediate(statsObject);
            }
        }

        [Test]
        public void ExtremePartModifiers_KeepStatsInPlayableBounds()
        {
            var store = new MemoryProgressStore();
            var statsObject = new GameObject("PlayerStats");
            var negativeParts =
                ScriptableObject.CreateInstance<MachinePartsDataSo>();
            var positiveParts =
                ScriptableObject.CreateInstance<MachinePartsDataSo>();
            var blockData = ScriptableObject.CreateInstance<BlockData>();

            try
            {
                using (PlayerProgressPersistence.OverrideDefaultForTests(
                           store))
                {
                    PlayerStats stats =
                        statsObject.AddComponent<PlayerStats>();
                    stats.InitializeForTests();
                    negativeParts.statData = new List<StatModifier>
                    {
                        new StatModifier
                        {
                            type = StatType.Engine,
                            modifierType = ModifierType.Multiplier,
                            value = -10f
                        }
                    };
                    positiveParts.statData = new List<StatModifier>
                    {
                        new StatModifier
                        {
                            type = StatType.Engine,
                            modifierType = ModifierType.Multiplier,
                            value = 10f
                        }
                    };
                    var negativeInstance = new BlockInstance(
                        blockData,
                        negativeParts,
                        Vector2Int.zero);

                    stats.ApplyModifiers(negativeInstance);
                    Assert.That(
                        stats.GetStat(StatType.Engine),
                        Is.EqualTo(25f));

                    stats.RemoveModifiers(negativeInstance);
                    stats.ApplyModifiers(
                        new BlockInstance(
                            blockData,
                            positiveParts,
                            Vector2Int.zero));
                    Assert.That(
                        stats.GetStat(StatType.Engine),
                        Is.EqualTo(200f));
                }
            }
            finally
            {
                PlayerStats.ResetSessionFlightStatsForTests();
                Object.DestroyImmediate(blockData);
                Object.DestroyImmediate(negativeParts);
                Object.DestroyImmediate(positiveParts);
                Object.DestroyImmediate(statsObject);
            }
        }

        [TestCase(3, 0.75f)]
        [TestCase(4, 1f)]
        [TestCase(6, 1.5f)]
        public void PartShapeEfficiency_ScalesWithOccupiedCells(
            int occupiedCells,
            float expectedMultiplier)
        {
            Assert.That(
                DraggableBlockView.GetShapeEfficiencyMultiplier(
                    occupiedCells),
                Is.EqualTo(expectedMultiplier));
        }

        [Test]
        public void ResourceManager_SaturatesRewardsAndRejectsNegativeCosts()
        {
            const string resourceKey = "SpaceGame.Resource.Current";
            var resourceObject = new GameObject("ResourceManager");

            try
            {
                PlayerPrefs.DeleteKey(resourceKey);
                ResourceManager resourceManager =
                    resourceObject.AddComponent<ResourceManager>();
                resourceManager.CurrentResource = int.MaxValue - 2;

                resourceManager.AddResource(10);

                Assert.That(
                    resourceManager.CurrentResource,
                    Is.EqualTo(int.MaxValue));
                Assert.Throws<System.ArgumentOutOfRangeException>(
                    () => resourceManager.ConsumeResource(-1));
            }
            finally
            {
                PlayerPrefs.DeleteKey(resourceKey);
                Object.DestroyImmediate(resourceObject);
            }
        }

        [Test]
        public void UpgradeCostCalculation_MatchesAuthoredProgression()
        {
            Assert.That(
                BaseStatController.CalculateNextUpgradeCost(
                    100,
                    1.22f,
                    20),
                Is.EqualTo(142));
            Assert.That(
                BaseStatController.CalculateNextUpgradeCost(
                    int.MaxValue,
                    2f,
                    100),
                Is.EqualTo(int.MaxValue));
        }

        [TestCase(250f)]
        [TestCase(300f)]
        [TestCase(450f)]
        public void DrillAtOrAbovePermanentCap_DoesNotSpendOrReduceProgress(
            float permanentDrill)
        {
            var store = new MemoryProgressStore();
            var statsObject = new GameObject("PlayerStats");
            var resourceObject = new GameObject("ResourceManager");
            var controllerObject = new GameObject("BaseStatController");
            var costTextObject = new GameObject(
                "Drill Cost",
                typeof(RectTransform),
                typeof(TMPro.TextMeshProUGUI));

            try
            {
                using (PlayerProgressPersistence.OverrideDefaultForTests(
                           store))
                {
                    PlayerStats stats =
                        statsObject.AddComponent<PlayerStats>();
                    stats.InitializeForTests();
                    stats.UpgradeBaseStat(
                        StatType.Drill,
                        permanentDrill - PlayerStats.DefaultStatValue);

                    ResourceManager resources =
                        resourceObject.AddComponent<ResourceManager>();
                    resources.CurrentResource = 1000;
                    BaseStatController controller =
                        controllerObject.AddComponent<BaseStatController>();
                    var serializedController =
                        new SerializedObject(controller);
                    serializedController.FindProperty("playerStats")
                        .objectReferenceValue = stats;
                    serializedController.FindProperty("resourceManager")
                        .objectReferenceValue = resources;
                    TMPro.TextMeshProUGUI costText =
                        costTextObject.GetComponent<TMPro.TextMeshProUGUI>();
                    serializedController.FindProperty("drillUpgrade")
                        .FindPropertyRelative("costText")
                        .objectReferenceValue = costText;
                    serializedController.ApplyModifiedPropertiesWithoutUndo();

                    controller.OnClickUpgradeDrill();

                    Assert.That(resources.CurrentResource, Is.EqualTo(1000));
                    Assert.That(costText.text, Is.EqualTo("MAX"));
                    Assert.That(
                        stats.GetBaseStat(StatType.Drill),
                        Is.EqualTo(
                            SpaceGame.RunOutcome.RunRewardCalculator
                                .MaximumPermanentDrillStat));
                }
            }
            finally
            {
                PlayerStats.ResetSessionFlightStatsForTests();
                Object.DestroyImmediate(costTextObject);
                Object.DestroyImmediate(controllerObject);
                Object.DestroyImmediate(resourceObject);
                Object.DestroyImmediate(statsObject);
            }
        }

        [Test]
        public void PermanentUpgradeCaps_ProtectEngineAndDrillBaseValues()
        {
            Assert.That(
                PlayerStats.MaximumEffectiveEngineStat,
                Is.EqualTo(200f));
            Assert.That(
                PlayerStats.MaximumPermanentEngineStat,
                Is.EqualTo(150f));
            Assert.That(
                SpaceGame.RunOutcome.RunRewardCalculator
                    .MaximumEffectiveDrillStat,
                Is.EqualTo(300f));
            Assert.That(
                SpaceGame.RunOutcome.RunRewardCalculator
                    .MaximumPermanentDrillStat,
                Is.EqualTo(250f));
            Assert.That(
                BaseStatController.IsPermanentUpgradeAtCap(
                    StatType.Drill,
                    249f),
                Is.False);
            Assert.That(
                BaseStatController.IsPermanentUpgradeAtCap(
                    StatType.Drill,
                    250f),
                Is.True);
            Assert.That(
                BaseStatController.IsPermanentUpgradeAtCap(
                    StatType.Engine,
                    149f),
                Is.False);
            Assert.That(
                BaseStatController.IsPermanentUpgradeAtCap(
                    StatType.Engine,
                    150f),
                Is.True);
            Assert.That(
                BaseStatController.CalculateAllowedUpgradeAmount(
                    StatType.Drill,
                    245f,
                    10f),
                Is.EqualTo(5f));
            Assert.That(
                BaseStatController.CalculateAllowedUpgradeAmount(
                    StatType.Drill,
                    250f,
                    10f),
                Is.Zero);
            Assert.That(
                BaseStatController.CalculateAllowedUpgradeAmount(
                    StatType.Engine,
                    149f,
                    3f),
                Is.EqualTo(1f));
            Assert.That(
                BaseStatController.CalculateAllowedUpgradeAmount(
                    StatType.Engine,
                    999f,
                    3f),
                Is.Zero);
        }

        [Test]
        public void PermanentDrillCap_LeavesUsefulHeadroomForParts()
        {
            float baseMultiplier =
                SpaceGame.RunOutcome.RunRewardCalculator
                    .MaximumPermanentDrillStat
                / SpaceGame.RunOutcome.RunRewardCalculator
                    .DrillStatPointsPerRewardMultiplier;
            float multiplierWithSmallestAuthoredPart =
                SpaceGame.RunOutcome.RunRewardCalculator
                    .MaximumPermanentDrillStat
                * 1.05f
                / SpaceGame.RunOutcome.RunRewardCalculator
                    .DrillStatPointsPerRewardMultiplier;

            Assert.That(multiplierWithSmallestAuthoredPart,
                Is.GreaterThan(baseMultiplier));
            Assert.That(multiplierWithSmallestAuthoredPart,
                Is.LessThanOrEqualTo(
                    SpaceGame.RunOutcome.RunRewardCalculator
                        .MaximumRewardMultiplier));
        }

        [Test]
        public void UpgradeCosts_AreIsolatedByStatType()
        {
            var store = new MemoryProgressStore();
            var persistence = new PlayerProgressPersistence(store);

            persistence.SaveNextUpgradeCost(StatType.Engine, 200);
            persistence.SaveNextUpgradeCost(StatType.Fuel, 350);

            Assert.That(
                persistence.LoadOrCreateNextUpgradeCost(
                    StatType.Engine,
                    100),
                Is.EqualTo(200));
            Assert.That(
                persistence.LoadOrCreateNextUpgradeCost(
                    StatType.Fuel,
                    100),
                Is.EqualTo(350));
            Assert.That(
                persistence.LoadOrCreateNextUpgradeCost(
                    StatType.Armor,
                    150),
                Is.EqualTo(150));
        }

        [Test]
        public void RemovedGridBlock_CanBeRestoredToItsOriginalCell()
        {
            var gridObject = new GameObject("Grid");
            var blockData = ScriptableObject.CreateInstance<BlockData>();

            try
            {
                InventoryGrid grid =
                    gridObject.AddComponent<InventoryGrid>();
                grid.InitializeForTests();
                blockData.cells.Add(Vector2Int.zero);
                var original = new BlockInstance(
                    blockData,
                    null,
                    new Vector2Int(2, 3));
                int placedCount = 0;
                int removedCount = 0;
                BlockInstance lastPlaced = null;
                grid.OnBlockPlaced += placed =>
                {
                    placedCount++;
                    lastPlaced = placed;
                };
                grid.OnBlockRemoved += _ => removedCount++;

                Assert.That(grid.TryPlace(original), Is.True);
                grid.Remove(original);

                Assert.That(grid.TryPlace(original), Is.True);
                Assert.That(placedCount, Is.EqualTo(2));
                Assert.That(removedCount, Is.EqualTo(1));
                Assert.That(lastPlaced, Is.SameAs(original));
                Assert.That(
                    grid.CanPlace(
                        new BlockInstance(
                            blockData,
                            null,
                            original.origin)),
                    Is.False);
            }
            finally
            {
                Object.DestroyImmediate(blockData);
                Object.DestroyImmediate(gridObject);
            }
        }

        [Test]
        public void SubmissionScenes_HaveConsistentUiAndNoMissingScripts()
        {
            string[] expectedScenePaths =
            {
                "Assets/Dev/CSU/01_Scenes/MainMenu.unity",
                "Assets/Dev/CSU/01_Scenes/Rocket Shooting.unity",
                "Assets/Dev/CSU/01_Scenes/InGame.unity",
                "Assets/Dev/CSU/01_Scenes/Ending.unity"
            };
            string[] enabledScenePaths = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            Assert.That(
                enabledScenePaths,
                Is.EqualTo(expectedScenePaths));

            SceneSetup[] originalSetup =
                EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string scenePath in expectedScenePaths)
                {
                    Scene scene = EditorSceneManager.OpenScene(
                        scenePath,
                        OpenSceneMode.Single);
                    int missingScriptCount = scene.GetRootGameObjects()
                        .SelectMany(root =>
                            root.GetComponentsInChildren<Transform>(true))
                        .Sum(transform =>
                            GameObjectUtility
                                .GetMonoBehavioursWithMissingScriptCount(
                                    transform.gameObject));
                    Assert.That(
                        missingScriptCount,
                        Is.Zero,
                        $"Missing script found in {scenePath}.");

                    InputSystemUIInputModule[] inputModules =
                        FindSceneComponents<InputSystemUIInputModule>(scene);
                    Assert.That(inputModules, Has.Length.EqualTo(1));
                    Assert.That(inputModules[0].move, Is.Not.Null);
                    Assert.That(
                        inputModules[0].move.action?.name,
                        Is.EqualTo("Navigate"));
                    Assert.That(
                        inputModules[0].move.action.bindings,
                        Is.Empty,
                        $"Pointer-only UI navigation bindings remain in "
                        + $"{scenePath}.");

                    CanvasScaler[] scalers =
                        FindSceneComponents<CanvasScaler>(scene);
                    Assert.That(scalers, Is.Not.Empty);
                    foreach (CanvasScaler scaler in scalers)
                    {
                        Assert.That(
                            scaler.uiScaleMode,
                            Is.EqualTo(
                                CanvasScaler.ScaleMode.ScaleWithScreenSize));
                        Assert.That(
                            scaler.referenceResolution,
                            Is.EqualTo(new Vector2(1920f, 1080f)));
                        Assert.That(
                            scaler.matchWidthOrHeight,
                            Is.EqualTo(0.5f).Within(0.001f));
                    }

                    if (!scenePath.EndsWith("InGame.unity"))
                    {
                        EventSystem eventSystem =
                            FindSceneComponents<EventSystem>(scene).Single();
                        Assert.That(
                            eventSystem.firstSelectedGameObject,
                            Is.Not.Null,
                            $"Initial UI focus is missing in {scenePath}.");
                    }
                }
            }
            finally
            {
                RestoreSceneSetupOrCreateEmpty(originalSetup);
            }
        }

        [Test]
        public void SubmissionInspectorBalance_MatchesQaTargets()
        {
            SceneSetup[] originalSetup =
                EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Scene preparationScene = EditorSceneManager.OpenScene(
                    "Assets/Dev/CSU/01_Scenes/Rocket Shooting.unity",
                    OpenSceneMode.Single);
                SerializedObject director = new SerializedObject(
                    FindSceneBehaviour(
                        preparationScene,
                        "RocketShootingDirector"));
                Assert.That(
                    director.FindProperty("autoStart").boolValue,
                    Is.False);

                SerializedObject upgrades = new SerializedObject(
                    FindSceneBehaviour(
                        preparationScene,
                        "BaseStatController"));
                AssertUpgrade(
                    upgrades,
                    "engineUpgrade",
                    3f,
                    1.22f,
                    20);
                AssertUpgrade(
                    upgrades,
                    "fuelUpgrade",
                    10f,
                    1.2f,
                    20);
                AssertUpgrade(
                    upgrades,
                    "armorUpgrade",
                    10f,
                    1.18f,
                    15);
                AssertUpgrade(
                    upgrades,
                    "drillUpgrade",
                    10f,
                    1.25f,
                    25);

                Scene inGameScene = EditorSceneManager.OpenScene(
                    "Assets/Dev/CSU/01_Scenes/InGame.unity",
                    OpenSceneMode.Single);
                SerializedObject spawner = new SerializedObject(
                    FindSceneBehaviour(inGameScene, "MeteorSpawner"));
                AssertFloat(spawner, "maximumScale", 1.35f);
                AssertFloat(spawner, "maximumSpeed", 7f);
                AssertFloat(spawner, "minimumSpawnInterval", 1.7f);
                AssertFloat(spawner, "maximumSpawnInterval", 2.8f);
                SerializedProperty profiles =
                    spawner.FindProperty("stageDifficultyProfiles");
                Assert.That(profiles.arraySize, Is.EqualTo(3));
                AssertFloat(
                    profiles.GetArrayElementAtIndex(0),
                    "speedMultiplier",
                    0.85f);
                AssertFloat(
                    profiles.GetArrayElementAtIndex(1),
                    "speedMultiplier",
                    1f);
                AssertFloat(
                    profiles.GetArrayElementAtIndex(2),
                    "speedMultiplier",
                    1.2f);

                SerializedObject playerController = new SerializedObject(
                    FindSceneBehaviour(inGameScene, "PlayerController"));
                AssertFloat(
                    playerController,
                    "engineFuelConsumptionWeight",
                    0.5f);
                SerializedObject health = new SerializedObject(
                    FindSceneBehaviour(inGameScene, "Health"));
                Assert.That(
                    health.FindProperty("successRewardBonus").intValue,
                    Is.EqualTo(500));

                SerializedObject planetController = new SerializedObject(
                    FindSceneBehaviour(
                        inGameScene,
                        "PlanetParallaxController"));
                SerializedProperty decorations =
                    planetController.FindProperty("decorationSettings");
                Assert.That(decorations.arraySize, Is.EqualTo(2));
                AssertFloat(
                    decorations.GetArrayElementAtIndex(0),
                    "stageTravelDistance",
                    600f);
                AssertFloat(
                    decorations.GetArrayElementAtIndex(1),
                    "stageTravelDistance",
                    500f);
                SerializedProperty planets =
                    planetController.FindProperty("planetSettings");
                Assert.That(planets.arraySize, Is.EqualTo(5));
                for (int index = 0; index < planets.arraySize; index++)
                {
                    AssertFloat(
                        planets.GetArrayElementAtIndex(index),
                        "planetChangeDistance",
                        160f);
                }
            }
            finally
            {
                RestoreSceneSetupOrCreateEmpty(originalSetup);
            }
        }

        [Test]
        public void PartCatalog_HasValidRangesAndExpectedVariety()
        {
            string[] partGuids = AssetDatabase.FindAssets(
                "t:MachinePartsDataSo",
                new[] {"Assets/Dev/NKY/SO/MachineSo"});
            string[] blockGuids = AssetDatabase.FindAssets(
                "t:BlockData",
                new[] {"Assets/Dev/NKY/SO/BlockSo"});

            Assert.That(partGuids, Has.Length.EqualTo(20));
            Assert.That(blockGuids, Has.Length.EqualTo(10));

            foreach (string guid in partGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                MachinePartsDataSo part =
                    AssetDatabase.LoadAssetAtPath<MachinePartsDataSo>(path);
                Assert.That(part, Is.Not.Null, path);
                foreach (StatModifier modifier in part.statData)
                {
                    Assert.That(float.IsNaN(modifier.value), Is.False, path);
                    Assert.That(float.IsInfinity(modifier.value), Is.False, path);
                    if (modifier.isRandom)
                    {
                        Assert.That(
                            modifier.minValue,
                            Is.LessThanOrEqualTo(modifier.maxValue),
                            path);
                    }
                    else
                    {
                        Assert.That(
                            modifier.value,
                            Is.Not.EqualTo(0f),
                            $"{path} contains a fixed zero-effect modifier.");
                    }
                }
            }

            foreach (string guid in blockGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                BlockData block =
                    AssetDatabase.LoadAssetAtPath<BlockData>(path);
                Assert.That(block, Is.Not.Null, path);
                Assert.That(block.cells.Count, Is.InRange(3, 6), path);
            }
        }

        [Test]
        public void CorrectedPartAssets_KeepTheirBalanceContracts()
        {
            MachinePartsDataSo survival = AssetDatabase.LoadAssetAtPath<
                MachinePartsDataSo>(
                "Assets/Dev/NKY/SO/MachineSo/survival module.asset");
            MachinePartsDataSo expedition = AssetDatabase.LoadAssetAtPath<
                MachinePartsDataSo>(
                "Assets/Dev/NKY/SO/MachineSo/expedition package.asset");

            Assert.That(survival, Is.Not.Null);
            Assert.That(expedition, Is.Not.Null);

            StatModifier survivalFuel = survival.statData.Single(
                modifier => modifier.type == StatType.Fuel);
            Assert.That(survivalFuel.modifierType,
                Is.EqualTo(ModifierType.Flat));
            Assert.That(survivalFuel.isRandom, Is.True);
            Assert.That(survivalFuel.minValue, Is.EqualTo(15f));
            Assert.That(survivalFuel.maxValue, Is.EqualTo(30f));

            StatModifier expeditionFuel = expedition.statData.Single(
                modifier => modifier.type == StatType.Fuel);
            Assert.That(expeditionFuel.modifierType,
                Is.EqualTo(ModifierType.Multiplier));
            Assert.That(expeditionFuel.isRandom, Is.False);
            Assert.That(expeditionFuel.value, Is.EqualTo(0.2f));

            StatModifier expeditionDrill = expedition.statData.Single(
                modifier => modifier.type == StatType.Drill);
            Assert.That(expeditionDrill.modifierType,
                Is.EqualTo(ModifierType.Multiplier));
            Assert.That(expeditionDrill.isRandom, Is.False);
            Assert.That(expeditionDrill.value, Is.EqualTo(0.05f));
        }

        private static T[] FindSceneComponents<T>(Scene scene)
            where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
        }

        private static void RestoreSceneSetupOrCreateEmpty(
            SceneSetup[] originalSetup)
        {
            if (originalSetup != null
                && originalSetup.Any(setup =>
                    setup.isLoaded && setup.isActive))
            {
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
                return;
            }

            EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
        }

        private static MonoBehaviour FindSceneBehaviour(
            Scene scene,
            string typeName)
        {
            MonoBehaviour[] matches = FindSceneComponents<MonoBehaviour>(scene)
                .Where(behaviour =>
                    behaviour != null
                    && behaviour.GetType().Name == typeName)
                .ToArray();
            Assert.That(
                matches,
                Has.Length.EqualTo(1),
                $"Expected one {typeName} in {scene.path}.");
            return matches[0];
        }

        private static void AssertUpgrade(
            SerializedObject controller,
            string propertyName,
            float increase,
            float multiplier,
            int flatAdd)
        {
            SerializedProperty upgrade =
                controller.FindProperty(propertyName);
            AssertFloat(upgrade, "increaseAmount", increase);
            AssertFloat(upgrade, "costMultiplier", multiplier);
            Assert.That(
                upgrade.FindPropertyRelative("flatCostAdd").intValue,
                Is.EqualTo(flatAdd));
        }

        private static void AssertFloat(
            SerializedObject serializedObject,
            string propertyName,
            float expected)
        {
            Assert.That(
                serializedObject.FindProperty(propertyName).floatValue,
                Is.EqualTo(expected).Within(0.001f));
        }

        private static void AssertFloat(
            SerializedProperty parent,
            string relativePropertyName,
            float expected)
        {
            Assert.That(
                parent.FindPropertyRelative(relativePropertyName).floatValue,
                Is.EqualTo(expected).Within(0.001f));
        }

        private static Dictionary<StatType, float> CreateDefaults(
            float value)
        {
            return new Dictionary<StatType, float>
            {
                [StatType.Engine] = value,
                [StatType.Fuel] = value,
                [StatType.Armor] = value,
                [StatType.Drill] = value
            };
        }

        private sealed class MemoryProgressStore :
            IPlayerProgressStore
        {
            private readonly Dictionary<string, int> ints =
                new Dictionary<string, int>();
            private readonly Dictionary<string, float> floats =
                new Dictionary<string, float>();

            public int SaveCount { get; private set; }
            public int WriteCount { get; private set; }

            public bool HasKey(string key)
                => ints.ContainsKey(key) || floats.ContainsKey(key);

            public int GetInt(string key, int defaultValue)
                => ints.TryGetValue(key, out int value)
                    ? value
                    : defaultValue;

            public float GetFloat(string key, float defaultValue)
                => floats.TryGetValue(key, out float value)
                    ? value
                    : defaultValue;

            public void SetInt(string key, int value)
            {
                ints[key] = value;
                WriteCount++;
            }

            public void SetFloat(string key, float value)
            {
                floats[key] = value;
                WriteCount++;
            }

            public void Save()
            {
                SaveCount++;
            }
        }
    }
}
#endif
