using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Live2DAction.Characters;
using Live2DAction.Input;
using Object = UnityEngine.Object;

// Runs the real Update loop (CharacterController.Move only actually displaces the
// GameObject when driven by Unity's own engine tick, not a manually-invoked method),
// with a fixed-orientation camera so "forward" in MoveInput has a known, checkable
// meaning in world space: confirms WASD-equivalent input actually moves Character 1
// in the matching world direction, not just that some vector math runs.
public class CharacterMovementTests
{
    private class StubInputBehaviour : MonoBehaviour, IInputCommand
    {
        public Vector2 MoveInput { get; set; }
        public bool AttackPressed { get; set; }
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

    [SetUp]
    public void SetUp()
    {
        // Other test fixtures (e.g. CameraRelativeMovementRegressionTests) load the real
        // GreyboxTest scene and, if they ran first in the same session, leave its Ground/
        // TrainingDummy/CoverBlock colliders and "Main Camera" behind - a fresh Player
        // spawned at the origin here would physically collide with that leftover geometry
        // and/or Camera.main could resolve to the wrong camera. Wipe every root object in
        // the active scene first so this fixture always starts from a truly blank scene
        // regardless of what ran before it, rather than only guarding against the one
        // specific symptom (duplicate MainCamera tag) found in earlier debugging.
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
        SetField(movement, "moveSpeed", 5f);
        SetField(movement, "acceleration", 100f); // reach target velocity almost immediately
        SetField(movement, "gravity", 0f); // isolate horizontal movement from falling
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_player);
        Object.DestroyImmediate(_camera);
    }

    // A fixed frame count isn't reliable here: headless batchmode ticks Update() with a
    // near-zero deltaTime per frame, so 30 frames might cover only ~0.01s of simulated
    // time. WaitForSecondsRealtime isn't a fix either - measured empirically, it does not
    // tick every active MonoBehaviour's Update() proportionally to the real time it waits
    // in this environment. Looping yield-return-null (which does tick Update() correctly,
    // confirmed empirically: 10 frames measured ~0.006s of Time.deltaTime against ~0.006s
    // of real elapsed time) until enough real time has actually passed is the only
    // combination that reliably accumulates the expected moveSpeed * seconds distance.
    private IEnumerator MoveForSeconds(Vector2 moveInput, float seconds)
    {
        _input.MoveInput = moveInput;
        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < seconds)
        {
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator MoveInput_Forward_MovesTowardCameraForwardWorldAxis()
    {
        Vector3 start = _player.transform.position;

        yield return MoveForSeconds(new Vector2(0f, 1f), 1f);

        Vector3 delta = _player.transform.position - start;
        Assert.Greater(delta.z, 0.4f, "Forward input should move the player in +Z (camera forward)");
        Assert.Less(Mathf.Abs(delta.x), 0.1f, "Forward input should not cause sideways drift");
    }

    [UnityTest]
    public IEnumerator MoveInput_Back_MovesOppositeCameraForwardWorldAxis()
    {
        Vector3 start = _player.transform.position;

        yield return MoveForSeconds(new Vector2(0f, -1f), 1f);

        Vector3 delta = _player.transform.position - start;
        Assert.Less(delta.z, -0.4f, "Back input should move the player in -Z");
        Assert.Less(Mathf.Abs(delta.x), 0.1f, "Back input should not cause sideways drift");
    }

    [UnityTest]
    public IEnumerator MoveInput_Left_MovesNegativeCameraRightWorldAxis()
    {
        Vector3 start = _player.transform.position;

        yield return MoveForSeconds(new Vector2(-1f, 0f), 1f);

        Vector3 delta = _player.transform.position - start;
        Assert.Less(delta.x, -0.4f, "Left input should move the player in -X");
        Assert.Less(Mathf.Abs(delta.z), 0.1f, "Left input should not cause forward/back drift");
    }

    [UnityTest]
    public IEnumerator MoveInput_Right_MovesPositiveCameraRightWorldAxis()
    {
        Vector3 start = _player.transform.position;

        yield return MoveForSeconds(new Vector2(1f, 0f), 1f);

        Vector3 delta = _player.transform.position - start;
        Assert.Greater(delta.x, 0.4f, "Right input should move the player in +X");
        Assert.Less(Mathf.Abs(delta.z), 0.1f, "Right input should not cause forward/back drift");
    }

    [UnityTest]
    public IEnumerator MoveInput_NoInput_DoesNotDrift()
    {
        Vector3 start = _player.transform.position;

        yield return MoveForSeconds(Vector2.zero, 0.25f);

        Vector3 delta = _player.transform.position - start;
        Assert.Less(delta.magnitude, 0.01f, "No input should not move the player");
    }

    [UnityTest]
    public IEnumerator MoveInput_Forward_RotatesToFaceMovementDirection()
    {
        // Start facing away from the movement direction so this can't trivially pass
        // just because the default rotation already happens to match world forward.
        _player.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
        SetField(_player.GetComponent<CharacterMovement>(), "rotationSpeedDegrees", 100000f); // snap for the test

        yield return MoveForSeconds(new Vector2(0f, 1f), 1f);

        float angleToForward = Quaternion.Angle(_player.transform.rotation, Quaternion.LookRotation(Vector3.forward, Vector3.up));
        Assert.Less(angleToForward, 5f, "Player should face world forward (+Z) after moving forward with a forward-facing camera");
    }
}
