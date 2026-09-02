using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Live2DAction.Combat.Boss;
using Live2DAction.Core;
using Object = UnityEngine.Object;

// spec WUSHI_COMBAT_ENGINEERING_SPEC.md §4 (M2 項目 3) - a katana rotating about the wrist barely
// translates its collider centre while the tip carves a wide arc. The default centre-translation
// sweep (and its `distance < 0.0001` early-out) misses that arc; the rotational sweep samples
// root/mid/tip and sweeps each. Flag OFF must leave every other BossHitbox untouched.
public class BossHitboxRotationalSweepTests
{
    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Expected a private field named '{fieldName}' on {target.GetType().Name}");
        field.SetValue(target, value);
    }

    private static BossAttackDefinition MakeAttack(float healthDamage)
    {
        var def = ScriptableObject.CreateInstance<BossAttackDefinition>();
        SetField(def, "baseHealthDamage", healthDamage);
        SetField(def, "basePoiseDamage", 0f);
        SetField(def, "knockbackForce", 0f);
        return def;
    }

    private static BossHitWindow WeaponWindow()
    {
        return new BossHitWindow { part = BossHitboxPart.Weapon, startNormalized = 0f, endNormalized = 1f, damageMultiplier = 1f };
    }

    [SetUp]
    public void SetUp()
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            Object.DestroyImmediate(root);
        }
    }

    // A blade capsule (along local X) whose centre is pinned at the world origin; caller rotates it.
    private static BossHitbox MakeBladeHitbox(Transform bossRoot, bool rotationalSweep)
    {
        var go = new GameObject("BladeHitbox");
        go.transform.SetParent(bossRoot, false);
        var cap = go.AddComponent<CapsuleCollider>();
        cap.direction = 0;      // X
        cap.height = 1.8f;      // half-line ~0.8 after radius
        cap.radius = 0.1f;
        cap.center = Vector3.zero;

        var hb = go.AddComponent<BossHitbox>();
        SetField(hb, "useRotationalSweep", rotationalSweep);
        hb.Configure(bossRoot, "boss");
        return hb;
    }

    private static Health MakeTarget(string name, Vector3 pos, float halfExtent)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var box = go.AddComponent<BoxCollider>();
        box.size = Vector3.one * (halfExtent * 2f);
        return go.AddComponent<Health>();
    }

    [UnityTest]
    public IEnumerator RotationalSweep_TipArc_HitsATargetTheCentreSweepWouldTunnelPast()
    {
        var boss = new GameObject("Boss");
        boss.transform.position = Vector3.zero;
        BossHitbox hb = MakeBladeHitbox(boss.transform, rotationalSweep: true);

        // Target on the tip's arc at ~67.5 deg, radius ~0.78 from the pivot - never inside the thin
        // capsule volume on a sampled frame (the blade is at 45 then 90), only crossed by the arc.
        float r = 0.78f;
        var pos = new Vector3(r * Mathf.Cos(67.5f * Mathf.Deg2Rad), 0f, r * Mathf.Sin(67.5f * Mathf.Deg2Rad));
        Health target = MakeTarget("ArcTarget", pos, 0.12f);

        hb.Activate(MakeAttack(10f), WeaponWindow());

        float[] anglesDeg = { 0f, 45f, 90f, 135f };
        foreach (float a in anglesDeg)
        {
            hb.transform.rotation = Quaternion.Euler(0f, -a, 0f); // sweep +X toward +Z
            yield return new WaitForFixedUpdate();
        }

        Assert.AreEqual(target.MaxHealth - 10f, target.CurrentHealth, 0.01f,
            "the tip's arc crossed the target between sampled frames - the rotational sweep catches it");

        hb.Deactivate();
    }

    [UnityTest]
    public IEnumerator FlagOff_CentreSweep_StillLandsAPlainTranslatingHit()
    {
        var boss = new GameObject("Boss");
        boss.transform.position = Vector3.zero;
        BossHitbox hb = MakeBladeHitbox(boss.transform, rotationalSweep: false);

        Health target = MakeTarget("AheadTarget", new Vector3(0f, 0f, 2f), 0.3f);

        hb.Activate(MakeAttack(10f), WeaponWindow());

        // Translate the whole hitbox forward through the target (the centre moves - the classic case).
        for (int i = 0; i <= 8; i++)
        {
            hb.transform.position = new Vector3(0f, 0f, Mathf.Lerp(0f, 3f, i / 8f));
            yield return new WaitForFixedUpdate();
        }

        Assert.AreEqual(target.MaxHealth - 10f, target.CurrentHealth, 0.01f,
            "flag off = the original centre-translation sweep, unchanged");

        hb.Deactivate();
    }

    [UnityTest]
    public IEnumerator RotationalSweep_ResolvesOncePerTarget_AcrossManyFixedUpdates()
    {
        var boss = new GameObject("Boss");
        boss.transform.position = Vector3.zero;
        BossHitbox hb = MakeBladeHitbox(boss.transform, rotationalSweep: true);

        // Big target the blade sweeps into and then keeps rotating through.
        Health target = MakeTarget("BigTarget", new Vector3(0.5f, 0f, 0.5f), 0.6f);
        hb.Activate(MakeAttack(10f), WeaponWindow());

        for (int i = 0; i <= 12; i++)
        {
            hb.transform.rotation = Quaternion.Euler(0f, -i * 12f, 0f);
            yield return new WaitForFixedUpdate();
        }

        Assert.AreEqual(target.MaxHealth - 10f, target.CurrentHealth, 0.01f,
            "one activation resolves against a target once, no per-physics-step re-tick");

        hb.Deactivate();
    }
}
