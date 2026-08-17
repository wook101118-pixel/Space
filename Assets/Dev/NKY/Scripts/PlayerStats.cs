using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dev.NKY.Scripts
{
    [Serializable]
    public struct BaseStats
    {
        public StatType Type;
        public float Value;
    }
    public class PlayerStats : MonoBehaviour
    {
        public const float DefaultStatValue = 100f;
        public const float MinimumPartPercent = -0.75f;
        public const float MaximumPartPercent = 1f;
        public const float MaximumPermanentEngineStat = 150f;
        public const float MaximumEffectiveEngineStat = 200f;

        private const float MinimumEngine = 25f;
        private const float MinimumFuel = 20f;
        private const float MinimumArmor = 10f;
        private const float MinimumDrill = 50f;

        [SerializeField] private InventoryGrid grid; // ★ 그리드 자동 연동용 참조
        [SerializeField] private List<BaseStats> baseStats; // 인스펙터 기본값 목록
        [SerializeField] private List<BaseStats> minValues;
 
        private readonly Dictionary<StatType, float> baseValues = new Dictionary<StatType, float>();
        private readonly Dictionary<StatType, float> flatSum = new Dictionary<StatType, float>();
        private readonly Dictionary<StatType, float> percentSum = new Dictionary<StatType, float>();

        private static readonly Dictionary<StatType, float>
            sessionFlightStats = new Dictionary<StatType, float>();
        private static bool hasSessionFlightStats;

        public Dictionary<StatType, float> FinalStats { get; private set; } =
            new Dictionary<StatType, float>();
 
        public event Action<StatType, float> OnStatChanged;
        public event Action<Dictionary<StatType, float>> OnAllStatsUpdated;

        private void Awake()
        {
            InitializeBaseValues();
            LoadSavedValues();
            CaptureSessionFlightStats();
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSessionFlightStats()
        {
            sessionFlightStats.Clear();
            hasSessionFlightStats = false;
        }

#if UNITY_INCLUDE_TESTS
        public static void ResetSessionFlightStatsForTests()
        {
            ResetSessionFlightStats();
        }

        public void InitializeForTests()
        {
            InitializeBaseValues();
            LoadSavedValues();
            CaptureSessionFlightStats();
        }
#endif

        /// <summary>
        /// ★ [핵심] baseStats 리스트의 값들을 StatType Enum에 맞춰 baseValues Dictionary에 넣어줍니다.
        /// </summary>
        private void InitializeBaseValues()
        {
            baseValues.Clear();

            // 1. 모든 StatType의 기본값을 먼저 100f로 안전하게 초기화
            foreach (StatType type in Enum.GetValues(typeof(StatType)))
            {
                baseValues[type] = DefaultStatValue;
            }

            // 2. 인스펙터의 baseStats 리스트에 설정된 데이터가 있다면 덮어쓰기
            if (baseStats != null)
            {
                foreach (var stat in baseStats)
                {
                    baseValues[stat.Type] = stat.Value;
                }
            }
        }
 
        private void LoadSavedValues()
        {
            Dictionary<StatType, float> savedValues =
                PlayerProgressPersistence.Default.LoadBaseStats(baseValues);

            foreach (StatType type in Enum.GetValues(typeof(StatType)))
            {
                float fallback = baseValues.TryGetValue(type, out float value)
                    ? value
                    : DefaultStatValue;
                baseValues[type] = savedValues.TryGetValue(
                    type,
                    out float savedValue)
                    ? savedValue
                    : fallback;
            }
        }

        public float GetStat(StatType type)
        {
            float baseVal = baseValues.TryGetValue(type, out var bv) ? bv : 0f;
            float flat = flatSum.TryGetValue(type, out var f) ? f : 0f;
            float percent = percentSum.TryGetValue(type, out var p) ? p : 0f;

            percent = Mathf.Clamp(
                IsFinite(percent) ? percent : 0f,
                MinimumPartPercent,
                MaximumPartPercent);
            float additiveValue = IsFinite(baseVal + flat)
                ? baseVal + flat
                : DefaultStatValue;
            float finalVal = additiveValue * (1f + percent);
            float minimum = GetSafetyMinimum(type);

            if (minValues != null)
            {
                foreach (var minValue in minValues)
                {
                    if (type == minValue.Type && IsFinite(minValue.Value))
                    {
                        minimum = Mathf.Max(minimum, minValue.Value);
                    }
                }
            }

            float boundedValue = Mathf.Max(minimum, finalVal);
            float maximum = GetSafetyMaximum(type);
            return float.IsPositiveInfinity(maximum)
                ? boundedValue
                : Mathf.Min(maximum, boundedValue);
        }

        public float GetBaseStat(StatType type)
        {
            return baseValues.TryGetValue(type, out float value)
                ? value
                : DefaultStatValue;
        }

        private static float GetSafetyMinimum(StatType type)
        {
            return type switch
            {
                StatType.Engine => MinimumEngine,
                StatType.Fuel => MinimumFuel,
                StatType.Armor => MinimumArmor,
                StatType.Drill => MinimumDrill,
                _ => 0f
            };
        }

        private static float GetSafetyMaximum(StatType type)
        {
            return type switch
            {
                StatType.Engine => MaximumEffectiveEngineStat,
                StatType.Drill => SpaceGame.RunOutcome.RunRewardCalculator
                    .MaximumEffectiveDrillStat,
                _ => float.PositiveInfinity
            };
        }

        public static float GetMaximumPermanentStat(StatType type)
        {
            return type switch
            {
                StatType.Engine => MaximumPermanentEngineStat,
                StatType.Drill => SpaceGame.RunOutcome.RunRewardCalculator
                    .MaximumPermanentDrillStat,
                _ => float.PositiveInfinity
            };
        }

        public static float ClampPermanentStat(StatType type, float value)
        {
            float safeValue = IsFinite(value) && value >= 0f
                ? value
                : DefaultStatValue;
            float maximum = GetMaximumPermanentStat(type);
            return float.IsPositiveInfinity(maximum)
                ? safeValue
                : Mathf.Min(safeValue, maximum);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        public Dictionary<StatType, float> GetAllFinalStats()
        {
            FinalStats.Clear();

            foreach (StatType type in Enum.GetValues(typeof(StatType)))
            {
                FinalStats[type] = GetStat(type);
            }
            
            return FinalStats;
        }
 
        public void ApplyModifiers(BlockInstance instance)
        {
            if (instance.partsData?.statData == null) return;

            foreach (var stat in instance.partsData.statData)
                AccumulateModifier(stat, +1f);
        }
 
        public void RemoveModifiers(BlockInstance instance)
        {
            if (instance?.partsData?.statData == null) return;

            foreach (var stat in instance.partsData.statData)
                AccumulateModifier(stat, -1f);
        }
 
        private void AccumulateModifier(StatModifier mod, float sign)
        {
            float val = mod.value;

            if (mod.modifierType == ModifierType.Flat)
            {
                float cur = flatSum.TryGetValue(mod.type, out var f) ? f : 0f;
                flatSum[mod.type] = cur + val * sign;
            }
            else
            {
                float cur = percentSum.TryGetValue(mod.type, out var p) ? p : 0f;
                percentSum[mod.type] = cur + val * sign;
            }
 
            // 개별 및 전체 스탯 변경 이벤트 발송
            OnStatChanged?.Invoke(mod.type, GetStat(mod.type));
            Dictionary<StatType, float> finalStats = GetAllFinalStats();
            CaptureSessionFlightStats(finalStats);
            OnAllStatsUpdated?.Invoke(finalStats);
        }
        
        public void UpgradeBaseStat(StatType type, float amount)
        {
            if (baseValues.ContainsKey(type))
            {
                baseValues[type] = ClampPermanentStat(
                    type,
                    baseValues[type] + amount);
            }
            else
            {
                baseValues[type] = ClampPermanentStat(type, amount);
            }

            // ★ 스탯 변경 이벤트 발송 (UI 및 타 시스템 자동 갱신)
            PlayerProgressPersistence.Default.SaveBaseStats(baseValues);
            OnStatChanged?.Invoke(type, GetStat(type));
            Dictionary<StatType, float> finalStats = GetAllFinalStats();
            CaptureSessionFlightStats(finalStats);
            OnAllStatsUpdated?.Invoke(finalStats);
        }

        public static Dictionary<StatType, float> GetSavedStats()
        {
            if (hasSessionFlightStats)
            {
                return new Dictionary<StatType, float>(
                    sessionFlightStats);
            }

            var defaults = new Dictionary<StatType, float>();
            foreach (StatType type in Enum.GetValues(typeof(StatType)))
            {
                defaults[type] = DefaultStatValue;
            }

            return PlayerProgressPersistence.Default.LoadBaseStats(
                defaults);
        }

        private void CaptureSessionFlightStats()
        {
            CaptureSessionFlightStats(GetAllFinalStats());
        }

        private static void CaptureSessionFlightStats(
            IReadOnlyDictionary<StatType, float> finalStats)
        {
            sessionFlightStats.Clear();

            foreach (StatType type in Enum.GetValues(typeof(StatType)))
            {
                sessionFlightStats[type] =
                    finalStats.TryGetValue(type, out float value)
                        ? value
                        : DefaultStatValue;
            }

            hasSessionFlightStats = true;
        }
    }
}
