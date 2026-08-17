using System;

namespace SpaceGame.RunOutcome
{
    public enum RunTerminalOutcome
    {
        None = 0,
        Death = 1,
        Success = 2
    }

    /// <summary>
    /// Pure state machine for one flight's terminal result. The first terminal
    /// result owns both the reward commit and the scene-transition request.
    /// </summary>
    public sealed class RunOutcomeState
    {
        public RunTerminalOutcome Outcome { get; private set; }
        public bool RewardCommitted { get; private set; }
        public bool TransitionStarted { get; private set; }

        public bool TryResolve(RunTerminalOutcome outcome)
        {
            if (outcome == RunTerminalOutcome.None || Outcome != RunTerminalOutcome.None)
            {
                return false;
            }

            Outcome = outcome;
            return true;
        }

        public bool TryCommitReward(RunTerminalOutcome outcome)
        {
            if (outcome == RunTerminalOutcome.None
                || Outcome != outcome
                || RewardCommitted)
            {
                return false;
            }

            RewardCommitted = true;
            return true;
        }

        public bool TryBeginTransition(RunTerminalOutcome outcome)
        {
            if (outcome == RunTerminalOutcome.None
                || Outcome != outcome
                || !RewardCommitted
                || TransitionStarted)
            {
                return false;
            }

            TransitionStarted = true;
            return true;
        }

        public bool TryCancelTransition(RunTerminalOutcome outcome)
        {
            if (Outcome != outcome || !TransitionStarted)
            {
                return false;
            }

            TransitionStarted = false;
            return true;
        }

        public void Reset()
        {
            Outcome = RunTerminalOutcome.None;
            RewardCommitted = false;
            TransitionStarted = false;
        }
    }

    public static class RunRewardCalculator
    {
        public const float MetersPerResource = 1000f;
        public const float MinimumRewardMultiplier = 0.5f;
        public const float MaximumRewardMultiplier = 3f;
        public const float DrillStatPointsPerRewardMultiplier = 100f;
        public const float MaximumPermanentDrillStat = 250f;
        public const float MaximumEffectiveDrillStat =
            MaximumRewardMultiplier * DrillStatPointsPerRewardMultiplier;

        public static int Calculate(float distanceMeters, float rewardMultiplier)
        {
            double normalizedDistance = NormalizeDistance(distanceMeters);
            double normalizedMultiplier = NormalizeMultiplier(rewardMultiplier);
            double distanceReward = Math.Floor(
                normalizedDistance / MetersPerResource);
            double calculatedReward = Math.Floor(
                distanceReward * normalizedMultiplier);

            if (calculatedReward <= 0d)
            {
                return 0;
            }

            return calculatedReward >= int.MaxValue
                ? int.MaxValue
                : (int)calculatedReward;
        }

        private static double NormalizeDistance(float distanceMeters)
        {
            return float.IsNaN(distanceMeters)
                || float.IsInfinity(distanceMeters)
                || distanceMeters <= 0f
                    ? 0d
                    : distanceMeters;
        }

        private static double NormalizeMultiplier(float rewardMultiplier)
        {
            return float.IsNaN(rewardMultiplier)
                || float.IsInfinity(rewardMultiplier)
                    ? 1d
                    : Math.Min(
                        MaximumRewardMultiplier,
                        Math.Max(
                            MinimumRewardMultiplier,
                            rewardMultiplier));
        }
    }
}
