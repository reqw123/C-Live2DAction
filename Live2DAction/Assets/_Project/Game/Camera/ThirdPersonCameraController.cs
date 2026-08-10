using UnityEngine;

namespace Live2DAction.CameraSystem
{
    // Fixed-angle follow camera: translates to track the target's position every frame but
    // never rotates, whether from mouse input or from the character's own facing. This
    // replaces an earlier mouse-look orbit design and, before that, Cinemachine's
    // orbital/aim system - both let the camera's rendered orientation drift independently
    // of what CharacterMovement used for its camera-relative direction math, which produced
    // two separate reported bugs (see Docs/KNOWN_ISSUES.md). With yaw/pitch held constant,
    // "pressing W moves the character away from the camera" is true on every frame by
    // construction, matching the standard fixed-camera/camera-relative-movement pattern used
    // by most third-person action games.
    //
    // CharacterMovement reads YawDegrees directly via ICameraYawSource, so movement math and
    // the camera's actual rendered direction are always the same fixed value.
    public class ThirdPersonCameraController : MonoBehaviour, ICameraYawSource
    {
        [SerializeField] private Transform target;
        [SerializeField] private float distance = 4f;
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.4f, 0f);
        [SerializeField] private float yawDegrees;
        [SerializeField] private float pitchDegrees = 25f;

        public float YawDegrees => yawDegrees;

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Quaternion rotation = Quaternion.Euler(pitchDegrees, yawDegrees, 0f);
            Vector3 lookAtPoint = target.position + targetOffset;
            Vector3 desiredPosition = lookAtPoint - rotation * Vector3.forward * distance;

            transform.SetPositionAndRotation(desiredPosition, rotation);
        }
    }
}
