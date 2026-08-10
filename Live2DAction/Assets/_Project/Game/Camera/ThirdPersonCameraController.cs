using UnityEngine;
using UnityEngine.InputSystem;

namespace Live2DAction.CameraSystem
{
    // Mouse-driven third-person orbit camera (Genshin Impact-style: the camera always
    // follows the player's look input, no button needs to be held). Replaces Cinemachine's
    // orbital/aim system, which let the camera's rendered orientation drift independently
    // of what CharacterMovement used for its camera-relative direction math (see
    // Docs/KNOWN_ISSUES.md for the investigation). Here yaw/pitch are plain fields this
    // script owns outright and updates only from mouse delta - there is no separate "Body"
    // vs "Aim" step that could disagree with itself, so CharacterMovement reading
    // YawDegrees via ICameraYawSource always matches what's actually rendered.
    public class ThirdPersonCameraController : MonoBehaviour, ICameraYawSource
    {
        [SerializeField] private Transform target;
        [SerializeField] private float distance = 4f;
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.4f, 0f);
        [SerializeField] private float mouseSensitivity = 0.15f;
        [SerializeField] private float minPitch = -20f;
        [SerializeField] private float maxPitch = 60f;
        [SerializeField] private float initialYaw;
        [SerializeField] private float initialPitch = 25f;

        private float _yaw;
        private float _pitch;

        public float YawDegrees => _yaw;

        private void Awake()
        {
            _yaw = initialYaw;
            _pitch = initialPitch;
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector2 mouseDelta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
            _yaw += mouseDelta.x * mouseSensitivity;
            _pitch -= mouseDelta.y * mouseSensitivity;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 lookAtPoint = target.position + targetOffset;
            Vector3 desiredPosition = lookAtPoint - rotation * Vector3.forward * distance;

            transform.SetPositionAndRotation(desiredPosition, rotation);
        }
    }
}
