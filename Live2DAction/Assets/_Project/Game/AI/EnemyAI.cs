using UnityEngine;
using Live2DAction.Characters;
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

        // 2026-08-17, real bug report ("076靠我太近時會飛到我頭上") - mirrors CharacterMovement's
        // own slideSpeed/GroundSlopeUtility fix for the reverse direction of the same bug
        // (see that field's comment: "跳躍有機會卡在敵人頭上，需要自行下來"). This class's own
        // gravity handling had the identical gap - isGrounded reads true the moment this AI
        // ends up resting on another character's CharacterController's rounded top (however it
        // got there - a brief overlap-recovery push while chasing is enough, no jump needed),
        // and nothing here ever pushed it back off. Player4 (the other user of this class) was
        // presumably equally exposed, just not yet reported.
        [SerializeField] private float slideSpeed = 4f;

        // 2026-08-17, explicit user request ("移除面對鏡頭的需求 改為統一面對玩家") - supersedes
        // that same day's earlier attackFacingOverride workaround. 076/077 originally had
        // CubismBillboard re-facing the root at Camera.main every LateUpdate (so the flat Live2D
        // plane never appeared edge-on to the camera), which fought this class's own facing
        // logic below and was the actual root cause of that day's "076攻擊不到我" bug -
        // attackFacingOverride patched around it with a second aim-only Transform. Now that
        // CubismBillboard is removed from 076 entirely (user's explicit choice: always face the
        // player instead of always facing the camera), the root's own rotation below IS already
        // the correct, unhijacked aim direction again - PlayerCombat.attackOrigin can go back to
        // its default null/self fallback, no override Transform needed. When true, the facing
        // block below runs every frame regardless of CurrentState (including Idle/out-of-range)
        // instead of only while Chasing/Attacking - "統一面對玩家" (uniformly/always face the
        // player), a full replacement for what CubismBillboard used to do unconditionally every
        // frame. Left false for ordinary enemies (e.g. Player4), which should keep only turning
        // to face the player once actually aware of them.
        [SerializeField] private bool alwaysFaceTarget;

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
        public bool UltimatePressed => false; // AI never triggers the player-only ultimate

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

            // 2026-08-17, real bug report ("076會看著我 但是不會追我也不會攻擊") - root cause:
            // this used to measure raw 3D Vector3.Distance (including Y), which was fine back
            // when every character's root sat at roughly chest height (Y~0.5-0.6). Once 076
            // became a 5m-tall standee, its root had to move up to ~Y=2 so the visual feet line
            // up with the ground (see the scene's own position comment/CHANGELOG) - but the
            // *target*'s root (the player) is still down at Y~0.5, so that leftover ~1.5m of
            // pure vertical separation was eating almost this whole 1.6m detectionRange budget
            // before the player had closed any actual (horizontal) distance at all. Detection
            // and the attack-range check both need "is the target within reach along the
            // ground", not "including however tall this particular character happens to be" -
            // matches the horizontal-only semantics toTarget/direction below already use for
            // movement, just computed first now so distance can reuse it instead of measuring
            // the (wrong) 3D distance separately.
            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            CurrentState = EnemyBehaviorUtility.DetermineState(distance, detectionRange, ResolveEffectiveAttackRange());

            Vector3 direction = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector3.zero;

            _horizontalVelocity = CurrentState == EnemyState.Chasing ? direction * moveSpeed : Vector3.zero;
            MoveInput = new Vector2(direction.x, direction.z);
            AttackPressed = CurrentState == EnemyState.Attacking;

            if (_controller.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -1f;
            }
            _verticalVelocity += gravity * Time.deltaTime;

            // See CharacterMovement's own identical block/GroundSlopeUtility comment - isGrounded
            // alone doesn't mean "standing somewhere walkable", so an active push is needed to
            // actually slide off another character's rounded capsule top instead of resting
            // there indefinitely once ended up there.
            Vector3 slideVelocity = Vector3.zero;
            if (_controller.isGrounded && TryGetGroundNormal(out Vector3 groundNormal, out CharacterController standingOnCharacter))
            {
                bool standingOnAnotherCharacter = standingOnCharacter != null;
                bool tooSteep = GroundSlopeUtility.IsTooSteepToStandOn(groundNormal, _controller.slopeLimit);
                if (standingOnAnotherCharacter || tooSteep)
                {
                    Vector3 slideDirection = GroundSlopeUtility.ComputeSlideDirection(groundNormal);
                    if (slideDirection == Vector3.zero && standingOnAnotherCharacter)
                    {
                        slideDirection = GroundSlopeUtility.ComputeFallbackAwayDirection(transform.position, standingOnCharacter.transform.position);
                    }

                    slideVelocity = slideDirection * slideSpeed;
                }
            }

            Vector3 motion = _horizontalVelocity + slideVelocity;
            motion.y = _verticalVelocity;
            _controller.Move(motion * Time.deltaTime);

            // Faces the target whenever aware of it (chasing or attacking), not only while
            // actually moving - an idle-but-stationary attacker that never turns to track a
            // circling player would keep swinging at empty air. alwaysFaceTarget (see that
            // field's own comment) additionally runs this while Idle/out of detection range too.
            if ((CurrentState != EnemyState.Idle || alwaysFaceTarget) && direction.sqrMagnitude > 0.0001f)
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

        // Direct port of CharacterMovement.TryGetGroundNormal - see that method's own comment
        // for why the cast origin/distance are computed this way (bottom-hemisphere-center
        // origin, not the capsule's naive local Y=0). Kept as a separate copy rather than
        // extracted into a shared static utility: it needs live instance state (_controller,
        // transform.root) that would just turn into extra parameters either way, and this
        // class's own header comment already documents the deliberate choice not to share
        // movement code with CharacterMovement.
        private bool TryGetGroundNormal(out Vector3 normal, out CharacterController otherCharacterController)
        {
            float capsuleBottomLocalY = _controller.center.y - _controller.height / 2f;
            Vector3 origin = transform.position + new Vector3(0f, capsuleBottomLocalY + _controller.radius, 0f);
            float castDistance = _controller.radius + 0.3f;
            float castRadius = Mathf.Max(0.05f, _controller.radius * 0.8f);

            RaycastHit[] hits = Physics.SphereCastAll(origin, castRadius, Vector3.down, castDistance, ~0, QueryTriggerInteraction.Ignore);
            float closestDistance = float.MaxValue;
            normal = Vector3.up;
            otherCharacterController = null;
            bool found = false;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null || hit.collider.transform.root == transform.root)
                {
                    continue;
                }

                if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    normal = hit.normal;
                    otherCharacterController = hit.collider.GetComponent<CharacterController>();
                    found = true;
                }
            }

            return found;
        }
    }
}
