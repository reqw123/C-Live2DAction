using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Live2DAction.Input;

// Regression test for a real bug found via user playtesting: holding a pure strafe input
// spun the character in a continuous 360-degree circle instead of moving in a straight
// line. Root cause was CharacterMovement reading the camera's fully-composed
// Transform.forward, which reactively sweeps as CinemachineRotationComposer's aim tracks
// the player translating sideways past it - feeding back into movement direction, which
// feeds back into the camera's aim, and so on. Fixed by reading the orbital camera's raw,
// mouse-only yaw angle instead (see ICameraYawSource / OrbitalCameraYawSource). This test
// uses the real GreyboxTest scene (real Cinemachine rig), not a synthetic fixed camera,
// specifically to catch this class of bug again if the camera rig setup regresses.
public class CameraRelativeMovementRegressionTests
{
    private class StubInputBehaviour : MonoBehaviour, IInputCommand
    {
        public Vector2 MoveInput { get; set; }
        public bool AttackPressed { get; set; }
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Expected a private field named '{fieldName}' on {target.GetType().Name}");
        field.SetValue(target, value);
    }

    [UnityTest]
    public IEnumerator HoldingPureStrafeInput_ConvergesToAFacingInsteadOfSpinning()
    {
        SceneManager.LoadScene("GreyboxTest");
        yield return null;
        yield return null;

        GameObject player = GameObject.Find("Player");
        StubInputBehaviour stub = player.AddComponent<StubInputBehaviour>();
        var movement = player.GetComponent<Live2DAction.Characters.CharacterMovement>();
        SetField(movement, "inputSource", stub);

        stub.MoveInput = new Vector2(-1f, 0f); // pure left, held

        Vector3 facingAtHalfSecond = Vector3.zero;
        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < 0.5f)
        {
            yield return null;
        }
        facingAtHalfSecond = player.transform.forward;

        // If it were still spinning, another 1.5s of the same held input would keep
        // rotating the character well past its 0.5s facing. A fixed, converged facing
        // should barely change at all.
        while (Time.realtimeSinceStartup - start < 2f)
        {
            yield return null;
        }
        Vector3 facingAtTwoSeconds = player.transform.forward;

        float driftDegrees = Vector3.Angle(facingAtHalfSecond, facingAtTwoSeconds);
        Assert.Less(driftDegrees, 10f,
            $"Facing direction drifted {driftDegrees} degrees between t=0.5s and t=2s while holding a constant pure-strafe " +
            "input - this is the signature of the camera-relative-direction feedback loop regressing (see class comment).");
    }
}
