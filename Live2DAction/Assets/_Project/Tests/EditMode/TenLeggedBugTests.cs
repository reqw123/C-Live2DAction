using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Live2DAction.AI;

// Pure-logic coverage for the ten-legged bug: the strict one-leg-at-a-time gait cycle
// (TenLeggedBugGaitUtility), the rhino-horn stab timing + 30-degree attack cone
// (TenLeggedBugAttackUtility), and the controller's rest-pose snapshot. Mirrors the
// pure-helper-first test style already used by CatProceduralWalkTests / WanderUtilityTests /
// AttackResolverTests.
public class TenLeggedBugTests
{
    // ---------------------------------------------------------------- gait: one leg at a time ----

    [Test]
    public void SteppingLegIndex_WalksThroughEveryLegInStrictOrderAcrossOneCycle()
    {
        const int legs = 8;
        // Sample the middle of each of the 8 equal slices - leg k must own slice k, in order.
        for (int k = 0; k < legs; k++)
        {
            float phase = (k + 0.5f) / legs;
            Assert.AreEqual(k, TenLeggedBugGaitUtility.SteppingLegIndex(phase, legs),
                $"slice {k} should belong to leg {k}");
        }
        // Wraps back to leg 0 at the top of the next cycle.
        Assert.AreEqual(0, TenLeggedBugGaitUtility.SteppingLegIndex(1.0f, legs));
        Assert.AreEqual(0, TenLeggedBugGaitUtility.SteppingLegIndex(1.0f / legs * 0.1f, legs));
    }

    [Test]
    public void LegLift_IsNonZeroForExactlyTheOneSteppingLeg()
    {
        const int legs = 10;
        float phase = 3.5f / legs; // squarely inside leg index 3's slice
        int lifted = 0;
        for (int i = 0; i < legs; i++)
        {
            if (TenLeggedBugGaitUtility.LegLift01(phase, legs, i) > 0f) lifted++;
        }
        Assert.AreEqual(1, lifted, "exactly one leg may be off the ground at any instant");
        Assert.Greater(TenLeggedBugGaitUtility.LegLift01(phase, legs, 3), 0f);
    }

    [Test]
    public void LegLift_PeaksAtTheMiddleOfTheStepAndIsZeroAtBothEnds()
    {
        const int legs = 8;
        float sliceStart = 2f / legs;
        float sliceMid = 2.5f / legs;
        Assert.Less(TenLeggedBugGaitUtility.LegLift01(sliceStart + 0.0001f, legs, 2), 0.05f);
        Assert.Greater(TenLeggedBugGaitUtility.LegLift01(sliceMid, legs, 2), 0.95f);
    }

    [Test]
    public void SteppingLegStride_SweepsFromBackToFrontAcrossItsSlice()
    {
        const int legs = 8;
        float sliceStart = 4f / legs;
        float sliceEnd = 5f / legs - 0.0001f;
        Assert.Less(TenLeggedBugGaitUtility.LegStride(sliceStart + 0.0001f, legs, 4), -0.8f, "starts fully back");
        Assert.Greater(TenLeggedBugGaitUtility.LegStride(sliceEnd, legs, 4), 0.8f, "ends fully forward");
    }

    [Test]
    public void AdvancePhase_ScalesWithSpeed_AndZeroSpeedFreezesTheCycle()
    {
        float still = TenLeggedBugGaitUtility.AdvancePhase(0.2f, moveSpeed: 0f,
            speedForFullRate: 5f, baseRateHz: 2f, deltaTime: 0.5f);
        Assert.AreEqual(0.2f, still, 1e-5f, "a stationary bug must not cycle its legs");

        float slow = TenLeggedBugGaitUtility.AdvancePhase(0f, moveSpeed: 1f,
            speedForFullRate: 5f, baseRateHz: 2f, deltaTime: 0.1f);
        float fast = TenLeggedBugGaitUtility.AdvancePhase(0f, moveSpeed: 5f,
            speedForFullRate: 5f, baseRateHz: 2f, deltaTime: 0.1f);
        Assert.Greater(fast, slow, "faster movement advances the gait cycle faster");
    }

    [Test]
    public void SteppingLegIndex_WithNoLegs_ReturnsMinusOne()
    {
        Assert.AreEqual(-1, TenLeggedBugGaitUtility.SteppingLegIndex(0.5f, 0));
    }

    // -------------------------------------------------------------------- attack: cone + horn ----

