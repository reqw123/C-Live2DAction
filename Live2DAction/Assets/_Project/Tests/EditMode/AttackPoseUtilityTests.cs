using NUnit.Framework;
using Live2DAction.Combat;

public class AttackPoseUtilityTests
{
    private const float WindUp = 20f;
    private const float Swing = 60f;

    [Test]
    public void Idle_AlwaysReturnsZero_RegardlessOfProgress()
    {
        Assert.AreEqual(0f, AttackPoseUtility.ComputeSwingAngle(AttackPhase.Idle, 0f, WindUp, Swing));
        Assert.AreEqual(0f, AttackPoseUtility.ComputeSwingAngle(AttackPhase.Idle, 1f, WindUp, Swing));
    }

    [Test]
    public void Startup_ErasesFromZeroToNegativeWindUp()
    {
        Assert.AreEqual(0f, AttackPoseUtility.ComputeSwingAngle(AttackPhase.Startup, 0f, WindUp, Swing), 0.001f);
        Assert.AreEqual(-WindUp / 2f, AttackPoseUtility.ComputeSwingAngle(AttackPhase.Startup, 0.5f, WindUp, Swing), 0.001f);
        Assert.AreEqual(-WindUp, AttackPoseUtility.ComputeSwingAngle(AttackPhase.Startup, 1f, WindUp, Swing), 0.001f);
    }

    [Test]
    public void Active_SwingsFromNegativeWindUpToSwingAngle()
    {
        Assert.AreEqual(-WindUp, AttackPoseUtility.ComputeSwingAngle(AttackPhase.Active, 0f, WindUp, Swing), 0.001f);
        Assert.AreEqual(Swing, AttackPoseUtility.ComputeSwingAngle(AttackPhase.Active, 1f, WindUp, Swing), 0.001f);
    }

    [Test]
    public void Recovery_EasesFromSwingAngleBackToZero()
    {
        Assert.AreEqual(Swing, AttackPoseUtility.ComputeSwingAngle(AttackPhase.Recovery, 0f, WindUp, Swing), 0.001f);
        Assert.AreEqual(0f, AttackPoseUtility.ComputeSwingAngle(AttackPhase.Recovery, 1f, WindUp, Swing), 0.001f);
    }

    [Test]
    public void Progress_OutOfRange_IsClamped()
    {
        Assert.AreEqual(0f, AttackPoseUtility.ComputeSwingAngle(AttackPhase.Startup, -1f, WindUp, Swing), 0.001f);
        Assert.AreEqual(-WindUp, AttackPoseUtility.ComputeSwingAngle(AttackPhase.Startup, 2f, WindUp, Swing), 0.001f);
    }
}
