using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Live2DAction.Characters;
using Live2DAction.Input;
using Object = UnityEngine.Object;

// Runs the real Update loop, same reasoning as CharacterMovementTests: CharacterController.Move
// only actually displaces the GameObject when driven by Unity's own engine tick.
public class JumpTests
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

    private GameObject _player;
    private StubInputBehaviour _input;

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Expected a private field named '{fieldName}' on {target.GetType().Name}");
        field.SetValue(target, value);
    }

    [SetUp]
    public void SetUp()
    {
        // See CharacterMovementTests.SetUp for why every root object is wiped first.
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            Object.DestroyImmediate(root);
        }

        _player = new GameObject("Player");
        CharacterController controller = _player.AddComponent<CharacterController>();
        // See CharacterMovementTests.SetUp for why this matters - default 0.001 silently
        // drops sub-threshold Move() calls at the frame rates headless batchmode can hit.
        controller.minMoveDistance = 0f;
        _input = _player.AddComponent<StubInputBehaviour>();
        CharacterMovement movement = _player.AddComponent<CharacterMovement>();
        SetField(movement, "inputSource", _input);
        SetField(movement, "jumpSpeed", 7f);
        // Real gravity here (unlike most other movement tests) - jumping needs it to arc back
        // down, and isGrounded needs a real floor under the character to ever read true.
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.position = new Vector3(0f, -0.5f, 0f);
        ground.transform.localScale = new Vector3(10f, 1f, 10f);
        // Ground's top face is at world Y=0 (position -0.5 + half-scale 0.5). Spawn Y must be
        // groundTopY + controller.center.y + controller.height/2 - the CharacterController
        // just added above keeps Unity's own default height (2) and center (0,0,0), so that's
        // 0 + 0 + 1 = 1, not a flat 0.5 (which sinks the capsule half a unit into the floor).
        // This is exactly the "height/spawn Y mismatch" bug this project has hit for both
        // Player and Enemy before (see GreyboxSceneBuilder's CreatePlayer/CreateEnemy
        // comments and FixPlayerGroundedSpawn.cs/FixEnemyGroundedSpawn.cs) - isGrounded never
        // reads true while still overlapping the floor, so gravity accumulates unbounded
        // before it resolves, occasionally still dragging the player below its start position
        // by the time JumpPressed_WhileGrounded's short "let isGrounded settle" window ends
        // (intermittent, timing-dependent - not something last-minute-tuning the wait fixes).
        _player.transform.position = new Vector3(0f, 1f, 0f);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            Object.DestroyImmediate(root);
        }
    }

    private IEnumerator RunForSeconds(float seconds)
    {
        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < seconds)
        {
            yield return null;
        }
    }

    // 2026-08-16: replaces a blind RunForSeconds(0.05f) "let isGrounded settle" wait, which was
    // the documented source of this test's intermittent failure (see SetUp's own comment) - a
    // fixed real-time budget doesn't guarantee isGrounded has actually turned true yet (its
    // first read is always false, before any Move() call has swept the capsule against the
    // floor), so on a slow/first frame gravity could still be accumulating unchecked when
    // startY got captured, occasionally already well below the real resting position. Polling
    // the actual precondition instead of guessing a duration removes that race outright; the
    // timeout is just a safety net so a genuine regression fails fast with a clear message
    // instead of hanging.
    private IEnumerator WaitUntilGrounded(float timeoutSeconds = 1f)
    {
        CharacterController controller = _player.GetComponent<CharacterController>();
        float start = Time.realtimeSinceStartup;
        while (!controller.isGrounded)
        {
            Assert.Less(Time.realtimeSinceStartup - start, timeoutSeconds,
                "Player never reported isGrounded=true within the timeout - check the test's Ground collider setup.");
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator JumpPressed_WhileGrounded_LiftsPlayerUpward()
    {
        yield return WaitUntilGrounded();
        float startY = _player.transform.position.y;

        _input.JumpPressed = true;
        yield return RunForSeconds(0.02f); // hold across at least one real Update tick
        _input.JumpPressed = false;

        yield return RunForSeconds(0.1f);

        Assert.Greater(_player.transform.position.y, startY + 0.05f,
            "Pressing jump while grounded should lift the player upward.");
    }

    [UnityTest]
    public IEnumerator JumpPressed_WhileAirborne_DoesNotDoubleJump()
    {
        yield return WaitUntilGrounded();
        float startY = _player.transform.position.y;

        _input.JumpPressed = true;
        yield return RunForSeconds(0.02f); // hold across at least one real Update tick
        _input.JumpPressed = false;

        yield return RunForSeconds(0.05f); // now airborne, mid-arc

        float yBeforeSecondJump = _player.transform.position.y;
        Assert.Greater(yBeforeSecondJump, startY + 0.05f,
            "Test precondition failed: the first jump should already have lifted the player off the ground.");

        _input.JumpPressed = true; // pressed again while still in the air
        yield return RunForSeconds(0.02f);
        _input.JumpPressed = false;

        // A double jump would produce a sharp new upward velocity; without one, the character
        // is just continuing its existing (by now likely still-ascending, not yet descending -
        // jumpSpeed=7/gravity=-20 means it's still well short of the arc's peak this early)
        // arc. 0.35 (not the old 0.05) is the tolerance: this early in the arc, natural
        // continuation alone covers roughly 0.2-0.25 units in this final 0.04s window - 0.05
        // was miscalibrated against a since-fixed bug (CharacterController.minMoveDistance's
        // default was silently dropping most Move() calls at headless batchmode's frame rate,
        // see CharacterMovementTests.SetUp, which coincidentally suppressed enough of this
        // natural continuation to fit under the old tight tolerance). A real double-jump burst
        // adds another full jumpSpeed kick on top of that, which this margin still comfortably
        // catches.
        yield return RunForSeconds(0.02f);
        float yRightAfterSecondPress = _player.transform.position.y;

        Assert.LessOrEqual(yRightAfterSecondPress, yBeforeSecondJump + 0.35f,
            "A second jump press while airborne should not add another upward burst.");
    }
}
