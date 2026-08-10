using NUnit.Framework;
using Live2DAction.Characters;

public class CharacterAnimatorLinkTests
{
    [Test]
    public void ComputeSpeedParameter_Idle_ReturnsZero()
    {
        float value = CharacterAnimatorLink.ComputeSpeedParameter(0f, 5f, 2f);
        Assert.AreEqual(0f, value);
    }

    [Test]
    public void ComputeSpeedParameter_FullSpeed_ReturnsScale()
    {
        float value = CharacterAnimatorLink.ComputeSpeedParameter(5f, 5f, 2f);
        Assert.AreEqual(2f, value, 0.0001f);
    }

    [Test]
    public void ComputeSpeedParameter_HalfSpeed_ReturnsHalfScale()
    {
        float value = CharacterAnimatorLink.ComputeSpeedParameter(2.5f, 5f, 2f);
        Assert.AreEqual(1f, value, 0.0001f);
    }

    [Test]
    public void ComputeSpeedParameter_OverspeedClampsToScale()
    {
        float value = CharacterAnimatorLink.ComputeSpeedParameter(50f, 5f, 2f);
        Assert.AreEqual(2f, value, 0.0001f);
    }

    [Test]
    public void ComputeSpeedParameter_ZeroMoveSpeed_ReturnsZeroWithoutDividingByZero()
    {
        float value = CharacterAnimatorLink.ComputeSpeedParameter(3f, 0f, 2f);
        Assert.AreEqual(0f, value);
    }
}
