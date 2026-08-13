using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Live2DAction.AI;
using Live2DAction.Combat;
using Object = UnityEngine.Object;

// Runs the real Update loop, same reasoning as CharacterMovementTests: CharacterController.Move
// only actually displaces the GameObject when driven by Unity's own engine tick.
public class EnemyAITests
{
    private GameObject _enemy;
    private GameObject _target;

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Expected a private field named '{fieldName}' on {target.GetType().Name}");
        field.SetValue(target, value);
    }

    [SetUp]
    public void SetUp()
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            Object.DestroyImmediate(root);
        }

        _enemy = new GameObject("Enemy");
        CharacterController controller = _enemy.AddComponent<CharacterController>();
        // See CharacterMovementTests.SetUp for why this matters - default 0.001 silently
        // drops sub-threshold Move() calls at the frame rates headless batchmode can hit.
        controller.minMoveDistance = 0f;
        EnemyAI ai = _enemy.AddComponent<EnemyAI>();
        SetField(ai, "detectionRange", 8f);
        SetField(ai, "attackRange", 2f);
        SetField(ai, "moveSpeed", 3f);
        SetField(ai, "gravity", 0f); // isolate horizontal movement from falling

        _target = new GameObject("Target");
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_enemy);
        Object.DestroyImmediate(_target);
    }

    private IEnumerator RunForSeconds(float seconds)
    {
        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < seconds)
        {
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator TargetBeyondDetectionRange_StaysIdleAndDoesNotMove()
    {
        _target.transform.position = new Vector3(0f, 0f, 50f);
        EnemyAI ai = _enemy.GetComponent<EnemyAI>();
        SetField(ai, "target", _target.transform);

        Vector3 start = _enemy.transform.position;
        yield return RunForSeconds(0.3f);

        Assert.AreEqual(EnemyState.Idle, ai.CurrentState);
        Assert.IsFalse(ai.AttackPressed);
        Assert.Less(Vector3.Distance(start, _enemy.transform.position), 0.05f, "Enemy should not move while idle");
    }

    [UnityTest]
    public IEnumerator TargetWithinDetectionRange_ChasesTowardTarget()
    {
        _target.transform.position = new Vector3(0f, 0f, 5f);
        EnemyAI ai = _enemy.GetComponent<EnemyAI>();
        SetField(ai, "target", _target.transform);

        Vector3 start = _enemy.transform.position;
        // 0.5s at moveSpeed=3 covers ~1.5 units - enough to show forward movement while still
        // 1.5 units short of entering attackRange (2), i.e. still mid-chase. 2026-08-12:
        // previously 1.5s "generous margin" - that assumption dates from when
        // CharacterController.minMoveDistance's default (0.001) was silently dropping most
        // Move() calls at headless batchmode's frame rate (see CharacterMovementTests.SetUp),
        // crippling movement enough that the enemy never actually covered the 3 units needed
        // to reach attackRange even in 1.5s. Now that's fixed, movement works at full speed,
        // so 1.5s overshoots straight into Attacking - this was only ever "passing" by
        // accident.
        yield return RunForSeconds(0.5f);

        Vector3 delta = _enemy.transform.position - start;
        Assert.Greater(delta.z, 0.05f, "Enemy should move toward the target (+Z)");
        Assert.AreEqual(EnemyState.Chasing, ai.CurrentState);
    }

    [UnityTest]
    public IEnumerator TargetWithinAttackRange_StopsAndSetsAttackPressed()
    {
        _target.transform.position = new Vector3(0f, 0f, 1f); // within attackRange (2)
        EnemyAI ai = _enemy.GetComponent<EnemyAI>();
        SetField(ai, "target", _target.transform);

        yield return null;

        Assert.AreEqual(EnemyState.Attacking, ai.CurrentState);
        Assert.IsTrue(ai.AttackPressed);
    }

    [UnityTest]
    public IEnumerator NoTarget_StaysIdleAndDoesNotThrow()
    {
        // target left unassigned (null)
        EnemyAI ai = _enemy.GetComponent<EnemyAI>();

        yield return null;

        Assert.AreEqual(EnemyState.Idle, ai.CurrentState);
        Assert.IsFalse(ai.AttackPressed);
    }

    [UnityTest]
    public IEnumerator TargetWithinAttackRange_RotatesToFaceTarget()
    {
        _target.transform.position = new Vector3(1f, 0f, 0f); // to the enemy's +X side
        EnemyAI ai = _enemy.GetComponent<EnemyAI>();
        SetField(ai, "target", _target.transform);
        SetField(ai, "rotationSpeedDegrees", 100000f); // snap for the test
        _enemy.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);

        // A single frame isn't reliably enough to complete the snap: headless batchmode's
        // per-frame Time.deltaTime can be tiny (see CharacterMovementTests.MoveForSeconds),
        // so bound by real elapsed time instead of a fixed frame count.
        yield return RunForSeconds(0.2f);

        float angleToTarget = Quaternion.Angle(_enemy.transform.rotation, Quaternion.LookRotation(Vector3.right, Vector3.up));
        Assert.Less(angleToTarget, 5f, "Enemy should face the target even while stationary in attack range");
    }

    // Real 2026-08-13 bug report: "我已經盡到敵人範圍內，線條從紅色變成黃色，但敵人尚未作出攻
    // 擊，這代表視覺呈現與數值邏輯判定很明顯不一致" - PlayerCombat's Gizmo (and the real hit
    // judgment) answer "in range" using the capsule's true forward reach (Range+Radius), but
    // EnemyAI's own Attacking decision used a stale, separately-tuned attackRange float that
    // had drifted smaller than Range+Radius. This test positions the target beyond the naive
    // attackRange (2, unset here on purpose - left at the class default) but within the true
    // capsule reach (Range=2, Radius=1 -> 3), with "combat" wired, and asserts Attacking is
    // still reached - proving the fix actually closes the gap the user hit.
    [UnityTest]
    public IEnumerator TargetBeyondAttackRangeButWithinCapsuleReach_StillAttacksWhenCombatWired()
    {
        _target.transform.position = new Vector3(0f, 0f, 2.5f); // beyond attackRange(2), within Range(2)+Radius(1)=3
        EnemyAI ai = _enemy.GetComponent<EnemyAI>();
        SetField(ai, "target", _target.transform);

        PlayerCombat combat = _enemy.AddComponent<PlayerCombat>();
        var attack = ScriptableObject.CreateInstance<AttackData>();
        SetField(attack, "range", 2f);
        SetField(attack, "radius", 1f);
        SetField(combat, "comboAttacks", new[] { attack });
        SetField(ai, "combat", combat);

        yield return null;

        Assert.AreEqual(EnemyState.Attacking, ai.CurrentState, "Should attack once combat's actual Range+Radius covers the target, even beyond the fallback attackRange");
        Assert.IsTrue(ai.AttackPressed);
    }

    // Sanity check that the fallback still applies when "combat" isn't wired (e.g. an enemy
    // with no PlayerCombat at all, or an isolated test that only cares about attackRange) -
    // same target position as above, but this time it should NOT be close enough to attack,
    // since the fallback attackRange(2) doesn't reach 2.5.
    [UnityTest]
    public IEnumerator TargetBeyondAttackRange_WithoutCombatWired_StaysChasing()
    {
        _target.transform.position = new Vector3(0f, 0f, 2.5f);
        EnemyAI ai = _enemy.GetComponent<EnemyAI>();
        SetField(ai, "target", _target.transform);

        yield return null;

        Assert.AreEqual(EnemyState.Chasing, ai.CurrentState, "Without combat wired, the fallback attackRange(2) should still gate attacking");
        Assert.IsFalse(ai.AttackPressed);
    }
}
