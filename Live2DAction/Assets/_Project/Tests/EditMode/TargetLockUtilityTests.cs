using NUnit.Framework;
using UnityEngine;
using Live2DAction.Targeting;

public class TargetLockUtilityTests
{
    [Test]
    public void FindBestTarget_PicksClosestWithinRangeAndAngle()
    {
        var near = new GameObject("Near");
        near.transform.position = new Vector3(0f, 0f, 2f);
        var far = new GameObject("Far");
        far.transform.position = new Vector3(0f, 0f, 5f);

        Transform result = TargetLockUtility.FindBestTarget(
            Vector3.zero, Vector3.forward, maxRange: 10f, maxAngleDegrees: 60f,
            candidates: new[] { far.transform, near.transform });

        Assert.AreSame(near.transform, result);

        Object.DestroyImmediate(near);
        Object.DestroyImmediate(far);
    }

    [Test]
    public void FindBestTarget_IgnoresCandidatesOutOfRange()
    {
        var tooFar = new GameObject("TooFar");
        tooFar.transform.position = new Vector3(0f, 0f, 20f);

        Transform result = TargetLockUtility.FindBestTarget(
            Vector3.zero, Vector3.forward, maxRange: 10f, maxAngleDegrees: 60f,
            candidates: new[] { tooFar.transform });

        Assert.IsNull(result);

        Object.DestroyImmediate(tooFar);
    }

    [Test]
    public void FindBestTarget_IgnoresCandidatesOutsideViewAngle()
    {
        var behind = new GameObject("Behind");
        behind.transform.position = new Vector3(0f, 0f, -5f); // directly behind viewDirection = forward

        Transform result = TargetLockUtility.FindBestTarget(
            Vector3.zero, Vector3.forward, maxRange: 10f, maxAngleDegrees: 60f,
            candidates: new[] { behind.transform });

        Assert.IsNull(result);

        Object.DestroyImmediate(behind);
    }

    [Test]
    public void FindBestTarget_IgnoresInactiveCandidates()
    {
        var inactive = new GameObject("Inactive");
        inactive.transform.position = new Vector3(0f, 0f, 2f);
        inactive.SetActive(false);

        Transform result = TargetLockUtility.FindBestTarget(
            Vector3.zero, Vector3.forward, maxRange: 10f, maxAngleDegrees: 60f,
            candidates: new[] { inactive.transform });

        Assert.IsNull(result);

        Object.DestroyImmediate(inactive);
    }

    [Test]
    public void FindBestTarget_NoCandidates_ReturnsNull()
    {
        Transform result = TargetLockUtility.FindBestTarget(
            Vector3.zero, Vector3.forward, maxRange: 10f, maxAngleDegrees: 60f,
            candidates: new Transform[0]);

        Assert.IsNull(result);
    }

    [Test]
    public void IsStillValid_TrueWithinBreakRange()
    {
        var target = new GameObject("Target");
        target.transform.position = new Vector3(0f, 0f, 5f);

        Assert.IsTrue(TargetLockUtility.IsStillValid(Vector3.zero, target.transform, breakRange: 10f));

        Object.DestroyImmediate(target);
    }

    [Test]
    public void IsStillValid_FalseBeyondBreakRange()
    {
        var target = new GameObject("Target");
        target.transform.position = new Vector3(0f, 0f, 50f);

        Assert.IsFalse(TargetLockUtility.IsStillValid(Vector3.zero, target.transform, breakRange: 10f));

        Object.DestroyImmediate(target);
    }

    [Test]
    public void IsStillValid_FalseWhenTargetDeactivated()
    {
        var target = new GameObject("Target");
        target.transform.position = new Vector3(0f, 0f, 2f);
        target.SetActive(false);

        Assert.IsFalse(TargetLockUtility.IsStillValid(Vector3.zero, target.transform, breakRange: 10f));

        Object.DestroyImmediate(target);
    }

    [Test]
    public void IsStillValid_FalseWhenTargetDestroyed()
    {
        var target = new GameObject("Target");
        Transform targetTransform = target.transform;
        Object.DestroyImmediate(target);

        Assert.IsFalse(TargetLockUtility.IsStillValid(Vector3.zero, targetTransform, breakRange: 10f));
    }

    [Test]
    public void ComputeLockOnYawPitch_ResultingDirectionPointsAtTarget()
    {
        Vector3 from = new Vector3(1f, 2f, 3f);
        Vector3 to = new Vector3(4f, 0f, 8f); // above, to the side, further away

        TargetLockUtility.ComputeLockOnYawPitch(from, to, minPitch: -89f, maxPitch: 89f, out float yaw, out float pitch);

        Vector3 reconstructedDirection = Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;
        Vector3 expectedDirection = (to - from).normalized;

        Assert.AreEqual(expectedDirection.x, reconstructedDirection.x, 0.001f);
        Assert.AreEqual(expectedDirection.y, reconstructedDirection.y, 0.001f);
        Assert.AreEqual(expectedDirection.z, reconstructedDirection.z, 0.001f);
    }

    [Test]
    public void ComputeLockOnYawPitch_ClampsPitchToProvidedRange()
    {
        Vector3 from = Vector3.zero;
        Vector3 to = new Vector3(0f, 100f, 1f); // almost straight up - would need a very steep pitch

        TargetLockUtility.ComputeLockOnYawPitch(from, to, minPitch: -30f, maxPitch: 30f, out _, out float pitch);

        Assert.LessOrEqual(pitch, 30f);
        Assert.GreaterOrEqual(pitch, -30f);
    }

    [Test]
    public void ComputeLockOnYawPitch_SamePosition_ReturnsZero()
    {
        TargetLockUtility.ComputeLockOnYawPitch(Vector3.one, Vector3.one, -60f, 60f, out float yaw, out float pitch);

        Assert.AreEqual(0f, yaw);
        Assert.AreEqual(0f, pitch);
    }
}
