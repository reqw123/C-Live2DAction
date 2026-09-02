using NUnit.Framework;
using UnityEngine;
using Live2DAction.AI;

// 2026-08-31 - the NavMesh query side of NavPathFollower needs a baked mesh (PlayMode), but the
// corner-advance rule that decides which waypoint the character steers toward each frame is pure
// and covered here.
public class NavPathFollowerTests
{
    private static Vector3[] Corners(params (float x, float z)[] pts)
    {
        var c = new Vector3[pts.Length];
        for (int i = 0; i < pts.Length; i++)
        {
            c[i] = new Vector3(pts[i].x, 0f, pts[i].z);
        }
        return c;
    }

    [Test]
    public void AdvanceCorner_AtStart_SteersTowardFirstRealCorner()
    {
        var corners = Corners((0, 0), (5, 0), (5, 5));
        int result = NavPathFollower.AdvanceCorner(corners, Vector3.zero, 1, 0.75f);
        Assert.AreEqual(1, result);
    }

    [Test]
    public void AdvanceCorner_WithinReachOfCurrentCorner_AdvancesToNext()
    {
        var corners = Corners((0, 0), (5, 0), (5, 5));
        // standing 0.5m short of corner[1], reach distance 0.75 -> skip to corner[2]
        int result = NavPathFollower.AdvanceCorner(corners, new Vector3(4.5f, 0f, 0f), 1, 0.75f);
        Assert.AreEqual(2, result);
    }

    [Test]
    public void AdvanceCorner_NeverPastTheLastCorner()
    {
        var corners = Corners((0, 0), (5, 0), (5, 5));
        // sitting right on the final corner
        int result = NavPathFollower.AdvanceCorner(corners, new Vector3(5f, 0f, 5f), 2, 0.75f);
        Assert.AreEqual(2, result);
    }

    [Test]
    public void AdvanceCorner_SkipsMultipleAlreadyReachedCorners()
    {
        var corners = Corners((0, 0), (1, 0), (2, 0), (2, 8));
        // standing between corner[1] and corner[2], within reach of BOTH (they're 1m apart, reach
        // 0.75, self is 0.5 from each) - advances past both to corner[3].
        int result = NavPathFollower.AdvanceCorner(corners, new Vector3(1.5f, 0f, 0f), 1, 0.75f);
        Assert.AreEqual(3, result);
    }

    [Test]
    public void AdvanceCorner_IgnoresVerticalDistance()
    {
        var corners = Corners((0, 0), (5, 0), (5, 5));
        // horizontally on top of corner[1] but 10m below it - still counts as reached
        int result = NavPathFollower.AdvanceCorner(corners, new Vector3(5f, -10f, 0f), 1, 0.75f);
        Assert.AreEqual(2, result);
    }

    [Test]
    public void AdvanceCorner_ClampsAnOutOfRangeCurrentIndex()
    {
        var corners = Corners((0, 0), (5, 0));
        Assert.AreEqual(1, NavPathFollower.AdvanceCorner(corners, new Vector3(2f, 0f, 0f), 99, 0.75f));
        Assert.AreEqual(0, NavPathFollower.AdvanceCorner(corners, new Vector3(-9f, 0f, 0f), -5, 0.75f));
    }

    [Test]
    public void AdvanceCorner_EmptyOrNull_ReturnsZero()
    {
        Assert.AreEqual(0, NavPathFollower.AdvanceCorner(new Vector3[0], Vector3.zero, 3, 0.75f));
        Assert.AreEqual(0, NavPathFollower.AdvanceCorner(null, Vector3.zero, 3, 0.75f));
    }
}
