using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
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
}
