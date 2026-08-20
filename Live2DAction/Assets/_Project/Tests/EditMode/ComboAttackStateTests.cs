using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Live2DAction.Combat;

public class ComboAttackStateTests
{
    private static AttackData CreateAttackData(int startupFrames, int activeFrames, int recoveryFrames, int comboWindowFrames)
    {
        var data = ScriptableObject.CreateInstance<AttackData>();
        SetField(data, "startupFrames", startupFrames);
        SetField(data, "activeFrames", activeFrames);
        SetField(data, "recoveryFrames", recoveryFrames);
        SetField(data, "comboWindowFrames", comboWindowFrames);
        return data;
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Expected a private field named '{fieldName}' on {target.GetType().Name}");
        field.SetValue(target, value);
    }

    // 1 frame at AttackData.FramesPerSecond (60), expressed in seconds, used as a Tick step
    // small enough that tests can walk through phases one frame at a time deterministically.
    private const float FrameSeconds = 1f / AttackData.FramesPerSecond;

    [Test]
    public void Idle_NoInput_StaysIdle()
    {
        var combo = new[] { CreateAttackData(6, 4, 14, 10) };
        var state = new ComboAttackState(combo);

        bool didHit = state.Tick(FrameSeconds, attackPressed: false);

        Assert.IsFalse(didHit);
        Assert.AreEqual(AttackPhase.Idle, state.Phase);
        Assert.AreEqual(-1, state.ComboIndex);
    }

    [Test]
    public void AttackPressed_FromIdle_EntersStartupOnFirstHit()
    {
        var combo = new[] { CreateAttackData(6, 4, 14, 10) };
        var state = new ComboAttackState(combo);

        state.Tick(FrameSeconds, attackPressed: true);

        Assert.AreEqual(AttackPhase.Startup, state.Phase);
        Assert.AreEqual(0, state.ComboIndex);
    }

    [Test]
    public void Tick_ResolvesHitExactlyOnceOnEnteringActive()
    {
        // startup=2 frames means elapsed must accumulate 2 whole frame-steps before the
        // Startup->Active transition is evaluated true; the transition and the hit-resolving
        // step land on separate Tick calls since the transition check runs inside the
        // Startup case and the hit check runs inside the (now current) Active case, which
        // only executes starting the following call.
        var combo = new[] { CreateAttackData(startupFrames: 2, activeFrames: 4, recoveryFrames: 4, comboWindowFrames: 2) };
        var state = new ComboAttackState(combo);

        state.Tick(FrameSeconds, true); // elapsed=0, Startup begins
        bool hit2 = state.Tick(FrameSeconds, false); // elapsed=1 frame, still Startup
        bool hit3 = state.Tick(FrameSeconds, false); // elapsed=2 frames, Startup->Active transition (no hit yet)
        bool hit4 = state.Tick(FrameSeconds, false); // first Tick evaluated while Phase==Active -> hit resolves
        bool hit5 = state.Tick(FrameSeconds, false); // still Active, must not hit again

        Assert.IsFalse(hit2);
        Assert.IsFalse(hit3);
        Assert.IsTrue(hit4);
        Assert.IsFalse(hit5);
        Assert.AreEqual(AttackPhase.Active, state.Phase);
    }

    [Test]
    public void Recovery_WithoutInput_ReturnsToIdleAfterRecoveryFrames()
    {
        var combo = new[] { CreateAttackData(startupFrames: 1, activeFrames: 1, recoveryFrames: 2, comboWindowFrames: 1) };
        var state = new ComboAttackState(combo);

        state.Tick(FrameSeconds, true); // start the attack
        for (int i = 0; i < 10; i++)
        {
            state.Tick(FrameSeconds, false);
        }

        Assert.AreEqual(AttackPhase.Idle, state.Phase);
        Assert.AreEqual(-1, state.ComboIndex);
    }

    [Test]
    public void AttackPressedDuringComboWindow_ChainsToNextAttack()
    {
        AttackData first = CreateAttackData(startupFrames: 1, activeFrames: 1, recoveryFrames: 4, comboWindowFrames: 4);
        AttackData second = CreateAttackData(startupFrames: 1, activeFrames: 1, recoveryFrames: 4, comboWindowFrames: 4);
        var state = new ComboAttackState(new[] { first, second });

        state.Tick(FrameSeconds, true); // start attack 0 (Startup)
        state.Tick(FrameSeconds, false); // Startup -> Active
        state.Tick(FrameSeconds, false); // Active hit, -> Recovery starts
        bool comboHit = state.Tick(FrameSeconds, true); // pressed inside combo window -> chains to attack 1

        Assert.AreEqual(1, state.ComboIndex);
        Assert.AreEqual(AttackPhase.Startup, state.Phase);
        Assert.IsFalse(comboHit, "Chaining into the next attack should not itself resolve a hit");
    }

