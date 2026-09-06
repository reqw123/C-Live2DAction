using NUnit.Framework;
using UnityEngine;
using Live2DAction.AI.Boss.Yuanpei;

// 續183e - pure lane geometry + damage maths for the boss's opening 下馬威 barrage.
public class YuanpeiOpeningBarrageLanesTests
{
    static readonly Vector3 Boss = new Vector3(0f, 5f, 0f);
    static readonly Vector3 Player = new Vector3(0f, 1f, 10f);   // straight ahead of the boss, +Z

    [Test]
    public void Mid_IsTheLockedPlayerCentre()
    {
        var p = YuanpeiOpeningBarrageLanes.AimPoint(Player, Boss, 1.15f, YuanpeiOpeningBarrageLanes.Lane.Mid);
        Assert.That(Vector3.Distance(p, Player), Is.LessThan(1e-4f));
    }

    [Test]
    public void LeftAndRight_AreOffsetPerpendicularToTheLineOfFire()
    {
        const float off = 1.15f;
        var left = YuanpeiOpeningBarrageLanes.AimPoint(Player, Boss, off, YuanpeiOpeningBarrageLanes.Lane.Left);
        var right = YuanpeiOpeningBarrageLanes.AimPoint(Player, Boss, off, YuanpeiOpeningBarrageLanes.Lane.Right);

        // player is along +Z of the boss -> lanes spread along X
        Assert.That(Mathf.Abs(left.x - (-off)), Is.LessThan(1e-3f));
        Assert.That(Mathf.Abs(right.x - off), Is.LessThan(1e-3f));
        Assert.That(left.z, Is.EqualTo(Player.z).Within(1e-3f));
        Assert.That(right.z, Is.EqualTo(Player.z).Within(1e-3f));

        // each lane is `off` from mid, and the two are 2*off apart, 90° to the boss->player line
        Assert.That(Vector3.Distance(left, Player), Is.EqualTo(off).Within(1e-3f));
        Assert.That(Vector3.Distance(right, Player), Is.EqualTo(off).Within(1e-3f));
        Assert.That(Vector3.Distance(left, right), Is.EqualTo(2f * off).Within(1e-3f));
        Vector3 laneAxis = (right - left).normalized;
        Vector3 lineOfFire = (Player - Boss); lineOfFire.y = 0f; lineOfFire.Normalize();
        Assert.That(Vector3.Dot(laneAxis, lineOfFire), Is.EqualTo(0f).Within(1e-3f));
    }

    [Test]
    public void LaneSpread_RotatesWithTheBossPlayerLine()
    {
        // player off to the boss's +X instead: lanes must now spread along Z
        Vector3 playerX = new Vector3(10f, 1f, 0f);
        var left = YuanpeiOpeningBarrageLanes.AimPoint(playerX, Boss, 1f, YuanpeiOpeningBarrageLanes.Lane.Left);
        var right = YuanpeiOpeningBarrageLanes.AimPoint(playerX, Boss, 1f, YuanpeiOpeningBarrageLanes.Lane.Right);
        Assert.That(Mathf.Abs(left.x - playerX.x), Is.LessThan(1e-3f));   // no X shift now
        Assert.That(Mathf.Abs(right.x - playerX.x), Is.LessThan(1e-3f));
        Assert.That(Vector3.Distance(left, right), Is.EqualTo(2f).Within(1e-3f));
    }

    [Test]
    public void AllStreamsConnecting_MassivelyOverkillsAFullHealthPlayer()
    {
        // 續183h authored numbers (YuanpeiOpeningBarrageSetup): 6 spears × 40 + ~6 laser ticks × 18
        // + 12 orbs × 20.  The barrage must be a "下馬威" - not just lethal, overwhelmingly so.
        float total = YuanpeiOpeningBarrageLanes.TotalDamageIfAllHit(40f, 6, 18f, 6, 20f, 12);
        Assert.That(total, Is.EqualTo(6 * 40f + 6 * 18f + 12 * 20f).Within(0.01f));   // 588
        Assert.That(total, Is.GreaterThan(400f), "下馬威 - 全命中應遠遠超過滿血玩家 (~100 HP)");
        // and each of the three straight lanes ALONE should be lethal on a full-health player
        Assert.That(6 * 40f, Is.GreaterThan(150f), "長矛左線單獨就致命");
        Assert.That(12 * 20f, Is.GreaterThan(150f), "六連彈右線單獨就致命");
        Assert.That(6 * 18f, Is.GreaterThan(80f), "雷射中線單獨接近致命");
    }

    [Test]
    public void ThreeLanes_AreDistinctAndSeparatedButStillOnTheBody()
    {
        // 續183h - 3 SEPARATE straight lanes: player must be able to tell them apart (offset > 0),
        // but a stationary player's ~0.35m capsule + the fat bullets still cover all three (offset small).
        const float offset = 0.6f;
        var left = YuanpeiOpeningBarrageLanes.AimPoint(Player, Boss, offset, YuanpeiOpeningBarrageLanes.Lane.Left);
        var mid = YuanpeiOpeningBarrageLanes.AimPoint(Player, Boss, offset, YuanpeiOpeningBarrageLanes.Lane.Mid);
        var right = YuanpeiOpeningBarrageLanes.AimPoint(Player, Boss, offset, YuanpeiOpeningBarrageLanes.Lane.Right);
        Assert.That(Vector3.Distance(left, right), Is.EqualTo(1.2f).Within(1e-3f), "左右線要分得開");
        Assert.That(Vector3.Distance(mid, left), Is.LessThan(1.0f), "但仍在玩家身上 (胖子彈能涵蓋)");
    }

    [Test]
    public void TotalDamage_ClampsNegativeInputsToZero()
    {
        Assert.That(YuanpeiOpeningBarrageLanes.TotalDamageIfAllHit(-5f, -5, -5f, -5, -5f, -5), Is.EqualTo(0f));
    }
}
