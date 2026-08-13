using UnityEngine;
using Live2DAction.CameraSystem;
using Live2DAction.Core;
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
        // don't set up a real camera. 2026-08-12: reinstated after a same-day detour into
        // tank controls (A/D turn in place) paired with a camera rigidly locked to the
        // character's own facing - reverted back to this camera-relative-strafe scheme by
        // explicit request ("改回剛剛那樣...參考原神鳴潮等等"). The critical invariant this
        // depends on: cameraYawSource must be driven independently of this component's own
        // rotation (mouse input, not read back from the character) - see
        // ThirdPersonCameraController's class comment and
        // CameraRelativeMovementRegressionTests for what breaks if that's ever violated again
        // (the character spins in a continuous circle on any pure-strafe input).
        [SerializeField] private MonoBehaviour cameraYawSource;

        // Matches the top threshold of Maya's Locomotion blend tree (CharacterAnimatorLink)
        // so translation speed and the Run clip's authored pace line up - a mismatch here
        // is what caused the reported foot-sliding, since these clips have no root motion
        // to derive the "correct" speed from and must be tuned by eye instead.
        [SerializeField] private float moveSpeed = 2f;

        // Eased (SmoothDamp/SmoothDampAngle) rather than constant-rate (MoveTowards/
        // RotateTowards): a constant rate accelerates linearly and then cuts off the instant
        // it reaches the target, which reads as mechanical - reported as "movement doesn't
        // feel natural". SmoothDamp approaches the target asymptotically, giving the
        // character a bit of weight both starting and stopping, and is the standard
        // technique third-person controllers use for natural turning without a dedicated
        // turn-in-place animation (see Docs/Research/CAMERA_MOVEMENT_RESEARCH.md). Smaller
        // values are snappier; these are reasonable starting guesses tuned by eye, not
        // derived from any authored animation data (same caveat as moveSpeed below).
        [SerializeField] private float accelerationSmoothTime = 0.08f;

        // Lowered from 0.12s after "releasing the move key doesn't stop the character right
        // away" was reported - 0.12s (deliberately slower than acceleration, for a bit of
        // trailing weight on stopping) read as too much coast/slide once the character was
        // actually being played. Still eased, not an instant MoveTowards-style stop (that was
        // the "movement doesn't feel natural" complaint this smoothing originally fixed) - just
        // eased fast enough that the coast is barely noticeable instead of a deliberate feature.
        [SerializeField] private float decelerationSmoothTime = 0.05f;
        [SerializeField] private float rotationSmoothTime = 0.1f;
        [SerializeField] private float gravity = -20f;

        // sqrt(2 * |gravity| * desired peak height) would give an exact peak height, but
        // there's no specific target height requested - this is a reasonable starting guess
        // (roughly a 1.5-2 unit hop at gravity=-20), tune by eye.
        [SerializeField] private float jumpSpeed = 7f;

        [SerializeField] private DodgeData dodgeData;

        // Optional: while this reports a locked target, the character always faces it
        // (unless dodging) instead of the movement direction, so attacks aim at the target
        // even while strafing around it or standing still.
        [SerializeField] private MonoBehaviour lockOnSource;

        // Optional: kept in sync with IsDodgeInvulnerable every frame so dodging actually
        // avoids damage, not just an inert flag nothing consumes.
        [SerializeField] private Health health;

        private CharacterController _controller;
        private Vector3 _horizontalVelocity;

        // SmoothDamp's own internal "current rate of change" state - not the same value as
        // _horizontalVelocity itself. Reset to zero whenever a dodge takes over so the eased
        // ramp doesn't inherit a stale rate once normal movement resumes.
        private Vector3 _horizontalVelocitySmoothDampRef;
        private float _verticalVelocity;

        // SmoothDampAngle's internal angular-velocity state, mirroring _horizontalVelocitySmoothDampRef above.
        private float _yawAngularVelocity;
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

        // Raw camera-relative input axes this frame (y = W/S, x = A/D), not the resulting
        // world-space direction - exposed so ThirdPersonCameraController's auto-center can
        // tell "walking forward/back" apart from "strafing sideways" (see that class's field
        // comment: auto-centering during a held pure-strafe measurably drifted the character's
        // facing, confirmed by CameraRelativeMovementRegressionTests, because the camera
        // easing toward a facing that's itself still chasing a camera-relative strafe target
        // converges far slower than walking forward does).
        public Vector2 CurrentMoveInput { get; private set; }

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
            CurrentMoveInput = moveInput;
            bool dodgePressed = inputCommand != null && inputCommand.DodgePressed;
            bool jumpPressed = inputCommand != null && inputCommand.JumpPressed;
            Vector3 desiredDirection = CameraRelativeDirection(moveInput, CurrentCameraYawDegrees());

            // Dodge backward (relative to current facing) if there's no move input held,
            // matching the common "backstep" convention when dodging from a standstill.
            Vector3 dodgeDirectionIfStarting = desiredDirection.sqrMagnitude > 0.0001f ? desiredDirection : -transform.forward;
            Vector3 dodgeVelocity = _dodgeState.Tick(Time.deltaTime, dodgePressed, dodgeDirectionIfStarting);

            if (health != null)
            {
                health.IsInvulnerable = _dodgeState.IsInvulnerable;
            }

            Vector3 facingDirection;
            if (_dodgeState.Phase == DodgePhase.Dodging)
            {
                // A dodge commits to its locked-in direction and speed for its whole
                // duration - it overrides normal eased movement entirely rather than
                // blending with it.
                _horizontalVelocity = dodgeVelocity;
                _horizontalVelocitySmoothDampRef = Vector3.zero;
                facingDirection = _dodgeState.Direction;
            }
            else
            {
                Vector3 desiredVelocity = desiredDirection * moveSpeed;
                float smoothTime = desiredVelocity.sqrMagnitude > 0.0001f ? accelerationSmoothTime : decelerationSmoothTime;
                _horizontalVelocity = Vector3.SmoothDamp(_horizontalVelocity, desiredVelocity, ref _horizontalVelocitySmoothDampRef, smoothTime);

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

            // Ground-only (no air jump/double jump) - checked after the grounded reset above
            // so a jump this frame isn't immediately clobbered back down to -1.
            if (jumpPressed && _controller.isGrounded)
            {
                _verticalVelocity = jumpSpeed;
            }

            _verticalVelocity += gravity * Time.deltaTime;

            Vector3 motion = _horizontalVelocity;
            motion.y = _verticalVelocity;
            _controller.Move(motion * Time.deltaTime);

            if (facingDirection.sqrMagnitude > 0.0001f)
            {
                // SmoothDampAngle instead of a constant-degrees/sec RotateTowards, so the
                // turn eases out near the target facing instead of stopping dead the instant
                // it arrives - see the field comment above for why.
                float currentYaw = transform.eulerAngles.y;
                float targetYaw = Quaternion.LookRotation(facingDirection, Vector3.up).eulerAngles.y;
                float newYaw = Mathf.SmoothDampAngle(currentYaw, targetYaw, ref _yawAngularVelocity, rotationSmoothTime);
                transform.rotation = Quaternion.Euler(0f, newYaw, 0f);
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
