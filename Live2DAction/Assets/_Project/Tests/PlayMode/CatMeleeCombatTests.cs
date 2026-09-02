using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Live2DAction.Combat;
using Live2DAction.Core;
using Object = UnityEngine.Object;

// 2026-08-29, cat combat design (Docs/CAT_COMBAT_DESIGN.md, slice 2). The cat reuses the
// player's whole PlayerCombat / ComboAttackState / AttackResolver pipeline; the genuinely new
// pieces are the external-attack-input path (FeedAttackPressed / TryStartOverrideAttack, since
// the cat's melee button is mediated by CatChargeAttack), the knockback dispatch added to
// AttackResolver, and the possession-gated hitstop. This fixture drives the real Update loop
// (same reasoning as DodgeMovementTests / CatFlightAndDashTests) and checks each of those.
public class CatMeleeCombatTests
{
    private static void SetField(object t, string f, object v)
    {
        FieldInfo fi = t.GetType().GetField(f, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(fi, $"no field {f} on {t.GetType().Name}");
        fi.SetValue(t, v);
    }

    private static AttackData MakeAttack(string id, float damage, int startup, int active, int recovery, int comboWindow,
        float range, float radius, float knockback = 0f)
    {
        var d = ScriptableObject.CreateInstance<AttackData>();
        SetField(d, "attackId", id);
        SetField(d, "damage", damage);
        SetField(d, "startupFrames", startup);
        SetField(d, "activeFrames", active);
        SetField(d, "recoveryFrames", recovery);
        SetField(d, "comboWindowFrames", comboWindow);
        SetField(d, "range", range);
        SetField(d, "radius", radius);
        SetField(d, "knockbackForce", knockback);
        return d;
    }

    private GameObject _attacker;
    private GameObject _target;
    private PlayerCombat _combat;
    private Health _targetHealth;

    [SetUp]
    public void SetUp()
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            Object.DestroyImmediate(root);
        }

        _attacker = new GameObject("CatAttacker");
        _attacker.transform.position = Vector3.zero;
        _attacker.transform.rotation = Quaternion.identity; // forward = +Z
        var origin = new GameObject("AttackOrigin");
        origin.transform.SetParent(_attacker.transform);
        origin.transform.localPosition = new Vector3(0f, 0f, 0.3f);
        origin.transform.localRotation = Quaternion.identity;

        _combat = _attacker.AddComponent<PlayerCombat>();
        SetField(_combat, "attackOrigin", origin.transform);
        SetField(_combat, "comboAttacks", new[]
        {
            MakeAttack("CatSwipe1", 6f, 3, 3, 10, 8, 1.4f, 0.6f),
            MakeAttack("CatSwipe2", 7f, 3, 3, 10, 8, 1.4f, 0.6f),
            MakeAttack("CatSwipe3", 12f, 4, 3, 14, 0, 1.6f, 0.7f),
        });
        // inputSource left null - the cat feeds attacks externally.

        _target = new GameObject("Target");
        _target.transform.position = new Vector3(0f, 0f, 1f); // 1m ahead, inside swipe range
        var col = _target.AddComponent<SphereCollider>();
        col.radius = 0.4f;
        _targetHealth = _target.AddComponent<Health>();
        Physics.SyncTransforms();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_attacker);
        Object.DestroyImmediate(_target);
        Time.timeScale = 1f; // safety net - the hitstop test dips it
        foreach (var hs in Object.FindObjectsByType<HitStopController>(FindObjectsSortMode.None))
        {
            Object.DestroyImmediate(hs.gameObject);
        }
    }

    private IEnumerator RunFrames(int n)
    {
        for (int i = 0; i < n; i++) yield return null;
    }

    [UnityTest]
    public IEnumerator FeedAttackPressed_FromIdle_DamagesTargetInFront()
    {
        float startHp = _targetHealth.CurrentHealth;
        _combat.FeedAttackPressed();
        yield return RunFrames(30);

        Assert.Less(_targetHealth.CurrentHealth, startHp, "an external attack press should run the combo and land a hit");
    }

    [UnityTest]
    public IEnumerator OutOfRangeTarget_IsNotHit()
    {
        _target.transform.position = new Vector3(0f, 0f, 6f); // way past range
        Physics.SyncTransforms();
        float startHp = _targetHealth.CurrentHealth;

        _combat.FeedAttackPressed();
        yield return RunFrames(30);

        Assert.AreEqual(startHp, _targetHealth.CurrentHealth, 0.001f);
    }

    [UnityTest]
    public IEnumerator FeedAttackPressed_DuringComboWindow_ChainsToASecondHit()
    {
        _combat.FeedAttackPressed();
        // Wait for the first hit to land + enter recovery.
        yield return RunFrames(10);
        float afterFirst = _targetHealth.CurrentHealth;
        Assert.Less(afterFirst, _targetHealth.MaxHealth, "first swipe should have landed");

        // Press again while in the combo window.
        _combat.FeedAttackPressed();
        yield return RunFrames(20);

        Assert.Less(_targetHealth.CurrentHealth, afterFirst, "a second press in the combo window should chain a second hit");
        Assert.AreEqual(1, _combat.ComboIndex, "chained to combo step index 1");
    }

    [UnityTest]
    public IEnumerator SphericalJudgment_HitsATargetOffToTheSide_ThatTheForwardCapsuleWouldMiss()
    {
        // Directly beside the attacker, not in front - the forward capsule misses this.
        _target.transform.position = new Vector3(1f, 0f, 0f);
        Physics.SyncTransforms();

        _combat.UseSphericalJudgment = true;
        float startHp = _targetHealth.CurrentHealth;

        _combat.FeedAttackPressed();
        yield return RunFrames(30);

        Assert.Less(_targetHealth.CurrentHealth, startHp, "sphere judgment should hit a target beside the attacker");
    }

    [UnityTest]
    public IEnumerator TryStartOverrideAttack_LandsHeavyDamageAndKnocksTheTargetBack()
    {
        var heavy = MakeAttack("CatHeavy", 22f, 4, 4, 12, 0, 1.6f, 0.8f, knockback: 6f);
        SetField(heavy, "knockbackLaunches", false);
        _target.AddComponent<MeleeKnockback>();
        Vector3 startPos = _target.transform.position;
        float startHp = _targetHealth.CurrentHealth;

        Assert.IsTrue(_combat.TryStartOverrideAttack(heavy));
        Assert.AreEqual(-1, _combat.ComboIndex, "override attack is not a combo step");

        yield return RunFrames(40);

        Assert.AreEqual(startHp - 22f, _targetHealth.CurrentHealth, 0.01f, "heavy damage applied");
        Assert.Greater(Vector3.Distance(_target.transform.position, startPos), 0.2f, "knockback shoved the target");
    }

    [UnityTest]
    public IEnumerator HitStopController_DipsThenRestoresTimeScale_AndCancelRestoresImmediately()
    {
        var go = new GameObject("HitStop");
        go.AddComponent<HitStopController>();
        yield return null;

        HitStopController.Request(0.05f);
        Assert.Less(Time.timeScale, 1f, "timescale dips on request");

        yield return new WaitForSecondsRealtime(0.15f);
        Assert.AreEqual(1f, Time.timeScale, 0.001f, "timescale restores after the window");

        HitStopController.Request(2f); // long dip
        Assert.Less(Time.timeScale, 1f);
        HitStopController.CancelAndRestore();
        Assert.AreEqual(1f, Time.timeScale, 0.001f, "cancel restores immediately (the possession-switch-away path)");

        Object.DestroyImmediate(go);
    }
}
