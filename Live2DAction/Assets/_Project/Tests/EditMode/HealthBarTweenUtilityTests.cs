using NUnit.Framework;
using UnityEngine;
using Live2DAction.UI;

public class HealthBarTweenUtilityTests
{
    [Test]
    public void SmoothApproach_ZeroDeltaTime_StaysAtCurrent()
    {
        Assert.AreEqual(0.5f, HealthBarTweenUtility.SmoothApproach(0.5f, 1f, 10f, 0f), 0.0001f);
    }

    [Test]
    public void SmoothApproach_LargeDeltaTime_ConvergesOnTarget()
    {
        Assert.AreEqual(1f, HealthBarTweenUtility.SmoothApproach(0.5f, 1f, 10f, 5f), 0.001f);
    }

    [Test]
    public void SmoothApproach_ZeroSpeed_SnapsToTarget()
    {
        Assert.AreEqual(0.8f, HealthBarTweenUtility.SmoothApproach(0.2f, 0.8f, 0f, 0.016f));
    }

    [Test]
    public void ComputeDelayedFill_Heal_SnapsUpImmediately()
    {
        Assert.AreEqual(0.9f, HealthBarTweenUtility.ComputeDelayedFill(0.5f, 0.9f, 999f, 0.5f, 0.6f, 0.016f));
    }

    [Test]
    public void ComputeDelayedFill_WithinHoldWindow_StaysAtOldValue()
    {
        Assert.AreEqual(1f, HealthBarTweenUtility.ComputeDelayedFill(1f, 0.4f, 0.1f, 0.5f, 0.6f, 0.016f));
    }

    [Test]
    public void ComputeDelayedFill_AfterHoldWindow_ChasesTargetDown()
    {
        float result = HealthBarTweenUtility.ComputeDelayedFill(1f, 0.4f, 0.6f, 0.5f, 0.6f, 0.1f);
        Assert.AreEqual(1f - 0.6f * 0.1f, result, 0.0001f);
    }

    [Test]
    public void ComputeDelayedFill_ChaseNeverOvershootsPastTarget()
    {
        float result = HealthBarTweenUtility.ComputeDelayedFill(0.45f, 0.4f, 10f, 0.5f, 0.6f, 1f);
        Assert.AreEqual(0.4f, result, 0.0001f);
    }

    [Test]
    public void ComputeLowHealthIntensity_AtOrAboveThreshold_ReturnsZero()
    {
        Assert.AreEqual(0f, HealthBarTweenUtility.ComputeLowHealthIntensity(0.5f, 0.3f));
        Assert.AreEqual(0f, HealthBarTweenUtility.ComputeLowHealthIntensity(0.3f, 0.3f));
    }

    [Test]
    public void ComputeLowHealthIntensity_Empty_ReturnsOne()
    {
        Assert.AreEqual(1f, HealthBarTweenUtility.ComputeLowHealthIntensity(0f, 0.3f));
    }

    [Test]
    public void ComputeLowHealthIntensity_ZeroThreshold_NeverDividesByZero()
    {
        Assert.AreEqual(0f, HealthBarTweenUtility.ComputeLowHealthIntensity(0f, 0f));
    }

    [Test]
    public void ComputeShakeOffset_ZeroIntensity_ReturnsZeroVector()
    {
        Assert.AreEqual(Vector2.zero, HealthBarTweenUtility.ComputeShakeOffset(0f, 1.23f, 10f));
    }

    [Test]
    public void ComputeEdgeGlowLocalX_ZeroFill_SitsAtLeftInset()
    {
        Assert.AreEqual(2f, HealthBarTweenUtility.ComputeEdgeGlowLocalX(0f, 176f, 2f, 2f), 0.0001f);
    }

    [Test]
    public void ComputeEdgeGlowLocalX_FullFill_SitsAtRightInset()
    {
        Assert.AreEqual(174f, HealthBarTweenUtility.ComputeEdgeGlowLocalX(1f, 176f, 2f, 2f), 0.0001f);
    }

    [Test]
    public void ComputeEdgeGlowLocalX_HalfFill_SitsAtMidpoint()
    {
        Assert.AreEqual(88f, HealthBarTweenUtility.ComputeEdgeGlowLocalX(0.5f, 176f, 2f, 2f), 0.0001f);
    }

    [Test]
    public void ComputeSparkOffset_AtStart_IsZero()
    {
        Assert.AreEqual(Vector2.zero, HealthBarTweenUtility.ComputeSparkOffset(0f, 0.7f, 90f, 220f));
    }

    [Test]
    public void ComputeSparkOffset_HorizontalAngle_MovesRightWithNoVerticalDrop()
    {
        Vector2 result = HealthBarTweenUtility.ComputeSparkOffset(0.5f, 0f, 90f, 0f);
        Assert.AreEqual(45f, result.x, 0.001f);
        Assert.AreEqual(0f, result.y, 0.001f);
    }
}