    [Test]
    public void AttackPressedAfterComboWindowCloses_DoesNotChain()
    {
        AttackData first = CreateAttackData(startupFrames: 1, activeFrames: 1, recoveryFrames: 10, comboWindowFrames: 2);
        AttackData second = CreateAttackData(startupFrames: 1, activeFrames: 1, recoveryFrames: 4, comboWindowFrames: 4);
        var state = new ComboAttackState(new[] { first, second });

        state.Tick(FrameSeconds, true); // Startup
        state.Tick(FrameSeconds, false); // -> Active
        state.Tick(FrameSeconds, false); // hit, -> Recovery

        // Let the combo window (2 frames into Recovery) fully close before pressing again.
        for (int i = 0; i < 5; i++)
        {
            state.Tick(FrameSeconds, false);
        }

        state.Tick(FrameSeconds, true); // pressed too late - should be ignored, still combo index 0 recovering

        Assert.AreEqual(0, state.ComboIndex, "A late press should not retroactively chain into attack 1");
        Assert.AreEqual(AttackPhase.Recovery, state.Phase);
    }

    [Test]
    public void ThirdComboHit_HasNoFurtherChain_ReturnsToIdleAfterRecovery()
    {
        AttackData first = CreateAttackData(1, 1, 2, 2);
        AttackData second = CreateAttackData(1, 1, 2, 2);
        AttackData third = CreateAttackData(1, 1, 2, 2);
        var state = new ComboAttackState(new[] { first, second, third });

        state.Tick(FrameSeconds, true);
        state.Tick(FrameSeconds, false);
        state.Tick(FrameSeconds, false); // attack 0 active hit -> recovery
        state.Tick(FrameSeconds, true); // chain to attack 1
        state.Tick(FrameSeconds, false);
        state.Tick(FrameSeconds, false); // attack 1 active hit -> recovery
        state.Tick(FrameSeconds, true); // chain to attack 2 (last)

        Assert.AreEqual(2, state.ComboIndex);

        // Pressing again during attack 2's combo window must not chain past the array end.
        state.Tick(FrameSeconds, false);
        state.Tick(FrameSeconds, false); // attack 2 active hit -> recovery
        bool chainedPastEnd = false;
        for (int i = 0; i < 10; i++)
        {
            state.Tick(FrameSeconds, true);
            if (state.ComboIndex > 2)
            {
                chainedPastEnd = true;
            }
        }

        Assert.IsFalse(chainedPastEnd);
    }

    [Test]
    public void CurrentAttack_IsNullWhileIdle()
    {
        var combo = new[] { CreateAttackData(1, 1, 1, 1) };
        var state = new ComboAttackState(combo);

        Assert.IsNull(state.CurrentAttack);
    }

    [Test]
    public void PhaseProgress_IsZero_WhileIdle()
    {
        var combo = new[] { CreateAttackData(6, 4, 14, 10) };
        var state = new ComboAttackState(combo);

        Assert.AreEqual(0f, state.PhaseProgress);
    }

    [Test]
    public void PhaseProgress_AdvancesWithinStartupPhase()
    {
        var combo = new[] { CreateAttackData(startupFrames: 4, activeFrames: 4, recoveryFrames: 4, comboWindowFrames: 2) };
        var state = new ComboAttackState(combo);

        state.Tick(FrameSeconds, true); // elapsed=0, Startup begins
        Assert.AreEqual(0f, state.PhaseProgress, 0.01f);

        state.Tick(FrameSeconds, false); // elapsed=1 of 4 startup frames
        Assert.AreEqual(0.25f, state.PhaseProgress, 0.01f);
    }

    [Test]
    public void PhaseProgress_IsRelativeToNewPhase_NotCumulativeAcrossWholeAttack()
    {
        var combo = new[] { CreateAttackData(startupFrames: 2, activeFrames: 4, recoveryFrames: 4, comboWindowFrames: 2) };
        var state = new ComboAttackState(combo);

        state.Tick(FrameSeconds, true); // elapsed=0, Startup
        state.Tick(FrameSeconds, false); // elapsed=1 frame, still Startup
        state.Tick(FrameSeconds, false); // elapsed=2 frames, Startup->Active transition

        Assert.AreEqual(AttackPhase.Active, state.Phase);
        Assert.AreEqual(0f, state.PhaseProgress, 0.01f, "Progress should reset relative to the new (Active) phase, not keep the cumulative attack elapsed time");
    }

    [Test]
    public void PhaseProgress_IsOne_WhenPhaseHasZeroDuration()
    {
        var combo = new[] { CreateAttackData(startupFrames: 0, activeFrames: 0, recoveryFrames: 4, comboWindowFrames: 0) };
        var state = new ComboAttackState(combo);

        state.Tick(FrameSeconds, true); // Startup begins (0-frame duration)
        state.Tick(FrameSeconds, false); // elapsed >= 0 -> transitions straight to Active

        Assert.AreEqual(AttackPhase.Active, state.Phase);
        Assert.AreEqual(1f, state.PhaseProgress, "A zero-duration phase should report fully progressed rather than dividing by zero");
    }
}
