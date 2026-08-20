using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Live2DAction.CameraSystem;
using Object = UnityEngine.Object;

// Runs the real LateUpdate loop with actual Physics colliders, verifying
// ThirdPersonCameraController's SphereCast-based obstruction avoidance actually pulls the
// camera in front of geometry instead of clipping through it - the pure
// ClampDistanceForObstruction arithmetic is covered in isolation by
// ThirdPersonCameraControllerTests (EditMode), but that doesn't exercise the real
// Physics.SphereCastAll call this depends on.
//
// 2026-08-12: added after a real user report ("很靠近敵人時角色1突然消失，畫面定格") that
// persisted even after fixing the CharacterController-climbing root cause and raising
// distance to 2 - this project never had any camera collision avoidance at all (documented
// known gap), and a camera with no obstruction check ending up inside nearby geometry reads
// exactly like "the character disappeared".
public class ThirdPersonCameraObstructionTests
{
    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Expected a private field named '{fieldName}' on {target.GetType().Name}");
        field.SetValue(target, value);
    }

    [SetUp]
    public void SetUp()
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            Object.DestroyImmediate(root);
        }
    }

    [UnityTest]
    public IEnumerator ObstructionBehindTarget_PullsCameraInFrontOfIt()
    {
        var target = new GameObject("Player");
        target.transform.position = Vector3.zero;

        // A solid box sitting where the camera's naive (unobstructed) position would land -
        // clearly separated from the target itself so the SphereCast doesn't start inside it.
        GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obstacle.transform.position = new Vector3(0f, 0f, -2f);
        obstacle.transform.localScale = Vector3.one;
        // CreatePrimitive spawns at the origin - without this, PhysX's broadphase can still
        // reflect the pre-move position for the very next Physics query in the same frame
        // (confirmed by a first attempt at this test reporting the obstacle at distance ~0,
        // i.e. still "at the origin" as far as SphereCastAll was concerned).
        Physics.SyncTransforms();

        var cameraGo = new GameObject("Camera");
        ThirdPersonCameraController controller = cameraGo.AddComponent<ThirdPersonCameraController>();
        SetField(controller, "target", target.transform);
        SetField(controller, "distance", 5f); // naive position (0,0,-5) is well past the obstacle
        SetField(controller, "targetOffset", Vector3.zero);
        SetField(controller, "initialYaw", 0f);
        SetField(controller, "initialPitch", 0f);
        SetField(controller, "enableAutoCenter", false); // isolate this from auto-center's own yaw changes

        yield return null; // let LateUpdate run under Application.isPlaying

        float actualDistance = Vector3.Distance(cameraGo.transform.position, target.transform.position);
        Assert.Less(actualDistance, 4f,
            $"Camera ended up {actualDistance} units from target - should have been pulled well in front of the obstacle at ~1.3-1.9 units, not left at the naive 5.");
        Assert.Greater(actualDistance, 0.5f, "Camera clamped all the way down to the target itself - the skin buffer isn't being applied.");

        Object.Destroy(target);
        Object.Destroy(obstacle);
        Object.Destroy(cameraGo);
    }

    [UnityTest]
    public IEnumerator NoObstruction_CameraSitsAtFullDistance()
    {
        var target = new GameObject("Player");
        target.transform.position = Vector3.zero;

        var cameraGo = new GameObject("Camera");
        ThirdPersonCameraController controller = cameraGo.AddComponent<ThirdPersonCameraController>();
        SetField(controller, "target", target.transform);
        SetField(controller, "distance", 5f);
        SetField(controller, "targetOffset", Vector3.zero);
        SetField(controller, "initialYaw", 0f);
        SetField(controller, "initialPitch", 0f);
        SetField(controller, "enableAutoCenter", false);

        yield return null;

        float actualDistance = Vector3.Distance(cameraGo.transform.position, target.transform.position);
        Assert.AreEqual(5f, actualDistance, 0.05f, "With nothing in the way, the camera should sit at the full configured distance.");

        Object.Destroy(target);
        Object.Destroy(cameraGo);
    }
}
