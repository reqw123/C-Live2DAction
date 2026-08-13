using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Live2DAction.Core;
using Live2DAction.Combat;

public class AttackResolverTests
{
    private static AttackData CreateAttackData(float damage)
    {
        var data = ScriptableObject.CreateInstance<AttackData>();
        FieldInfo field = typeof(AttackData).GetField("damage", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, "Expected a private field named 'damage' on AttackData");
        field.SetValue(data, damage);
        return data;
    }

    private static GameObject CreateDamageableTarget(string name, out Health health)
    {
        var go = new GameObject(name);
        var collider = go.AddComponent<SphereCollider>();
        collider.radius = 0.5f;
        health = go.AddComponent<Health>();
        return go;
    }

    [Test]
    public void ResolveHits_AppliesDamageToTarget()
    {
        AttackData attackData = CreateAttackData(25f);
        var attacker = new GameObject("Attacker");
        GameObject target = CreateDamageableTarget("Target", out Health health);
        target.transform.position = Vector3.zero;

        var hits = AttackResolver.ResolveHits(Vector3.zero, attackData, attacker.transform, new Collider[] { target.GetComponent<Collider>() });

        Assert.AreEqual(1, hits.Count);
        Assert.AreEqual(health.MaxHealth - 25f, health.CurrentHealth);
    }

    [Test]
    public void ResolveHits_IgnoresCollidersUnderAttackersOwnRoot()
    {
        AttackData attackData = CreateAttackData(25f);
        var attacker = new GameObject("Attacker");
        var attackerCollider = attacker.AddComponent<SphereCollider>();
        var selfHealth = attacker.AddComponent<Health>();

        var hits = AttackResolver.ResolveHits(Vector3.zero, attackData, attacker.transform, new Collider[] { attackerCollider });

        Assert.AreEqual(0, hits.Count);
        Assert.AreEqual(selfHealth.MaxHealth, selfHealth.CurrentHealth, "Attacker should never damage itself");
    }

    [Test]
    public void ResolveHits_SkipsCollidersWithoutIDamageable()
    {
        AttackData attackData = CreateAttackData(25f);
        var attacker = new GameObject("Attacker");
        var plainGeometry = new GameObject("Wall");
        var wallCollider = plainGeometry.AddComponent<BoxCollider>();

        var hits = AttackResolver.ResolveHits(Vector3.zero, attackData, attacker.transform, new Collider[] { wallCollider });

        Assert.AreEqual(0, hits.Count);
    }

    [Test]
    public void ResolveHits_MultipleTargets_DamagesEachOnce()
    {
        AttackData attackData = CreateAttackData(10f);
        var attacker = new GameObject("Attacker");
        GameObject targetA = CreateDamageableTarget("TargetA", out Health healthA);
        GameObject targetB = CreateDamageableTarget("TargetB", out Health healthB);

        var hits = AttackResolver.ResolveHits(
            Vector3.zero,
            attackData,
            attacker.transform,
            new[] { targetA.GetComponent<Collider>(), targetB.GetComponent<Collider>() });

        Assert.AreEqual(2, hits.Count);
        Assert.AreEqual(healthA.MaxHealth - 10f, healthA.CurrentHealth);
        Assert.AreEqual(healthB.MaxHealth - 10f, healthB.CurrentHealth);
    }

    // 2026-08-12: ResolveHits started returning actual hit points (not just a count) so
    // PlayerCombat can spawn a hit-effect at each real impact location.
    [Test]
    public void ResolveHits_ReturnedPointIsOnTheTargetsSurface()
    {
        AttackData attackData = CreateAttackData(10f);
        var attacker = new GameObject("Attacker");
        GameObject target = CreateDamageableTarget("Target", out _);
        target.transform.position = new Vector3(5f, 0f, 0f);

        var hits = AttackResolver.ResolveHits(Vector3.zero, attackData, attacker.transform, new Collider[] { target.GetComponent<Collider>() });

        Assert.AreEqual(1, hits.Count);
        // The target's SphereCollider (radius 0.5) centered at (5,0,0), queried from the
        // origin - the closest point on its surface should be roughly (4.5, 0, 0), not the
        // target's own center or the query origin.
        Assert.AreEqual(4.5f, hits[0].x, 0.01f);
    }
}
