using NUnit.Framework;
using Live2DAction.UI;

public class HealthBarUtilityTests
{
    [Test]
    public void ComputeFillAmount_FullHealth_ReturnsOne()
    {
        Assert.AreEqual(1f, HealthBarUtility.ComputeFillAmount(100f, 100f));
    }

    [Test]
    public void ComputeFillAmount_HalfHealth_ReturnsHalf()
    {
        Assert.AreEqual(0.5f, HealthBarUtility.ComputeFillAmount(50f, 100f));
    }

    [Test]
    public void ComputeFillAmount_AfterOneHit_ReturnsNinetyPercent()
    {
        // 100 HP, 10 damage per hit (2026-08-12 balance request).
        Assert.AreEqual(0.9f, HealthBarUtility.ComputeFillAmount(90f, 100f), 0.0001f);
    }

    [Test]
    public void ComputeFillAmount_ZeroHealth_ReturnsZero()
    {
        Assert.AreEqual(0f, HealthBarUtility.ComputeFillAmount(0f, 100f));
    }

    [Test]
    public void ComputeFillAmount_NegativeHealth_ClampsToZero()
    {
        Assert.AreEqual(0f, HealthBarUtility.ComputeFillAmount(-10f, 100f));
    }

    [Test]
    public void ComputeFillAmount_ZeroMaxHealth_ReturnsZeroInsteadOfDividingByZero()
    {
        Assert.AreEqual(0f, HealthBarUtility.ComputeFillAmount(0f, 0f));
    }
}
