using UnityEngine;
using Live2DAction.CameraSystem;
using Live2DAction.Input;
using Live2DAction.Targeting;

namespace Live2DAction.Characters
{
    [RequireComponent(typeof(CharacterController))]
    public class CharacterMovement : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour inputSource;

        // Optional: a yaw driven only by explicit mouse-look input (see ICameraYawSource /
        // ThirdPersonCameraController - this must not be the camera's fully-composed
        // Transform.forward). Falls back to Camera.main's yaw if unassigned, for tests that
        // don't set up a real camera.
        [SerializeField] private MonoBehaviour cameraYawSource;

        // Matches the top threshold of Maya's Locomotion blend tree (CharacterAnimatorLink)
        // so translation speed and the Run clip's authored pace line up - a mismatch here
        // is what caused the reported foot-sliding, since these clips have no root motion
        // to derive the "correct" speed from and must be tuned by eye instead.
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private float rotationSpeedDegrees = 720f;
        [SerializeField] private float acceleration = 20f;
        [SerializeField] private float deceleration = 25f;
        [SerializeField] private float gravity = -20f;
        [SerializeField] private DodgeData dodgeData;

        // Optional: while this reports a locked target, the character always faces it
        // (unless dodging) instead of the movement direction, so attacks aim at the target
        // even while strafing around it or standing still.
        [SerializeField] private MonoBehaviour lockOnSource;

        private CharacterController _controller;
        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;
        private DodgeState _dodgeState;

        // Resolved on every use rather than cached in Awake(), so assigning inputSource
        // after the component has already Awoken (e.g. from a test) still takes effect.
        private IInputCommand InputCommand => inputSource as IInputCommand;
        private ICameraYawSource CameraYawSource => cameraYawSource as ICameraYawSource;
        private ILockOnSource LockOnSource => lockOnSource as ILockOnSource;

        public float MoveSpeed => moveSpeed;
        public float CurrentHorizontalSpeed => _horizontalVelocity.magnitude;
        public DodgePhase CurrentDodgePhase => _dodgeState != null ? _dodgeState.Phase : DodgePhase.Idle;
        public bool IsDodgeInvulnerable => _dodgeState != null && _dodgeState.IsInvulnerable;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        private void Update()
        {
            // Built lazily rather than in Awake, same reasoning as PlayerCombat's
            // ComboAttackState: tests assign dodgeData via reflection right after
            // AddComponent, which already runs Awake synchronously.
            if (_dodgeState == null)
            {
                _dodgeState = new DodgeState(dodgeData);
            }

            IInputCommand inputCommand = InputCommand;
            Vector2 moveInput = inputCommand != null ? inputCommand.MoveInput : Vector2.zero;
            bool dodgePressed = inputCommand != null && inputCommand.DodgePressed;
            Vector3 desiredDirection = CameraRelativeDirection(moveInput, CurrentCameraYawDegrees());

            // Dodge backward (relative to current facing) if there's no move input held,
            // matching the common "backstep" convention when dodging from a standstill.
            Vector3 dodgeDirectionIfStarting = desiredDirection.sqrMagnitude > 0.0001f ? desiredDirection : -transform.forward;
            Vector3 dodgeVelocity = _dodgeState.Tick(Time.deltaTime, dodgePressed, dodgeDirectionIfStarting);

            Vector3 facingDirection;
            if (_dodgeState.Phase == DodgePhase.Dodging)
            {
                // A dodge commits to its locked-in direction and speed for its whole
                // duration - it overrides normal acceleration-based movement entirely
                // rather than blending with it.
                _horizontalVelocity = dodgeVelocity;
                facingDirection = _dodgeState.Direction;
            }
            else
            {
                Vector3 desiredVelocity = desiredDirection * moveSpeed;
                float rate = desiredVelocity.sqrMagnitude > 0.0001f ? acceleration : deceleration;
                _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, desiredVelocity, rate * Time.deltaTime);

                Transform lockedTarget = LockOnSource?.LockedTarget;
                if (lockedTarget != null)
                {
                    Vector3 toTarget = lockedTarget.position - transform.position;
                    toTarget.y = 0f;
                    facingDirection = toTarget;
                }
                else
                {
                    facingDirection = desiredDirection;
                }
            }

            if (_controller.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -1f;
            }
            _verticalVelocity += gravity * Time.deltaTime;

            Vector3 motion = _horizontalVelocity;
            motion.y = _verticalVelocity;
            _controller.Move(motion * Time.deltaTime);

            if (facingDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(facingDirection, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeedDegrees * Time.deltaTime);
            }
        }

        private float CurrentCameraYawDegrees()
        {
            ICameraYawSource yawSource = CameraYawSource;
            if (yawSource != null)
            {
                return yawSource.YawDegrees;
            }

            Camera mainCamera = Camera.main;
            return mainCamera != null ? mainCamera.transform.eulerAngles.y : 0f;
        }

        public static Vector3 CameraRelativeDirection(Vector2 moveInput, float cameraYawDegrees)
        {
            if (moveInput.sqrMagnitude < 0.0001f)
            {
                return Vector3.zero;
            }

            Quaternion yaw = Quaternion.Euler(0f, cameraYawDegrees, 0f);
            Vector3 forward = yaw * Vector3.forward;
            Vector3 right = yaw * Vector3.right;
            return (forward * moveInput.y + right * moveInput.x).normalized;
        }
    }
}
