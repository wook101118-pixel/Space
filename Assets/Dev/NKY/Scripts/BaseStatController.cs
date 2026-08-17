using Dev.CSU._02_Scripts.RocketShooting;
using SpaceGame.RunOutcome;
using UnityEngine;
using TMPro; // TextMeshPro 사용 시 (기본 Text 사용 시 UnityEngine.UI로 변경)

namespace Dev.NKY.Scripts
{
    public class BaseStatController : MonoBehaviour
    {
        private const string MaximumCostLabel = "MAX";

        [System.Serializable]
        public class StatUpgradeInfo
        {
            public StatType statType;
            public float increaseAmount = 10f; // 1회 강화당 스탯 증가량
            public int currentCost = 100;      // 현재 강화 비용 (초기 비용)
            public float costMultiplier = 1.3f;// 강화 후 비용 증가 배율
            public int flatCostAdd = 0;        // (선택) 고정 비용 추가분
            
            [Header("UI Reference")]
            public TextMeshProUGUI costText;          // ★ 버튼 안의 소모 자원 표시 텍스트
        }

        [SerializeField] private SoundDataSO statUpSound;

        [Header("References")]
        [SerializeField] private PlayerStats playerStats;
        [SerializeField] private ResourceManager resourceManager;

        [Header("Stat Upgrade Settings")]
        [SerializeField] private StatUpgradeInfo engineUpgrade = new StatUpgradeInfo { statType = StatType.Engine, increaseAmount = 10f, currentCost = 100, costMultiplier = 1.3f };
        [SerializeField] private StatUpgradeInfo fuelUpgrade = new StatUpgradeInfo { statType = StatType.Fuel, increaseAmount = 20f, currentCost = 100, costMultiplier = 1.3f };
        [SerializeField] private StatUpgradeInfo armorUpgrade = new StatUpgradeInfo { statType = StatType.Armor, increaseAmount = 15f, currentCost = 150, costMultiplier = 1.4f };
        [SerializeField] private StatUpgradeInfo drillUpgrade = new StatUpgradeInfo { statType = StatType.Drill, increaseAmount = 5f, currentCost = 200, costMultiplier = 1.5f };

        private void Start()
        {
            LoadPersistedCost(engineUpgrade);
            LoadPersistedCost(fuelUpgrade);
            LoadPersistedCost(armorUpgrade);
            LoadPersistedCost(drillUpgrade);

            // ★ 게임 시작 시 각 버튼의 소모 자원 텍스트를 초기화합니다.
            UpdateCostUI(engineUpgrade);
            UpdateCostUI(fuelUpgrade);
            UpdateCostUI(armorUpgrade);
            UpdateCostUI(drillUpgrade);
        }

        /// <summary>
        /// 자원을 검사 및 소모하고, 강화 성공 시 다음 강화 비용 계산 및 UI를 갱신합니다.
        /// </summary>
        private bool TryUpgradeStat(StatUpgradeInfo info)
        {
            if (info == null
                || playerStats == null
                || resourceManager == null)
            {
                Debug.LogWarning("[BaseStatController] PlayerStats 또는 ResourceManager가 할당되지 않았습니다!");
                return false;
            }

            if (HasReachedUpgradeLimit(info))
            {
                UpdateCostUI(info);
                Debug.Log(
                    $"[Stat] {info.statType} 영구 강화가 최대치에 "
                    + "도달해 자원을 소모하지 않았습니다.",
                    this);
                return false;
            }

            float appliedIncrease = CalculateAllowedUpgradeAmount(
                info.statType,
                playerStats.GetBaseStat(info.statType),
                info.increaseAmount);
            if (appliedIncrease <= 0f)
            {
                return false;
            }

            // 1. 자원이 부족한 경우
            if (!resourceManager.HasEnoughResource(info.currentCost))
            {
                Debug.LogWarning($"[Stat] 자원이 부족합니다! 필요: {info.currentCost}, 보유: {resourceManager.CurrentResource}");
                return false;
            }

            // 2. 자원 차감 성공 시
            if (resourceManager.ConsumeResource(info.currentCost))
            {
                int spentCost = info.currentCost;

                SoundManager.Instance.PlaySFX(statUpSound);
                // 스탯 증가 처리
                playerStats.UpgradeBaseStat(info.statType, appliedIncrease);

                // 3. 다음 강화 비용 증가 계산
                info.currentCost = CalculateNextUpgradeCost(
                    info.currentCost,
                    info.costMultiplier,
                    info.flatCostAdd);
                PlayerProgressPersistence.Default.SaveNextUpgradeCost(
                    info.statType,
                    info.currentCost);

                // ★ 4. 버튼 텍스트 UI 자동 갱신
                UpdateCostUI(info);
                RocketShootingSoundPlayer.Play(
                    RocketShootingSoundCue.PartUpgrade);

                Debug.Log($"[Stat] {info.statType} 강화 성공! (소모 자원: {spentCost} -> 다음 필요 자원: {info.currentCost})");
                return true;
            }

            return false;
        }

