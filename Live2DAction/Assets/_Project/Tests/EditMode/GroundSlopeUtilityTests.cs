using NUnit.Framework;
using UnityEngine;
using Live2DAction.Characters;

public class GroundSlopeUtilityTests
{
    [Test]
    public void IsTooSteepToStandOn_FlatGround_ReturnsFalse()
    {
        Assert.IsFalse(GroundSlopeUtility.IsTooSteepToStandOn(Vector3.up, 45f));
    }

    [Test]
    public void IsTooSteepToStandOn_JustUnderLimit_ReturnsFalse()
    {
        // 44 degrees off vertical, under a 45-degree limit.
        Vector3 normal = Quaternion.Euler(44f, 0f, 0f) * Vector3.up;
        Assert.IsFalse(GroundSlopeUtility.IsTooSteepToStandOn(normal, 45f));
    }

    [Test]
    public void IsTooSteepToStandOn_JustOverLimit_ReturnsTrue()
    {
        Vector3 normal = Quaternion.Euler(46f, 0f, 0f) * Vector3.up;
        Assert.IsTrue(GroundSlopeUtility.IsTooSteepToStandOn(normal, 45f));
    }

    [Test]
    public void IsTooSteepToStandOn_TopOfDome_ReturnsTrue()
    {
        // Near the equator of a round capsule/head - the classic "landed on top of an enemy"
        // case this fix targets, normal pointing mostly sideways.
        Vector3 normal = new Vector3(0.9f, 0.1f, 0f).normalized;
        Assert.IsTrue(GroundSlopeUtility.IsTooSteepToStandOn(normal, 45f));
    }

    [Test]
    public void ComputeSlideDirection_FlatGround_ReturnsZero()
    {
        Assert.AreEqual(Vector3.zero, GroundSlopeUtility.ComputeSlideDirection(Vector3.up));
    }

    [Test]
    public void ComputeSlideDirection_TiltedSurface_PointsAwayFromApex()
    {
        // A normal tilted towards +X means this point sits on the +X side of a dome (for a
        // sphere, surface normal = outward radial direction) - continuing "downhill" from
        // there means continuing further away from the apex, i.e. further towards +X, not
        // back towards it. This is exactly the desired behavior for sliding off the top of
        // another character's capsule: push outward, away from center.
        Vector3 normal = new Vector3(0.6f, 0.8f, 0f).normalized;
        Vector3 slide = GroundSlopeUtility.ComputeSlideDirection(normal);

        Assert.Greater(slide.x, 0f, "Should slide further away from the apex, in the direction the slope tilts towards");
        Assert.AreEqual(0f, slide.y, 0.0001f, "Slide direction is horizontal-only - vertical motion stays gravity's job");
    }

    [Test]
    public void ComputeSlideDirection_IsNormalized()
    {
        Vector3 normal = new Vector3(0.3f, 0.7f, 0.2f).normalized;
        Vector3 slide = GroundSlopeUtility.ComputeSlideDirection(normal);
        Assert.AreEqual(1f, slide.magnitude, 0.0001f);
    }

    [Test]
    public void ComputeFallbackAwayDirection_PointsFromOtherTowardsSelf()
    {
        Vector3 self = new Vector3(1f, 2f, 0f);
        Vector3 other = new Vector3(0f, 0.5f, 0f);
        Vector3 direction = GroundSlopeUtility.ComputeFallbackAwayDirection(self, other);

        Assert.Greater(direction.x, 0f);
        Assert.AreEqual(0f, direction.y, 0.0001f, "Fallback direction is horizontal-only");
        Assert.AreEqual(1f, direction.magnitude, 0.0001f);
    }

    [Test]
    public void ComputeFallbackAwayDirection_CoincidentPositions_DefaultsToPositiveX()
    {
        Vector3 same = new Vector3(3f, 1f, -2f);
        Vector3 direction = GroundSlopeUtility.ComputeFallbackAwayDirection(same, same);
        Assert.AreEqual(Vector3.right, direction);
    }

    [Test]
    public void ComputeSlideDirection_OppositeTiltsSlideOppositeWays()
    {
        Vector3 normalA = new Vector3(0.6f, 0.8f, 0f).normalized;
        Vector3 normalB = new Vector3(-0.6f, 0.8f, 0f).normalized;

        Vector3 slideA = GroundSlopeUtility.ComputeSlideDirection(normalA);
        Vector3 slideB = GroundSlopeUtility.ComputeSlideDirection(normalB);

        Assert.Greater(slideA.x, 0f);
        Assert.Less(slideB.x, 0f);
    }
}
