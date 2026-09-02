using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Live2DAction.Combat;
using Live2DAction.Core;
using Live2DAction.Input;
using Object = UnityEngine.Object;

// spec WUSHI_COMBAT_ENGINEERING_SPEC.md §5.4 (M2 項目 4) - the swept blade hitbox lands on where the
// BLADE is, not where the player ROOT is: a tip that carves through a target connects even with the
// root out of the old OverlapCapsule range, a swing whose blade points away misses even point-blank,
// and one swing resolves once per target but can catch several different targets.
public class PlayerWeaponHitboxPlayModeTests
{
    private class StubInput : MonoBehaviour, IInputCommand
    {
        public Vector2 MoveInput { get; set; }
        public bool AttackPressed { get; set; }
        public bool DodgePressed { get; set; }
        public bool LockOnPressed { get; set; }
        public bool JumpPressed { get; set; }
        public bool UltimatePressed { get; set; }
        public bool FlyPressed { get; set; }
        public bool FlyDescendPressed { get; set; }
        public bool BoostPressed { get; set; }
        public bool AimPressed { get; set; }
        public bool FirePressed { get; set; }
        public bool ViewTogglePressed { get; set; }
        public bool ZoomInPressed { get; set; }
        public bool ZoomOutPressed { get; set; }
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Expected a private field named '{fieldName}' on {target.GetType().Name}");
        field.SetValue(target, value);
    }

    // Long Active window (5s at 60fps) so the blade has many FixedUpdates to sweep through.
    private static AttackData MakeAttack(float damage)
    {
        var data = ScriptableObject.CreateInstance<AttackData>();
        SetField(data, "damage", damage);
        SetField(data, "range", 0.3f);   // deliberately tiny - a root-anchored query would whiff far targets
        SetField(data, "radius", 0.3f);
        SetField(data, "startupFrames", 0);
        SetField(data, "activeFrames", 300);
        SetField(data, "recoveryFrames", 0);
        return data;
    }

    private StubInput _input;
    private PlayerCombat _combat;
    private PlayerWeaponHitbox _hitbox;
    private Transform _bladeRoot;
    private Transform _bladeTip;
    private GameObject _attacker;

