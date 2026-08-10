using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Live2DAction.CameraSystem;
using Live2DAction.Characters;
using Live2DAction.Input;
using Live2DAction.Targeting;
using Object = UnityEngine.Object;

// Runs the real Update/LateUpdate loop to verify CharacterMovement and
// ThirdPersonCameraController actually react to a locked target, not just that the
// underlying TargetLockUtility math is correct in isolation (covered by
// TargetLockUtilityTests in EditMode).
public class LockOnFacingAndCameraTests
{
    private class StubInputBehaviour : MonoBehaviour, IInputCommand
    {
        public Vector2 MoveInput { get; set; }
        public bool AttackPressed { get; set; }
        public bool DodgePressed { get; set; }
        public bool LockOnPressed { get; set; }
    }

    private class StubLockOnSource : MonoBehaviour, ILockOnSource
    {
        public Transform LockedTarget { get; set; }
    }

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
    }

    [UnityTest]
    public IEnumerator CharacterMovement_WithLockedTarget_FacesTargetEvenWithoutMoveInput()
    {
        var camera = new GameObject("TestMainCamera");
        camera.tag = "MainCamera";
        camera.AddComponent<Camera>();
        camera.transform.rotation = Quaternion.identity;

        var player = new GameObject("Player");
        player.AddComponent<CharacterController>();
        var input = player.AddComponent<StubInputBehaviour>();
        var lockOnSource = player.AddComponent<StubLockOnSource>();
        CharacterMovement movement = player.AddComponent<CharacterMovement>();
        SetField(movement, "inputSource", input);
        SetField(movement, "gravity", 0f);
        SetField(movement, "rotationSpeedDegrees", 100000f); // snap for the test
        SetField(movement, "lockOnSource", lockOnSource);

        var target = new GameObject("Target");
        target.transform.position = new Vector3(5f, 0f, 0f); // to the player's +X side
        lockOnSource.LockedTarget = target.transform;

        // Face away from the target first so this can't trivially pass by coincidence.
        player.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);

        yield return null; // no move input at all this frame

        float angleToTarget = Quaternion.Angle(player.transform.rotation, Quaternion.LookRotation(Vector3.right, Vector3.up));
        Assert.Less(angleToTarget, 5f, "With a locked target and zero move input, the player should still turn to face it");

        Object.Destroy(player);
        Object.Destroy(camera);
        Object.Destroy(target);
    }

    [UnityTest]
    public IEnumerator ThirdPersonCameraController_WithLockedTarget_YawPitchMatchTargetDirection()
    {
        var target = new GameObject("Player");
        target.transform.position = Vector3.zero;

        var lockOnSourceGo = new GameObject("LockOnSource");
        var lockOnSource = lockOnSourceGo.AddComponent<StubLockOnSource>();

        var lockedTargetGo = new GameObject("Enemy");
        lockedTargetGo.transform.position = new Vector3(3f, 1f, 4f);
        lockOnSource.LockedTarget = lockedTargetGo.transform;

        var cameraGo = new GameObject("Camera");
        ThirdPersonCameraController controller = cameraGo.AddComponent<ThirdPersonCameraController>();
        SetField(controller, "target", target.transform);
        SetField(controller, "lockOnSource", lockOnSource);
        SetField(controller, "minPitch", -60f);
        SetField(controller, "maxPitch", 60f);

        yield return null; // let LateUpdate run with the lock already in place

        TargetLockUtility.ComputeLockOnYawPitch(target.transform.position, lockedTargetGo.transform.position, -60f, 60f, out float expectedYaw, out _);

        Assert.AreEqual(expectedYaw, controller.YawDegrees, 0.01f);

        // Camera should actually be looking toward the locked target's position.
        Vector3 cameraForward = controller.transform.rotation * Vector3.forward;
        Vector3 expectedDirection = (lockedTargetGo.transform.position - target.transform.position).normalized;
        Assert.Greater(Vector3.Dot(cameraForward, expectedDirection), 0.99f, "Camera should be looking toward the locked target");

        Object.Destroy(target);
        Object.Destroy(lockOnSourceGo);
        Object.Destroy(lockedTargetGo);
        Object.Destroy(cameraGo);
    }
}
