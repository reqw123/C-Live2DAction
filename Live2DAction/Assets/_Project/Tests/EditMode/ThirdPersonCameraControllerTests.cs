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
    public void YawDegrees_ReflectsFixedYawField_NotMouseOrLockOn()
    {
        var cameraGo = new GameObject("Camera");
        ThirdPersonCameraController controller = cameraGo.AddComponent<ThirdPersonCameraController>();

        System.Reflection.FieldInfo field = typeof(ThirdPersonCameraController).GetField("fixedYaw", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        field.SetValue(controller, 30f);

        Assert.AreEqual(30f, controller.YawDegrees);

        Object.DestroyImmediate(cameraGo);
    }
}
