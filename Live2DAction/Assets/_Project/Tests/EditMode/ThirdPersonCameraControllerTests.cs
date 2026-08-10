using NUnit.Framework;
using UnityEngine;
using Live2DAction.CameraSystem;

public class ThirdPersonCameraControllerTests
{
    [Test]
    public void ComputeCameraPosition_FirstPerson_IgnoresDistanceAndUsesEyeOffset()
    {
        Vector3 targetPosition = new Vector3(1f, 2f, 3f);
        Vector3 eyeOffset = new Vector3(0f, 1.6f, 0f);
        Quaternion rotation = Quaternion.Euler(30f, 90f, 0f);

        Vector3 position = ThirdPersonCameraController.ComputeCameraPosition(
            CameraViewMode.FirstPerson, targetPosition, rotation, distance: 4f, thirdPersonOffset: new Vector3(0f, 1.4f, 0f), firstPersonEyeOffset: eyeOffset);

        Assert.AreEqual(targetPosition + eyeOffset, position);
    }

    [Test]
    public void ComputeCameraPosition_FirstPerson_IsIndependentOfRotation()
    {
        Vector3 targetPosition = Vector3.zero;
        Vector3 eyeOffset = new Vector3(0f, 1.6f, 0f);

        Vector3 positionAtYaw0 = ThirdPersonCameraController.ComputeCameraPosition(
            CameraViewMode.FirstPerson, targetPosition, Quaternion.identity, 4f, Vector3.zero, eyeOffset);
        Vector3 positionAtYaw180 = ThirdPersonCameraController.ComputeCameraPosition(
            CameraViewMode.FirstPerson, targetPosition, Quaternion.Euler(0f, 180f, 0f), 4f, Vector3.zero, eyeOffset);

        Assert.AreEqual(positionAtYaw0, positionAtYaw180, "First-person eye position should not move as the camera turns in place");
    }

    [Test]
    public void ComputeCameraPosition_ThirdPerson_SitsBehindTargetAtDistance()
    {
        Vector3 targetPosition = Vector3.zero;
        Vector3 thirdPersonOffset = new Vector3(0f, 1.4f, 0f);
        Quaternion rotation = Quaternion.identity; // looking down world +Z

        Vector3 position = ThirdPersonCameraController.ComputeCameraPosition(
            CameraViewMode.ThirdPerson, targetPosition, rotation, distance: 4f, thirdPersonOffset: thirdPersonOffset, firstPersonEyeOffset: Vector3.zero);

        Vector3 expected = thirdPersonOffset - Vector3.forward * 4f;
        Assert.AreEqual(expected, position);
    }

    [Test]
    public void ComputeCameraPosition_ThirdPerson_MovesWithTargetPosition()
    {
        Vector3 thirdPersonOffset = new Vector3(0f, 1.4f, 0f);
        Quaternion rotation = Quaternion.identity;

        Vector3 positionAtOrigin = ThirdPersonCameraController.ComputeCameraPosition(
            CameraViewMode.ThirdPerson, Vector3.zero, rotation, 4f, thirdPersonOffset, Vector3.zero);
        Vector3 positionOffset = ThirdPersonCameraController.ComputeCameraPosition(
            CameraViewMode.ThirdPerson, new Vector3(5f, 0f, 0f), rotation, 4f, thirdPersonOffset, Vector3.zero);

        Assert.AreEqual(positionAtOrigin + new Vector3(5f, 0f, 0f), positionOffset);
    }
}
