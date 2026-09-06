using NUnit.Framework;
using UnityEngine;
using Live2DAction.AI.Boss.Yuanpei;

// 2026-09-06 - the pure state-machine / envelope maths for the Boss 支配領域 screen effect,
// verified with no MonoBehaviour, renderer or frame loop.
public class BossDomainScreenVFXTests
{
    static BossDomainEnvelope New(float enter = 1.2f, float exit = 2.0f, float pulse = 0.6f)
        => new BossDomainEnvelope { EnterDuration = enter, ExitDuration = exit, PulseDuration = pulse };

    static void Run(BossDomainEnvelope e, float seconds, float step = 1f / 60f)
    {
        for (float t = 0f; t < seconds; t += step) e.Tick(step);
    }

    [Test]
    public void StartsInactive_NothingRendering()
    {
        var e = New();
        Assert.AreEqual(BossDomainState.Inactive, e.State);
        Assert.IsFalse(e.IsRendering);
        Assert.AreEqual(0f, e.EnterExit);
    }

    [Test]
    public void Begin_EntersThenReachesActiveAfterEnterDuration()
    {
        var e = New(enter: 1.2f);
        e.Begin();
        Assert.AreEqual(BossDomainState.Entering, e.State);
        Assert.IsTrue(e.IsRendering);

        Run(e, 0.6f);
        Assert.AreEqual(BossDomainState.Entering, e.State);
        Assert.That(e.EnterExit, Is.InRange(0.35f, 0.65f)); // ~halfway

        Run(e, 0.8f);
        Assert.AreEqual(BossDomainState.Active, e.State);
        Assert.AreEqual(1f, e.EnterExit, 0.001f);
    }

    [Test]
    public void End_FromActive_ExitsAndFullyClearsAfterExitDuration()
    {
        var e = New(enter: 1.2f, exit: 2.0f);
        e.Begin();
        Run(e, 1.5f); // settle to Active

        e.End();
        Assert.AreEqual(BossDomainState.Exiting, e.State);
        Assert.IsTrue(e.IsRendering);

        Run(e, 1.0f);
        Assert.AreEqual(BossDomainState.Exiting, e.State); // still fading

        Run(e, 1.3f);
        Assert.AreEqual(BossDomainState.Inactive, e.State);
        Assert.AreEqual(0f, e.EnterExit);
        Assert.IsFalse(e.IsRendering); // §7 - nothing to render once the exit finishes
        Assert.AreEqual(1, e.Phase);   // reset
    }

    [Test]
    public void SetPhase_WhileActive_FiresAOneShotPulseThatDecaysToZero()
    {
        var e = New(pulse: 0.6f);
        e.Begin();
        Run(e, 1.5f);
        Assert.AreEqual(0f, e.Pulse);

        e.SetPhase(2);
        Assert.AreEqual(2, e.Phase);
        Assert.AreEqual(BossDomainState.PhasePulse, e.State);

        Run(e, 0.15f);
        Assert.Greater(e.Pulse, 0.4f); // risen near peak

        Run(e, 0.8f);
        Assert.AreEqual(0f, e.Pulse);            // decayed
        Assert.AreEqual(BossDomainState.Active, e.State); // returned to Active
    }

    [Test]
    public void SetPhase_SameValue_DoesNothing()
    {
        var e = New();
        e.Begin();
        Run(e, 1.5f);
        e.SetPhase(1);
        Assert.AreEqual(BossDomainState.Active, e.State);
        Assert.AreEqual(0f, e.Pulse);
    }

    [Test]
    public void SetPhase_ClampsToOneToThree()
    {
        var e = New();
        e.Begin();
        e.SetPhase(9);
        Assert.AreEqual(3, e.Phase);
        e.SetPhase(-4);
        Assert.AreEqual(1, e.Phase);
    }

    [Test]
    public void SetPhase_WhileInactive_DoesNotStartRendering()
    {
        var e = New();
        e.SetPhase(2);
        Assert.AreEqual(BossDomainState.Inactive, e.State);
        Assert.IsFalse(e.IsRendering);
    }

    [Test]
    public void Pulse_StrengthIsHonouredAndClamped()
    {
        var e = New(pulse: 0.6f);
        e.Begin();
        Run(e, 1.5f);

        e.FirePulse(0.5f);
        Run(e, 0.15f); // ~peak
        Assert.That(e.Pulse, Is.InRange(0.3f, 0.55f));

        Run(e, 1f); // fully decayed
        e.FirePulse(5f); // over-range -> clamps to 1
        Run(e, 0.16f);
        Assert.That(e.Pulse, Is.InRange(0.7f, 1.0f));
    }

    [Test]
    public void SetIntensity_ClampsZeroToOne()
    {
        var e = New();
        e.SetIntensity(2.5f);
        Assert.AreEqual(1f, e.Intensity);
        e.SetIntensity(-1f);
        Assert.AreEqual(0f, e.Intensity);
    }

    [Test]
    public void Begin_AfterExit_RestartsCleanly()
    {
        var e = New(enter: 1.2f, exit: 2.0f);
        e.Begin();
        Run(e, 1.5f);
        e.SetPhase(3);
        e.End();
        Run(e, 3f);
        Assert.AreEqual(BossDomainState.Inactive, e.State);

        e.Begin();
        Assert.AreEqual(BossDomainState.Entering, e.State);
        Assert.AreEqual(1, e.Phase);   // phase reset on a fresh Begin
        Assert.AreEqual(0f, e.Pulse);
    }

    [Test]
    public void Tick_NegativeDeltaTimeIsIgnored_NoNaN()
    {
        var e = New();
        e.Begin();
        e.Tick(-5f);
        Assert.AreEqual(0f, e.EnterExit);
        Assert.IsFalse(float.IsNaN(e.EnterExit));
    }

    [Test]
    public void EnterExit_NeverLeavesZeroToOne()
    {
        var e = New(enter: 0.3f, exit: 0.3f);
        e.Begin();
        for (int i = 0; i < 200; i++)
        {
            e.Tick(0.1f);
            Assert.That(e.EnterExit, Is.InRange(0f, 1f));
            if (i == 50) e.End();
            if (i == 120) e.Begin();
        }
    }
}
