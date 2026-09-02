using NUnit.Framework;
using UnityEngine;
using Live2DAction.Combat.Boss;

// spec WUSHI_COMBAT_ENGINEERING_SPEC.md §6.2 (M3 項目 5, 5A). Pure coverage for the lunge curve:
// the window clamps, the curve is sampled inside it, and forwardDistance 0 reads as "off".
public class BossAttackMotionProfileTests
{
    private static BossAttackMotionProfile Profile(float dist, float start, float end, AnimationCurve curve = null)
    {
        return new BossAttackMotionProfile
        {
            forwardDistance = dist,
            moveStartNormalized = start,
            moveEndNormalized = end,
            movementCurve = curve ?? AnimationCurve.Linear(0f, 0f, 1f, 1f),
        };
    }

    [Test]
    public void TravelFraction01_ClampsOutsideTheWindow()
    {
        var p = Profile(4f, 0.2f, 0.8f);
        Assert.AreEqual(0f, p.TravelFraction01(0f), 1e-4f);
        Assert.AreEqual(0f, p.TravelFraction01(0.2f), 1e-4f);   // exactly at start
        Assert.AreEqual(1f, p.TravelFraction01(0.8f), 1e-4f);   // exactly at end
        Assert.AreEqual(1f, p.TravelFraction01(1f), 1e-4f);
    }

    [Test]
    public void TravelFraction01_LinearCurve_IsProportionalInsideTheWindow()
    {
        var p = Profile(4f, 0.2f, 0.6f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
        Assert.AreEqual(0.5f, p.TravelFraction01(0.4f), 1e-3f);   // window midpoint
        Assert.AreEqual(0.25f, p.TravelFraction01(0.3f), 1e-3f);
    }

    [Test]
    public void TravelFraction01_EaseInOutCurve_IsSymmetricAtTheMidpoint()
    {
        var p = Profile(4f, 0f, 1f, AnimationCurve.EaseInOut(0f, 0f, 1f, 1f));
        Assert.AreEqual(0.5f, p.TravelFraction01(0.5f), 1e-3f);
        Assert.Less(p.TravelFraction01(0.25f), 0.25f); // eases in - slower at the start
        Assert.Greater(p.TravelFraction01(0.75f), 0.75f);
    }

    [Test]
    public void TravelFraction01_NoCurve_FallsBackToLinear()
    {
        var p = Profile(4f, 0f, 1f);
        p.movementCurve = null;
        Assert.AreEqual(0.5f, p.TravelFraction01(0.5f), 1e-3f);
    }

    [Test]
    public void TravelFraction01_ResultNeverLeavesZeroToOne_EvenWithAnOvershootingCurve()
    {
        var overshoot = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.5f, 1.4f), new Keyframe(1f, 1f));
        var p = Profile(4f, 0f, 1f, overshoot);
        Assert.LessOrEqual(p.TravelFraction01(0.5f), 1f);
        Assert.GreaterOrEqual(p.TravelFraction01(0.5f), 0f);
    }

    [Test]
    public void HasDisplacement_OnlyWhenForwardDistanceIsMeaningful()
    {
        Assert.IsFalse(Profile(0f, 0f, 1f).HasDisplacement);
        Assert.IsFalse(Profile(0.00001f, 0f, 1f).HasDisplacement);
        Assert.IsTrue(Profile(2f, 0f, 1f).HasDisplacement);
    }
}
