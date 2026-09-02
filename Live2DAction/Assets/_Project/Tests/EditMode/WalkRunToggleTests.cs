using NUnit.Framework;
using Live2DAction.Characters;

// 2026-08-30, Genshin-style walk/run toggle. Covers the pure state rule
// CharacterMovement.NextWalkMode (the speed swap + animation fall out of the existing blend
// tree once the mode flips, so this is the only bit worth unit-testing headlessly).
public class WalkRunToggleTests
{
    [Test]
    public void DefaultIsRun()
    {
        Assert.IsFalse(CharacterMovement.NextWalkMode(current: false, togglePressed: false, isFlying: false));
    }

    [Test]
    public void Press_FlipsRunToWalk()
    {
        Assert.IsTrue(CharacterMovement.NextWalkMode(current: false, togglePressed: true, isFlying: false));
    }

    [Test]
    public void Press_FlipsWalkBackToRun()
    {
        Assert.IsFalse(CharacterMovement.NextWalkMode(current: true, togglePressed: true, isFlying: false));
    }

    [Test]
    public void NoPress_ModePersists()
    {
        Assert.IsTrue(CharacterMovement.NextWalkMode(current: true, togglePressed: false, isFlying: false));
        Assert.IsFalse(CharacterMovement.NextWalkMode(current: false, togglePressed: false, isFlying: false));
    }

    [Test]
    public void Flying_ForcesRunMode_EvenIfWalkWasOn()
    {
        Assert.IsFalse(CharacterMovement.NextWalkMode(current: true, togglePressed: false, isFlying: true));
    }

    [Test]
    public void Flying_IgnoresAPress()
    {
        Assert.IsFalse(CharacterMovement.NextWalkMode(current: false, togglePressed: true, isFlying: true));
    }
}
