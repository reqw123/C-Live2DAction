using NUnit.Framework;
using UnityEngine;
using Live2DAction.Combat;

// 2026-09-01, user request - Sekiro-style deflect. The priority chain and the two time windows
// are the whole mechanic's core; they must be right regardless of any Animator / physics.
// Maps directly onto the spec's 6 verification scenarios.
public class BladeClashUtilityTests
{
    // --- priority chain (spec 一) ------------------------------------------------------------

    [Test]
    public void Classify_InParryWindow_Frontal_IsParried_EvenIfButtonNotHeld()
    {
        // Sekiro tap-to-deflect: a quick tap that landed in the window still parries.
        Assert.AreEqual(BladeClashResult.Parried,
            BladeClashUtility.Classify(isFrontal: true, withinParryWindow: true, guardHeld: false));
    }

    [Test]
    public void Classify_PastParryWindow_ButHeld_IsGuarded()
    {
        Assert.AreEqual(BladeClashResult.Guarded,
            BladeClashUtility.Classify(isFrontal: true, withinParryWindow: false, guardHeld: true));
    }

    [Test]
    public void Classify_NoWindowNoHold_IsNone()
    {
        Assert.AreEqual(BladeClashResult.None,
            BladeClashUtility.Classify(isFrontal: true, withinParryWindow: false, guardHeld: false));
    }

    [Test]
    public void Classify_HitFromBehind_IsNone_EvenInParryWindow()
    {
        Assert.AreEqual(BladeClashResult.None,
            BladeClashUtility.Classify(isFrontal: false, withinParryWindow: true, guardHeld: true));
    }

    // --- parry window is measured from the PRESS EDGE, holding doesn't refresh (spec 二) -----

    [Test]
    public void WithinParryWindow_JustPressed_IsOpen()
    {
        Assert.IsTrue(BladeClashUtility.WithinParryWindow(now: 10.00f, guardStartTime: 10.00f, parryWindowDuration: 0.12f));
        Assert.IsTrue(BladeClashUtility.WithinParryWindow(now: 10.11f, guardStartTime: 10.00f, parryWindowDuration: 0.12f));
    }

    [Test]
    public void WithinParryWindow_HeldPastTheWindow_IsClosed()
    {
        // pressed at t=10, still holding at t=10.5 -> 0.5s elapsed -> no longer a parry, only a guard
        Assert.IsFalse(BladeClashUtility.WithinParryWindow(now: 10.50f, guardStartTime: 10.00f, parryWindowDuration: 0.12f));
    }

    [Test]
    public void WithinParryWindow_NoPressRecorded_IsClosed()
    {
        Assert.IsFalse(BladeClashUtility.WithinParryWindow(now: 5f, guardStartTime: -1f, parryWindowDuration: 0.12f));
    }

    // --- clash debounce (spec 三 / 六.5) ---------------------------------------------------

    [Test]
    public void ClashCooldown_TooSoon_NotElapsed()
    {
        Assert.IsFalse(BladeClashUtility.ClashCooldownElapsed(now: 1.05f, lastClashTime: 1.00f, cooldownSeconds: 0.1f));
    }

    [Test]
    public void ClashCooldown_AfterCooldown_Elapsed()
    {
        Assert.IsTrue(BladeClashUtility.ClashCooldownElapsed(now: 1.20f, lastClashTime: 1.00f, cooldownSeconds: 0.1f));
    }

    // --- deflect reaction plumbing (spec item 1) -----------------------------------------------

    [Test]
    public void BladeClashInfo_DefaultsToRecoil_SoUnmigratedWindowsKeepOldBehaviour()
    {
        // The 5-arg form (every existing caller / test) must still mean "parry interrupts the swing".
        var info = new BladeClashInfo(null, 10f, 5f, Vector3.zero, Vector3.forward);
        Assert.AreEqual(DeflectReaction.Recoil, info.Reaction);
        Assert.AreEqual(0, (int)DeflectReaction.Recoil); // zero value == old behaviour == default(enum)
    }

    [Test]
    public void BladeClashInfo_CarriesAnExplicitReaction()
    {
        var info = new BladeClashInfo(null, 10f, 5f, Vector3.zero, Vector3.forward, DeflectReaction.ContinueCombo);
        Assert.AreEqual(DeflectReaction.ContinueCombo, info.Reaction);
    }

    [Test]
    public void BossHitWindow_DeflectReactionDefaultsToRecoil()
    {
        Assert.AreEqual(DeflectReaction.Recoil, new Live2DAction.Combat.Boss.BossHitWindow().deflectReaction);
    }
}
