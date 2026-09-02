using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Live2DAction.Characters;
using Live2DAction.Combat;

// 2026-08-29, cat combat design (Docs/CAT_COMBAT_DESIGN.md, slice 2). Pure-logic coverage for
// the cat's melee stack - the swing-pose shape, the override-attack state machine, the pounce
// lunge speed, and the knockback decay curve. The Play-loop behaviour (cat actually damaging a
// dummy, combo chaining, aerial sphere judgment, hitstop gating) is in the PlayMode
// CatMeleeCombatTests.
public class CatCombatTests
{
    private static AttackData Attack(int startup, int active, int recovery, int comboWindow)
    {
        var d = ScriptableObject.CreateInstance<AttackData>();
        SetField(d, "startupFrames", startup);
        SetField(d, "activeFrames", active);
        SetField(d, "recoveryFrames", recovery);
        SetField(d, "comboWindowFrames", comboWindow);
        return d;
    }

    private static void SetField(object t, string f, object v)
    {
        FieldInfo fi = t.GetType().GetField(f, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(fi, $"no field {f} on {t.GetType().Name}");
        fi.SetValue(t, v);
    }

    private const float Frame = 1f / AttackData.FramesPerSecond;

    // ---- CatAttackPose.ComputeSwing ----

    [Test]
    public void ComputeSwing_Idle_IsZero()
    {
        Assert.AreEqual(0f, CatAttackPose.ComputeSwing(AttackPhase.Idle, 0.5f));
    }

    [Test]
    public void ComputeSwing_WindsUpNegativeDuringStartup_ThenSnapsPositiveThroughActive()
    {
        Assert.AreEqual(0f, CatAttackPose.ComputeSwing(AttackPhase.Startup, 0f), 0.001f);
        Assert.AreEqual(-1f, CatAttackPose.ComputeSwing(AttackPhase.Startup, 1f), 0.001f);
        Assert.AreEqual(-1f, CatAttackPose.ComputeSwing(AttackPhase.Active, 0f), 0.001f);
        Assert.AreEqual(1f, CatAttackPose.ComputeSwing(AttackPhase.Active, 1f), 0.001f);
        Assert.AreEqual(1f, CatAttackPose.ComputeSwing(AttackPhase.Recovery, 0f), 0.001f);
        Assert.AreEqual(0f, CatAttackPose.ComputeSwing(AttackPhase.Recovery, 1f), 0.001f);
    }

    [Test]
    public void LeadPaw_AlternatesByComboStep_BothForOverride()
    {
        Assert.AreEqual(CatAttackPose.PawSide.Right, CatAttackPose.LeadPawFor(0, false));
        Assert.AreEqual(CatAttackPose.PawSide.Left, CatAttackPose.LeadPawFor(1, false));
        Assert.AreEqual(CatAttackPose.PawSide.Right, CatAttackPose.LeadPawFor(2, false));
        Assert.AreEqual(CatAttackPose.PawSide.Both, CatAttackPose.LeadPawFor(-1, true));
    }

    // ---- ComboAttackState.StartOverride (charged heavy / pounce) ----

    [Test]
    public void StartOverride_FromIdle_RunsItsOwnFrameData_ComboIndexStaysMinusOne()
    {
        var combo = new[] { Attack(6, 4, 14, 10) };
        var state = new ComboAttackState(combo);
        var heavy = Attack(2, 3, 6, 0);

        Assert.IsTrue(state.StartOverride(heavy));
        Assert.AreEqual(AttackPhase.Startup, state.Phase);
        Assert.AreEqual(-1, state.ComboIndex, "override attack must not look like a combo step");
        Assert.IsTrue(state.IsOverrideAttackActive);
        Assert.AreSame(heavy, state.CurrentAttack);
    }

    [Test]
    public void StartOverride_IsRejectedWhileAnotherAttackIsResolving()
    {
        var combo = new[] { Attack(2, 2, 6, 2) };
        var state = new ComboAttackState(combo);
        state.Tick(Frame, true); // in Startup now

        Assert.IsFalse(state.StartOverride(Attack(2, 2, 6, 0)));
        Assert.AreEqual(0, state.ComboIndex, "still the normal combo attack");
    }

    [Test]
    public void OverrideAttack_ResolvesHitOnce_AndItsRecoveryNeverChainsACombo()
    {
        var combo = new[] { Attack(6, 4, 14, 10), Attack(6, 4, 14, 10) };
        var state = new ComboAttackState(combo);
        var pounce = Attack(1, 1, 2, 4); // startup 1 / active 1 / recovery 2 frames
        state.StartOverride(pounce);

        int hits = 0;
        // 4 frames = the whole override lifetime. Press the whole time: a combo would chain into
        // step 1 on a press inside its window; the override guard must stop that.
        for (int i = 0; i < 4; i++)
        {
            if (state.Tick(Frame, attackPressed: true)) hits++;
        }

        Assert.AreEqual(1, hits, "override active window resolves exactly once");
        Assert.IsFalse(state.IsOverrideAttackActive, "override finished");
        Assert.AreEqual(-1, state.ComboIndex, "override never became / chained into a combo step");
    }

    // ---- CatPounce.LungeSpeed ----

    [Test]
    public void LungeSpeed_IsDistanceOverDuration()
    {
        Assert.AreEqual(12.5f, CatPounce.LungeSpeed(3.5f, 0.28f), 0.01f);
        Assert.AreEqual(3.5f / 0.01f, CatPounce.LungeSpeed(3.5f, 0f), 0.01f, "guards against divide-by-zero");
    }

    // ---- CatPounce.ShouldPounce (2026-08-29 "有時普通攻擊也會衝刺" exclusion rules) ----

    private static bool Pounce(Vector2 move, float speed, float moveSpeed = 3f, float frac = 0.7f,
        bool offCd = true, bool idle = true, bool grounded = true, bool flying = false)
        => CatPounce.ShouldPounce(offCd, idle, grounded, flying, move, speed, moveSpeed, frac);

    [Test]
    public void ShouldPounce_OnlyWhenRunningWithAHeldDirection()
    {
        Assert.IsTrue(Pounce(Vector2.up, speed: 3f), "running forward + press = pounce");
        Assert.IsFalse(Pounce(Vector2.zero, speed: 0f), "standing still = normal swipe");
        Assert.IsFalse(Pounce(Vector2.up, speed: 0.5f), "key held but barely moving (accel ramp / wall) = normal swipe");
        Assert.IsFalse(Pounce(Vector2.zero, speed: 3f), "coasting after releasing the key = normal swipe");
    }

    [Test]
    public void ShouldPounce_NeverWhileAiring_MidCombo_OrOnCooldown()
    {
        Assert.IsFalse(Pounce(Vector2.up, 3f, flying: true));
        Assert.IsFalse(Pounce(Vector2.up, 3f, grounded: false));
        Assert.IsFalse(Pounce(Vector2.up, 3f, idle: false), "mid-combo a press just continues the combo");
        Assert.IsFalse(Pounce(Vector2.up, 3f, offCd: false));
    }

    // ---- MeleeKnockback.DecayFactor ----

    [Test]
    public void KnockbackDecayFactor_GoesFromOneToZeroOverTheWindow()
    {
        Assert.AreEqual(1f, MeleeKnockback.DecayFactor(0.35f, 0.35f), 0.001f);
        Assert.AreEqual(0.5f, MeleeKnockback.DecayFactor(0.175f, 0.35f), 0.001f);
        Assert.AreEqual(0f, MeleeKnockback.DecayFactor(0f, 0.35f), 0.001f);
        Assert.AreEqual(0f, MeleeKnockback.DecayFactor(-1f, 0.35f), 0.001f);
    }
}
