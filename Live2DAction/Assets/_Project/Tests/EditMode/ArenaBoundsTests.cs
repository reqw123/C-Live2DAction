using NUnit.Framework;
using UnityEngine;
using Live2DAction.AI;

// 2026-08-29, user request: Enemy (普通怪物) / 屁孩王 (精怪) give up the instant the player crosses
// out of 本地 through the doorway, regardless of alert/chase distance. ArenaBounds.IsOutside is the
// pure box test they share.
public class ArenaBoundsTests
{
    static readonly Vector2 Center = Vector2.zero;
    const float Half = 15.5f;

    [Test]
    public void IsOutside_FalseAtCenter()
    {
        Assert.IsFalse(ArenaBounds.IsOutside(Vector2.zero, Center, Half));
    }

    [Test]
    public void IsOutside_FalseJustInsideEdge()
    {
        Assert.IsFalse(ArenaBounds.IsOutside(new Vector2(15.4f, -15.4f), Center, Half));
    }

    [Test]
    public void IsOutside_TrueBeyondXEdge()
    {
        Assert.IsTrue(ArenaBounds.IsOutside(new Vector2(16f, 0f), Center, Half));
    }

    [Test]
    public void IsOutside_TrueBeyondZEdge_ThroughDoorway()
    {
        // player drives south out the vehicle hole
        Assert.IsTrue(ArenaBounds.IsOutside(new Vector2(0f, -20f), Center, Half));
    }

    [Test]
    public void IsOutside_IgnoresHeight_Vector3Overload()
    {
        // player high overhead but still horizontally inside -> not outside
        Assert.IsFalse(ArenaBounds.IsOutside(new Vector3(2f, 50f, 2f), Center, Half));
        Assert.IsTrue(ArenaBounds.IsOutside(new Vector3(40f, 50f, 0f), Center, Half));
    }

    [Test]
    public void IsOutside_RespectsNonZeroCenter()
    {
        var center = new Vector2(0f, -95f); // 學校 ground
        Assert.IsFalse(ArenaBounds.IsOutside(new Vector2(5f, -90f), center, Half));
        Assert.IsTrue(ArenaBounds.IsOutside(new Vector2(5f, -70f), center, Half));
    }

    // 2026-08-29 follow-up: hold at the gate and watch instead of teleporting home on contact.

    [Test]
    public void ClampInside_PullsPositionBackToWall_KeepsHeight()
    {
        var clamped = ArenaBounds.ClampInside(new Vector3(3f, 2.5f, -22f), Center, Half);
        Assert.AreEqual(3f, clamped.x, 0.0001f);
        Assert.AreEqual(2.5f, clamped.y, 0.0001f);
        Assert.AreEqual(-15.5f, clamped.z, 0.0001f);
    }

    [Test]
    public void ClampInside_LeavesInteriorPositionUntouched()
    {
        var p = new Vector3(4f, 1f, -6f);
        Assert.AreEqual(p, ArenaBounds.ClampInside(p, Center, Half));
    }
}
