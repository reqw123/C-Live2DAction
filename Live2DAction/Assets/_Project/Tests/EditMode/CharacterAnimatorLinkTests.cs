using NUnit.Framework;
using Live2DAction.Characters;

public class CharacterAnimatorLinkTests
{
    [Test]
    public void ComputeSpeedParameter_Idle_ReturnsZero()
    {
        float value = CharacterAnimatorLink.ComputeSpeedParameter(0f, 2f);
        Assert.AreEqual(0f, value);
    }

    [Test]
    public void ComputeSpeedParameter_WithinRange_PassesThroughUnscaled()
    {
        float value = CharacterAnimatorLink.ComputeSpeedParameter(1.2f, 2f);
        Assert.AreEqual(1.2f, value, 0.0001f);
    }

    [Test]
    public void ComputeSpeedParameter_ExceedsMax_ClampsToMax()
    {
        float value = CharacterAnimatorLink.ComputeSpeedParameter(5f, 2f);
        Assert.AreEqual(2f, value, 0.0001f);
    }

    [Test]
    public void ComputeSpeedParameter_NegativeSpeed_ClampsToZero()
    {
        float value = CharacterAnimatorLink.ComputeSpeedParameter(-1f, 2f);
        Assert.AreEqual(0f, value);
    }

    // 2026-08-29, foot-sync ("移動速度太慢了 *1.5倍 腳步要配合")

    [Test]
    public void ComputeStrideRate_NotGrounded_IsOne()
    {
        Assert.AreEqual(1f, CharacterAnimatorLink.ComputeStrideRate(6f, 2f, 2.5f, grounded: false), 0.0001f);
    }

    [Test]
    public void ComputeStrideRate_AtOrBelowAuthoredTop_IsOne()
    {
        Assert.AreEqual(1f, CharacterAnimatorLink.ComputeStrideRate(2f, 2f, 2.5f, grounded: true), 0.0001f);
        Assert.AreEqual(1f, CharacterAnimatorLink.ComputeStrideRate(0.5f, 2f, 2.5f, grounded: true), 0.0001f);
    }

    [Test]
    public void ComputeStrideRate_Overspeed_ScalesByRatio()
    {
        // moveSpeed 3 against a blend tree authored for 2 -> feet play 1.5x faster
        Assert.AreEqual(1.5f, CharacterAnimatorLink.ComputeStrideRate(3f, 2f, 2.5f, grounded: true), 0.0001f);
    }

    [Test]
    public void ComputeStrideRate_ExtremeOverspeed_CapsAtMaxRate()
    {
        Assert.AreEqual(2.5f, CharacterAnimatorLink.ComputeStrideRate(20f, 2f, 2.5f, grounded: true), 0.0001f);
    }
}