    [SetUp]
    public void SetUp()
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            Object.DestroyImmediate(root);
        }

        _attacker = new GameObject("Attacker");
        _attacker.transform.position = Vector3.zero;
        _attacker.transform.rotation = Quaternion.LookRotation(Vector3.forward);
        _attacker.AddComponent<CharacterController>().minMoveDistance = 0f;
        _input = _attacker.AddComponent<StubInput>();
        _combat = _attacker.AddComponent<PlayerCombat>();
        SetField(_combat, "inputSource", _input);
        SetField(_combat, "comboAttacks", new[] { MakeAttack(10f) });

        _bladeRoot = new GameObject("BladeRoot").transform;
        _bladeTip = new GameObject("BladeTip").transform;
        _bladeRoot.SetParent(_attacker.transform, false);
        _bladeTip.SetParent(_attacker.transform, false);
        _bladeRoot.localPosition = new Vector3(0f, 1f, 0.2f);
        _bladeTip.localPosition = new Vector3(0f, 1f, 0.5f);

        var hitboxGo = new GameObject("WeaponHitbox");
        hitboxGo.transform.SetParent(_attacker.transform, false);
        _hitbox = hitboxGo.AddComponent<PlayerWeaponHitbox>();
        SetField(_hitbox, "combat", _combat);
        SetField(_hitbox, "attackerRoot", _attacker.transform);
        SetField(_hitbox, "bladeRoot", _bladeRoot);
        SetField(_hitbox, "bladeTip", _bladeTip);
        SetField(_hitbox, "sweepRadius", 0.12f);

        // Real config: PlayerCombat hands damage to the swept hitbox, never runs its OverlapCapsule.
        SetField(_combat, "useSweptBladeHitbox", true);
        SetField(_combat, "sweptBladeHitbox", _hitbox);
    }

    private static Health MakeTarget(string name, Vector3 pos, float halfExtent)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var box = go.AddComponent<BoxCollider>();
        box.size = Vector3.one * (halfExtent * 2f);
        return go.AddComponent<Health>();
    }

    private IEnumerator DriveToActiveWindow()
    {
        _input.AttackPressed = true;
        for (int i = 0; i < 8 && _combat.CurrentPhase != AttackPhase.Active; i++)
        {
            yield return null;
        }
        Assert.AreEqual(AttackPhase.Active, _combat.CurrentPhase, "combo should be in its Active window");
    }

    // Sweep the blade line forward by `step` each FixedUpdate for `steps` steps.
    private IEnumerator SweepBladeForward(float startZ, float endZ, int steps)
    {
        for (int i = 0; i <= steps; i++)
        {
            float z = Mathf.Lerp(startZ, endZ, i / (float)steps);
            _bladeRoot.localPosition = new Vector3(0f, 1f, z - 0.15f);
            _bladeTip.localPosition = new Vector3(0f, 1f, z + 0.15f);
            yield return new WaitForFixedUpdate();
        }
    }

    [UnityTest]
    public IEnumerator SweptTip_CrossesFarTarget_HitsEvenThoughRootIsOutOfRange()
    {
        Health target = MakeTarget("FarTarget", new Vector3(0f, 1f, 2.5f), 0.35f);
        yield return DriveToActiveWindow();

        // Root stays at origin; blade line sweeps from just ahead of the player out past z=2.5.
        yield return SweepBladeForward(0.5f, 3.2f, 10);

        Assert.AreEqual(target.MaxHealth - 10f, target.CurrentHealth, 0.01f,
            "the blade tip passed through the target - a root-anchored 0.3m query never would have");
    }

    // Regression ("玩家完全傷害不到武士"): an enemy's own outgoing attack hitboxes are child
    // colliders that carry Health only in a PARENT, not on themselves. If one of those is nearer
    // than the real hurtbox it must NOT win the per-target slot and drop the whole target.
    [UnityTest]
    public IEnumerator NearerNonDamageableCollider_DoesNotBlockTheHurtboxHit()
    {
        Health target = MakeTarget("BossLike", new Vector3(0f, 1f, 2f), 0.5f); // hurtbox: BoxCollider + Health on this GO
        var ownHitbox = new GameObject("BossOwnHitbox");
        ownHitbox.transform.SetParent(target.transform, false);
        ownHitbox.transform.localPosition = new Vector3(0f, 0f, -0.6f); // sits between the player and the hurtbox
        ownHitbox.AddComponent<SphereCollider>().radius = 0.3f;         // trigger-less collider, NO Health/IDamageable on it
        float hpBefore = target.CurrentHealth;

        yield return DriveToActiveWindow();
        yield return SweepBladeForward(0.4f, 2.6f, 9); // blade crosses the own-hitbox first, then the hurtbox

        Assert.AreEqual(hpBefore - 10f, target.CurrentHealth, 0.01f,
            "the nearer non-damageable collider is ignored; the hurtbox behind it still registers");
    }

    [UnityTest]
    public IEnumerator BladePointingAway_MissesEvenPointBlank()
    {
        Health target = MakeTarget("CloseTarget", new Vector3(0f, 1f, 0.3f), 0.3f);
        yield return DriveToActiveWindow();

        // Blade held off to the player's right, never near the target in front.
        for (int i = 0; i < 12; i++)
        {
            _bladeRoot.localPosition = new Vector3(2f, 1f, 0f);
            _bladeTip.localPosition = new Vector3(3f, 1f, 0f);
            yield return new WaitForFixedUpdate();
        }

        Assert.AreEqual(target.MaxHealth, target.CurrentHealth, 0.01f,
            "the visual blade never crossed the target, so no hit despite the root being point-blank");
    }

    [UnityTest]
    public IEnumerator OneSwing_ResolvesOncePerTarget_EvenWhileTheBladeLingers()
    {
        Health target = MakeTarget("LingerTarget", new Vector3(0f, 1f, 1.2f), 0.5f);
        yield return DriveToActiveWindow();

        // Sweep in, then dwell right on top of the target for many more FixedUpdates.
        yield return SweepBladeForward(0.4f, 1.2f, 5);
        for (int i = 0; i < 15; i++)
        {
            yield return new WaitForFixedUpdate();
        }

        Assert.AreEqual(target.MaxHealth - 10f, target.CurrentHealth, 0.01f,
            "one swing = one hit per target, no per-physics-step re-tick");
    }

    [UnityTest]
    public IEnumerator OneSwing_CanHitTwoDifferentTargets()
    {
        Health a = MakeTarget("TargetA", new Vector3(-0.6f, 1f, 1.5f), 0.4f);
        Health b = MakeTarget("TargetB", new Vector3(0.6f, 1f, 1.5f), 0.4f);
        yield return DriveToActiveWindow();

        // Wide blade line swung across both.
        for (int i = 0; i <= 10; i++)
        {
            float x = Mathf.Lerp(-1.2f, 1.2f, i / 10f);
            _bladeRoot.localPosition = new Vector3(x, 1f, 1.35f);
            _bladeTip.localPosition = new Vector3(x, 1f, 1.65f);
            yield return new WaitForFixedUpdate();
        }

        Assert.AreEqual(a.MaxHealth - 10f, a.CurrentHealth, 0.01f, "target A hit");
        Assert.AreEqual(b.MaxHealth - 10f, b.CurrentHealth, 0.01f, "target B hit");
    }
}
