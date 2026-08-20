using NUnit.Framework;
using UnityEngine;
using Live2DAction.Combat;

public class UltimateBurstUtilityTests
{
    [Test]
    public void ComputeRadius_AtStart_IsZero()
    {
        Assert.AreEqual(0f, UltimateBurstUtility.ComputeRadius(0f, 2.5f), 0.0001f);
    }

    [Test]
    public void ComputeRadius_AtEnd_ReachesMaxRadius()
    {
        Assert.AreEqual(2.5f, UltimateBurstUtility.ComputeRadius(1f, 2.5f), 0.0001f);
    }

    [Test]
    public void ComputeRadius_EasesOut_ExpandsFasterEarlyThanLate()
    {
        // Ease-out cubic: the first half of the timeline should cover more than half the
        // distance to maxRadius (fast punch-out, not a linear/constant-rate expansion).
        float atHalfway = UltimateBurstUtility.ComputeRadius(0.5f, 2.5f);
        Assert.Greater(atHalfway, 1.25f, "Ease-out should already be past the halfway radius by t=0.5");
    }

    [Test]
    public void ComputeBrightnessMultiplier_AtStart_IsFullBrightness()
    {
        Assert.AreEqual(1f, UltimateBurstUtility.ComputeBrightnessMultiplier(0f), 0.0001f);
    }

    [Test]
    public void ComputeBrightnessMultiplier_AtEnd_IsZero()
    {
        Assert.AreEqual(0f, UltimateBurstUtility.ComputeBrightnessMultiplier(1f), 0.0001f);
    }

    [Test]
    public void ComputeBrightnessMultiplier_StaysHighEarlyOn()
    {
        // Quadratic falloff - should still be mostly bright a quarter of the way through,
        // reading as a sudden flash rather than an immediate linear dim.
        float atQuarter = UltimateBurstUtility.ComputeBrightnessMultiplier(0.25f);
        Assert.Greater(atQuarter, 0.5f);
    }

    [Test]
    public void BuildRingPoints_IsAClosedLoop()
    {
        Vector3[] points = UltimateBurstUtility.BuildRingPoints(2f, 0.1f, 8);
        float distance = Vector3.Distance(points[0], points[points.Length - 1]);
        Assert.Less(distance, 0.0001f, "First and last ring points should coincide (within float precision) to close the loop");
    }

    [Test]
    public void BuildRingPoints_AllPointsAtGivenRadiusAndHeight()
    {
        Vector3[] points = UltimateBurstUtility.BuildRingPoints(3f, 0.5f, 12);
        foreach (Vector3 point in points)
        {
            Assert.AreEqual(0.5f, point.y, 0.0001f);
            Assert.AreEqual(3f, new Vector2(point.x, point.z).magnitude, 0.001f);
        }
    }

    [Test]
    public void ComputeRayDirection_IsUnitLength()
    {
        Vector3 direction = UltimateBurstUtility.ComputeRayDirection(2, 8);
        Assert.AreEqual(1f, direction.magnitude, 0.0001f);
    }

    [Test]
    public void ComputeRayDirection_EvenlySpacedAroundCircle()
    {
        // 4 rays should be 90 degrees apart - ray 2 should point opposite ray 0.
        Vector3 ray0 = UltimateBurstUtility.ComputeRayDirection(0, 4);
        Vector3 ray2 = UltimateBurstUtility.ComputeRayDirection(2, 4);
        Assert.AreEqual(-ray0.x, ray2.x, 0.0001f);
        Assert.AreEqual(-ray0.z, ray2.z, 0.0001f);
    }
}
