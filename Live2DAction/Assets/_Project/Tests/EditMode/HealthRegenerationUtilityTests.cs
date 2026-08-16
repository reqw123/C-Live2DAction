using NUnit.Framework;
using Live2DAction.Core;

public class HealthRegenerationUtilityTests
{
    [Test]
    public void AdvanceIdleTimer_HealthDropped_ResetsToZero()
    {
        Assert.AreEqual(0f, HealthRegenerationUtility.AdvanceIdleTimer(90f, 80f, 7f, 0.5f));
    }

    [Test]
    public void AdvanceIdleTimer_HealthUnchanged_AccumulatesDeltaTime()
    {
        Assert.AreEqual(7.5f, HealthRegenerationUtility.AdvanceIdleTimer(80f, 80f, 7f, 0.5f), 0.0001f);
    }

    [Test]
    public void AdvanceIdleTimer_HealthIncreased_AccumulatesDeltaTime()
    {
        // Healing (e.g. from regen itself ticking) is not "taking damage" - the timer should
        // keep accumulating, not reset.
        Assert.AreEqual(7.5f, HealthRegenerationUtility.AdvanceIdleTimer(80f, 82f, 7f, 0.5f), 0.0001f);
    }

    [Test]
    public void ShouldRegenerate_BelowIdleThreshold_ReturnsFalse()
    {
        Assert.IsFalse(HealthRegenerationUtility.ShouldRegenerate(9.9f, 10f, 50f, 100f));
    }

    [Test]
    public void ShouldRegenerate_AtOrAboveIdleThreshold_ReturnsTrue()
    {
        Assert.IsTrue(HealthRegenerationUtility.ShouldRegenerate(10f, 10f, 50f, 100f));
    }

    [Test]
    public void ShouldRegenerate_AlreadyFullHealth_ReturnsFalse()
    {
        Assert.IsFalse(HealthRegenerationUtility.ShouldRegenerate(15f, 10f, 100f, 100f));
    }
}
