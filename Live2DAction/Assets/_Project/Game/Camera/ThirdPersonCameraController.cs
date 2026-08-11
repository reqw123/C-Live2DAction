using UnityEngine;

namespace Live2DAction.CameraSystem
{
    // Fixed-world-axis camera (Diablo/POE-style planted angle): yaw/pitch never change at
    // runtime, whether from mouse input or from locking onto an enemy (see ILockOnSource
    // usage history in CharacterMovement - that component still turns the character to face
    // a locked target on its own; the camera itself no longer reacts to a lock at all). The
    // camera only re-centers every LateUpdate to keep following the target's position at a
    // constant offset/angle. Because YawDegrees is therefore a constant, CharacterMovement's
    // camera-relative movement direction is always relative to the same world axes too - see
    // Docs/KNOWN_ISSUES.md for the earlier mouse-look iterations this replaces.
    public class ThirdPersonCameraController : MonoBehaviour, ICameraYawSource
    {
        [SerializeField] private Transform target;
        [SerializeField] private float distance = 8f;
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.4f, 0f);
        [SerializeField] private float fixedYaw;
        [SerializeField] private float fixedPitch = 45f;

        public float YawDegrees => fixedYaw;

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Quaternion rotation = Quaternion.Euler(fixedPitch, fixedYaw, 0f);
            Vector3 position = ComputeCameraPosition(target.position, rotation, distance, targetOffset);

            transform.SetPositionAndRotation(position, rotation);
        }

        // Pure so the positioning math can be verified directly in EditMode tests without a
        // live scene or Play loop.
        public static Vector3 ComputeCameraPosition(Vector3 targetPosition, Quaternion rotation, float distance, Vector3 targetOffset)
        {
            Vector3 lookAtPoint = targetPosition + targetOffset;
            return lookAtPoint - rotation * Vector3.forward * distance;
        }
    }
}
