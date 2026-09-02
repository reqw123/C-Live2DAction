using NUnit.Framework;
using UnityEngine;
using Live2DAction.Combat;

// Pure-logic coverage for the multi-point weapon sweep (spec WUSHI_COMBAT_ENGINEERING_SPEC.md §4,
// M2 項目 3/4): the subdivision count that stops a fast swing tunnelling a target, the even-split
// sub-segment maths, and the blade-midpoint fallback. Same pure-helper-first style as
// PlayerGuardUtilityTests / AttackResolverTests - the SphereCast itself lives in PlayerWeaponHitbox
// and is exercised in PlayMode.
public class WeaponSweepUtilityTests
{
    [Test]
    public void SubdivisionCount_ShortSweep_IsOneSegment()
    {
        Assert.AreEqual(1, WeaponSweepUtility.SubdivisionCount(0f, 0.25f));
        Assert.AreEqual(1, WeaponSweepUtility.SubdivisionCount(0.1f, 0.25f));
        Assert.AreEqual(1, WeaponSweepUtility.SubdivisionCount(0.25f, 0.25f)); // exactly at the cap
    }

    [Test]
    public void SubdivisionCount_LongSweep_SplitsSoNoSubStepExceedsTheCap()
    {
        // 2.5 units in one physics step (BossHitbox's measured peak blade-tip speed) / 0.25 cap => 10.
        Assert.AreEqual(10, WeaponSweepUtility.SubdivisionCount(2.5f, 0.25f));
        // Rounds up, never down - 0.26 still needs 2 so no sub-step is longer than the cap.
        Assert.AreEqual(2, WeaponSweepUtility.SubdivisionCount(0.26f, 0.25f));
    }

    [Test]
    public void SubdivisionCount_NonPositiveCap_DisablesSubdivision()
    {
        Assert.AreEqual(1, WeaponSweepUtility.SubdivisionCount(5f, 0f));
        Assert.AreEqual(1, WeaponSweepUtility.SubdivisionCount(5f, -1f));
    }

    [Test]
    public void SubSegmentStart_EvenlySplitsPreviousToCurrent()
    {
        var a = new Vector3(0f, 0f, 0f);
        var b = new Vector3(0f, 0f, 4f);

        Assert.AreEqual(a, WeaponSweepUtility.SubSegmentStart(a, b, 0, 4));
        Assert.AreEqual(new Vector3(0f, 0f, 1f), WeaponSweepUtility.SubSegmentStart(a, b, 1, 4));
        Assert.AreEqual(new Vector3(0f, 0f, 3f), WeaponSweepUtility.SubSegmentStart(a, b, 3, 4));
    }

    [Test]
    public void SubSegmentStart_ClampsOutOfRangeIndices()
    {
        var a = new Vector3(1f, 2f, 3f);
        var b = new Vector3(4f, 5f, 6f);

        Assert.AreEqual(a, WeaponSweepUtility.SubSegmentStart(a, b, -2, 4));
        Assert.AreEqual(b, WeaponSweepUtility.SubSegmentStart(a, b, 4, 4));
        Assert.AreEqual(b, WeaponSweepUtility.SubSegmentStart(a, b, 99, 4));
        Assert.AreEqual(a, WeaponSweepUtility.SubSegmentStart(a, b, 0, 1)); // single segment starts at previous
    }

    [Test]
    public void SubSegmentLength_DividesTravelEvenly_AndFloorsAtZero()
    {
        Assert.AreEqual(1f, WeaponSweepUtility.SubSegmentLength(4f, 4), 1e-4f);
        Assert.AreEqual(4f, WeaponSweepUtility.SubSegmentLength(4f, 1), 1e-4f);
        Assert.AreEqual(0f, WeaponSweepUtility.SubSegmentLength(-3f, 4), 1e-4f);
    }

    [Test]
    public void ResolveMidpoint_UsesExplicitTransformWhenPresent_ElseGeometricMiddle()
    {
        var root = new Vector3(0f, 0f, 0f);
        var tip = new Vector3(0f, 0f, 2f);
        var explicitMid = new Vector3(0f, 1f, 0.5f);

        Assert.AreEqual(explicitMid, WeaponSweepUtility.ResolveMidpoint(true, explicitMid, root, tip));
        Assert.AreEqual(new Vector3(0f, 0f, 1f), WeaponSweepUtility.ResolveMidpoint(false, Vector3.zero, root, tip));
    }
}
