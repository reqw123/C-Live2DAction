using UnityEngine;
using UnityEngine.InputSystem;

namespace Live2DAction.CameraSystem
{
    // Mouse-driven camera with a togglable third-person orbit / first-person eye view
    // (Genshin Impact-style third-person by default; V toggles to first-person). Replaces
    // Cinemachine's orbital/aim system, which let the camera's rendered orientation drift
    // independently of what CharacterMovement used for its camera-relative direction math
    // (see Docs/KNOWN_ISSUES.md for the investigation). Yaw/pitch are plain fields this
    // script owns outright and updates only from mouse delta - there is no separate "Body"
    // vs "Aim" step that could disagree with itself, so CharacterMovement reading YawDegrees
    // via ICameraYawSource always matches what's actually rendered, in either view mode.
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

        [SerializeField] private CameraViewMode startingViewMode = CameraViewMode.ThirdPerson;
        [SerializeField] private Vector3 firstPersonEyeOffset = new Vector3(0f, 1.6f, 0f);

        // The Player's visible mesh, hidden in first-person mode so the camera doesn't sit
        // inside its own head - Maya has no separate first-person arms rig, so the whole
        // model is hidden rather than just the head (see Docs/KNOWN_ISSUES.md).
        [SerializeField] private GameObject visualToHide;

        private float _yaw;
        private float _pitch;
        private CameraViewMode _viewMode;

        public float YawDegrees => _yaw;
        public CameraViewMode ViewMode => _viewMode;

        private void Awake()
        {
            _yaw = initialYaw;
            _pitch = initialPitch;
            _viewMode = startingViewMode;
            ApplyVisualVisibility();
        }

        public void ToggleViewMode()
        {
            _viewMode = _viewMode == CameraViewMode.ThirdPerson ? CameraViewMode.FirstPerson : CameraViewMode.ThirdPerson;
            ApplyVisualVisibility();
        }

        private void ApplyVisualVisibility()
        {
            if (visualToHide != null)
            {
                visualToHide.SetActive(_viewMode == CameraViewMode.ThirdPerson);
            }
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            if (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame)
            {
                ToggleViewMode();
            }

            Vector2 mouseDelta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
            _yaw += mouseDelta.x * mouseSensitivity;
            _pitch -= mouseDelta.y * mouseSensitivity;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 position = ComputeCameraPosition(_viewMode, target.position, rotation, distance, targetOffset, firstPersonEyeOffset);

            transform.SetPositionAndRotation(position, rotation);
        }

        // Pure so the two view modes' positioning math can be verified directly in EditMode
        // tests without a live scene or Play loop.
        public static Vector3 ComputeCameraPosition(CameraViewMode mode, Vector3 targetPosition, Quaternion rotation, float distance, Vector3 thirdPersonOffset, Vector3 firstPersonEyeOffset)
        {
            if (mode == CameraViewMode.FirstPerson)
            {
                return targetPosition + firstPersonEyeOffset;
            }

            Vector3 lookAtPoint = targetPosition + thirdPersonOffset;
            return lookAtPoint - rotation * Vector3.forward * distance;
        }
    }
}
