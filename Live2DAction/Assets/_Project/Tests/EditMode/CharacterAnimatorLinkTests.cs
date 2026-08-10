using NUnit.Framework;
using Live2DAction.Characters;

public class CharacterAnimatorLinkTests
{
    [Test]
    public void ComputeSpeedParameter_Idle_ReturnsZero()
    {
        float value = CharacterAnimatorLink.ComputeSpeedParameter(0f, 2f);
        Assert.AreEqual(0f, value);
    }

    [Test]
    public void ComputeSpeedParameter_WithinRange_PassesThroughUnscaled()
    {
        float value = CharacterAnimatorLink.ComputeSpeedParameter(1.2f, 2f);
        Assert.AreEqual(1.2f, value, 0.0001f);
    }

    [Test]
    public void ComputeSpeedParameter_ExceedsMax_ClampsToMax()
    {
        float value = CharacterAnimatorLink.ComputeSpeedParameter(5f, 2f);
        Assert.AreEqual(2f, value, 0.0001f);
    }

    [Test]
    public void ComputeSpeedParameter_NegativeSpeed_ClampsToZero()
    {
        float value = CharacterAnimatorLink.ComputeSpeedParameter(-1f, 2f);
        Assert.AreEqual(0f, value);
    }
}
