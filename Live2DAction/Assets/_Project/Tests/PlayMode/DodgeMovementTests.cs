using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Live2DAction.Characters;
using Live2DAction.Core;
using Live2DAction.Input;
using Object = UnityEngine.Object;

// Runs the real Update loop, same reasoning as CharacterMovementTests: CharacterController.Move
// only actually displaces the GameObject when driven by Unity's own engine tick.
public class DodgeMovementTests
{
    private class StubInputBehaviour : MonoBehaviour, IInputCommand
    {
        public Vector2 MoveInput { get; set; }
        public bool AttackPressed { get; set; }
        public bool DodgePressed { get; set; }
        public bool LockOnPressed { get; set; }
    }

    private GameObject _player;
    private GameObject _camera;
    private StubInputBehaviour _input;

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Expected a private field named '{fieldName}' on {target.GetType().Name}");
        field.SetValue(target, value);
    }

    private static DodgeData CreateDodgeData(float distance, int durationFrames, int invulnerabilityFrames, int cooldownFrames)
    {
        var data = ScriptableObject.CreateInstance<DodgeData>();
        SetField(data, "distance", distance);
        SetField(data, "durationFrames", durationFrames);
        SetField(data, "invulnerabilityFrames", invulnerabilityFrames);
        SetField(data, "cooldownFrames", cooldownFrames);
        return data;
    }

    [SetUp]
    public void SetUp()
    {
        // See CharacterMovementTests.SetUp for why every root object is wiped first.
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            Object.DestroyImmediate(root);
        }

        _camera = new GameObject("TestMainCamera");
        _camera.tag = "MainCamera";
        _camera.AddComponent<Camera>();
        _camera.transform.rotation = Quaternion.identity; // forward = world +Z, right = world +X

        _player = new GameObject("Player");
        _player.AddComponent<CharacterController>();
        _input = _player.AddComponent<StubInputBehaviour>();
        CharacterMovement movement = _player.AddComponent<CharacterMovement>();
        SetField(movement, "inputSource", _input);
        SetField(movement, "gravity", 0f); // isolate horizontal movement from falling
        SetField(movement, "dodgeData", CreateDodgeData(distance: 2f, durationFrames: 6, invulnerabilityFrames: 6, cooldownFrames: 12));
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_player);
        Object.DestroyImmediate(_camera);
    }

    // Same rationale as CharacterMovementTests.MoveForSeconds: headless batchmode's tiny
    // per-frame deltaTime means a fixed frame count can't be trusted to accumulate a given
    // amount of simulated time.
    private IEnumerator RunForSeconds(float seconds)
    {
        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < seconds)
        {
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator DodgePressed_WithNoMoveInput_MovesBackwardAndBecomesInvulnerable()
    {
        Vector3 start = _player.transform.position;
        CharacterMovement movement = _player.GetComponent<CharacterMovement>();

        _input.DodgePressed = true;
        yield return null; // let the dodge state machine register the press and start
        _input.DodgePressed = false;

        Assert.AreEqual(DodgePhase.Dodging, movement.CurrentDodgePhase);
        Assert.IsTrue(movement.IsDodgeInvulnerable);

        yield return RunForSeconds(0.5f); // comfortable margin over the nominal 0.1s dodge duration

        Vector3 delta = _player.transform.position - start;
        Assert.Less(delta.z, -0.1f, "Dodging with no move input should move the player backward (-Z, opposite of camera forward)");
    }

    [UnityTest]
    public IEnumerator Dodge_EndsInvulnerabilityAndReturnsToIdleAfterCooldown()
    {
        CharacterMovement movement = _player.GetComponent<CharacterMovement>();

        _input.DodgePressed = true;
        yield return null;
        _input.DodgePressed = false;

        yield return RunForSeconds(1f); // comfortably longer than duration + cooldown at real framerate

        Assert.AreEqual(DodgePhase.Idle, movement.CurrentDodgePhase);
        Assert.IsFalse(movement.IsDodgeInvulnerable);
    }

    [UnityTest]
    public IEnumerator DodgePressedRepeatedly_DoesNotChainIntoAnotherDodgeDuringCooldown()
    {
        CharacterMovement movement = _player.GetComponent<CharacterMovement>();

        // Hold the (real, edge-triggered in production but held here for the stub) input
        // down while waiting for Cooldown - a fixed small frame count isn't reliable here
        // since headless batchmode's per-frame Time.deltaTime can be tiny (see
        // CharacterMovementTests.MoveForSeconds), so bound by real elapsed time instead.
        _input.DodgePressed = true;
        float start = Time.realtimeSinceStartup;
        while (movement.CurrentDodgePhase != DodgePhase.Cooldown && Time.realtimeSinceStartup - start < 2f)
        {
            yield return null;
        }

        Assert.AreEqual(DodgePhase.Cooldown, movement.CurrentDodgePhase, "Test setup expectation: should have reached Cooldown within 2 real seconds");

        yield return null; // still holding DodgePressed = true

        Assert.AreEqual(DodgePhase.Cooldown, movement.CurrentDodgePhase, "A held/repeated press during Cooldown must not re-trigger Dodging");
    }

    [UnityTest]
    public IEnumerator Dodge_SyncsInvulnerabilityIntoAssignedHealth()
    {
        CharacterMovement movement = _player.GetComponent<CharacterMovement>();
        Health health = _player.AddComponent<Health>();
        SetField(movement, "health", health);

        yield return null; // let Health/CharacterMovement settle with no dodge active
        Assert.IsFalse(health.IsInvulnerable, "Should not be invulnerable before any dodge is triggered");

        _input.DodgePressed = true;
        yield return null;
        _input.DodgePressed = false;

        Assert.IsTrue(health.IsInvulnerable, "Health should reflect the dodge's invulnerability window");

        health.ApplyDamage(new DamageInfo(50f, Vector3.zero, Vector3.forward, null));
        Assert.AreEqual(health.MaxHealth, health.CurrentHealth, "Damage taken while dodge-invulnerable should be ignored");

        yield return RunForSeconds(1f); // comfortably longer than duration + cooldown

        Assert.IsFalse(health.IsInvulnerable, "Invulnerability should end once the dodge (and its window) is over");
    }
}
