using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dev.NKY.Scripts
{
    public interface IPlayerProgressStore
    {
        bool HasKey(string key);
        int GetInt(string key, int defaultValue);
        float GetFloat(string key, float defaultValue);
        void SetInt(string key, int value);
        void SetFloat(string key, float value);
        void Save();
    }

    public sealed class PlayerPrefsProgressStore : IPlayerProgressStore
    {
        public bool HasKey(string key) => PlayerPrefs.HasKey(key);

        public int GetInt(string key, int defaultValue)
            => PlayerPrefs.GetInt(key, defaultValue);

        public float GetFloat(string key, float defaultValue)
            => PlayerPrefs.GetFloat(key, defaultValue);

        public void SetInt(string key, int value)
            => PlayerPrefs.SetInt(key, value);

        public void SetFloat(string key, float value)
            => PlayerPrefs.SetFloat(key, value);

        public void Save() => PlayerPrefs.Save();
    }

    /// <summary>
    /// Stores permanent progression separately from temporary equipped-part
    /// modifiers. Legacy builds stored only a combined final stat, so migration
    /// cannot safely infer how much came from upgrades versus equipped parts.
    /// The combined value is backed up and preserved once to avoid destructive
    /// stat loss; all writes after migration contain base stats only.
    /// </summary>
    public sealed class PlayerProgressPersistence
    {
        private const string PlayerStatsPrefix = "SpaceGame.PlayerStats.";
        private const string SchemaVersionKey =
            PlayerStatsPrefix + "SchemaVersion";
        private const string BaseStatPrefix =
            PlayerStatsPrefix + "Base.";
        private const string UpgradeCostPrefix =
            PlayerStatsPrefix + "Upgrade.NextCost.";
        private const string LegacyBackupPrefix =
            PlayerStatsPrefix + "LegacyBackup.";
        private const string SafetyBackupPrefix =
            PlayerStatsPrefix + "SafetyBackupV3.";
        private const string LegacyInitializedKey =
            PlayerStatsPrefix + "Initialized";
        private const int CurrentSchemaVersion = 3;

        private readonly IPlayerProgressStore store;

        public static PlayerProgressPersistence Default { get; private set; } =
            new PlayerProgressPersistence(new PlayerPrefsProgressStore());

        public PlayerProgressPersistence(IPlayerProgressStore store)
        {
            this.store = store
                ?? throw new ArgumentNullException(nameof(store));
        }

#if UNITY_INCLUDE_TESTS
        public static IDisposable OverrideDefaultForTests(
            IPlayerProgressStore testStore)
        {
            PlayerProgressPersistence previous = Default;
            Default = new PlayerProgressPersistence(testStore);
            return new RestoreDefaultScope(previous);
        }
#endif

        public Dictionary<StatType, float> LoadBaseStats(
            IReadOnlyDictionary<StatType, float> authoredDefaults)
        {
            var result = new Dictionary<StatType, float>();
            bool repaired = EnsureMigrated(authoredDefaults);

            foreach (StatType type in Enum.GetValues(typeof(StatType)))
            {
                float fallback = GetDefault(authoredDefaults, type);
                string key = GetBaseStatKey(type);

                if (!store.HasKey(key))
                {
                    float sanitizedFallback =
                        PlayerStats.ClampPermanentStat(type, fallback);
                    result[type] = sanitizedFallback;
                    store.SetFloat(key, sanitizedFallback);
                    repaired = true;
                    continue;
                }

                float value = store.GetFloat(key, fallback);

                if (!IsValidStat(value))
                {
                    value = fallback;
                    store.SetFloat(key, value);
                    repaired = true;
                }

                float sanitizedValue =
                    PlayerStats.ClampPermanentStat(type, value);
                if (!Mathf.Approximately(value, sanitizedValue))
                {
                    string backupKey = GetSafetyBackupKey(type);
                    if (!store.HasKey(backupKey))
                    {
                        store.SetFloat(backupKey, value);
                    }

                    value = sanitizedValue;
                    store.SetFloat(key, value);
                    store.SetFloat(GetLegacyStatKey(type), value);
                    repaired = true;
                    Debug.LogWarning(
                        $"[PlayerProgress] Clamped unsafe permanent {type} "
                        + $"stat to {value:F1}. The previous value was "
                        + "preserved in SafetyBackupV3.*.");
                }

                result[type] = value;
            }

            if (repaired)
            {
                store.Save();
            }

            return result;
        }

        public void SaveBaseStats(
            IReadOnlyDictionary<StatType, float> baseStats)
        {
            foreach (StatType type in Enum.GetValues(typeof(StatType)))
            {
                float value = PlayerStats.ClampPermanentStat(
                    type,
                    GetDefault(baseStats, type));
                store.SetFloat(GetBaseStatKey(type), value);

                // Keep the old build's base-stat fallback coherent without
                // writing temporary equipped-part modifiers into it.
                store.SetFloat(GetLegacyStatKey(type), value);
            }

            store.SetInt(LegacyInitializedKey, 1);
            store.SetInt(SchemaVersionKey, CurrentSchemaVersion);
            store.Save();
        }

        public int LoadOrCreateNextUpgradeCost(
            StatType type,
            int authoredInitialCost)
        {
            int fallback = Math.Max(0, authoredInitialCost);
            string key = GetUpgradeCostKey(type);

            if (!store.HasKey(key))
            {
                store.SetInt(key, fallback);
                store.Save();
                return fallback;
            }

            int savedCost = store.GetInt(key, fallback);
            if (savedCost >= 0)
            {
                return savedCost;
            }

            store.SetInt(key, fallback);
            store.Save();
            return fallback;
        }

        public void SaveNextUpgradeCost(StatType type, int nextCost)
        {
            store.SetInt(GetUpgradeCostKey(type), Math.Max(0, nextCost));
            store.Save();
        }

        private bool EnsureMigrated(
            IReadOnlyDictionary<StatType, float> authoredDefaults)
        {
            int previousSchemaVersion = store.GetInt(SchemaVersionKey, 0);
            if (previousSchemaVersion >= CurrentSchemaVersion)
            {
                return false;
            }

            bool hasLegacyStats =
                store.GetInt(LegacyInitializedKey, 0) != 0;

            foreach (StatType type in Enum.GetValues(typeof(StatType)))
            {
                float fallback = GetDefault(authoredDefaults, type);
                float legacyValue = hasLegacyStats
                    ? store.GetFloat(GetLegacyStatKey(type), fallback)
                    : fallback;

                if (hasLegacyStats)
                {
                    string backupKey = GetLegacyBackupKey(type);
                    if (!store.HasKey(backupKey))
                    {
                        // Preserve the exact pre-migration value before any v2
                        // key is written. This is intentionally non-destructive:
                        // old saves do not contain enough information to split
                        // permanent upgrades from a once-equipped part.
                        store.SetFloat(backupKey, legacyValue);
                    }
                }

                string newKey = GetBaseStatKey(type);
                if (store.HasKey(newKey))
                {
                    continue;
                }

                store.SetFloat(
                    newKey,
                    IsValidStat(legacyValue) ? legacyValue : fallback);
            }

            // The version marker is written last. If the application exits
            // during migration, the next run safely repeats the same copies.
            store.SetInt(SchemaVersionKey, CurrentSchemaVersion);

            if (hasLegacyStats && previousSchemaVersion < 2)
            {
                Debug.Log(
                    "[PlayerProgress] Migrated legacy combined stats to "
                    + "the versioned base-stat format. Original values "
                    + "were preserved under "
                    + "LegacyBackup.* because the old save cannot distinguish "
                    + "base upgrades from temporary part modifiers.");
            }

            Debug.Log(
                $"[PlayerProgress] Progression schema upgraded from "
                + $"v{previousSchemaVersion} to v{CurrentSchemaVersion}.");

            return true;
        }

        private static float GetDefault(
            IReadOnlyDictionary<StatType, float> values,
            StatType type)
        {
            if (values != null
                && values.TryGetValue(type, out float value)
                && IsValidStat(value))
            {
                return value;
            }

            return PlayerStats.DefaultStatValue;
        }

        private static string GetBaseStatKey(StatType type)
            => BaseStatPrefix + type;

        private static string GetUpgradeCostKey(StatType type)
            => UpgradeCostPrefix + type;

        private static string GetLegacyStatKey(StatType type)
            => PlayerStatsPrefix + type;

        private static string GetLegacyBackupKey(StatType type)
            => LegacyBackupPrefix + type;

        private static string GetSafetyBackupKey(StatType type)
            => SafetyBackupPrefix + type;

        private static bool IsValidStat(float value)
            => !float.IsNaN(value)
               && !float.IsInfinity(value)
               && value >= 0f;

#if UNITY_INCLUDE_TESTS
        private sealed class RestoreDefaultScope : IDisposable
        {
            private readonly PlayerProgressPersistence previous;
            private bool disposed;

            public RestoreDefaultScope(
                PlayerProgressPersistence previous)
            {
                this.previous = previous;
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                Default = previous;
            }
        }
#endif
    }
}