    [Test]
    public void TargetWithinAttackCone_OnlyTrueWhenTargetIsRoughlyDeadAhead()
    {
        Vector3 fwd = Vector3.forward;
        Assert.IsTrue(TenLeggedBugAttackUtility.TargetWithinAttackCone(fwd, Vector3.forward, 30f));
        Assert.IsTrue(TenLeggedBugAttackUtility.TargetWithinAttackCone(fwd, new Vector3(0.4f, 0f, 1f), 30f));
        Assert.IsFalse(TenLeggedBugAttackUtility.TargetWithinAttackCone(fwd, Vector3.right, 30f), "90 deg to the side");
        Assert.IsFalse(TenLeggedBugAttackUtility.TargetWithinAttackCone(fwd, Vector3.back, 30f), "directly behind");
    }

    [Test]
    public void HornPitch_RaisesDuringWindup_DrivesDownForTheStab_ThenReturnsToRest()
    {
        // Spec: 0..0.25 up, 0.25..0.45 slam down, 0.45..1 recover to 0.
        float atRaisePeak = TenLeggedBugAttackUtility.HornPitchDegrees(0.25f, 0.25f, 0.45f, 28f, 46f);
        float atStabEnd = TenLeggedBugAttackUtility.HornPitchDegrees(0.45f, 0.25f, 0.45f, 28f, 46f);
        float atRecovered = TenLeggedBugAttackUtility.HornPitchDegrees(1f, 0.25f, 0.45f, 28f, 46f);

        Assert.AreEqual(28f, atRaisePeak, 0.5f, "horn is fully raised at the end of the wind-up");
        Assert.AreEqual(-46f, atStabEnd, 0.5f, "horn is fully driven down at the end of the stab");
        Assert.AreEqual(0f, atRecovered, 0.5f, "horn is back to the ready pose by cycle end");
        Assert.Greater(atRaisePeak, 0f);
        Assert.Less(atStabEnd, 0f);
    }

    [Test]
    public void HornStrikeIsLive_OnlyDuringTheContactSubWindow_NotWheneverInRange()
    {
        Assert.IsFalse(TenLeggedBugAttackUtility.HornStrikeIsLive(0.0f, 0.28f, 0.45f), "not during wind-up");
        Assert.IsTrue(TenLeggedBugAttackUtility.HornStrikeIsLive(0.35f, 0.28f, 0.45f), "live mid down-stab");
        Assert.IsFalse(TenLeggedBugAttackUtility.HornStrikeIsLive(0.6f, 0.28f, 0.45f), "not during recovery");
        Assert.IsFalse(TenLeggedBugAttackUtility.HornStrikeIsLive(1.0f, 0.28f, 0.45f), "not at rest");
    }

    // ----------------------------------------------------------------- controller: rest pose ----

    [Test]
    public void CaptureRestPose_SnapshotsBodyHornAndEveryLegRootLocalRotation()
    {
        var root = new GameObject("Bug");
        root.AddComponent<CharacterController>();

        var body = new GameObject("Body"); body.transform.SetParent(root.transform);
        body.transform.localRotation = Quaternion.Euler(3f, 12f, 0f);
        var horn = new GameObject("Horn"); horn.transform.SetParent(body.transform);
        horn.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);

        var legRoots = new System.Collections.Generic.List<Transform>();
        for (int i = 0; i < 8; i++)
        {
            var hip = new GameObject("Hip" + i); hip.transform.SetParent(body.transform);
            hip.transform.localRotation = Quaternion.Euler(0f, 40f * i, 5f);
            var knee = new GameObject("Knee" + i); knee.transform.SetParent(hip.transform);
            legRoots.Add(hip.transform);
        }

        var bug = root.AddComponent<TenLeggedBugController>();
        var t = typeof(TenLeggedBugController);
        t.GetField("bodyRootBone", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bug, body.transform);
        t.GetField("hornBone", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bug, horn.transform);
        t.GetField("legRootBones", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bug, legRoots);

        Assert.DoesNotThrow(() => bug.CaptureRestPose());

        var swingRest = (Quaternion[])t.GetField("_legSwingRest", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(bug);
        Assert.AreEqual(8, swingRest.Length);
        Assert.Less(Quaternion.Angle(swingRest[3], Quaternion.Euler(0f, 120f, 5f)), 0.01f);

        var bendResolved = (Transform[])t.GetField("_legBend", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(bug);
        Assert.IsNotNull(bendResolved[0], "bend bone auto-resolves to the leg root's first child");
        Assert.AreEqual("Knee0", bendResolved[0].name);

        Object.DestroyImmediate(root);
    }

    [Test]
    public void CaptureRestPose_WithNoLegs_DoesNotThrow()
    {
        var root = new GameObject("Bug");
        root.AddComponent<CharacterController>();
        var bug = root.AddComponent<TenLeggedBugController>();
        Assert.DoesNotThrow(() => bug.CaptureRestPose());
        Object.DestroyImmediate(root);
    }
}
