using UnityEngine;
using Live2DAction.Input;

namespace Live2DAction.AI
{
    // Drives its own CharacterController movement directly rather than reusing
    // CharacterMovement, which carries player-only concerns (camera-relative direction,
    // dodge, lock-on facing) that don't apply to a simple chase-and-attack enemy. It still
    // implements IInputCommand purely so PlayerCombat (added alongside this component) can
    // read AttackPressed and run the exact same frame-data combo pipeline the player uses -
    // satisfying the project rule that player and AI share one input interface without
    // forcing AI through player-specific movement code.
    [RequireComponent(typeof(CharacterController))]
    public class EnemyAI : MonoBehaviour, IInputCommand
    {
        [SerializeField] private Transform target;
        [SerializeField] private float detectionRange = 8f;
        [SerializeField] private float attackRange = 2f;
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private float rotationSpeedDegrees = 480f;
        [SerializeField] private float gravity = -20f;

        private CharacterController _controller;
        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;

        public EnemyState CurrentState { get; private set; } = EnemyState.Idle;

        // MoveInput is exposed for IInputCommand compliance/inspection, but EnemyAI drives
        // its own CharacterController.Move directly rather than anything consuming this
        // value the way CharacterMovement consumes the player's MoveInput.
        public Vector2 MoveInput { get; private set; }
        public bool AttackPressed { get; private set; }
        public bool DodgePressed => false;
        public bool LockOnPressed => false;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        private void Update()
        {
            if (target == null)
            {
                MoveInput = Vector2.zero;
                AttackPressed = false;
                return;
            }

            float distance = Vector3.Distance(transform.position, target.position);
            CurrentState = EnemyBehaviorUtility.DetermineState(distance, detectionRange, attackRange);

            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            Vector3 direction = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector3.zero;

            _horizontalVelocity = CurrentState == EnemyState.Chasing ? direction * moveSpeed : Vector3.zero;
            MoveInput = new Vector2(direction.x, direction.z);
            AttackPressed = CurrentState == EnemyState.Attacking;

            if (_controller.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -1f;
            }
            _verticalVelocity += gravity * Time.deltaTime;

            Vector3 motion = _horizontalVelocity;
            motion.y = _verticalVelocity;
            _controller.Move(motion * Time.deltaTime);

            // Faces the target whenever aware of it (chasing or attacking), not only while
            // actually moving - an idle-but-stationary attacker that never turns to track a
            // circling player would keep swinging at empty air.
            if (CurrentState != EnemyState.Idle && direction.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeedDegrees * Time.deltaTime);
            }
        }
    }
}
