using NUnit.Framework;
using Live2DAction.Core;

// spec WUSHI_COMBAT_ENGINEERING_SPEC.md §8.2 (M4 項目 7) - the Deathblow node bookkeeping: a
// non-final node spends and transitions phase, the last one kills, and an exhausted boss refuses.
public class ExecutionNodeLogicTests
{
    [Test]
    public void Deathblow_TwoNodeBoss_FirstIsAPhaseTransition_SecondIsAKill()
    {
        var first = ExecutionNodeLogic.Deathblow(2);
        Assert.AreEqual(1, first.remaining);
        Assert.AreEqual(ExecutionOutcome.PhaseTransition, first.outcome);

        var second = ExecutionNodeLogic.Deathblow(first.remaining);
        Assert.AreEqual(0, second.remaining);
        Assert.AreEqual(ExecutionOutcome.Killed, second.outcome);
    }

    [Test]
    public void Deathblow_ThreeNodeBoss_KeepsTransitioningUntilTheLast()
    {
        Assert.AreEqual(ExecutionOutcome.PhaseTransition, ExecutionNodeLogic.Deathblow(3).outcome);
        Assert.AreEqual(ExecutionOutcome.PhaseTransition, ExecutionNodeLogic.Deathblow(2).outcome);
        Assert.AreEqual(ExecutionOutcome.Killed, ExecutionNodeLogic.Deathblow(1).outcome);
    }

    [Test]
    public void Deathblow_ExhaustedBoss_RefusesAndStaysAtZero()
    {
        var r = ExecutionNodeLogic.Deathblow(0);
        Assert.AreEqual(0, r.remaining);
        Assert.AreEqual(ExecutionOutcome.Refused, r.outcome);

        Assert.AreEqual(ExecutionOutcome.Refused, ExecutionNodeLogic.Deathblow(-1).outcome);
    }
}
