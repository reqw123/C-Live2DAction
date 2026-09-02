using NUnit.Framework;
using Live2DAction.Combat.Boss;

// spec WUSHI_COMBAT_ENGINEERING_SPEC.md §9.2 (M4 項目 8) - the shared periodic-special cooldown gate.
public class SpecialScheduleUtilityTests
{
    [Test]
    public void SharedCooldownReady_ZeroOrNegativeCooldown_IsAlwaysReady()
    {
        Assert.IsTrue(SpecialScheduleUtility.SharedCooldownReady(lastFireTime: 100f, now: 100.01f, cooldownSeconds: 0f));
        Assert.IsTrue(SpecialScheduleUtility.SharedCooldownReady(100f, 100f, -1f));
    }

    [Test]
    public void SharedCooldownReady_BlocksUntilTheCooldownElapses_ThenAllows()
    {
        Assert.IsFalse(SpecialScheduleUtility.SharedCooldownReady(lastFireTime: 10f, now: 13f, cooldownSeconds: 7f));
        Assert.IsFalse(SpecialScheduleUtility.SharedCooldownReady(10f, 16.99f, 7f));
        Assert.IsTrue(SpecialScheduleUtility.SharedCooldownReady(10f, 17f, 7f));  // exactly at the boundary
        Assert.IsTrue(SpecialScheduleUtility.SharedCooldownReady(10f, 25f, 7f));
    }

    [Test]
    public void SharedCooldownReady_NeverFiredSentinel_IsReady()
    {
        Assert.IsTrue(SpecialScheduleUtility.SharedCooldownReady(lastFireTime: -999f, now: 0.5f, cooldownSeconds: 8f));
    }
}
