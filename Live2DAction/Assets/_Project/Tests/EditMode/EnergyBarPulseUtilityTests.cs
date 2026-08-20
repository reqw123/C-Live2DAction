using NUnit.Framework;
using UnityEngine;
using Live2DAction.UI;

public class EnergyBarPulseUtilityTests
{
    [Test]
    public void ComputePulseBrightness_NotFull_ReturnsFlatOne()
    {
        Assert.AreEqual(1f, EnergyBarPulseUtility.ComputePulseBrightness(false, 5f, 6f, 1f, 1.8f));
    }

    [Test]
    public void ComputePulseBrightness_NotFull_IgnoresTime()
    {
        // Regardless of what time it is, a not-full bar should never pulse.
        Assert.AreEqual(1f, EnergyBarPulseUtility.ComputePulseBrightness(false, 100f, 6f, 1f, 1.8f));
    }

    [Test]
    public void ComputePulseBrightness_Full_StaysWithinMinMaxRange()
    {
        for (float t = 0f; t < 10f; t += 0.1f)
        {
            float brightness = EnergyBarPulseUtility.ComputePulseBrightness(true, t, 6f, 1f, 1.8f);
            Assert.GreaterOrEqual(brightness, 1f);
            Assert.LessOrEqual(brightness, 1.8f);
        }
    }

    [Test]
    public void ComputePulseBrightness_Full_AtSineTrough_ReturnsMinBrightness()
    {
        // sin(x) = -1 at x = -pi/2 (or 3*pi/2, etc.) - pulseSpeed=1 so time IS the sine input.
        float time = -Mathf.PI / 2f;
        Assert.AreEqual(1f, EnergyBarPulseUtility.ComputePulseBrightness(true, time, 1f, 1f, 1.8f), 0.0001f);
    }

    [Test]
    public void ComputePulseBrightness_Full_AtSinePeak_ReturnsMaxBrightness()
    {
        float time = Mathf.PI / 2f;
        Assert.AreEqual(1.8f, EnergyBarPulseUtility.ComputePulseBrightness(true, time, 1f, 1f, 1.8f), 0.0001f);
    }
}
