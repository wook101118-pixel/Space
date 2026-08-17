using System;
using System.Collections.Generic;
using Dev.CSU._02_Scripts.SceneTransition;
using Dev.CSU._02_Scripts.SpaceShip;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dev.NKY.Scripts
{
    public class PlayerController : MonoBehaviour
    {
        private const float BaseStatScale = 100f;

        [SerializeField] private PlayerStats stats;
        [SerializeField] private RocketMovement movement;
        [SerializeField] private Health.Health health;
        [SerializeField] private Slider fuelSlider;
        [SerializeField] private TextMeshProUGUI fuelText;

        [Header("Fuel Consumption")]
        [SerializeField, Min(0f)]
        private float fuelConsumptionPerSecond = 1f;

        [Tooltip("How strongly engine power increases fuel use. 0 keeps a flat drain; 1 scales it fully with engine power.")]
        [SerializeField, Range(0f, 1f)]
        private float engineFuelConsumptionWeight = 0.5f;

        [Header("Applied Rocket Stats")]
        [field: SerializeField] public float EnginePower { get; private set; }
        [field: SerializeField] public float MaxFuel { get; private set; }
        [field: SerializeField] public float Armor { get; private set; }
        [field: SerializeField] public float DrillPower { get; private set; }

        public float CurrentFuel { get; private set; }
        public float DrillRewardMultiplier { get; private set; } = 1f;

        public event Action<float, float> OnFuelChanged;
        public event Action FuelDepleted;

        private float _baseMovementSpeed;
        private bool _fuelDepletionHandled;

        private void Awake()
        {
            if (movement == null)
            {
                movement = GetComponent<RocketMovement>();
            }

            if (health == null)
            {
                health = GetComponent<Health.Health>();
            }

            _baseMovementSpeed = movement != null ? movement.Speed : 0f;

            if (stats != null)
            {
                stats.OnAllStatsUpdated += ApplyStats;
            }
        }

        private void Start()
        {
            Dictionary<StatType, float> initialStats = stats != null
                ? stats.GetAllFinalStats()
                : PlayerStats.GetSavedStats();
            ApplyStats(initialStats);
        }

        private void Update()
        {
            if (fuelConsumptionPerSecond <= 0f
                || CurrentFuel <= 0f
                || Time.deltaTime <= 0f
                || SceneTransitions.IsTransitioning
                || (health != null && health.IsDead))
            {
                return;
            }

            TryConsumeFuel(
                CalculateFuelConsumptionPerSecond() * Time.deltaTime);
        }

        private void OnDestroy()
        {
            if (stats != null)
            {
                stats.OnAllStatsUpdated -= ApplyStats;
            }
        }

        private void ApplyStats(Dictionary<StatType, float> updatedStats)
        {
            if (updatedStats == null)
            {
                return;
            }

            if (updatedStats.TryGetValue(StatType.Engine, out float engineValue))
            {
                EnginePower = engineValue;

                if (movement != null)
                {
                    float engineScale = Mathf.Max(0f, EnginePower) / BaseStatScale;
                    movement.ChangeSpeed(_baseMovementSpeed * engineScale);
                }
            }

            if (updatedStats.TryGetValue(StatType.Fuel, out float fuelValue))
            {
                SetMaxFuel(fuelValue, true);
            }

            if (updatedStats.TryGetValue(StatType.Armor, out float armorValue))
            {
                Armor = armorValue;

                if (health != null)
                {
                    health.SetHealth(Armor);
                }
            }

            if (updatedStats.TryGetValue(StatType.Drill, out float drillValue))
            {
                DrillPower = drillValue;
                DrillRewardMultiplier = Mathf.Clamp(
                    DrillPower
                    / SpaceGame.RunOutcome.RunRewardCalculator
                        .DrillStatPointsPerRewardMultiplier,
                    SpaceGame.RunOutcome.RunRewardCalculator.MinimumRewardMultiplier,
                    SpaceGame.RunOutcome.RunRewardCalculator.MaximumRewardMultiplier);

                if (health != null)
                {
                    health.SetDeathRewardMultiplier(DrillRewardMultiplier);
                }
            }

            Debug.Log(
                $"[PlayerController] Stats applied | Engine: {EnginePower:F1} "
                + $"(speed: {movement?.Speed ?? 0f:F2}) | Fuel: {MaxFuel:F1} "
                + $"| Armor: {Armor:F1} | Drill: {DrillPower:F1} "
                + $"(reward x{DrillRewardMultiplier:F2})",
                this);
        }

        public bool TryConsumeFuel(float amount)
        {
            if (amount <= 0f)
            {
                return true;
            }

            bool consumedFullAmount = CurrentFuel >= amount;
            float previousFuel = CurrentFuel;
            CurrentFuel = Mathf.Max(0f, CurrentFuel - amount);

            if (!Mathf.Approximately(previousFuel, CurrentFuel))
            {
                RefreshFuelUi();
            }

            if (CurrentFuel <= 0f)
            {
                HandleFuelDepleted();
            }

            return consumedFullAmount;
        }

        public float CalculateFuelConsumptionPerSecond()
        {
            float engineScale = Mathf.Max(0f, EnginePower) / BaseStatScale;
            float weightedScale = Mathf.Lerp(
                1f,
                engineScale,
                engineFuelConsumptionWeight);
            return fuelConsumptionPerSecond * Mathf.Max(0f, weightedScale);
        }

        public void RefillFuel()
        {
            CurrentFuel = MaxFuel;
            _fuelDepletionHandled = false;
            RefreshFuelUi();
        }

        private void SetMaxFuel(float value, bool refill)
        {
            MaxFuel = Mathf.Max(1f, value);
            CurrentFuel = refill ? MaxFuel : Mathf.Min(CurrentFuel, MaxFuel);
            _fuelDepletionHandled = false;
            RefreshFuelUi();
        }

        private void HandleFuelDepleted()
        {
            if (_fuelDepletionHandled)
            {
                return;
            }

            _fuelDepletionHandled = true;
            FuelDepleted?.Invoke();

            if (health != null && !health.IsDead)
            {
                health.TakeDamage(
                    Mathf.Max(1f, health.CurrentHealth));
            }
        }

        private void RefreshFuelUi()
        {
            if (fuelSlider != null)
            {
                fuelSlider.minValue = 0f;
                fuelSlider.maxValue = Mathf.Max(1f, MaxFuel);
                fuelSlider.value = CurrentFuel;
            }

            if (fuelText != null)
            {
                fuelText.text =
                    $"{Mathf.CeilToInt(CurrentFuel)} / {Mathf.CeilToInt(MaxFuel)}";
            }

            OnFuelChanged?.Invoke(CurrentFuel, MaxFuel);
        }

        private void OnValidate()
        {
            fuelConsumptionPerSecond = Mathf.Max(0f, fuelConsumptionPerSecond);
            engineFuelConsumptionWeight = Mathf.Clamp01(
                engineFuelConsumptionWeight);
        }
    }
}
