using Dev.CSU._02_Scripts.Distance;
using SpaceGame.RunOutcome;
using UnityEngine;

namespace Dev.NKY.Scripts.Health
{
    public class Health : DamageTask
    {
        [Header("Run Reward")]
        [SerializeField] private ResourceManager resourceManager;
        [SerializeField] private HorizontalDistanceTracker distanceTracker;
        [SerializeField, Range(
            RunRewardCalculator.MinimumRewardMultiplier,
            RunRewardCalculator.MaximumRewardMultiplier)]
        private float deathRewardMultiplier = 1f;
        [SerializeField, Min(0)] private int successRewardBonus = 500;

        private readonly RunOutcomeState _runOutcomeState =
            new RunOutcomeState();

        public RunTerminalOutcome CurrentRunOutcome =>
            _runOutcomeState.Outcome;
        public bool RunRewardCommitted =>
            _runOutcomeState.RewardCommitted;
        public int LastRunReward { get; private set; }
        public int LastDeathReward { get; private set; }
        public float LastRewardDistanceMeters { get; private set; }
        public float LastRewardMultiplier { get; private set; } = 1f;
        public int LastResourceBalanceBefore { get; private set; }
        public int LastResourceBalanceAfter { get; private set; }

        public override void Awake()
        {
            base.Awake();
            ResolveRewardDependencies();
        }

        private void ResolveRewardDependencies()
        {
            if (resourceManager == null)
            {
                resourceManager = FindFirstObjectByType<ResourceManager>();
            }

            if (distanceTracker == null)
            {
                distanceTracker = GetComponent<HorizontalDistanceTracker>();
            }
        }

        public int CalculateRunReward()
        {
            float distanceMeters = distanceTracker != null
                ? distanceTracker.DistanceMeters
                : 0f;
            return RunRewardCalculator.Calculate(
                distanceMeters,
                deathRewardMultiplier);
        }

        public int CalculateDeathReward()
        {
            return CalculateRunReward();
        }

        public bool TryResolveRunOutcome(RunTerminalOutcome outcome)
        {
            if (outcome == RunTerminalOutcome.None)
            {
                return false;
            }

            ResolveRewardDependencies();
            if (resourceManager == null)
            {
                Debug.LogError(
                    "[Health] Cannot resolve the run outcome because no "
                    + "ResourceManager is available.",
                    this);
                return false;
            }

            if (!_runOutcomeState.TryResolve(outcome))
            {
                return false;
            }

            LastRewardDistanceMeters = distanceTracker != null
                ? Mathf.Max(0f, distanceTracker.DistanceMeters)
                : 0f;
            LastRewardMultiplier = NormalizeRewardMultiplier(
                deathRewardMultiplier);
            int distanceReward = RunRewardCalculator.Calculate(
                LastRewardDistanceMeters,
                LastRewardMultiplier);
            int outcomeBonus = outcome == RunTerminalOutcome.Success
                ? Mathf.Max(0, successRewardBonus)
                : 0;
            LastRunReward = SaturatingAdd(distanceReward, outcomeBonus);
            LastResourceBalanceBefore = resourceManager.CurrentResource;

            if (!_runOutcomeState.TryCommitReward(outcome))
            {
                Debug.LogError(
                    $"[Health] Outcome '{outcome}' was resolved without an "
                    + "available reward commit.",
                    this);
                return false;
            }

            resourceManager.AddResource(LastRunReward);
            LastResourceBalanceAfter = resourceManager.CurrentResource;
            LastDeathReward = outcome == RunTerminalOutcome.Death
                ? LastRunReward
                : 0;

            Debug.Log(
                $"[RunOutcome] outcome={outcome}, "
                + $"distance={LastRewardDistanceMeters:F1}m, "
                + $"multiplier={LastRewardMultiplier:F2}, "
                + $"reward={LastRunReward}, "
                + $"balance={LastResourceBalanceBefore}"
                + $"->{LastResourceBalanceAfter}.",
                this);
            return true;
        }

        public bool TryBeginSceneTransition(RunTerminalOutcome outcome)
        {
            return _runOutcomeState.TryBeginTransition(outcome);
        }

        public bool TryCancelSceneTransition(RunTerminalOutcome outcome)
        {
            return _runOutcomeState.TryCancelTransition(outcome);
        }

        public void SetDeathRewardMultiplier(float multiplier)
        {
            deathRewardMultiplier = NormalizeRewardMultiplier(multiplier);
        }

        protected override void OnHealthReset()
        {
            _runOutcomeState.Reset();
            LastRunReward = 0;
            LastDeathReward = 0;
            LastRewardDistanceMeters = 0f;
            LastRewardMultiplier = 1f;
            LastResourceBalanceBefore = 0;
            LastResourceBalanceAfter = 0;
        }

        public override void Dead()
        {
            if (IsDead)
            {
                return;
            }

            TryResolveRunOutcome(RunTerminalOutcome.Death);
            base.Dead();
        }

        private static float NormalizeRewardMultiplier(float multiplier)
        {
            if (float.IsNaN(multiplier) || float.IsInfinity(multiplier))
            {
                return 1f;
            }

            return Mathf.Clamp(
                multiplier,
                RunRewardCalculator.MinimumRewardMultiplier,
                RunRewardCalculator.MaximumRewardMultiplier);
        }

        private static int SaturatingAdd(int left, int right)
        {
            long sum = (long)left + right;
            return sum >= int.MaxValue
                ? int.MaxValue
                : (int)sum;
        }
    }
}
