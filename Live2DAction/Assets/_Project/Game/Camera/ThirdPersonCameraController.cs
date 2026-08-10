using UnityEngine;
using UnityEngine.InputSystem;

namespace Live2DAction.CameraSystem
{
    // Self-contained third-person orbit camera - replaces Cinemachine's
    // CinemachineOrbitalFollow/CinemachineRotationComposer, which behaved inconsistently
    // with their own documented/source-level contract in ways that resisted five separate
    // configuration fixes (see Docs/KNOWN_ISSUES.md for the full investigation: BindingMode,
    // a position-only follow anchor, removing the Aim component, and zero damping all
    // measured zero effect on the camera's rotation tracking the player's rotation, despite
    // the Follow target's own rotation being confirmed locked to identity throughout).
    //
    // Owns yaw/pitch as plain fields driven only by mouse delta - nothing else can
    // influence them, and CharacterMovement reads YawDegrees directly via ICameraYawSource
    // for camera-relative movement, so movement direction and what's rendered on screen can
    // never disagree.
    public class ThirdPersonCameraController : MonoBehaviour, ICameraYawSource
    {
        [SerializeField] private Transform target;
        [SerializeField] private float distance = 4f;
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.4f, 0f);
        [SerializeField] private float mouseSensitivity = 0.15f;
        [SerializeField] private float minPitch = -20f;
        [SerializeField] private float maxPitch = 60f;
        [SerializeField] private bool invertY;

        private float _yaw;
        private float _pitch = 15f;

        public float YawDegrees => _yaw;

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector2 mouseDelta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
            _yaw += mouseDelta.x * mouseSensitivity;
            float pitchDelta = mouseDelta.y * mouseSensitivity;
            _pitch += invertY ? pitchDelta : -pitchDelta;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 lookAtPoint = target.position + targetOffset;
            Vector3 desiredPosition = lookAtPoint - rotation * Vector3.forward * distance;

            transform.SetPositionAndRotation(desiredPosition, rotation);
        }
    }
}
