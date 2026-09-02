using NUnit.Framework;
using Live2DAction.AI.Boss;

// 2026-08-29, user request ("移動速度太慢了 *1.5倍 腳步要配合"). The only pure-logic slice of the
// boss's locomotion foot-sync - see BossStateMachine.locomotionAuthoredSpeed.
public class BossStrideRateTests
{
    [Test]
    public void ComputeStrideRate_Disabled_IsOne()
    {
        Assert.AreEqual(1f, BossStateMachine.ComputeStrideRate(5f, 0f), 0.0001f);
    }

    [Test]
    public void ComputeStrideRate_FullSpeed_ScalesByRatio()
    {
        // WalkSpeed pushed 2 -> 3 against a run clip authored for 2
        Assert.AreEqual(1.5f, BossStateMachine.ComputeStrideRate(3f, 2f), 0.0001f);
    }

    [Test]
    public void ComputeStrideRate_DecelerationTail_FloorsAtMinRate()
    {
        // Approach tapers to WalkSpeed * 0.35 near the player
        Assert.AreEqual(0.6f, BossStateMachine.ComputeStrideRate(3f * 0.35f, 2f), 0.0001f);
    }

    [Test]
    public void ComputeStrideRate_ExtremeOverspeed_CapsAtMaxRate()
    {
        Assert.AreEqual(2.5f, BossStateMachine.ComputeStrideRate(50f, 2f), 0.0001f);
    }
}
