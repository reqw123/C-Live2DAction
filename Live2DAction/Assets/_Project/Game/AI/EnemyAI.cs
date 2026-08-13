using UnityEngine;
using Live2DAction.Combat;
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

        // Fallback only when "combat" below is unset (e.g. isolated unit tests that build a
        // bare EnemyAI without a PlayerCombat) - see "combat" field's own comment for why the
        // real scene doesn't rely on this value being kept correct by hand anymore.
        [SerializeField] private float attackRange = 2f;
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private float rotationSpeedDegrees = 480f;
        [SerializeField] private float gravity = -20f;

        // 2026-08-13, real bug report ("我已經盡到敵人範圍內，線條從紅色變成黃色，但敵人尚未作
        // 出攻擊，這代表視覺呈現與數值邏輯判定很明顯不一致") - root cause: PlayerCombat's own
        // Gizmo (and the real hit judgment in ResolveActiveHit) answer "is the target within the
        // Range/Radius CAPSULE", whose true forward reach is Range+Radius (the far end-cap
        // sphere itself has radius Radius, extending that much further past the Range point) -
        // but this component's own "should I attack" decision used a plain omnidirectional
        // distance sphere against a SEPARATELY-tuned attackRange float, manually kept in sync
        // via EnemyAttackRangeSync.cs. That manual sync had drifted stale again (attackRange=1
        // left over from when Range was 1.5, after Range was later changed to 1 without
        // re-running the tool) - but even with perfect syncing, a plain distance sphere can
        // never exactly match a forward-extending capsule's shape. Optional reference to this
        // same GameObject's PlayerCombat: when set, the attack-range decision is recomputed
        // every frame straight from PrimaryAttack.Range + PrimaryAttack.Radius (the capsule's
        // true maximum forward reach) instead of the manually-tuned attackRange field, so there
        // is no second number left to go stale - whatever Range/Radius PlayerCombat is
        // currently configured with IS the attack range, always. Left optional (defaults to
        // null, falling back to attackRange) so existing isolated tests that only set
        // attackRange directly via reflection keep working unchanged.
        [SerializeField] private PlayerCombat combat;

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
        public bool JumpPressed => false;

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
            CurrentState = EnemyBehaviorUtility.DetermineState(distance, detectionRange, ResolveEffectiveAttackRange());

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

        // See "combat" field's own comment for the full 2026-08-13 bug this fixes. Range+Radius
        // (not just Range) because the capsule's far end-cap is itself a sphere of radius
        // Radius, extending that much further past the Range point - PlayerCombat's Gizmo and
        // ResolveActiveHit's actual Physics.OverlapCapsule both already reach that far, so this
        // needs to match or the AI keeps declining to attack from positions its own attack
        // would clearly land from.
        private float ResolveEffectiveAttackRange()
        {
            if (combat == null)
            {
                return attackRange;
            }

            AttackData attack = combat.PrimaryAttack;
            return attack != null ? attack.Range + attack.Radius : attackRange;
        }

        // 2026-08-13, explicit user request ("能不能把 攻擊距離 警備距離 用不同顏色線條呈現嗎
        // 角色1和4都要") - detectionRange is Player4's own "警備距離"/alert range (how far away
        // it notices the player and starts chasing), the AI-side counterpart to
        // TargetLockController.maxLockRange's own Gizmo on Player (same cyan color - both
        // answer "how far can this character notice something", just from opposite sides).
        // attackRange isn't drawn here - the user only asked for these two, and
        // PlayerCombat.OnDrawGizmosSelected already covers the actual attack-judged capsule
        // (AttackData.Range/Radius) both Player and Player4 share via the same component.
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, detectionRange);
        }
    }
}
