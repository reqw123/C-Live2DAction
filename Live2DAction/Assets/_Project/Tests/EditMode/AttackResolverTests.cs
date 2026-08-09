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

        int hits = AttackResolver.ResolveHits(Vector3.zero, attackData, attacker.transform, new Collider[] { target.GetComponent<Collider>() });

        Assert.AreEqual(1, hits);
        Assert.AreEqual(health.MaxHealth - 25f, health.CurrentHealth);
    }

    [Test]
    public void ResolveHits_IgnoresCollidersUnderAttackersOwnRoot()
    {
        AttackData attackData = CreateAttackData(25f);
        var attacker = new GameObject("Attacker");
        var attackerCollider = attacker.AddComponent<SphereCollider>();
        var selfHealth = attacker.AddComponent<Health>();

        int hits = AttackResolver.ResolveHits(Vector3.zero, attackData, attacker.transform, new Collider[] { attackerCollider });

        Assert.AreEqual(0, hits);
        Assert.AreEqual(selfHealth.MaxHealth, selfHealth.CurrentHealth, "Attacker should never damage itself");
    }

    [Test]
    public void ResolveHits_SkipsCollidersWithoutIDamageable()
    {
        AttackData attackData = CreateAttackData(25f);
        var attacker = new GameObject("Attacker");
        var plainGeometry = new GameObject("Wall");
        var wallCollider = plainGeometry.AddComponent<BoxCollider>();

        int hits = AttackResolver.ResolveHits(Vector3.zero, attackData, attacker.transform, new Collider[] { wallCollider });

        Assert.AreEqual(0, hits);
    }

    [Test]
    public void ResolveHits_MultipleTargets_DamagesEachOnce()
    {
        AttackData attackData = CreateAttackData(10f);
        var attacker = new GameObject("Attacker");
        GameObject targetA = CreateDamageableTarget("TargetA", out Health healthA);
        GameObject targetB = CreateDamageableTarget("TargetB", out Health healthB);

        int hits = AttackResolver.ResolveHits(
            Vector3.zero,
            attackData,
            attacker.transform,
            new[] { targetA.GetComponent<Collider>(), targetB.GetComponent<Collider>() });

        Assert.AreEqual(2, hits);
        Assert.AreEqual(healthA.MaxHealth - 10f, healthA.CurrentHealth);
        Assert.AreEqual(healthB.MaxHealth - 10f, healthB.CurrentHealth);
    }
}
