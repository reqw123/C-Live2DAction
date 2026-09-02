using NUnit.Framework;
using UnityEngine;
using Live2DAction.Combat;

// Pure-logic coverage for the katana guard (2026-08-31, "把滑鼠右鍵改成武士刀防禦"): the frontal
// block cone, health-damage mitigation, "poise still builds in full" rule, and the eased pose
// blend. Same pure-helper-first style as AttackPoseUtilityTests / TenLeggedBugTests.
public class PlayerGuardUtilityTests
{
    private static readonly Vector3 FacingForward = Vector3.forward;

    // DamageInfo.Direction = target - attacker (away from attacker). Attacker dead ahead of a
    // forward-facing defender => that vector points backwards.
    private static Vector3 DirFromAttackerAt(float degreesFromFront)
    {
        Vector3 attackerOffset = Quaternion.Euler(0f, degreesFromFront, 0f) * Vector3.forward; // attacker relative to defender
        return -attackerOffset; // target - attacker
    }

    [Test]
    public void IsFrontalBlock_AttackerDeadAhead_Blocks()
    {
        Assert.IsTrue(PlayerGuardUtility.IsFrontalBlock(FacingForward, DirFromAttackerAt(0f), 150f));
    }

    [Test]
    public void IsFrontalBlock_AttackerBehind_DoesNotBlock()
    {
        Assert.IsFalse(PlayerGuardUtility.IsFrontalBlock(FacingForward, DirFromAttackerAt(180f), 150f));
    }

    [Test]
    public void IsFrontalBlock_JustInsideAndJustOutsideTheArcEdge()
    {
        // 150-degree full cone => +/-75 from front.
        Assert.IsTrue(PlayerGuardUtility.IsFrontalBlock(FacingForward, DirFromAttackerAt(70f), 150f));
        Assert.IsFalse(PlayerGuardUtility.IsFrontalBlock(FacingForward, DirFromAttackerAt(85f), 150f));
    }

    [Test]
    public void IsFrontalBlock_DegenerateVectors_ReturnFalse()
    {
        Assert.IsFalse(PlayerGuardUtility.IsFrontalBlock(Vector3.zero, Vector3.forward, 150f));
        Assert.IsFalse(PlayerGuardUtility.IsFrontalBlock(Vector3.forward, Vector3.zero, 150f));
    }

    [Test]
    public void IsFrontalBlock_IgnoresVerticalComponent()
    {
        // A hit coming in from above-front still counts as frontal once flattened.
        Vector3 fromAboveFront = new Vector3(0f, -2f, -1f); // target - attacker, attacker high and in front
        Assert.IsTrue(PlayerGuardUtility.IsFrontalBlock(Vector3.forward, fromAboveFront, 150f));
    }

    [Test]
    public void MitigatedAmount_CutsDamageAndClamps()
    {
        Assert.AreEqual(15f, PlayerGuardUtility.MitigatedAmount(100f, 0.15f), 1e-4f);
        Assert.AreEqual(0f, PlayerGuardUtility.MitigatedAmount(100f, -1f), 1e-4f);   // perfect block floor
        Assert.AreEqual(100f, PlayerGuardUtility.MitigatedAmount(100f, 2f), 1e-4f);  // multiplier clamped to 1
        Assert.AreEqual(0f, PlayerGuardUtility.MitigatedAmount(-50f, 0.5f), 1e-4f);  // negative damage floored
    }

    [Test]
    public void FullPoiseAmount_IsUnreducedByTheBlock()
    {
        // Same as an unblocked hit's derived poise gain (amount * stanceGainMultiplier), NOT the
        // mitigated amount - a turtling player still gets stagger-broken.
        Assert.AreEqual(20f, PlayerGuardUtility.FullPoiseAmount(100f, 0.2f), 1e-4f);
    }

    [Test]
    public void GuardPoiseGain_UsesTheAttacksOwnPoise()
    {
        // spec item 6: guarding a heavy attack (poise 22) costs more stance than a light one (12).
        Assert.AreEqual(22f, PlayerGuardUtility.GuardPoiseGain(22f, 1f, 6f), 1e-4f);
        Assert.AreEqual(12f, PlayerGuardUtility.GuardPoiseGain(12f, 1f, 6f), 1e-4f);
    }

    [Test]
    public void GuardPoiseGain_ScalesByTheGuardMultiplier()
    {
        Assert.AreEqual(11f, PlayerGuardUtility.GuardPoiseGain(22f, 0.5f, 6f), 1e-4f);
        Assert.AreEqual(0f, PlayerGuardUtility.GuardPoiseGain(22f, -1f, 6f), 1e-4f); // clamped
    }

    [Test]
    public void GuardPoiseGain_FallsBackOnlyWhenNoAttackPoise()
    {
        Assert.AreEqual(6f, PlayerGuardUtility.GuardPoiseGain(0f, 1f, 6f), 1e-4f);
        Assert.AreEqual(6f, PlayerGuardUtility.GuardPoiseGain(-5f, 1f, 6f), 1e-4f);
        Assert.AreEqual(3f, PlayerGuardUtility.GuardPoiseGain(0f, 0.5f, 6f), 1e-4f); // fallback still scaled
    }

    [Test]
    public void DefenseStateCode_ParryWinsOverGuard_GuardWinsOverNone()
    {
        // spec item 2: one place resolves None(0)/Guard(1)/Parry(2).
        Assert.AreEqual(2, PlayerGuardUtility.DefenseStateCode(inParryWindow: true, defenseActionActive: true));
        Assert.AreEqual(2, PlayerGuardUtility.DefenseStateCode(inParryWindow: true, defenseActionActive: false)); // parry window implies active anyway
        Assert.AreEqual(1, PlayerGuardUtility.DefenseStateCode(inParryWindow: false, defenseActionActive: true));
        Assert.AreEqual(0, PlayerGuardUtility.DefenseStateCode(inParryWindow: false, defenseActionActive: false));
    }

    [Test]
    public void StepBlend_MovesTowardTargetFrameRateIndependentlyAndClamps()
    {
        Assert.AreEqual(0.2f, PlayerGuardUtility.StepBlend(0f, 1f, 2f, 0.1f), 1e-4f);
        // Overshoot is clamped to the target, not past it.
        Assert.AreEqual(1f, PlayerGuardUtility.StepBlend(0f, 1f, 100f, 0.1f), 1e-4f);
        Assert.AreEqual(0f, PlayerGuardUtility.StepBlend(0.05f, 0f, 100f, 0.1f), 1e-4f);
    }
}
