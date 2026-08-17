#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Dev.NKY.Scripts.EditorTools
{
    internal static class LocalProgressTools
    {
        private const string PlayerStatsPrefix = "SpaceGame.PlayerStats.";
        private const string ResourceKey = "SpaceGame.Resource.Current";

        [MenuItem("Tools/Space/Reset Local Progress")]
        private static void ResetLocalProgress()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Reset Local Progress",
                "Reset stats, upgrade costs, and resources for the Unity "
                + "Editor profile? Graphics, input, UI, and tutorial "
                + "preferences will be preserved.",
                "Reset Progress",
                "Cancel");
            if (!confirmed)
            {
                return;
            }

            PlayerPrefs.DeleteKey(PlayerStatsPrefix + "Initialized");
            PlayerPrefs.DeleteKey(PlayerStatsPrefix + "SchemaVersion");

            foreach (StatType type in Enum.GetValues(typeof(StatType)))
            {
                string suffix = type.ToString();
                PlayerPrefs.DeleteKey(PlayerStatsPrefix + suffix);
                PlayerPrefs.DeleteKey(PlayerStatsPrefix + "Base." + suffix);
                PlayerPrefs.DeleteKey(
                    PlayerStatsPrefix + "Upgrade.NextCost." + suffix);
                PlayerPrefs.DeleteKey(
                    PlayerStatsPrefix + "LegacyBackup." + suffix);
                PlayerPrefs.DeleteKey(
                    PlayerStatsPrefix + "SafetyBackupV3." + suffix);
            }

            PlayerPrefs.DeleteKey(ResourceKey);
            PlayerPrefs.Save();
            Debug.Log(
                "[PlayerProgress] Editor stats, upgrade costs, and resources "
                + "were reset. Other local preferences were preserved.");
        }
    }
}
#endif
