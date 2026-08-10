using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Live2DAction.AI;
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
        _enemy.AddComponent<CharacterController>();
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
        yield return RunForSeconds(1.5f); // generous margin - see CharacterMovementTests on headless batchmode's variable integration efficiency

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
}
