using NUnit.Framework;
using Live2DAction.Characters;

public class CharacterAttackAnimationLinkTests
{
    [Test]
    public void TriggerNameForComboIndex_FirstHit_ReturnsAttack1()
    {
        Assert.AreEqual("Attack1", CharacterAttackAnimationLink.TriggerNameForComboIndex(0));
    }

    [Test]
    public void TriggerNameForComboIndex_SecondHit_ReturnsAttack2()
    {
        Assert.AreEqual("Attack2", CharacterAttackAnimationLink.TriggerNameForComboIndex(1));
    }

    [Test]
    public void TriggerNameForComboIndex_ThirdHit_ReturnsAttack3()
    {
        Assert.AreEqual("Attack3", CharacterAttackAnimationLink.TriggerNameForComboIndex(2));
    }

    [Test]
    public void TriggerNameForComboIndex_BeyondThirdHit_FallsBackToAttack3()
    {
        // Defensive: this project's combo is always 3 attacks (see
        // GreyboxSceneBuilder.CreateOrLoadComboAttacks), but this keeps any future longer
        // combo from indexing into a nonexistent trigger name instead of just reusing the
        // last one.
        Assert.AreEqual("Attack3", CharacterAttackAnimationLink.TriggerNameForComboIndex(5));
    }
}