        /// <summary>
        /// 해당 스탯의 소모 자원 UI 텍스트를 변경합니다.
        /// </summary>
        private void UpdateCostUI(StatUpgradeInfo info)
        {
            if (info != null && info.costText != null)
            {
                // 필요시 `${info.currentCost} G`나 `비용: {info.currentCost}` 형태로 변경 가능합니다.
                info.costText.text = HasReachedUpgradeLimit(info)
                    ? MaximumCostLabel
                    : $"{info.currentCost}";
            }
        }

        private bool HasReachedUpgradeLimit(StatUpgradeInfo info)
        {
            return info != null
                && playerStats != null
                && IsPermanentUpgradeAtCap(
                    info.statType,
                    playerStats.GetBaseStat(info.statType));
        }

        public static bool IsPermanentUpgradeAtCap(
            StatType statType,
            float permanentBaseValue)
        {
            float maximum = PlayerStats.GetMaximumPermanentStat(statType);
            return !float.IsPositiveInfinity(maximum)
                && permanentBaseValue >= maximum;
        }

        public static float CalculateAllowedUpgradeAmount(
            StatType statType,
            float permanentBaseValue,
            float increaseAmount)
        {
            if (float.IsNaN(permanentBaseValue)
                || float.IsInfinity(permanentBaseValue)
                || float.IsNaN(increaseAmount)
                || float.IsInfinity(increaseAmount))
            {
                return 0f;
            }

            float safeIncrease = Mathf.Max(0f, increaseAmount);
            float maximum = PlayerStats.GetMaximumPermanentStat(statType);
            if (float.IsPositiveInfinity(maximum))
            {
                return safeIncrease;
            }

            float remaining = maximum - permanentBaseValue;
            return Mathf.Min(safeIncrease, Mathf.Max(0f, remaining));
        }

        private static void LoadPersistedCost(StatUpgradeInfo info)
        {
            if (info == null)
            {
                return;
            }

            info.currentCost =
                PlayerProgressPersistence.Default
                    .LoadOrCreateNextUpgradeCost(
                        info.statType,
                        info.currentCost);
        }

        public static int CalculateNextUpgradeCost(
            int currentCost,
            float costMultiplier,
            int flatCostAdd)
        {
            int safeCurrentCost = Mathf.Max(0, currentCost);
            float safeMultiplier =
                float.IsNaN(costMultiplier)
                || float.IsInfinity(costMultiplier)
                    ? 1f
                    : Mathf.Max(0f, costMultiplier);
            float scaledCost = safeCurrentCost * safeMultiplier;
            int multipliedCost =
                float.IsInfinity(scaledCost)
                || scaledCost >= int.MaxValue
                    ? int.MaxValue
                    : Mathf.RoundToInt(scaledCost);
            long nextCost = (long)multipliedCost + flatCostAdd;

            if (nextCost <= 0L)
            {
                return 0;
            }

            return nextCost >= int.MaxValue
                ? int.MaxValue
                : (int)nextCost;
        }

        // ==========================================
        // UI 버튼 OnClick() 이벤트 메서드들
        // ==========================================
        public void OnClickUpgradeEngine() => TryUpgradeStat(engineUpgrade);
        public void OnClickUpgradeFuel() => TryUpgradeStat(fuelUpgrade);
        public void OnClickUpgradeArmor() => TryUpgradeStat(armorUpgrade);
        public void OnClickUpgradeDrill() => TryUpgradeStat(drillUpgrade);
    }
}
