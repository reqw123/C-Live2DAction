using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Live2DAction.CameraSystem;
using Object = UnityEngine.Object;

// Runs the real Unity engine loop to verify ToggleViewMode() actually drives GameObject
// activation on the visual, which EditMode tests can't check (no live scene/Awake).
public class CameraViewToggleTests
{
    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Expected a private field named '{fieldName}' on {target.GetType().Name}");
        field.SetValue(target, value);
    }

    [UnityTest]
    public IEnumerator ToggleViewMode_HidesAndShowsVisual_AndFlipsMode()
    {
        var targetGo = new GameObject("Target");
        var visualGo = new GameObject("Visual");
        visualGo.transform.SetParent(targetGo.transform);

        var cameraGo = new GameObject("Camera");
        ThirdPersonCameraController controller = cameraGo.AddComponent<ThirdPersonCameraController>();
        SetField(controller, "target", targetGo.transform);
        SetField(controller, "visualToHide", visualGo);

        yield return null; // let Awake run (starting mode = ThirdPerson by default)

        Assert.AreEqual(CameraViewMode.ThirdPerson, controller.ViewMode);
        Assert.IsTrue(visualGo.activeSelf, "Visual should be shown in third-person mode");

        controller.ToggleViewMode();

        Assert.AreEqual(CameraViewMode.FirstPerson, controller.ViewMode);
        Assert.IsFalse(visualGo.activeSelf, "Visual should be hidden in first-person mode");

        controller.ToggleViewMode();

        Assert.AreEqual(CameraViewMode.ThirdPerson, controller.ViewMode);
        Assert.IsTrue(visualGo.activeSelf, "Toggling back should show the visual again");

        Object.Destroy(cameraGo);
        Object.Destroy(targetGo);
    }

    [UnityTest]
    public IEnumerator ToggleViewMode_DoesNotChangeYawDegrees()
    {
        var targetGo = new GameObject("Target");
        var cameraGo = new GameObject("Camera");
        ThirdPersonCameraController controller = cameraGo.AddComponent<ThirdPersonCameraController>();
        SetField(controller, "target", targetGo.transform);

        yield return null;

        float yawBefore = controller.YawDegrees;
        controller.ToggleViewMode();
        float yawAfter = controller.YawDegrees;

        Assert.AreEqual(yawBefore, yawAfter, "Switching view mode must not itself change the yaw CharacterMovement reads");

        Object.Destroy(cameraGo);
        Object.Destroy(targetGo);
    }
}
