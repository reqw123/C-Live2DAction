using NUnit.Framework;
using UnityEngine;
using Live2DAction.CameraSystem;

public class ThirdPersonCameraControllerTests
{
    [Test]
    public void ComputeCameraPosition_SitsBehindTargetAtDistance()
    {
        Vector3 targetPosition = Vector3.zero;
        Vector3 targetOffset = new Vector3(0f, 1.4f, 0f);
        Quaternion rotation = Quaternion.identity; // looking down world +Z

        Vector3 position = ThirdPersonCameraController.ComputeCameraPosition(targetPosition, rotation, distance: 4f, targetOffset: targetOffset);

        Vector3 expected = targetOffset - Vector3.forward * 4f;
        Assert.AreEqual(expected, position);
    }

    [Test]
    public void ComputeCameraPosition_MovesWithTargetPosition()
    {
        Vector3 targetOffset = new Vector3(0f, 1.4f, 0f);
        Quaternion rotation = Quaternion.identity;

        Vector3 positionAtOrigin = ThirdPersonCameraController.ComputeCameraPosition(Vector3.zero, rotation, 4f, targetOffset);
        Vector3 positionOffset = ThirdPersonCameraController.ComputeCameraPosition(new Vector3(5f, 0f, 0f), rotation, 4f, targetOffset);

        Assert.AreEqual(positionAtOrigin + new Vector3(5f, 0f, 0f), positionOffset);
    }

    [Test]
    public void ComputeCameraPosition_SameTargetPosition_IsIndependentOfRotationChoiceUsedElsewhere()
    {
        // Sanity check that the fixed rotation is applied consistently regardless of target
        // position - a 45-degree downward pitch should place the camera up and back from the
        // target by the same amount whether the target sits at the origin or elsewhere.
        Vector3 targetOffset = Vector3.zero;
        Quaternion rotation = Quaternion.Euler(45f, 0f, 0f);

        Vector3 positionAtOrigin = ThirdPersonCameraController.ComputeCameraPosition(Vector3.zero, rotation, 8f, targetOffset);
        Vector3 positionElsewhere = ThirdPersonCameraController.ComputeCameraPosition(new Vector3(2f, 0f, -3f), rotation, 8f, targetOffset);

        Assert.AreEqual(positionAtOrigin + new Vector3(2f, 0f, -3f), positionElsewhere);
    }

    [Test]
    public void ComputeAutoCenterYaw_MovesTowardTargetWithoutOvershooting()
    {
        float yaw = 0f;
        for (int i = 0; i < 200; i++)
        {
            yaw = ThirdPersonCameraController.ComputeAutoCenterYaw(yaw, targetYaw: 90f, autoCenterSpeed: 2f, deltaTime: 0.016f);
        }

        Assert.Less(Mathf.Abs(Mathf.DeltaAngle(yaw, 90f)), 0.5f, "Repeated auto-center steps should converge on the target yaw");
    }

    [Test]
    public void ComputeAutoCenterYaw_AlreadyAtTarget_StaysPut()
    {
        float yaw = ThirdPersonCameraController.ComputeAutoCenterYaw(currentYaw: 40f, targetYaw: 40f, autoCenterSpeed: 2f, deltaTime: 0.016f);

        Assert.AreEqual(40f, yaw, 0.001f);
    }

    [Test]
    public void ComputeAutoCenterYaw_WrapsAcrossThe360DegreeBoundaryTheShortWay()
    {
        // 350 -> 10 is a 20-degree gap the short way around (through 360/0), not 340 degrees
        // the long way - LerpAngle should take the short path, same as everywhere else in
        // this codebase that eases an angle (e.g. CharacterMovement's SmoothDampAngle turns).
        float yaw = ThirdPersonCameraController.ComputeAutoCenterYaw(currentYaw: 350f, targetYaw: 10f, autoCenterSpeed: 100f, deltaTime: 0.016f);

        Assert.Greater(yaw, 350f - 1f);
        Assert.Less(Mathf.Abs(Mathf.DeltaAngle(yaw, 10f)), 20f, "Should move toward 10 via the short way (through 0/360), not backtrack toward the long way");
    }

    [Test]
    public void ClampDistanceForObstruction_NoObstruction_ReturnsDesiredDistance()
    {
        float result = ThirdPersonCameraController.ClampDistanceForObstruction(desiredDistance: 2f, obstructionDistance: null, skin: 0.15f);

        Assert.AreEqual(2f, result);
    }

    [Test]
    public void ClampDistanceForObstruction_ObstructionCloserThanDesired_ClampsWithSkinBuffer()
    {
        float result = ThirdPersonCameraController.ClampDistanceForObstruction(desiredDistance: 2f, obstructionDistance: 1f, skin: 0.15f);

        Assert.AreEqual(0.85f, result, 0.001f);
    }

    [Test]
    public void ClampDistanceForObstruction_ObstructionFartherThanDesired_ReturnsDesiredDistance()
    {
        // The obstruction is beyond where the camera would sit anyway - nothing to clamp.
        float result = ThirdPersonCameraController.ClampDistanceForObstruction(desiredDistance: 2f, obstructionDistance: 5f, skin: 0.15f);

        Assert.AreEqual(2f, result);
    }

    [Test]
    public void ClampDistanceForObstruction_VeryCloseObstruction_NeverGoesNegative()
    {
        float result = ThirdPersonCameraController.ClampDistanceForObstruction(desiredDistance: 2f, obstructionDistance: 0.05f, skin: 0.15f);

        Assert.AreEqual(0f, result);
    }

    [Test]
    public void YawDegrees_ReflectsCurrentYawState()
    {
        // A synchronous [Test] never ticks LateUpdate, so the lazy init-from-initialYaw path
        // never runs - set the runtime _yaw field directly rather than initialYaw, matching
        // what YawDegrees actually reads.
        var cameraGo = new GameObject("Camera");
        ThirdPersonCameraController controller = cameraGo.AddComponent<ThirdPersonCameraController>();

        System.Reflection.FieldInfo field = typeof(ThirdPersonCameraController).GetField("_yaw", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        field.SetValue(controller, 30f);

        Assert.AreEqual(30f, controller.YawDegrees);

        Object.DestroyImmediate(cameraGo);
    }
}
