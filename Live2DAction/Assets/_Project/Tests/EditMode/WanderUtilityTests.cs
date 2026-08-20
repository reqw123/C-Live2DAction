using NUnit.Framework;
using UnityEngine;
using Live2DAction.Characters;

public class WanderUtilityTests
{
    [Test]
    public void WellInsideBoundary_UsesTheSuppliedRandomAngle()
    {
        Vector3 direction = WanderUtility.ComputeDirection(Vector3.zero, Vector3.forward, boundaryHalfExtent: 13f, randomAngleDegrees: () => 90f);

        // 90 degrees -> +X, matching Quaternion.Euler(0, 90, 0) * Vector3.forward.
        Assert.Less(Vector3.Distance(direction, Vector3.right), 0.001f);
    }

    [Test]
    public void PastPositiveXBoundary_SteersBackTowardOrigin()
    {
        Vector3 direction = WanderUtility.ComputeDirection(new Vector3(14f, 0f, 0f), Vector3.forward, boundaryHalfExtent: 13f, randomAngleDegrees: () => 0f);

        Assert.Less(direction.x, 0f, "Past the +X boundary should steer back towards -X (the origin), not continue outward.");
    }

    [Test]
    public void PastNegativeZBoundary_SteersBackTowardOrigin()
    {
        Vector3 direction = WanderUtility.ComputeDirection(new Vector3(0f, 0f, -14f), Vector3.forward, boundaryHalfExtent: 13f, randomAngleDegrees: () => 0f);

        Assert.Greater(direction.z, 0f, "Past the -Z boundary should steer back towards +Z (the origin), not continue outward.");
    }

    [Test]
    public void ExactlyAtOriginButFlaggedPastBoundary_FallsBackToCurrentDirection()
    {
        // Degenerate case: "towards origin" from the origin itself is a zero vector, so this
        // must not divide by zero / return NaN - it should just keep going the way it was.
        Vector3 direction = WanderUtility.ComputeDirection(Vector3.zero, Vector3.right, boundaryHalfExtent: -1f, randomAngleDegrees: () => 0f);

        Assert.AreEqual(Vector3.right, direction);
    }

    [Test]
    public void ReturnedDirection_IsAlwaysHorizontalAndNormalized()
    {
        Vector3 direction = WanderUtility.ComputeDirection(new Vector3(5f, 0f, 5f), Vector3.forward, boundaryHalfExtent: 13f, randomAngleDegrees: () => 47f);

        Assert.AreEqual(0f, direction.y, 0.0001f);
        Assert.AreEqual(1f, direction.magnitude, 0.0001f);
    }
}
