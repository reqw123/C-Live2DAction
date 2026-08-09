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

    [UnityTest]
    public IEnumerator RealEngineLoop_AttackConnectsAndDamagesTarget()
    {
        var attackerGo = new GameObject("Attacker");
        attackerGo.transform.position = Vector3.zero;
        StubInputBehaviour stubInput = attackerGo.AddComponent<StubInputBehaviour>();
        PlayerCombat combat = attackerGo.AddComponent<PlayerCombat>();

        AttackData attackData = ScriptableObject.CreateInstance<AttackData>();
        SetField(attackData, "damage", 20f);
        SetField(attackData, "range", 1f);
        SetField(attackData, "radius", 0.6f);

        SetField(combat, "inputSource", stubInput);
        SetField(combat, "attackData", attackData);

        var targetGo = new GameObject("Target");
        targetGo.transform.position = attackerGo.transform.position + attackerGo.transform.forward * 1f;
        targetGo.AddComponent<SphereCollider>().radius = 0.5f;
        Health targetHealth = targetGo.AddComponent<Health>();

        yield return null; // let Awake run

        stubInput.AttackPressed = true;
        yield return null; // let Update() perform the attack

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

        AttackData attackData = ScriptableObject.CreateInstance<AttackData>();
        SetField(attackData, "damage", 999f);
        SetField(attackData, "range", 1f);
        SetField(attackData, "radius", 0.6f);

        SetField(combat, "inputSource", stubInput);
        SetField(combat, "attackData", attackData);

        var targetGo = new GameObject("Target");
        targetGo.transform.position = attackerGo.transform.position + attackerGo.transform.forward * 1f;
        targetGo.AddComponent<SphereCollider>().radius = 0.5f;
        Health targetHealth = targetGo.AddComponent<Health>();

        yield return null;

        stubInput.AttackPressed = true;
        yield return null;

        Assert.IsTrue(targetHealth.IsDead);
        Assert.IsFalse(targetGo.activeSelf);

        Object.Destroy(attackerGo);
        Object.Destroy(targetGo);
        Object.Destroy(attackData);
    }
}
