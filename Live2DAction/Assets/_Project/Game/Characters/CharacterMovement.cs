using UnityEngine;
using Live2DAction.Input;

namespace Live2DAction.Characters
{
    [RequireComponent(typeof(CharacterController))]
    public class CharacterMovement : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour inputSource;
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float rotationSpeedDegrees = 720f;
        [SerializeField] private float acceleration = 20f;
        [SerializeField] private float deceleration = 25f;
        [SerializeField] private float gravity = -20f;

        private CharacterController _controller;
        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;

        // Resolved on every use rather than cached in Awake(), so assigning inputSource
        // after the component has already Awoken (e.g. from a test) still takes effect.
        private IInputCommand InputCommand => inputSource as IInputCommand;

        public float MoveSpeed => moveSpeed;
        public float CurrentHorizontalSpeed => _horizontalVelocity.magnitude;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        private void Update()
        {
            IInputCommand inputCommand = InputCommand;
            Vector2 moveInput = inputCommand != null ? inputCommand.MoveInput : Vector2.zero;
            Vector3 desiredDirection = CameraRelativeDirection(moveInput);
            Vector3 desiredVelocity = desiredDirection * moveSpeed;

            float rate = desiredVelocity.sqrMagnitude > 0.0001f ? acceleration : deceleration;
            _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, desiredVelocity, rate * Time.deltaTime);

            if (_controller.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -1f;
            }
            _verticalVelocity += gravity * Time.deltaTime;

            Vector3 motion = _horizontalVelocity;
            motion.y = _verticalVelocity;
            _controller.Move(motion * Time.deltaTime);

            if (desiredDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(desiredDirection, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeedDegrees * Time.deltaTime);
            }
        }

        private static Vector3 CameraRelativeDirection(Vector2 moveInput)
        {
            if (moveInput.sqrMagnitude < 0.0001f)
            {
                return Vector3.zero;
            }

            Camera mainCamera = Camera.main;
            Vector3 forward;
            Vector3 right;
            if (mainCamera != null)
            {
                forward = Vector3.ProjectOnPlane(mainCamera.transform.forward, Vector3.up).normalized;
                right = Vector3.ProjectOnPlane(mainCamera.transform.right, Vector3.up).normalized;
            }
            else
            {
                forward = Vector3.forward;
                right = Vector3.right;
            }

            return (forward * moveInput.y + right * moveInput.x).normalized;
        }
    }
}
