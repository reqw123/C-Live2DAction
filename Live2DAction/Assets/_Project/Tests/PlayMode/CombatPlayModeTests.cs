using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Live2DAction.Core;
using Live2DAction.Combat;
using Live2DAction.Input;
using Object = UnityEngine.Object;

// Runs the real Unity engine loop (actual Play mode Update ticks) to verify PlayerCombat
// actually drives the attack -> Physics.OverlapSphere -> damage pipeline end to end,
// which EditMode tests cannot check since they never invoke MonoBehaviour Update callbacks.
public class CombatPlayModeTests
{
    private class StubInputBehaviour : MonoBehaviour, IInputCommand
    {
        public Vector2 MoveInput { get; set; }
        public bool AttackPressed { get; set; }
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Expected a private field named '{fieldName}' on {target.GetType().Name}");
        field.SetValue(target, value);
    }

    // Zero startup/active frames so the combo state machine reaches its hit-resolving step
    // within a handful of Update ticks regardless of how small Time.deltaTime is under
    // headless batchmode (see Docs/KNOWN_ISSUES.md on that timing quirk) - these tests are
    // about the attack -> physics -> damage wiring, not frame-data timing itself (that's
    // covered by ComboAttackStateTests in EditMode).
    private static AttackData CreateInstantHitAttackData(float damage)
    {
        var data = ScriptableObject.CreateInstance<AttackData>();
        SetField(data, "damage", damage);
        SetField(data, "range", 1f);
        SetField(data, "radius", 0.6f);
        SetField(data, "startupFrames", 0);
        SetField(data, "activeFrames", 0);
        SetField(data, "recoveryFrames", 0);
        return data;
    }

    [UnityTest]
    public IEnumerator RealEngineLoop_AttackConnectsAndDamagesTarget()
    {
        var attackerGo = new GameObject("Attacker");
        attackerGo.transform.position = Vector3.zero;
        StubInputBehaviour stubInput = attackerGo.AddComponent<StubInputBehaviour>();
        PlayerCombat combat = attackerGo.AddComponent<PlayerCombat>();

        AttackData attackData = CreateInstantHitAttackData(20f);

        SetField(combat, "inputSource", stubInput);
        SetField(combat, "comboAttacks", new[] { attackData, null, null });

        var targetGo = new GameObject("Target");
        targetGo.transform.position = attackerGo.transform.position + attackerGo.transform.forward * 1f;
        targetGo.AddComponent<SphereCollider>().radius = 0.5f;
        Health targetHealth = targetGo.AddComponent<Health>();

        yield return null; // let Awake run

        stubInput.AttackPressed = true;
        for (int i = 0; i < 5; i++)
        {
            yield return null; // step the combo state machine through Startup -> Active
        }

        Assert.AreEqual(targetHealth.MaxHealth - 20f, targetHealth.CurrentHealth);

        Object.Destroy(attackerGo);
        Object.Destroy(targetGo);
        Object.Destroy(attackData);
    }

    [UnityTest]
    public IEnumerator RealEngineLoop_LethalAttack_DisablesTarget()
    {
        var attackerGo = new GameObject("Attacker");
        attackerGo.transform.position = Vector3.zero;
        StubInputBehaviour stubInput = attackerGo.AddComponent<StubInputBehaviour>();
        PlayerCombat combat = attackerGo.AddComponent<PlayerCombat>();

        AttackData attackData = CreateInstantHitAttackData(999f);

        SetField(combat, "inputSource", stubInput);
        SetField(combat, "comboAttacks", new[] { attackData, null, null });

        var targetGo = new GameObject("Target");
        targetGo.transform.position = attackerGo.transform.position + attackerGo.transform.forward * 1f;
        targetGo.AddComponent<SphereCollider>().radius = 0.5f;
        Health targetHealth = targetGo.AddComponent<Health>();

        yield return null;

        stubInput.AttackPressed = true;
        for (int i = 0; i < 5; i++)
        {
            yield return null;
        }

        Assert.IsTrue(targetHealth.IsDead);
        Assert.IsFalse(targetGo.activeSelf);

        Object.Destroy(attackerGo);
        Object.Destroy(targetGo);
        Object.Destroy(attackData);
    }
}
