using NUnit.Framework;
using UnityEngine;
using Live2DAction.Core;

// Relies on Health's default maxHealth (100) rather than reflecting a custom value in,
// since Awake() already runs synchronously the moment AddComponent<Health>() executes -
// there is no supported way to re-run it afterwards with a different serialized value.
public class HealthTests
{
    [Test]
    public void ApplyDamage_ReducesCurrentHealth()
    {
        var go = new GameObject("Dummy");
        var health = go.AddComponent<Health>();

        health.ApplyDamage(new DamageInfo(30f, Vector3.zero, Vector3.forward, null));

        Assert.AreEqual(health.MaxHealth - 30f, health.CurrentHealth);
        Assert.IsFalse(health.IsDead);
    }

    [Test]
    public void ApplyDamage_LethalDamage_MarksDeadAndFiresDiedEvent()
    {
        var go = new GameObject("Dummy");
        var health = go.AddComponent<Health>();

        bool diedFired = false;
        health.Died += () => diedFired = true;

        health.ApplyDamage(new DamageInfo(health.MaxHealth, Vector3.zero, Vector3.forward, null));

        Assert.IsTrue(health.IsDead);
        Assert.IsTrue(diedFired);
        Assert.AreEqual(0f, health.CurrentHealth);
        Assert.IsFalse(go.activeSelf, "Dummy should be disabled once its health reaches zero");
    }

    [Test]
    public void ApplyDamage_AfterDeath_IsIgnored()
    {
        var go = new GameObject("Dummy");
        var health = go.AddComponent<Health>();

        health.ApplyDamage(new DamageInfo(health.MaxHealth, Vector3.zero, Vector3.forward, null));

        int diedCount = 0;
        health.Died += () => diedCount++;
        health.ApplyDamage(new DamageInfo(health.MaxHealth, Vector3.zero, Vector3.forward, null));

        Assert.AreEqual(0, diedCount);
        Assert.AreEqual(0f, health.CurrentHealth);
    }

    [Test]
    public void ApplyDamage_CannotReduceHealthBelowZero()
    {
        var go = new GameObject("Dummy");
        var health = go.AddComponent<Health>();

        health.ApplyDamage(new DamageInfo(health.MaxHealth + 999f, Vector3.zero, Vector3.forward, null));

        Assert.AreEqual(0f, health.CurrentHealth);
    }

    [Test]
    public void ApplyDamage_WhileInvulnerable_IsIgnored()
    {
        var go = new GameObject("Dummy");
        var health = go.AddComponent<Health>();
        health.IsInvulnerable = true;

        health.ApplyDamage(new DamageInfo(30f, Vector3.zero, Vector3.forward, null));

        Assert.AreEqual(health.MaxHealth, health.CurrentHealth);
    }

    [Test]
    public void ApplyDamage_AfterInvulnerabilityEnds_AppliesNormally()
    {
        var go = new GameObject("Dummy");
        var health = go.AddComponent<Health>();
        health.IsInvulnerable = true;
        health.ApplyDamage(new DamageInfo(30f, Vector3.zero, Vector3.forward, null));
        health.IsInvulnerable = false;

        health.ApplyDamage(new DamageInfo(30f, Vector3.zero, Vector3.forward, null));

        Assert.AreEqual(health.MaxHealth - 30f, health.CurrentHealth);
    }
}
