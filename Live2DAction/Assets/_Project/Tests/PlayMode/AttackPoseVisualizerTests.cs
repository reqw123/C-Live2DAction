using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Live2DAction.Characters;
using Live2DAction.Combat;
using Live2DAction.Input;
using Object = UnityEngine.Object;

// Runs the real Unity engine loop to verify AttackPoseVisualizer actually rotates its
// swingTransform once an attack reaches its Active/Recovery phases, not just that the pure
// AttackPoseUtility math is correct in isolation (covered by AttackPoseUtilityTests).
public class AttackPoseVisualizerTests
{
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
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Expected a private field named '{fieldName}' on {target.GetType().Name}");
        field.SetValue(target, value);
    }

    // Zero startup/active frames so the combo state machine reaches Active within a handful
    // of Update ticks regardless of headless batchmode's tiny Time.deltaTime (see
    // Docs/KNOWN_ISSUES.md), matching the existing pattern in CombatPlayModeTests.
    private static AttackData CreateInstantHitAttackData()
    {
        var data = ScriptableObject.CreateInstance<AttackData>();
        SetField(data, "range", 1f);
        SetField(data, "radius", 0.6f);
        SetField(data, "startupFrames", 0);
        SetField(data, "activeFrames", 0);
        SetField(data, "recoveryFrames", 4);
        return data;
    }

    [UnityTest]
    public IEnumerator RealEngineLoop_DuringAttack_SwingTransformRotatesAwayFromIdentity()
    {
        var attackerGo = new GameObject("Attacker");
        StubInputBehaviour stubInput = attackerGo.AddComponent<StubInputBehaviour>();
        PlayerCombat combat = attackerGo.AddComponent<PlayerCombat>();
        AttackData attackData = CreateInstantHitAttackData();
        SetField(combat, "inputSource", stubInput);
        SetField(combat, "comboAttacks", new[] { attackData, null, null });

        var swingGo = new GameObject("Swing");
        swingGo.transform.SetParent(attackerGo.transform);

        AttackPoseVisualizer visualizer = attackerGo.AddComponent<AttackPoseVisualizer>();
        SetField(visualizer, "combatSource", combat);
        SetField(visualizer, "swingTransform", swingGo.transform);
        SetField(visualizer, "swingAxis", Vector3.right);
        SetField(visualizer, "windUpAngleDegrees", 20f);
        SetField(visualizer, "swingAngleDegrees", 60f);

        yield return null; // let Awake run

        stubInput.AttackPressed = true;
        bool sawRotation = false;
        for (int i = 0; i < 6; i++)
        {
            yield return null;
            float angleFromIdentity = Quaternion.Angle(Quaternion.identity, swingGo.transform.localRotation);
            if (angleFromIdentity > 1f)
            {
                sawRotation = true;
            }
        }

        Assert.IsTrue(sawRotation, "Expected swingTransform to visibly rotate away from its baseline while the attack is Active/Recovery");

        Object.Destroy(attackerGo);
        Object.Destroy(attackData);
    }

    [UnityTest]
    public IEnumerator RealEngineLoop_NoAttackPressed_SwingTransformStaysAtIdentity()
    {
        var attackerGo = new GameObject("Attacker");
        StubInputBehaviour stubInput = attackerGo.AddComponent<StubInputBehaviour>();
        PlayerCombat combat = attackerGo.AddComponent<PlayerCombat>();
        AttackData attackData = CreateInstantHitAttackData();
        SetField(combat, "inputSource", stubInput);
        SetField(combat, "comboAttacks", new[] { attackData, null, null });

        var swingGo = new GameObject("Swing");
        swingGo.transform.SetParent(attackerGo.transform);

        AttackPoseVisualizer visualizer = attackerGo.AddComponent<AttackPoseVisualizer>();
        SetField(visualizer, "combatSource", combat);
        SetField(visualizer, "swingTransform", swingGo.transform);
        SetField(visualizer, "swingAxis", Vector3.right);
        SetField(visualizer, "windUpAngleDegrees", 20f);
        SetField(visualizer, "swingAngleDegrees", 60f);

        for (int i = 0; i < 5; i++)
        {
            yield return null;
        }

        float angleFromIdentity = Quaternion.Angle(Quaternion.identity, swingGo.transform.localRotation);
        Assert.AreEqual(0f, angleFromIdentity, 0.01f, "With no attack ever pressed, swingTransform should never move from its baseline");

        Object.Destroy(attackerGo);
        Object.Destroy(attackData);
    }
}
