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

// 2026-08-12: explicit user request "攻擊特效" -> clarified as hit effects (a spark/flash at
// the impact point when a hit lands). Uses a plain marker component instead of the real
// particle prefab (built by the Editor-only HitEffectSetup.cs, which PlayMode test assemblies
// shouldn't depend on) - PlayerCombat.Instantiate()-ing whatever's assigned to
// hitEffectPrefab is the behavior under test, not the specific VFX content.
public class PlayerCombatHitEffectTests
{
    private class HitEffectMarker : MonoBehaviour
    {
    }

    private class StubInputBehaviour : MonoBehaviour, IInputCommand
    {
        public Vector2 MoveInput { get; set; }
        public bool AttackPressed { get; set; }
        public bool DodgePressed { get; set; }
        public bool LockOnPressed { get; set; }
        public bool JumpPressed { get; set; }
        public bool UltimatePressed { get; set; }
        public bool FlyPressed { get; set; }
        public bool FlyDescendPressed { get; set; }
        public bool BoostPressed { get; set; } // 2026-08-20, flight system design - interface addition, stub needs it to compile
        public bool AimPressed { get; set; } // 2026-08-23, ranged weapon - interface addition, stub needs it to compile
        public bool FirePressed { get; set; }
        public bool ViewTogglePressed { get; set; } // 2026-08-23, first-person toggle - interface addition, stub needs it to compile
        public bool ZoomInPressed { get; set; } // 2026-08-23, aim-zoom controls - interface addition, stub needs it to compile
        public bool ZoomOutPressed { get; set; }
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Expected a private field named '{fieldName}' on {target.GetType().Name}");
        field.SetValue(target, value);
    }

    private static AttackData CreateInstantHitAttackData(float damage)
    {
        var data = ScriptableObject.CreateInstance<AttackData>();
        SetField(data, "damage", damage);
        SetField(data, "range", 1f);
        SetField(data, "radius", 1f);
        SetField(data, "startupFrames", 0);
        SetField(data, "activeFrames", 0);
        SetField(data, "recoveryFrames", 0);
        return data;
    }

    [SetUp]
    public void SetUp()
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            Object.DestroyImmediate(root);
        }
    }

    [UnityTest]
    public IEnumerator LandedHit_InstantiatesHitEffectAtImpactPoint()
    {
        var attacker = new GameObject("Attacker");
        attacker.transform.position = Vector3.zero;
        attacker.AddComponent<CharacterController>().minMoveDistance = 0f;
        var stub = attacker.AddComponent<StubInputBehaviour>();
        PlayerCombat combat = attacker.AddComponent<PlayerCombat>();
        SetField(combat, "inputSource", stub);
        SetField(combat, "comboAttacks", new[] { CreateInstantHitAttackData(10f) });

        // Left active (unlike a real saved prefab asset, which typically isn't itself a live
        // scene object) - Instantiate() preserves the source's active state on the clone, and
        // an inactive source produces an inactive clone that FindObjectsByType's default
        // (FindObjectsInactive.Exclude) would silently miss, which is exactly what a first
        // version of this test did (falsely looked like PlayerCombat wasn't spawning anything).
        var markerPrefab = new GameObject("HitEffectMarker");
        markerPrefab.AddComponent<HitEffectMarker>();
        SetField(combat, "hitEffectPrefab", markerPrefab);

        var target = new GameObject("Target");
        target.transform.position = attacker.transform.position + Vector3.forward * 0.5f;
        target.AddComponent<SphereCollider>().radius = 0.5f;
        target.AddComponent<Health>();

        // The prefab template itself carries the marker component and is active (see comment
        // above), so it's already found once here - the assertion after the attack checks for
        // exactly one MORE instance, not an absolute count.
        int markerCountBeforeAttack = Object.FindObjectsByType<HitEffectMarker>(FindObjectsSortMode.None).Length;

        stub.AttackPressed = true;
        // ComboAttackState needs multiple ticks even with 0 startup/active frames - Idle ->
        // Startup happens on the frame AttackPressed is first read, Startup -> Active on the
        // next, and the hit only resolves on the tick where it's already Active (see
        // ComboAttackState.Tick - each transition is its own switch-case, one per call).
        for (int i = 0; i < 5; i++)
        {
            yield return null;
        }

        HitEffectMarker[] allMarkers = Object.FindObjectsByType<HitEffectMarker>(FindObjectsSortMode.None);
        Assert.AreEqual(markerCountBeforeAttack + 1, allMarkers.Length, "Landing a hit should spawn exactly one new hit effect instance");

        HitEffectMarker spawnedInstance = System.Array.Find(allMarkers, m => m.gameObject != markerPrefab);
        Assert.IsNotNull(spawnedInstance, "The new instance should be a clone, not the template itself");

        float distanceFromTarget = Vector3.Distance(spawnedInstance.transform.position, target.transform.position);
        Assert.Less(distanceFromTarget, 1f, "The hit effect should spawn near the actual impact point, not at the origin or some unrelated position");

        Object.Destroy(attacker);
        Object.Destroy(target);
        Object.Destroy(markerPrefab);
    }

    [UnityTest]
    public IEnumerator NoHitEffectAssigned_DoesNotThrow()
    {
        var attacker = new GameObject("Attacker");
        attacker.AddComponent<CharacterController>().minMoveDistance = 0f;
        var stub = attacker.AddComponent<StubInputBehaviour>();
        PlayerCombat combat = attacker.AddComponent<PlayerCombat>();
        SetField(combat, "inputSource", stub);
        SetField(combat, "comboAttacks", new[] { CreateInstantHitAttackData(10f) });
        // hitEffectPrefab deliberately left unassigned (null) - should just skip spawning.

        var target = new GameObject("Target");
        target.transform.position = attacker.transform.position + Vector3.forward * 0.5f;
        target.AddComponent<SphereCollider>().radius = 0.5f;
        Health targetHealth = target.AddComponent<Health>();

        stub.AttackPressed = true;
        // ComboAttackState needs multiple ticks even with 0 startup/active frames - Idle ->
        // Startup happens on the frame AttackPressed is first read, Startup -> Active on the
        // next, and the hit only resolves on the tick where it's already Active (see
        // ComboAttackState.Tick - each transition is its own switch-case, one per call).
        for (int i = 0; i < 5; i++)
        {
            yield return null;
        }

        Assert.AreEqual(targetHealth.MaxHealth - 10f, targetHealth.CurrentHealth, "Damage should still apply even with no hit effect assigned");

        Object.Destroy(attacker);
        Object.Destroy(target);
    }
}
