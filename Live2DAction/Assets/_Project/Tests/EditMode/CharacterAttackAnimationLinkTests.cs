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
    public void TriggerNameForComboIndex_FourthHit_ReturnsAttack4()
    {
        // 2026-08-17: a 4th combo step was added (the katana combo is LightAttack1..4). Index 3
        // maps to its own Attack4 trigger, not a re-fire of Attack3.
        Assert.AreEqual("Attack4", CharacterAttackAnimationLink.TriggerNameForComboIndex(3));
    }

    [Test]
    public void TriggerNameForComboIndex_BeyondFourthHit_FallsBackToAttack4()
    {
        // Defensive: the combo is 4 attacks now. Anything past the last step reuses the last
        // trigger rather than indexing into a nonexistent name.
        Assert.AreEqual("Attack4", CharacterAttackAnimationLink.TriggerNameForComboIndex(5));
    }
}
