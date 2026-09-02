using NUnit.Framework;
using Live2DAction.Combat.Boss;

// spec WUSHI_COMBAT_ENGINEERING_SPEC.md §10.4 (M5 項目 9) - real first-contact / effective-window math.
public class BossAttackTimingUtilityTests
{
    [Test]
    public void RealClipSeconds_DividesByStateSpeed()
    {
        Assert.AreEqual(3.30f, BossAttackTimingUtility.RealClipSeconds(3.30f, 1f), 1e-4f);
        Assert.AreEqual(2.20f, BossAttackTimingUtility.RealClipSeconds(3.30f, 1.5f), 1e-4f);
        Assert.AreEqual(2.357f, BossAttackTimingUtility.RealClipSeconds(3.30f, 1.4f), 1e-3f);
    }

    [Test]
    public void RealClipSeconds_NonPositiveSpeedTreatedAsOne_AndZeroClipStaysZero()
    {
        Assert.AreEqual(3.30f, BossAttackTimingUtility.RealClipSeconds(3.30f, 0f), 1e-4f);
        Assert.AreEqual(3.30f, BossAttackTimingUtility.RealClipSeconds(3.30f, -2f), 1e-4f);
        Assert.AreEqual(0f, BossAttackTimingUtility.RealClipSeconds(0f, 1f), 1e-4f);
    }

    [Test]
    public void NormalizedToSeconds_ScalesAndClamps()
    {
        // SwordJudgment window-1 start 0.09 of a 3.3s clip at speed 1.
        Assert.AreEqual(0.297f, BossAttackTimingUtility.NormalizedToSeconds(0.09f, 3.30f), 1e-4f);
        Assert.AreEqual(0f, BossAttackTimingUtility.NormalizedToSeconds(-0.5f, 3.30f), 1e-4f);
        Assert.AreEqual(3.30f, BossAttackTimingUtility.NormalizedToSeconds(1.5f, 3.30f), 1e-4f);
    }

    [Test]
    public void WindowMilliseconds_SpanTimesRealLength()
    {
        // SwordJudgment window 1: 0.09-0.23 of 3.30s => 0.14 * 3300 = 462ms.
        Assert.AreEqual(462f, BossAttackTimingUtility.WindowMilliseconds(0.09f, 0.23f, 3.30f), 1e-2f);
        // Speed 1.4 on the same normalized span shrinks it: 3.30 / 1.4 = 2.357s => 0.14 * 2357 = 330ms.
        Assert.AreEqual(330f, BossAttackTimingUtility.WindowMilliseconds(0.09f, 0.23f,
            BossAttackTimingUtility.RealClipSeconds(3.30f, 1.4f)), 1f);
    }

    [Test]
    public void WindowMilliseconds_ReversedOrEmptyWindowIsZero()
    {
        Assert.AreEqual(0f, BossAttackTimingUtility.WindowMilliseconds(0.6f, 0.4f, 3.30f), 1e-4f);
        Assert.AreEqual(0f, BossAttackTimingUtility.WindowMilliseconds(0.5f, 0.5f, 3.30f), 1e-4f);
    }

    [Test]
    public void ParryDifficultyRatio_ComparesToThe200msParryWindow()
    {
        Assert.AreEqual(0f, BossAttackTimingUtility.ParryDifficultyRatio(0f), 1e-4f);
        Assert.AreEqual(1f, BossAttackTimingUtility.ParryDifficultyRatio(200f), 1e-4f);
        Assert.AreEqual(0.75f, BossAttackTimingUtility.ParryDifficultyRatio(150f), 1e-4f);
        Assert.Greater(BossAttackTimingUtility.ParryDifficultyRatio(462f), 2f);
    }
}
