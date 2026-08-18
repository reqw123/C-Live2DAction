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

        // 2026-08-17, explicit user request ("想要製作斬殺系統...滿格會陷入僵直") - optional
        // (null-safe below) so ordinary enemies without the stagger/execution mechanic (076,
        // any future enemy that never gets a StancePoise) behave exactly as before. While
        // staggered this OVERRIDES whatever EnemyBehaviorUtility.DetermineState would have
        // returned from raw distance - a staggered character stands frozen and open regardless
        // of how close the player is, that's the entire point of the mechanic.
        [SerializeField] private Live2DAction.Combat.StancePoise stance;

        // 2026-08-18, explicit user request (aerial combat grilling session - see CONTEXT.md's
        // own "Aerial Combat" entry). An independent, simplified AI hover/chase - deliberately
        // does NOT share the player's Flight/Glide/Flight Energy system (see that decision's own
        // reasoning: an AI doesn't need a player-facing resource meter, it just needs to
        // reliably reach the target altitude). Entering requires the target to be more than
        // aerialCombatEnterHeight away vertically (either direction) while still within
        // detectionRange; once engaged, only exits on closing to within its OWN effective attack
        // range vertically (see effectiveAttackRange below - NOT aerialCombatEnterHeight again,
        // that was a real playtested bug: reusing the same 3m value for both enter and exit gave
        // zero hysteresis, so the instant the climb reduced the gap to exactly 3m it would drop
        // out of aerial control, free-fall under gravity, the gap would grow past 3m again as it
        // fell, and it would re-enter next frame - a rapid enter/exit bounce that reads as
        // "奇怪的飛行姿勢和軌跡" and, worse, meant it always dropped out of the vertical-tolerant
        // spherical judgment BEFORE ever closing to melee range, so "盡量飛得很靠近玩家才攻擊得到
        // 玩家" never actually happened - it would bail 3m short of hitting range every time)
        // or exceeding aerialCombatChaseCeiling (a safety valve against chasing the player out of
        // the level).
        [SerializeField] private float aerialCombatEnterHeight = 3f;

        // 2026-08-18, explicit user request ("讓敵人能夠飛得比角色高1.5倍 飛行速度比玩家快1.2
        // 倍") - both tuned directly off the player's own CharacterMovement flight stats rather
        // than picked arbitrarily, so "1.5x/1.2x the player" stays literally true:
        //   - aerialCombatChaseCeiling: 1.5x the player's own max CONTINUOUS climb distance in
        //     one full Flight burst (flightAscendSpeed * maxEnergy / flightEnergyDrainPerSecond
        //     = 6 * 100/20 = 30m at this project's current player tuning) - 45m, so Player4 can
        //     always out-climb the highest the player alone could reach in a single ascent,
        //     never gives up the chase for merely matching the player's own ceiling.
        //   - aerialVerticalSpeed: 1.2x the player's flightAscendSpeed (6 * 1.2 = 7.2) - lets it
        //     actually gain on a climbing player instead of only matching their rate.
        // Deliberately hardcoded (not read live from CharacterMovement each frame) - same
        // "picked a number, wrote down why" precedent as every other stat tuned by direct request
        // this session (health/damage percentages etc.), not a live-coupling relationship that
        // needs to silently stay in sync if the player's own flight stats are retuned later.
        [SerializeField] private float aerialCombatChaseCeiling = 45f;
        [SerializeField] private float aerialVerticalSpeed = 7.2f;

        // Aerial-only horizontal chase/attack speed (2026-08-18, same request as above) - kept
        // SEPARATE from the ground `moveSpeed` above deliberately, so boosting Player4's flight
        // speed doesn't also make it faster on the ground (the user asked for flight speed
        // specifically). 1.2x the player's own ground moveSpeed (2 * 1.2 = 2.4) - the player's
        // Flight doesn't have its own distinct horizontal speed, it reuses moveSpeed while
        // airborne, so that's the correct "player's flight speed" baseline to scale from.
        [SerializeField] private float aerialHorizontalSpeed = 2.4f;

        // Shared with CharacterMovement's own identical field/AimUtility - see that field's
        // comment for why a hard clamp instead of tipping all the way to straight up/down.
        [SerializeField] private float maxPitchDegrees = 60f;

        private CharacterController _controller;
        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;
        private bool _isAerialCombat;

        // 2026-08-18, REVERTED same day from applying pitch to THIS transform's rotation - real
        // playtested bug: this is also the CharacterController's own transform, and the capsule's
        // "up" axis follows the transform's local Y axis, so pitching it didn't just aim the body,
        // it physically tipped the collision capsule over - the body visibly flickered between
        // standing and lying flat ("一下站立一下躺著反覆"), and the tilted capsule's collision
        // response fought the vertical climb enough that Player4 could never gain net altitude on
        // the player ("位置始終會低於玩家"). Fixed the same way as CharacterMovement's identical
        // problem: pitch now goes on the "Visual" child (Animator only, no CharacterController)
        // instead, so the body still visually looks up/down at an aerial target without the
        // capsule ever leaving upright.
        private Transform _visual;
        private float _visualPitch;

        public EnemyState CurrentState { get; private set; } = EnemyState.Idle;

        // Exposed for anything that wants to react to Player4 specifically being in Aerial
        // Combat (currently nothing external needs to - PlayerCombat.UseSphericalJudgment is
        // set directly from here each frame instead - but this mirrors CharacterMovement's own
        // IsFlying/IsGliding public exposure for consistency and future use).
        public bool IsAerialCombat => _isAerialCombat;

        // 2026-08-18, explicit user request ("上升氣流，任何人碰到...會快速飛向空中") - see
        // CharacterMovement.ApplyUpwardLaunch's own comment for why Max (not a flat assignment)
        // and why this needs to exist at all rather than a single one-shot impulse.
        public void ApplyUpwardLaunch(float speed)
        {
            _verticalVelocity = Mathf.Max(_verticalVelocity, speed);
        }

        // MoveInput is exposed for IInputCommand compliance/inspection, but EnemyAI drives
        // its own CharacterController.Move directly rather than anything consuming this
        // value the way CharacterMovement consumes the player's MoveInput.
        public Vector2 MoveInput { get; private set; }
        public bool AttackPressed { get; private set; }
        public bool DodgePressed => false;
        public bool LockOnPressed => false;
        public bool JumpPressed => false;
        public bool UltimatePressed => false; // AI never triggers the player-only ultimate
        public bool FlyPressed => false; // AI never triggers the player-only flight
        public bool FlyDescendPressed => false;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _visual = transform.Find("Visual");
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
            Vector3 fullToTarget = target.position - transform.position;
            Vector3 toTarget = fullToTarget;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            float heightDiff = fullToTarget.y;

            bool staggered = stance != null && stance.IsStaggered;

            // effectiveAttackRange is needed by the aerial exit check below now (not just
            // CurrentState further down), so resolve it first.
            float effectiveAttackRange = ResolveEffectiveAttackRange();

            // Aerial Combat enter/exit - see aerialCombatEnterHeight's own comment. Frozen
            // (neither entered nor exited) while staggered, same "don't touch anything while
            // frozen open" reasoning the rest of this method already applies. Exit uses
            // effectiveAttackRange, NOT aerialCombatEnterHeight - deliberate hysteresis so it
            // keeps closing the vertical gap all the way down to actual melee range instead of
            // bailing out (and losing the spherical judgment tolerance) 3m short of hitting range.
            //
            // 2026-08-18, real playtested bug ("敵人飛行時如果攻擊玩家會停留在原地 導致跟不上玩家
            // 最後一直卡在飛行-攻擊落空-飛行的狀態") - the "close enough, exit aerial" check used
            // to fire the instant heightDiff crossed effectiveAttackRange, INCLUDING mid-swing.
            // The moment it exited, vertical tracking switched off (gravity took over) and
            // horizontal chase froze too (see the Attacking branch below) - exactly the ground-
            // melee "plant your feet and swing" assumption, which falls apart against a target
            // that can freely reposition in 3D faster than any melee range tolerates. The result:
            // it swings at wherever the player WAS, the player's long since moved, the whiffed
            // attack ends, distance has grown back past the aerial threshold, it re-chases,
            // catches up, swings again, whiffs again - the reported loop. Fix: while a swing is
            // actually in progress (combat.CurrentPhase != Idle), don't exit aerial tracking even
            // if the height gap has already closed - keep adjusting altitude (and, per the
            // Attacking branch below, horizontal position too) for the swing's entire duration,
            // only actually "landing" once it's both close AND between swings.
            bool midSwing = combat != null && combat.CurrentPhase != AttackPhase.Idle;
            if (!staggered)
            {
                if (!_isAerialCombat)
                {
                    if (Mathf.Abs(heightDiff) > aerialCombatEnterHeight && distance <= detectionRange)
                    {
                        _isAerialCombat = true;
                    }
                }
                else if (Mathf.Abs(heightDiff) > aerialCombatChaseCeiling
                    || (!midSwing && Mathf.Abs(heightDiff) <= effectiveAttackRange))
                {
                    _isAerialCombat = false;
                }
            }

            if (combat != null)
            {
                combat.UseSphericalJudgment = _isAerialCombat;
            }

            CurrentState = staggered
                ? EnemyState.Staggered
                : EnemyBehaviorUtility.DetermineState(distance, detectionRange, effectiveAttackRange);

            Vector3 direction = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector3.zero;

            // Same bug/fix as the aerial exit condition above - ground melee freezes horizontal
            // movement while Attacking (the target isn't going anywhere mid-swing), but Aerial
            // Combat's target very much can, so it keeps chasing horizontally through its own
            // swing too instead of planting in place and letting the player fly out of range.
            bool shouldChaseHorizontally = CurrentState == EnemyState.Chasing
                || (_isAerialCombat && CurrentState == EnemyState.Attacking);
            // aerialHorizontalSpeed (not the ground moveSpeed) while genuinely in Aerial Combat -
            // see that field's own comment for the 1.2x-the-player's-flight-speed reasoning.
            float horizontalSpeed = _isAerialCombat ? aerialHorizontalSpeed : moveSpeed;
            _horizontalVelocity = shouldChaseHorizontally ? direction * horizontalSpeed : Vector3.zero;
            MoveInput = new Vector2(direction.x, direction.z);

            // Attacking horizontally-in-range isn't enough while Aerial Combat is active - the
            // vertical gap has to have actually closed too, or the swing has no real chance of
            // landing even with UseSphericalJudgment's extra tolerance (see this class's own
            // Aerial Combat comment - that tolerance is meant to forgive imperfect aim, not a
            // 10m miss).
            bool verticallyInRange = !_isAerialCombat || Mathf.Abs(heightDiff) <= effectiveAttackRange;
            AttackPressed = CurrentState == EnemyState.Attacking && verticallyInRange;

            if (!staggered && _isAerialCombat)
            {
                // Simple proportional approach toward the target's altitude, capped at
                // aerialVerticalSpeed - closes fast when far, slows naturally as it nears rather
                // than overshooting and oscillating around the target height.
                _verticalVelocity = Mathf.Clamp(heightDiff, -aerialVerticalSpeed, aerialVerticalSpeed);
            }
            else
            {
                if (_controller.isGrounded && _verticalVelocity < 0f)
                {
                    _verticalVelocity = -1f;
                }
                _verticalVelocity += gravity * Time.deltaTime;
            }

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
            // Body (this transform, yaw only - see _visual's own comment for why) always turns
            // toward the horizontal `direction`, matching ground combat exactly.
            if ((CurrentState != EnemyState.Idle || alwaysFaceTarget) && direction.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeedDegrees * Time.deltaTime);
            }

            // Visual-only pitch on top of that horizontal body turn, while Aerial Combat is
            // active - purely cosmetic (spherical judgment doesn't need it, see
            // UseSphericalJudgment's own comment), applied to _visual's LOCAL rotation so it
            // never touches the CharacterController's own upright transform. Tracked as a plain
            // float + MoveTowardsAngle (same constant-degrees/sec idiom this file already uses
            // for yaw above) rather than reading back _visual.localEulerAngles.x each frame -
            // Euler angles wrap at 0/360, which would fight a naive angle read-back near that seam.
            float targetPitch = _isAerialCombat ? AimUtility.ClampedPitchDegrees(fullToTarget, maxPitchDegrees) : 0f;
            _visualPitch = Mathf.MoveTowardsAngle(_visualPitch, targetPitch, rotationSpeedDegrees * Time.deltaTime);
            if (_visual != null)
            {
                _visual.localRotation = Quaternion.Euler(-_visualPitch, 0f, 0f);
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
