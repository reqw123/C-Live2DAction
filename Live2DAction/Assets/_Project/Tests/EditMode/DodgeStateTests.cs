using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Live2DAction.Characters;

public class DodgeStateTests
{
    private const float FrameSeconds = 1f / DodgeData.FramesPerSecond;

    private static DodgeData CreateDodgeData(float distance, int durationFrames, int invulnerabilityFrames, int cooldownFrames)
    {
        var data = ScriptableObject.CreateInstance<DodgeData>();
        SetField(data, "distance", distance);
        SetField(data, "durationFrames", durationFrames);
        SetField(data, "invulnerabilityFrames", invulnerabilityFrames);
        SetField(data, "cooldownFrames", cooldownFrames);
        return data;
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Expected a private field named '{fieldName}' on {target.GetType().Name}");
        field.SetValue(target, value);
    }

    [Test]
    public void Idle_NoInput_StaysIdleAndReturnsZeroVelocity()
    {
        var state = new DodgeState(CreateDodgeData(3f, 6, 6, 10));

        Vector3 velocity = state.Tick(FrameSeconds, dodgePressed: false, Vector3.forward);

        Assert.AreEqual(Vector3.zero, velocity);
        Assert.AreEqual(DodgePhase.Idle, state.Phase);
        Assert.IsFalse(state.IsInvulnerable);
    }

    [Test]
    public void DodgePressed_FromIdle_EntersDodgingAndReturnsVelocityAlongDirection()
    {
        var data = CreateDodgeData(distance: 3f, durationFrames: 6, invulnerabilityFrames: 6, cooldownFrames: 10);
        var state = new DodgeState(data);

        Vector3 velocity = state.Tick(FrameSeconds, dodgePressed: true, Vector3.right);

        Assert.AreEqual(DodgePhase.Dodging, state.Phase);
        Assert.AreEqual(data.Speed, velocity.magnitude, 0.001f);
        Assert.AreEqual(Vector3.right, velocity.normalized);
    }

    [Test]
    public void Direction_LockedInAtStart_IsNormalized()
    {
        var state = new DodgeState(CreateDodgeData(3f, 6, 6, 10));

        state.Tick(FrameSeconds, true, new Vector3(5f, 0f, 0f));

        Assert.AreEqual(Vector3.right, state.Direction);
    }

    [Test]
    public void IsInvulnerable_TrueDuringDodge_FalseAfterDurationEnds()
    {
        var state = new DodgeState(CreateDodgeData(distance: 1f, durationFrames: 2, invulnerabilityFrames: 2, cooldownFrames: 2));

        state.Tick(FrameSeconds, true, Vector3.forward); // frame 1: dodge starts
        Assert.IsTrue(state.IsInvulnerable, "Should be invulnerable on the first dodging step");

        state.Tick(FrameSeconds, false, Vector3.forward); // frame 2: still within duration
        Assert.IsTrue(state.IsInvulnerable);

        state.Tick(FrameSeconds, false, Vector3.forward); // frame 3: duration elapsed -> Cooldown
        Assert.IsFalse(state.IsInvulnerable, "Should no longer be invulnerable once Cooldown begins");
        Assert.AreEqual(DodgePhase.Cooldown, state.Phase);
    }

    [Test]
    public void Tick_ReturnsToIdleAfterCooldownFrames()
    {
        var state = new DodgeState(CreateDodgeData(distance: 1f, durationFrames: 1, invulnerabilityFrames: 1, cooldownFrames: 2));

        state.Tick(FrameSeconds, true, Vector3.forward); // Dodging begins
        for (int i = 0; i < 10; i++)
        {
            state.Tick(FrameSeconds, false, Vector3.forward);
        }

        Assert.AreEqual(DodgePhase.Idle, state.Phase);
    }

    [Test]
    public void DodgePressedDuringCooldown_DoesNotStartNewDodge()
    {
        var state = new DodgeState(CreateDodgeData(distance: 1f, durationFrames: 1, invulnerabilityFrames: 1, cooldownFrames: 10));

        state.Tick(FrameSeconds, true, Vector3.forward); // Dodging begins
        state.Tick(FrameSeconds, false, Vector3.forward); // duration elapsed -> Cooldown

        Assert.AreEqual(DodgePhase.Cooldown, state.Phase);

        Vector3 velocity = state.Tick(FrameSeconds, true, Vector3.right); // pressed again mid-cooldown

        Assert.AreEqual(DodgePhase.Cooldown, state.Phase, "A press during cooldown must not start a new dodge");
        Assert.AreEqual(Vector3.zero, velocity);
    }

    [Test]
    public void NoDodgeData_TickAlwaysReturnsZeroAndStaysIdle()
    {
        var state = new DodgeState(null);

        Vector3 velocity = state.Tick(FrameSeconds, dodgePressed: true, Vector3.forward);

        Assert.AreEqual(Vector3.zero, velocity);
        Assert.AreEqual(DodgePhase.Idle, state.Phase);
    }
}
