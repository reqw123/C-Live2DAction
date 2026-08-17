using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Live2DAction.Input;
using Live2DAction.Targeting;
using Object = UnityEngine.Object;

// Runs the real Update loop so Object.FindObjectsByType<LockOnTarget> and the
// press/auto-unlock logic actually execute, which EditMode tests can't check.
public class TargetLockControllerTests
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

    private GameObject _playerGo;
    private StubInputBehaviour _input;
    private TargetLockController _controller;
    private GameObject _targetGo;

    [SetUp]
    public void SetUp()
    {
        // TargetLockController.FindTarget() scans the whole loaded scene
        // (FindObjectsByType<LockOnTarget>), so a previous test that left the real GreyboxTest
        // scene loaded (e.g. anything using SceneManager.LoadScene("GreyboxTest") -
        // WanderMovementTests, CharacterCollisionBlockingTests, MovementFrameTimingTests,
        // CameraRelativeMovementRegressionTests) would leave extra LockOnTarget candidates
        // (Player2, TrainingDummy) lying around and break this fixture's "exactly one
        // candidate" assumptions. Same rationale as CharacterMovementTests.SetUp - wipe every
        // root object first so this fixture always starts from a truly blank scene.
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            Object.DestroyImmediate(root);
        }

        _playerGo = new GameObject("Player");
        _input = _playerGo.AddComponent<StubInputBehaviour>();
        _controller = _playerGo.AddComponent<TargetLockController>();
        SetField(_controller, "inputSource", _input);
        SetField(_controller, "maxLockRange", 10f);
        SetField(_controller, "maxLockAngleDegrees", 60f);
        SetField(_controller, "breakRange", 15f);

        _targetGo = new GameObject("Dummy");
        _targetGo.transform.position = new Vector3(0f, 0f, 3f);
        _targetGo.AddComponent<LockOnTarget>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_playerGo);
        if (_targetGo != null)
        {
            Object.DestroyImmediate(_targetGo);
        }
    }

    [UnityTest]
    public IEnumerator LockOnPressed_WithCandidateInRange_LocksOn()
    {
        _input.LockOnPressed = true;
        yield return null;

        Assert.IsTrue(_controller.IsLocked);
        Assert.AreSame(_targetGo.transform, _controller.LockedTarget);
    }

    [UnityTest]
    public IEnumerator LockOnPressedTwice_TogglesOff()
    {
        _input.LockOnPressed = true;
        yield return null;
        Assert.IsTrue(_controller.IsLocked);

        _input.LockOnPressed = false;
        yield return null; // release
        _input.LockOnPressed = true;
        yield return null; // press again

        Assert.IsFalse(_controller.IsLocked);
    }

    [UnityTest]
    public IEnumerator LockedTargetDeactivated_AutoUnlocksNextFrame()
    {
        _input.LockOnPressed = true;
        yield return null;
        Assert.IsTrue(_controller.IsLocked);

        _input.LockOnPressed = false;
        _targetGo.SetActive(false);
        yield return null;

        Assert.IsFalse(_controller.IsLocked, "A deactivated (e.g. dead) target should auto-unlock");
    }

    [UnityTest]
    public IEnumerator LockOnPressed_NoCandidates_StaysUnlocked()
    {
        Object.DestroyImmediate(_targetGo);
        _targetGo = null;

        _input.LockOnPressed = true;
        yield return null;

        Assert.IsFalse(_controller.IsLocked);
    }

    // 2026-08-13, explicit user request ("目前鎖定目標需要角色去面對敵人，能不能改為鼠標鏡頭面相
    // 來判斷?") - acquisition should go by wherever viewOrigin (wired to Main Camera in the real
    // scene, see LockOnViewSourceSetup.cs) is facing, not the character's own facing. Proves
    // this with the two deliberately pointed in opposite directions: the character faces AWAY
    // from the candidate (so this can't pass by the old default-to-transform.forward behavior
    // by coincidence) while viewOrigin faces toward it, and the target still gets acquired.
    [UnityTest]
    public IEnumerator LockOnPressed_ViewOriginFacesCandidateButCharacterFacesAway_StillLocksOn()
    {
        _playerGo.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up); // character faces -Z, candidate is at +Z

        var cameraGo = new GameObject("StubCamera");
        cameraGo.transform.rotation = Quaternion.identity; // faces +Z, toward the candidate
        SetField(_controller, "viewOrigin", cameraGo.transform);

        _input.LockOnPressed = true;
        yield return null;

        Assert.IsTrue(_controller.IsLocked, "Should acquire the candidate the camera (viewOrigin) faces, even though the character itself faces away");
        Assert.AreSame(_targetGo.transform, _controller.LockedTarget);

        Object.DestroyImmediate(cameraGo);
    }
}
