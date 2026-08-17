using NUnit.Framework;

namespace SpaceGame.RunOutcome.Tests
{
    public sealed class RunOutcomeStateTests
    {
        [Test]
        public void FirstTerminalOutcomeWins()
        {
            RunOutcomeState state = new RunOutcomeState();

            Assert.That(
                state.TryResolve(RunTerminalOutcome.Success),
                Is.True);
            Assert.That(
                state.TryResolve(RunTerminalOutcome.Death),
                Is.False);
            Assert.That(
                state.Outcome,
                Is.EqualTo(RunTerminalOutcome.Success));
        }

        [Test]
        public void DeathBeforeSuccessKeepsDeathAsTheOnlyWinner()
        {
            RunOutcomeState state = new RunOutcomeState();

            Assert.That(
                state.TryResolve(RunTerminalOutcome.Death),
                Is.True);
            Assert.That(
                state.TryResolve(RunTerminalOutcome.Success),
                Is.False);
            Assert.That(
                state.Outcome,
                Is.EqualTo(RunTerminalOutcome.Death));
        }

        [Test]
        public void RewardCanOnlyBeCommittedOnceForWinningOutcome()
        {
            RunOutcomeState state = new RunOutcomeState();
            state.TryResolve(RunTerminalOutcome.Death);

            Assert.That(
                state.TryCommitReward(RunTerminalOutcome.Success),
                Is.False);
            Assert.That(
                state.TryCommitReward(RunTerminalOutcome.Death),
                Is.True);
            Assert.That(
                state.TryCommitReward(RunTerminalOutcome.Death),
                Is.False);
        }

        [Test]
        public void TransitionRequiresCommittedRewardAndStartsOnce()
        {
            RunOutcomeState state = new RunOutcomeState();
            state.TryResolve(RunTerminalOutcome.Success);

            Assert.That(
                state.TryBeginTransition(RunTerminalOutcome.Success),
                Is.False);

            state.TryCommitReward(RunTerminalOutcome.Success);

            Assert.That(
                state.TryBeginTransition(RunTerminalOutcome.Success),
                Is.True);
            Assert.That(
                state.TryBeginTransition(RunTerminalOutcome.Success),
                Is.False);
        }

        [Test]
        public void FailedSceneRequestCanReleaseTransitionForRetry()
        {
            RunOutcomeState state = new RunOutcomeState();
            state.TryResolve(RunTerminalOutcome.Death);
            state.TryCommitReward(RunTerminalOutcome.Death);
            state.TryBeginTransition(RunTerminalOutcome.Death);

            Assert.That(
                state.TryCancelTransition(RunTerminalOutcome.Success),
                Is.False);
            Assert.That(
                state.TryCancelTransition(RunTerminalOutcome.Death),
                Is.True);
            Assert.That(
                state.TryBeginTransition(RunTerminalOutcome.Death),
                Is.True);
        }

        [TestCase(999f, 1f, 0)]
        [TestCase(1000f, 1f, 1)]
        [TestCase(2999f, 1.5f, 3)]
        [TestCase(3000f, 2f, 6)]
        [TestCase(3000f, 0.1f, 1)]
        [TestCase(3000f, 10f, 9)]
        [TestCase(-1f, 5f, 0)]
        public void RewardUsesDistanceAndDrillSnapshotPolicy(
            float distanceMeters,
            float multiplier,
            int expectedReward)
        {
            Assert.That(
                RunRewardCalculator.Calculate(
                    distanceMeters,
                    multiplier),
                Is.EqualTo(expectedReward));
        }

        [Test]
        public void ResetStartsANewIndependentRun()
        {
            RunOutcomeState state = new RunOutcomeState();
            state.TryResolve(RunTerminalOutcome.Success);
            state.TryCommitReward(RunTerminalOutcome.Success);
            state.TryBeginTransition(RunTerminalOutcome.Success);

            state.Reset();

            Assert.That(
                state.Outcome,
                Is.EqualTo(RunTerminalOutcome.None));
            Assert.That(state.RewardCommitted, Is.False);
            Assert.That(state.TransitionStarted, Is.False);
            Assert.That(
                state.TryResolve(RunTerminalOutcome.Death),
                Is.True);
        }
    }
}
