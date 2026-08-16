using NUnit.Framework;
using UnityEngine;
using Live2DAction.UI;

public class LightningAuraUtilityTests
{
    [Test]
    public void ComputeSpiralPoint_AtBottom_SitsAtBaseHeightOnPositiveX()
    {
        Vector3 point = LightningAuraUtility.ComputeSpiralPoint(0f, 0.05f, 1.3f, 0.55f, 2.5f);
        Assert.AreEqual(new Vector3(0.55f, 0.05f, 0f), point, "s=0 (bottom, angle 0) should sit at [radius, baseHeight, 0]");
    }

    [Test]
    public void ComputeSpiralPoint_AtTop_ReachesFullClimbHeight()
    {
        Vector3 point = LightningAuraUtility.ComputeSpiralPoint(1f, 0.05f, 1.3f, 0.55f, 2.5f);
        Assert.AreEqual(1.35f, point.y, 0.0001f, "s=1 should be baseHeight + totalHeight");
    }

    [Test]
    public void ComputeSpiralPoint_MultipleTurns_WrapsAroundMoreThanOnce()
    {
        // 2.5 turns means s=1 has rotated 900 degrees - same XZ angle as 180 degrees.
        Vector3 atTop = LightningAuraUtility.ComputeSpiralPoint(1f, 0f, 1f, 1f, 2.5f);
        Vector3 halfTurn = new Vector3(Mathf.Cos(180f * Mathf.Deg2Rad), 1f, Mathf.Sin(180f * Mathf.Deg2Rad));
        Assert.AreEqual(halfTurn.x, atTop.x, 0.0001f);
        Assert.AreEqual(halfTurn.z, atTop.z, 0.0001f);
    }

    [Test]
    public void ComputeLoopProgress_WithinFirstLoop_ScalesLinearly()
    {
        Assert.AreEqual(0.5f, LightningAuraUtility.ComputeLoopProgress(0.6f, 1.2f), 0.0001f);
    }

    [Test]
    public void ComputeLoopProgress_PastOneLoop_Wraps()
    {
        // 1.5s into a 1.2s loop is 0.3s into the second loop -> progress 0.25.
        Assert.AreEqual(0.25f, LightningAuraUtility.ComputeLoopProgress(1.5f, 1.2f), 0.0001f);
    }

    [Test]
    public void ComputeGrowthAmount_BeforeGrowFraction_IsPartial()
    {
        Assert.AreEqual(0.5f, LightningAuraUtility.ComputeGrowthAmount(0.25f, 0.5f), 0.0001f);
    }

    [Test]
    public void ComputeGrowthAmount_AfterGrowFraction_StaysFullyGrown()
    {
        Assert.AreEqual(1f, LightningAuraUtility.ComputeGrowthAmount(0.9f, 0.5f), 0.0001f);
    }

    [Test]
    public void ComputeBrightnessMultiplier_BeforeFadeStart_IsFullBrightness()
    {
        Assert.AreEqual(1f, LightningAuraUtility.ComputeBrightnessMultiplier(0.5f, 0.8f));
    }

    [Test]
    public void ComputeBrightnessMultiplier_AtLoopEnd_IsZero()
    {
        Assert.AreEqual(0f, LightningAuraUtility.ComputeBrightnessMultiplier(1f, 0.8f), 0.0001f);
    }

    [Test]
    public void ComputeBrightnessMultiplier_MidFade_IsPartial()
    {
        // Halfway between fadeStart (0.8) and the end (1.0) is progress 0.9 -> brightness 0.5.
        Assert.AreEqual(0.5f, LightningAuraUtility.ComputeBrightnessMultiplier(0.9f, 0.8f), 0.0001f);
    }

    [Test]
    public void ComputeJitterOffsets_ReturnsOnePerPointIncludingEndpoints()
    {
        Vector2[] offsets = LightningAuraUtility.ComputeJitterOffsets(24, 0.05f, new System.Random(1));
        Assert.AreEqual(25, offsets.Length);
    }

    [Test]
    public void ComputeJitterOffsets_StaysWithinJitterAmount()
    {
        Vector2[] offsets = LightningAuraUtility.ComputeJitterOffsets(24, 0.05f, new System.Random(42));
        foreach (Vector2 offset in offsets)
        {
            Assert.LessOrEqual(Mathf.Abs(offset.x), 0.05f);
            Assert.LessOrEqual(Mathf.Abs(offset.y), 0.05f);
        }
    }

    [Test]
    public void BuildSpiralPoints_PartialGrowth_ReturnsFewerPointsThanFull()
    {
        Vector2[] jitter = LightningAuraUtility.ComputeJitterOffsets(24, 0f, new System.Random(1));
        Vector3[] halfGrown = LightningAuraUtility.BuildSpiralPoints(0.5f, 0.05f, 1.3f, 0.55f, 2.5f, jitter);
        Vector3[] fullyGrown = LightningAuraUtility.BuildSpiralPoints(1f, 0.05f, 1.3f, 0.55f, 2.5f, jitter);
        Assert.Less(halfGrown.Length, fullyGrown.Length);
    }

    [Test]
    public void BuildSpiralPoints_ZeroGrowth_ReturnsAtLeastOnePoint()
    {
        Vector2[] jitter = LightningAuraUtility.ComputeJitterOffsets(24, 0f, new System.Random(1));
        Vector3[] points = LightningAuraUtility.BuildSpiralPoints(0f, 0.05f, 1.3f, 0.55f, 2.5f, jitter);
        Assert.GreaterOrEqual(points.Length, 1);
    }

    [Test]
    public void BuildSpiralPoints_FullyGrown_LastPointNearClimbTop()
    {
        Vector2[] jitter = LightningAuraUtility.ComputeJitterOffsets(24, 0f, new System.Random(1)); // zero jitterAmount, deterministic top position
        Vector3[] points = LightningAuraUtility.BuildSpiralPoints(1f, 0.05f, 1.3f, 0.55f, 2.5f, jitter);
        Assert.AreEqual(1.35f, points[points.Length - 1].y, 0.0001f);
    }

    [Test]
    public void BuildSpiralPoints_FirstPointAlwaysAtBase()
    {
        Vector2[] jitter = LightningAuraUtility.ComputeJitterOffsets(24, 0f, new System.Random(1));
        Vector3[] points = LightningAuraUtility.BuildSpiralPoints(0.7f, 0.1f, 1.3f, 0.55f, 2.5f, jitter);
        Assert.AreEqual(0.1f, points[0].y, 0.0001f);
    }
}
