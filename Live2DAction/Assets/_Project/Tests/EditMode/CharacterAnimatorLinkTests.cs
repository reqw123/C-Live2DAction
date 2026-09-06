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

    // 2026-09-06, immersive walk ("緩慢沉浸式行走")

    [Test]
    public void GroundAnimatorSpeed_Walking_UsesWalkRate()
    {
        float s = CharacterAnimatorLink.ComputeGroundAnimatorSpeed(
            isWalking: true, walkRate: 0.65f,
            currentSpeed: 0.55f, authoredTopSpeed: 2f, maxStrideRate: 2.5f,
            grounded: true, syncStride: false);
        Assert.AreEqual(0.65f, s, 0.0001f);
    }

    [Test]
    public void GroundAnimatorSpeed_Walking_WinsOverStrideSync()
    {
        float s = CharacterAnimatorLink.ComputeGroundAnimatorSpeed(
            isWalking: true, walkRate: 0.6f,
            currentSpeed: 6f, authoredTopSpeed: 2f, maxStrideRate: 2.5f,
            grounded: true, syncStride: true);
        Assert.AreEqual(0.6f, s, 0.0001f);
    }

    [Test]
    public void GroundAnimatorSpeed_NotWalking_NoSync_IsOne()
    {
        float s = CharacterAnimatorLink.ComputeGroundAnimatorSpeed(
            isWalking: false, walkRate: 0.65f,
            currentSpeed: 2f, authoredTopSpeed: 2f, maxStrideRate: 2.5f,
            grounded: true, syncStride: false);
        Assert.AreEqual(1f, s, 0.0001f);
    }

    [Test]
    public void GroundAnimatorSpeed_NotWalking_WithSync_UsesStrideRate()
    {
        float s = CharacterAnimatorLink.ComputeGroundAnimatorSpeed(
            isWalking: false, walkRate: 0.65f,
            currentSpeed: 3f, authoredTopSpeed: 2f, maxStrideRate: 2.5f,
            grounded: true, syncStride: true);
        Assert.AreEqual(1.5f, s, 0.0001f);
    }

    [Test]
    public void GroundAnimatorSpeed_WalkingButAirborne_IgnoresWalkRate()
    {
        float s = CharacterAnimatorLink.ComputeGroundAnimatorSpeed(
            isWalking: true, walkRate: 0.65f,
            currentSpeed: 0.5f, authoredTopSpeed: 2f, maxStrideRate: 2.5f,
            grounded: false, syncStride: false);
        Assert.AreEqual(1f, s, 0.0001f);
    }

    [Test]
    public void GroundAnimatorSpeed_WalkRateClampedToSaneRange()
    {
        Assert.AreEqual(1f, CharacterAnimatorLink.ComputeGroundAnimatorSpeed(true, 5f, 0.5f, 2f, 2.5f, true, false), 0.0001f);
        Assert.AreEqual(0.05f, CharacterAnimatorLink.ComputeGroundAnimatorSpeed(true, 0f, 0.5f, 2f, 2.5f, true, false), 0.0001f);
    }
}
