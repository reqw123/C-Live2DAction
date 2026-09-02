using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Live2DAction.Core;
using Live2DAction.Characters;

namespace Live2DAction.AI
{
    // A fast melee "ten-legged bug" enemy built for 十足蟲.glb (a rigged Meshy model with a
    // generic Bone_XXX skeleton and ZERO animation clips - so, like the Cat, everything visual is
    // procedural). Deliberately a fresh controller rather than an extension of
    // Combat/Boss/BossStateMachine: that class is 2000+ lines of humanoid-animation-clip-driven
    // boss logic (Breakdance / Leap Slam / Ultimate / dodge-counter / Cinemachine hooks) that has
    // nothing in common with a legless procedurally-animated bug. What it DOES reuse from the
    // existing project:
    //   * Live2DAction.Core.Health / IDamageable / DamageInfo  - HP and the player's damage pipeline
    //   * Live2DAction.AI.NavPathFollower (optional)           - route the chase around obstacles
    //   * Live2DAction.Combat.Boss.BossTeamMember (added by the setup tool) - friendly-fire tag
    //   * CharacterController + manual .Move()                 - the project's ONE movement system
    //   * TenLeggedBugGaitUtility / TenLeggedBugAttackUtility  - pure, EditMode-tested motion math
    //
    // Movement authority: this component turns the ROOT transform (which carries the
    // CharacterController = the one body capsule, and defines "forward" = the attack direction).
    // bodyRootBone visually follows the root (it's a child) plus an optional bank/lean, so the
    // spec's "body trunk bone drives the visual turn, root + collider + attack direction all turn
    // in sync" holds: they are the same yaw, applied at the root and mirrored (with flavour) onto
    // the bone.
    //
    // Every tunable the spec asked to expose (distances, speeds, turn rate, cone angle, attack
    // interval, damage, search time, stagger time, death fade times) is a serialized field below.
    [RequireComponent(typeof(CharacterController))]
    public class TenLeggedBugController : MonoBehaviour
    {
        public enum BugState
        {
            Patrol,   // wandering around its spawn point, unaware of the player
            Chase,    // player detected, closing distance fast (routed around obstacles)
            Attack,   // in range - planted, facing, doing the rhino-horn stab cycle
            Search,   // lost the player - moving to last-known position then looking around
            Stagger,  // horn stuck in the ground after a 3-hit combo, open to counterattack
            Death     // HP 0 - flipping belly-up, then fading out
        }

        // ---------------------------------------------------------------------------------------
        // 1. Bone references  (spec section 1 - assigned by hand in the Inspector, NEVER guessed
        //    from the GLB's generic bone names at runtime)
        // ---------------------------------------------------------------------------------------
        [Header("Bones (assign by hand - see setup tool / delivery notes)")]
        [Tooltip("Body trunk bone. Drives the visual turn/lean; the root transform turns with it.")]
        [SerializeField] private Transform bodyRootBone;

        [Tooltip("Horn / snout bone. Pitched up for the wind-up telegraph, whipped down for the stab.")]
        [SerializeField] private Transform hornBone;

        [Tooltip("Root ('hip') bone of each leg, in the spec's fixed order: index 0 = leg 1 " +
                 "(front-left), index 1 = leg 2 (front-right), then alternating L/R front-to-back. " +
                 "The gait steps through this list strictly in order. Any length is fine - the " +
                 "cycle just divides into that many slices (十足蟲.glb rigs 8 ground legs).")]
        [SerializeField] private List<Transform> legRootBones = new List<Transform>();

        [Tooltip("Optional. The bend ('knee') bone for each leg, same order as Leg Root Bones. " +
                 "Leave empty to auto-use each leg root's first child.")]
        [SerializeField] private List<Transform> legBendBones = new List<Transform>();

        // ---------------------------------------------------------------------------------------
        // 2. Targeting / detection
        // ---------------------------------------------------------------------------------------
        [Header("Targeting")]
        [Tooltip("The player (or whatever this bug hunts). Assigned by the setup tool to 'Player'.")]
        [SerializeField] private Transform target;

        [Tooltip("2026-09-02, user request (\"讓十足蟲不會自動追擊人\") - when false, the bug never leaves " +
                 "Patrol on its own (detectionRange is ignored). It still fully works if something else " +
                 "drives its state - a debug tool, a scripted trigger, or a future aggro-on-hit hook.")]
        [SerializeField] private bool autoAggro = true;

        [Tooltip("Horizontal distance at which an unaware bug notices the player and starts chasing.")]
        [SerializeField] private float detectionRange = 12f;

        [Tooltip("Horizontal distance past which a chasing bug gives up and switches to Search. " +
                 "Keep above detectionRange so it doesn't flip-flop at the edge.")]
        [SerializeField] private float loseTargetRange = 16f;

        [Tooltip("Horizontal distance within which the bug switches from Chase to Attack. It then " +
                 "keeps pressing IN to true body contact before it actually stabs - so keep this " +
                 "modest, it's the 'commit to the attack' range, not the strike range.")]
        [SerializeField] private float attackRange = 1.3f;

        [Tooltip("Assumed world radius of the player's body capsule. Used with the bug's own " +
                 "capsule radius to work out how close the two can physically get - the bug " +
                 "presses in to that distance before stabbing (so there's no visible gap).")]
        [SerializeField] private float playerBodyRadius = 0.45f;

        // ---------------------------------------------------------------------------------------
        // 3. Movement
        // ---------------------------------------------------------------------------------------
        [Header("Movement")]
        // 2026-08-31, user feedback ("蟲速度太快了 *0.7倍") - dialled the whole movement set down
        // to 0.7x (chase 5.5->3.85, patrol 1.4->0.98, search 3->2.1). gaitSpeedForFullRate below
        // tracks chaseSpeed so the leg cadence still matches at top speed.
        [Tooltip("Chase speed (units/sec) - this is the 'high-speed pursuit' the spec wants.")]
        [SerializeField] private float chaseSpeed = 3.85f;

        [Tooltip("Patrol wander speed (units/sec).")]
        [SerializeField] private float patrolSpeed = 0.98f;

        [Tooltip("Search-move speed (units/sec) on the way to the last-known player position.")]
        [SerializeField] private float searchMoveSpeed = 2.1f;

        [Tooltip("How fast the bug yaws to face its move/target direction (degrees/sec).")]
        [SerializeField] private float rotationSpeedDegrees = 360f;

        [Tooltip("Downward acceleration (units/sec^2) applied through the CharacterController.")]
        [SerializeField] private float gravity = -22f;

        [Tooltip("Radius around the spawn point the bug wanders within while patrolling.")]
        [SerializeField] private float patrolRadius = 8f;

        [Tooltip("How close (m) to a patrol/search destination counts as 'arrived'.")]
        [SerializeField] private float arriveDistance = 0.6f;

        // ---------------------------------------------------------------------------------------
        // 4. Procedural gait  (spec section 2 - visual bones only, NO per-leg colliders)
        // ---------------------------------------------------------------------------------------
        [Header("Gait")]
        [Tooltip("Move speed (units/sec) at which the gait runs at its reference cycle rate. " +
                 "Match roughly to chaseSpeed so a full-speed charge cycles briskly.")]
        [SerializeField] private float gaitSpeedForFullRate = 3.85f;

        [Tooltip("Gait cycles per second at gaitSpeedForFullRate. One cycle = every leg has " +
                 "stepped once. Phase advance is proportional to actual speed.")]
        [SerializeField] private float gaitBaseRateHz = 2.2f;

        [Tooltip("Peak fore-aft swing of a leg at its hip during a step, degrees.")]
        [SerializeField] private float legSwingDegrees = 22f;

        [Tooltip("Peak knee bend of the stepping leg (lift), degrees. Needs a bend bone " +
                 "(explicit or auto first-child).")]
        [SerializeField] private float legLiftDegrees = 34f;

        [Tooltip("Extra outward splay (deg) added to the front two leg pairs during an attack " +
                 "wind-up - the 'front legs open' telegraph.")]
        [SerializeField] private float attackFrontLegSplayDegrees = 18f;

        [Tooltip("How many of the leading legs count as 'front legs' for the splay / brace poses.")]
        [SerializeField] private int frontLegCount = 4;

        [Tooltip("How fast leg poses ease between gait / planted-brace / attack (blend units/sec).")]
        [SerializeField] private float legBlendSpeed = 10f;

        // ---------------------------------------------------------------------------------------
        // 5. Attack  (spec section 3)
        // ---------------------------------------------------------------------------------------
        [Header("Attack")]
        [Tooltip("Half-angle (deg) of the frontal cone the target must be inside for the bug to " +
                 "attack. Outside it, the bug stops attacking and turns to face first. Spec ~30.")]
        [SerializeField] private float attackConeAngleDegrees = 30f;

        [Tooltip("Seconds for one full stab cycle (wind-up + strike + recover). Spec: 1.")]
        [SerializeField] private float attackCycleSeconds = 1f;

        [Tooltip("Normalized cycle time the wind-up (horn rising) ends. Spec: 0.25.")]
        [Range(0f, 1f)] [SerializeField] private float hornRaiseEndT = 0.25f;

        [Tooltip("Normalized cycle time the down-stab ends. Spec: 0.45.")]
        [Range(0f, 1f)] [SerializeField] private float hornStabEndT = 0.45f;

        [Tooltip("Normalized cycle time the horn hitbox turns ON (contact frames start).")]
        [Range(0f, 1f)] [SerializeField] private float strikeWindowStartT = 0.28f;

        [Tooltip("Normalized cycle time the horn hitbox turns OFF (contact frames end).")]
        [Range(0f, 1f)] [SerializeField] private float strikeWindowEndT = 0.45f;

        [Tooltip("Degrees the horn/head raises during the wind-up telegraph.")]
        [SerializeField] private float hornRaiseDegrees = 28f;

        [Tooltip("Degrees the horn/head drives DOWN past rest during the stab.")]
        [SerializeField] private float hornStabDegrees = 46f;

        [Tooltip("HP removed from the player on a clean horn strike. Spec: 10.")]
        [SerializeField] private float hornDamage = 10f;

        [Tooltip("The horn hitbox component (on a child of the horn bone). Assigned by the setup tool.")]
        [SerializeField] private TenLeggedBugHornHitbox hornHitbox;

        [Tooltip("Number of stabs landed back-to-back before the bug staggers itself. Spec: 3.")]
        [SerializeField] private int attacksBeforeStagger = 3;

        // ---------------------------------------------------------------------------------------
        // 6. Stagger  (spec section 3 - the counterattack window)
        // ---------------------------------------------------------------------------------------
        [Header("Stagger")]
        [Tooltip("Minimum stagger duration (sec). Spec: 0.7.")]
        [SerializeField] private float staggerSecondsMin = 0.7f;

        [Tooltip("Maximum stagger duration (sec). Spec: 1.0.")]
        [SerializeField] private float staggerSecondsMax = 1f;

        [Tooltip("Degrees the horn is driven down and held while staggered ('stuck in the ground').")]
        [SerializeField] private float staggerHornDownDegrees = 52f;

        // ---------------------------------------------------------------------------------------
        // 7. Search  (spec section 4)
        // ---------------------------------------------------------------------------------------
        [Header("Search")]
        [Tooltip("Minimum look-around time at the last-known position (sec). Spec: 2.")]
        [SerializeField] private float searchSecondsMin = 2f;

        [Tooltip("Maximum look-around time (sec). Spec: 3.")]
        [SerializeField] private float searchSecondsMax = 3f;

        [Tooltip("Left-right sweep amplitude of the body/horn while searching, degrees.")]
        [SerializeField] private float searchSweepDegrees = 35f;

        // ---------------------------------------------------------------------------------------
        // 8. Death  (spec section 5 - flip belly-up, hold, fade, destroy)
        // ---------------------------------------------------------------------------------------
        [Header("Death / Respawn")]
        [Tooltip("Seconds to roll the body 180 degrees belly-up (legs up) when killed.")]
        [SerializeField] private float flipOverSeconds = 0.65f;

        [Tooltip("Seconds the bug stays dead (belly-up) before it revives. User request: 5.")]
        [SerializeField] private float respawnDelaySeconds = 5f;

        [Tooltip("Seconds to roll back upright and stand up on revive.")]
        [SerializeField] private float getUpSeconds = 0.6f;

        [Tooltip("Where the bug reappears on revive. In Place = flips back up where it died " +
                 "(matches the project's boss revive). At Spawn = teleports back to its spawn point.")]
        [SerializeField] private RespawnMode respawnMode = RespawnMode.InPlace;

        public enum RespawnMode { InPlace, AtSpawn }

        // ---------------------------------------------------------------------------------------
        // Runtime state
        // ---------------------------------------------------------------------------------------
        private CharacterController _controller;
        private Health _health;
        private NavPathFollower _pathFollower; // optional

        private BugState _state = BugState.Patrol;
        public BugState State => _state;

        private Vector3 _spawnPosition;
        private Vector3 _patrolDestination;
        private Vector3 _lastKnownTargetPos;

        private float _verticalVelocity;
        private float _gaitPhase;
        private float _gaitBlend;      // 0 = planted brace pose, 1 = full walking gait
        private float _attackBlend;    // 0 = no attack pose, 1 = full lunge pose

        private float _attackTimer;
        private int _attackComboCount;
        private bool _hornHitboxOpen;

        private float _staggerTimer;
        private float _staggerDuration;

        private float _searchTimer;
        private float _searchDuration;
        private bool _searchArrived;

        private bool _dead;

        // rest-pose snapshots (local rotations), captured once so every procedural pose is an
        // offset FROM the imported bind pose rather than an absolute the generic rig can't define.
        private Quaternion _bodyRootRest;
        private Quaternion _hornRest;
        private Quaternion[] _legSwingRest;
        private Quaternion[] _legBendRest;
        private Transform[] _legBend; // resolved (explicit or auto first-child)

        private static readonly System.Random Rng = new System.Random();

        // ---------------------------------------------------------------------------------------

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _health = GetComponent<Health>();
            _pathFollower = GetComponent<NavPathFollower>();

            _spawnPosition = transform.position;
            _patrolDestination = transform.position;

            CaptureRestPose();

            if (hornHitbox != null)
            {
                hornHitbox.Configure(transform);
                hornHitbox.Damage = hornDamage;
                hornHitbox.Deactivate();
            }

            if (_health != null)
            {
                _health.Died += OnDied;
            }
        }

        private void OnDestroy()
        {
            if (_health != null)
            {
                _health.Died -= OnDied;
            }
        }

        // Public so the setup tool / a test can re-snapshot after re-assigning bones.
        public void CaptureRestPose()
        {
            _bodyRootRest = bodyRootBone != null ? bodyRootBone.localRotation : Quaternion.identity;
            _hornRest = hornBone != null ? hornBone.localRotation : Quaternion.identity;

            int n = legRootBones.Count;
            _legSwingRest = new Quaternion[n];
            _legBendRest = new Quaternion[n];
            _legBend = new Transform[n];
            // If legBendBones is fully specified (one entry per leg) it is used verbatim - a null
            // entry then means "this leg has no separate knee bone", NOT "go find one". That
            // matters for 十足蟲.glb: some legs share a chain (leg #6's bone is the parent of
            // leg #8's), so blindly taking swing.GetChild(0) would steal another leg's bone.
            bool bendFullySpecified = legBendBones.Count == n;
            for (int i = 0; i < n; i++)
            {
                Transform swing = legRootBones[i];
                _legSwingRest[i] = swing != null ? swing.localRotation : Quaternion.identity;

                Transform bend = bendFullySpecified
                    ? legBendBones[i]
                    : (i < legBendBones.Count && legBendBones[i] != null)
                        ? legBendBones[i]
                        : (swing != null && swing.childCount > 0 ? swing.GetChild(0) : null);
                _legBend[i] = bend;
                _legBendRest[i] = bend != null ? bend.localRotation : Quaternion.identity;
            }
        }

        // =======================================================================================
        //  Main update - state machine + movement.  Visual bone posing is in LateUpdate.
        // =======================================================================================
        private void Update()
        {
            if (_dead)
            {
                return; // the death coroutine owns everything now
            }

            float dt = Time.deltaTime;

            Vector3 toTarget = Vector3.zero;
            float distance = float.PositiveInfinity;
            if (target != null)
            {
                toTarget = target.position - transform.position;
                toTarget.y = 0f;
                distance = toTarget.magnitude;
            }
            Vector3 dirToTarget = distance > 0.0001f ? toTarget / distance : transform.forward;

            switch (_state)
            {
                case BugState.Patrol:  TickPatrol(dt, distance); break;
                case BugState.Chase:   TickChase(dt, distance, dirToTarget); break;
                case BugState.Attack:  TickAttack(dt, distance, dirToTarget); break;
                case BugState.Search:  TickSearch(dt, distance); break;
                case BugState.Stagger: TickStagger(dt); break;
            }
        }

        // --------------------------------------------------------------------------- Patrol ----
        private void TickPatrol(float dt, float distanceToTarget)
        {
            if (autoAggro && target != null && distanceToTarget <= detectionRange)
            {
                EnterChase();
                return;
            }

            Vector3 toDest = _patrolDestination - transform.position;
            toDest.y = 0f;
            if (toDest.magnitude <= arriveDistance)
            {
                // Pick a new wander point around the spawn - reuse the project's WanderUtility
                // direction rule (steers back toward the origin near the boundary).
                Vector3 dir = WanderUtility.ComputeDirection(
                    transform.position - _spawnPosition, transform.forward, patrolRadius,
                    () => (float)(Rng.NextDouble() * 360.0));
                _patrolDestination = _spawnPosition + dir * (patrolRadius * (0.4f + 0.6f * (float)Rng.NextDouble()));
                toDest = _patrolDestination - transform.position;
                toDest.y = 0f;
            }

            Vector3 moveDir = toDest.sqrMagnitude > 0.0001f ? toDest.normalized : Vector3.zero;
            FaceDirection(moveDir, dt);
            MoveHorizontally(moveDir, patrolSpeed, dt);
            _gaitBlend = Mathf.MoveTowards(_gaitBlend, moveDir != Vector3.zero ? 1f : 0f, legBlendSpeed * dt);
            _attackBlend = Mathf.MoveTowards(_attackBlend, 0f, legBlendSpeed * dt);
        }

        // ---------------------------------------------------------------------------- Chase ----
        private void TickChase(float dt, float distanceToTarget, Vector3 dirToTarget)
        {
            if (target == null || distanceToTarget > loseTargetRange)
            {
                EnterSearch();
                return;
            }
            if (distanceToTarget <= attackRange)
            {
                EnterAttack();
                return;
            }

            // Route around scenery when a NavPathFollower + baked mesh are present; fail open to a
            // straight line at the player (same contract EnemyAI / BossStateMachine use).
            Vector3 moveDir = _pathFollower != null ? _pathFollower.SteeringDirection(target.position) : dirToTarget;
            if (moveDir.sqrMagnitude < 0.0001f)
            {
                moveDir = dirToTarget;
            }

            FaceDirection(dirToTarget, dt);           // always LOOK at the player,
            MoveHorizontally(moveDir, chaseSpeed, dt); // even while stepping around an obstacle
            _gaitBlend = Mathf.MoveTowards(_gaitBlend, 1f, legBlendSpeed * dt);
            _attackBlend = Mathf.MoveTowards(_attackBlend, 0f, legBlendSpeed * dt);
            _lastKnownTargetPos = target.position;
        }

        // --------------------------------------------------------------------------- Attack ----
        private void TickAttack(float dt, float distanceToTarget, Vector3 dirToTarget)
        {
            if (target == null)
            {
                EnterSearch();
                return;
            }
            if (distanceToTarget > attackRange * 1.6f) // generous hysteresis - it will press IN below
            {
                EnterChase();
                return;
            }

            // Always keep turning toward the player - smoothly, so a side/back player is first
            // faced before any stab resumes (spec section 3).
            FaceDirection(dirToTarget, dt);

            // 2026-08-31, user feedback ("不能讓蟲確實的衝破碰撞體到玩家腳下嗎，比較有真實感") - the
            // bug used to hard-stop the instant it crossed attackRange, leaving a visible ~1m gap.
            // Now it keeps pressing forward at chase speed until its own CharacterController is
            // physically jammed against the player's, THEN plants and stabs from true body contact.
            // contactDistance is the real minimum a solid capsule pair can reach: this bug's world
            // radius + an assumed player body radius + a small buffer (same shape EnemyAI's
            // groundContactDistance / alwaysChaseWhileAttacking fix uses).
            float ownWorldRadius = _controller.radius * transform.lossyScale.x;
            float contactDistance = ownWorldRadius + playerBodyRadius + 0.08f;
            bool atContact = distanceToTarget <= contactDistance;

            if (!atContact)
            {
                // Still closing the last stretch - keep walking straight in (tapered so it doesn't
                // shove the player around once jammed), legs keep striding. The stab clock does
                // NOT advance yet.
                float remaining = Mathf.Max(0f, distanceToTarget - contactDistance);
                float speed = Mathf.Min(chaseSpeed, remaining / Mathf.Max(dt, 0.0001f));
                MoveHorizontally(dirToTarget, speed, dt);
                _gaitBlend = Mathf.MoveTowards(_gaitBlend, 1f, legBlendSpeed * dt);
                _attackBlend = Mathf.MoveTowards(_attackBlend, 0f, legBlendSpeed * dt);
                _attackTimer = 0f;
                SetHornHitbox(false);
                return;
            }

            // At contact - planted, all legs brace (spec), no walking gait.
            MoveHorizontally(Vector3.zero, 0f, dt);
            _gaitBlend = Mathf.MoveTowards(_gaitBlend, 0f, legBlendSpeed * dt);

            bool inCone = TenLeggedBugAttackUtility.TargetWithinAttackCone(
                Flat(transform.forward), Flat(dirToTarget), attackConeAngleDegrees);

            if (!inCone)
            {
                // Abort the current swing cleanly and wait until we're facing the player.
                _attackTimer = 0f;
                SetHornHitbox(false);
                _attackBlend = Mathf.MoveTowards(_attackBlend, 0f, legBlendSpeed * dt);
                return;
            }

            _attackTimer += dt;
            float t01 = attackCycleSeconds > 0.0001f ? _attackTimer / attackCycleSeconds : 1f;

            // Drive the front-leg-spread / head-press telegraph from the same clock as the horn.
            _attackBlend = TenLeggedBugAttackUtility.AttackTelegraph01(
                Mathf.Clamp01(t01), hornRaiseEndT, hornStabEndT);

            // Horn hitbox: live ONLY across the contact frames of the down-stab.
            bool strikeLive = TenLeggedBugAttackUtility.HornStrikeIsLive(t01, strikeWindowStartT, strikeWindowEndT);
            SetHornHitbox(strikeLive);

            if (_attackTimer >= attackCycleSeconds)
            {
                _attackTimer = 0f;
                _attackComboCount++;
                SetHornHitbox(false);
                if (_attackComboCount >= attacksBeforeStagger)
                {
                    EnterStagger();
                }
            }
        }

        // -------------------------------------------------------------------------- Stagger ----
        private void TickStagger(float dt)
        {
            MoveHorizontally(Vector3.zero, 0f, dt);
            _gaitBlend = Mathf.MoveTowards(_gaitBlend, 0f, legBlendSpeed * dt);
            _attackBlend = Mathf.MoveTowards(_attackBlend, 1f, legBlendSpeed * dt); // stay hunched
            _staggerTimer += dt;
            if (_staggerTimer >= _staggerDuration)
            {
                _attackComboCount = 0;
                // Re-evaluate: chase or attack depending on where the player is now.
                if (target != null)
                {
                    Vector3 to = target.position - transform.position; to.y = 0f;
                    _state = to.magnitude <= attackRange ? BugState.Attack : BugState.Chase;
                    if (_state == BugState.Attack) { _attackTimer = 0f; }
                }
                else
                {
                    EnterSearch();
                }
            }
        }

        // --------------------------------------------------------------------------- Search ----
        private void TickSearch(float dt, float distanceToTarget)
        {
            if (target != null && distanceToTarget <= detectionRange)
            {
                EnterChase();
                return;
            }

            if (!_searchArrived)
            {
                Vector3 toDest = _lastKnownTargetPos - transform.position;
                toDest.y = 0f;
                if (toDest.magnitude <= arriveDistance)
                {
                    _searchArrived = true;
                    _searchTimer = 0f;
                }
                else
                {
                    Vector3 moveDir = _pathFollower != null
                        ? _pathFollower.SteeringDirection(_lastKnownTargetPos)
                        : toDest.normalized;
                    if (moveDir.sqrMagnitude < 0.0001f) moveDir = toDest.normalized;
                    FaceDirection(moveDir, dt);
                    MoveHorizontally(moveDir, searchMoveSpeed, dt);
                    _gaitBlend = Mathf.MoveTowards(_gaitBlend, 1f, legBlendSpeed * dt);
                    return;
                }
            }

            // Arrived - stand and sweep the body/horn left-right for a couple of seconds.
            MoveHorizontally(Vector3.zero, 0f, dt);
            _gaitBlend = Mathf.MoveTowards(_gaitBlend, 0f, legBlendSpeed * dt);
            _searchTimer += dt;
            if (_searchTimer >= _searchDuration)
            {
                EnterPatrol();
            }
        }

        // ------------------------------------------------------------------- state entries ----
        private void EnterPatrol()
        {
            _state = BugState.Patrol;
            _patrolDestination = transform.position;
        }

        private void EnterChase()
        {
            _state = BugState.Chase;
            if (target != null) _lastKnownTargetPos = target.position;
        }

        private void EnterAttack()
        {
            _state = BugState.Attack;
            _attackTimer = 0f;
        }

        private void EnterStagger()
        {
            _state = BugState.Stagger;
            _staggerTimer = 0f;
            _staggerDuration = Mathf.Lerp(staggerSecondsMin, staggerSecondsMax, (float)Rng.NextDouble());
            SetHornHitbox(false);
        }

        private void EnterSearch()
        {
            _state = BugState.Search;
            _searchArrived = false;
            _searchTimer = 0f;
            _searchDuration = Mathf.Lerp(searchSecondsMin, searchSecondsMax, (float)Rng.NextDouble());
            SetHornHitbox(false);
        }

        // ---------------------------------------------------------------- movement helpers ----
        private void FaceDirection(Vector3 flatDir, float dt)
        {
            flatDir.y = 0f;
            if (flatDir.sqrMagnitude < 0.0001f) return;
            Quaternion want = Quaternion.LookRotation(flatDir.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, rotationSpeedDegrees * dt);
        }

        private void MoveHorizontally(Vector3 flatDir, float speed, float dt)
        {
            // Gravity so it stays on the ground / follows terrain the same way every other
            // CharacterController character in this project does.
            if (_controller.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -1f;
            }
            _verticalVelocity += gravity * dt;

            Vector3 horizontal = flatDir.sqrMagnitude > 0.0001f ? flatDir.normalized * speed : Vector3.zero;
            Vector3 motion = horizontal;
            motion.y = _verticalVelocity;
            _controller.Move(motion * dt);
        }

        private void SetHornHitbox(bool open)
        {
            if (open == _hornHitboxOpen) return;
            _hornHitboxOpen = open;
            if (hornHitbox == null) return;
            if (open) hornHitbox.Activate(); else hornHitbox.Deactivate();
        }

        // =======================================================================================
        //  Visual bone posing - runs after movement so it layers on top of the final root yaw.
        //  Nothing else writes these bones (the model has no Animator clips).
        // =======================================================================================
        private void LateUpdate()
        {
            if (_dead || legRootBones.Count == 0) return;
            if (_legSwingRest == null || _legSwingRest.Length != legRootBones.Count)
            {
                CaptureRestPose(); // list was resized in the Inspector while playing
            }

            float dt = Time.deltaTime;
            Vector3 hinge = transform.right; // every swing/pitch is a hinge about the bug's own right axis

            PoseLegs(dt, hinge);
            PoseHornAndBody(dt, hinge);
        }

        private void PoseLegs(float dt, Vector3 hinge)
        {
            int n = legRootBones.Count;

            // Advance the strict 1 -> N -> repeat cycle by the bug's actual horizontal speed.
            float speed = new Vector3(_controller.velocity.x, 0f, _controller.velocity.z).magnitude;
            _gaitPhase = TenLeggedBugGaitUtility.AdvancePhase(
                _gaitPhase, speed, gaitSpeedForFullRate, gaitBaseRateHz, dt);

            for (int i = 0; i < n; i++)
            {
                Transform swing = legRootBones[i];
                if (swing == null) continue;

                // Walking gait contribution (faded by _gaitBlend): exactly one leg steps at a time.
                float stride = TenLeggedBugGaitUtility.LegStride(_gaitPhase, n, i);   // -1..1 fore-aft
                float lift = TenLeggedBugGaitUtility.LegLift01(_gaitPhase, n, i);     // 0..1 up

                float swingDeg = stride * legSwingDegrees * _gaitBlend;

                // Fore-aft swing: hinge about the bug's right axis (pitch the limb forward/back).
                Quaternion pose = Quaternion.AngleAxis(swingDeg, LocalAxis(swing, hinge)) * _legSwingRest[i];

                // Attack telegraph (faded by _attackBlend): the front two leg pairs YAW outward
                // about vertical - "前兩對腳張開" - while every other leg is unaffected. Left legs
                // (even index = odd leg number, spec) splay one way, right legs the other.
                bool isFrontLeg = i < Mathf.Min(Mathf.Max(0, frontLegCount), n);
                if (isFrontLeg && _attackBlend > 0.0001f)
                {
                    float splayDir = (i % 2 == 0) ? -1f : 1f; // even index = left leg
                    Quaternion splay = Quaternion.AngleAxis(
                        splayDir * attackFrontLegSplayDegrees * _attackBlend, LocalAxis(swing, Vector3.up));
                    pose = splay * pose;
                }

                swing.localRotation = pose;

                Transform bend = _legBend != null && i < _legBend.Length ? _legBend[i] : null;
                if (bend != null)
                {
                    // Knee folds up during this leg's lift; a small constant crouch while bracing.
                    float bendDeg = lift * legLiftDegrees * _gaitBlend + 8f * _attackBlend;
                    ApplyHinge(bend, _legBendRest[i], hinge, bendDeg);
                }
            }
        }

        private void PoseHornAndBody(float dt, Vector3 hinge)
        {
            float hornPitch = 0f;   // + = up, - = down (about the right axis)
            float hornYaw = 0f;     // left-right, for the search sweep
            float bodyPitch = 0f;   // head-down press during the stab
            float bodyBank = 0f;    // lean into turns / hunch

            switch (_state)
            {
                case BugState.Attack:
                {
                    float t01 = attackCycleSeconds > 0.0001f
                        ? Mathf.Clamp01(_attackTimer / attackCycleSeconds) : 0f;
                    hornPitch = TenLeggedBugAttackUtility.HornPitchDegrees(
                        t01, hornRaiseEndT, hornStabEndT, hornRaiseDegrees, hornStabDegrees);
                    // Head/front presses DOWN as the horn drives down (negative pitch phase),
                    // lifts a touch during the wind-up. Scaled by the telegraph blend.
                    bodyPitch = -Mathf.Min(0f, hornPitch) * 0.25f * _attackBlend
                                - Mathf.Max(0f, hornPitch) * 0.15f * _attackBlend;
                    bodyBank = 4f * _attackBlend;
                    break;
                }
                case BugState.Stagger:
                    // Horn buried in the ground, body hunched forward - a fat opening.
                    hornPitch = -staggerHornDownDegrees;
                    bodyPitch = 10f;
                    break;
                case BugState.Search:
                    if (_searchArrived)
                    {
                        float s01 = _searchDuration > 0.0001f ? Mathf.Clamp01(_searchTimer / _searchDuration) : 1f;
                        hornYaw = TenLeggedBugAttackUtility.SearchSweepDegrees(s01, searchSweepDegrees);
                        bodyBank = hornYaw * 0.4f;
                    }
                    break;
            }

            if (hornBone != null)
            {
                Quaternion pitchRot = Quaternion.AngleAxis(hornPitch, LocalAxis(hornBone, hinge));
                Quaternion yawRot = Quaternion.AngleAxis(hornYaw, LocalAxis(hornBone, Vector3.up));
                hornBone.localRotation = yawRot * pitchRot * _hornRest;
            }

            if (bodyRootBone != null)
            {
                // The body trunk bone visually mirrors the root's yaw (it's already a child, so
                // that comes for free) plus this small procedural pitch/bank for character. The
                // root transform itself remains the authority for the collider and attack facing.
                Quaternion pitchRot = Quaternion.AngleAxis(bodyPitch, LocalAxis(bodyRootBone, hinge));
                Quaternion bankRot = Quaternion.AngleAxis(bodyBank, LocalAxis(bodyRootBone, transform.forward));
                bodyRootBone.localRotation = bankRot * pitchRot * _bodyRootRest;
            }
        }

        // ------------------------------------------------------------------- Death / Respawn ----
        private void OnDied()
        {
            if (_dead) return;
            _dead = true;

            // Stop everything the instant HP hits 0 (spec section 5).
            SetHornHitbox(false);
            if (hornHitbox != null)
            {
                hornHitbox.enabled = false;
            }
            _controller.enabled = false; // no more pathing / gravity / being pushed
            StopAllCoroutines();
            StartCoroutine(DeathThenReviveSequence());
        }

        // Dies -> rolls belly-up -> lies there -> after respawnDelaySeconds total, rolls back
        // upright with full HP and resumes patrolling. The GameObject is NEVER deactivated or
        // destroyed (Health.deferDeactivationToDeathAnimation must be true - the setup tool sets
        // it), so this coroutine survives to run the whole cycle - unlike the project's generic
        // RespawnController, which has to live off-character precisely because Health there DOES
        // deactivate. "死亡五秒後復活" (user request).
        private IEnumerator DeathThenReviveSequence()
        {
            Quaternion bodyRest = _bodyRootRest;
            Quaternion hornRest = _hornRest;

            Quaternion bodyStart = bodyRootBone != null ? bodyRootBone.localRotation : Quaternion.identity;
            Quaternion bodyDead = bodyRootBone != null
                ? Quaternion.AngleAxis(180f, LocalAxis(bodyRootBone, transform.forward)) * bodyRest
                : Quaternion.identity;
            Quaternion hornStart = hornBone != null ? hornBone.localRotation : Quaternion.identity;
            Quaternion hornDead = hornBone != null
                ? Quaternion.AngleAxis(-35f, LocalAxis(hornBone, transform.right)) * hornRest
                : Quaternion.identity;

            var legStart = new Quaternion[legRootBones.Count];
            var legDead = new Quaternion[legRootBones.Count];
            for (int i = 0; i < legRootBones.Count; i++)
            {
                legStart[i] = legRootBones[i] != null ? legRootBones[i].localRotation : Quaternion.identity;
                legDead[i] = legRootBones[i] != null
                    ? Quaternion.AngleAxis((i % 2 == 0 ? 1f : -1f) * 30f, LocalAxis(legRootBones[i], transform.forward)) * legStart[i]
                    : Quaternion.identity;
            }

            // Phase 1 - roll belly-up (legs splay).
            yield return LerpDeathPose(bodyStart, bodyDead, hornStart, hornDead, legStart, legDead, flipOverSeconds);

            // Phase 2 - lie still for the rest of the death window.
            float lie = Mathf.Max(0f, respawnDelaySeconds - flipOverSeconds - getUpSeconds);
            yield return new WaitForSeconds(lie);

            // --- REVIVE ---
            if (respawnMode == RespawnMode.AtSpawn)
            {
                transform.SetPositionAndRotation(_spawnPosition, Quaternion.identity);
            }
            _health.ResetHealth();          // full HP again (Health also clears IsDead)
            _verticalVelocity = 0f;
            _attackComboCount = 0;
            _attackTimer = 0f;
            _gaitBlend = 0f;
            _attackBlend = 0f;

            // Phase 3 - roll back upright and settle the legs to rest.
            yield return LerpDeathPose(bodyDead, bodyRest, hornDead, hornRest, legDead, legStart, getUpSeconds);

            // Hand control back to the normal state machine.
            _controller.enabled = true;
            if (hornHitbox != null)
            {
                hornHitbox.enabled = true;
                hornHitbox.Deactivate();
            }
            _dead = false;
            _state = BugState.Patrol;
            _patrolDestination = transform.position;
        }

        private IEnumerator LerpDeathPose(Quaternion bodyA, Quaternion bodyB, Quaternion hornA, Quaternion hornB,
            Quaternion[] legA, Quaternion[] legB, float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float k = seconds > 0.0001f ? Mathf.SmoothStep(0f, 1f, t / seconds) : 1f;
                if (bodyRootBone != null) bodyRootBone.localRotation = Quaternion.Slerp(bodyA, bodyB, k);
                if (hornBone != null) hornBone.localRotation = Quaternion.Slerp(hornA, hornB, k);
                for (int i = 0; i < legRootBones.Count; i++)
                {
                    if (legRootBones[i] != null && i < legA.Length && i < legB.Length)
                    {
                        legRootBones[i].localRotation = Quaternion.Slerp(legA[i], legB[i], k);
                    }
                }
                yield return null;
            }
        }

        // ---------------------------------------------------------------------------- utils ----
        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        // Expresses a world-space hinge axis in `bone`'s parent space, so
        // bone.localRotation = AngleAxis(deg, axisInParentSpace) * rest works no matter how the
        // individual generic bone happens to be oriented (same trick as CatProceduralWalk).
        private static Vector3 LocalAxis(Transform bone, Vector3 worldAxis)
        {
            Transform parent = bone.parent;
            Vector3 local = parent != null ? parent.InverseTransformDirection(worldAxis) : worldAxis;
            return local.sqrMagnitude > 1e-6f ? local.normalized : Vector3.right;
        }

        private static void ApplyHinge(Transform bone, Quaternion restLocal, Vector3 worldAxis, float degrees)
        {
            bone.localRotation = Quaternion.AngleAxis(degrees, LocalAxis(bone, worldAxis)) * restLocal;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, detectionRange);
            Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, attackRange);
            Gizmos.color = new Color(0.4f, 0.4f, 0.4f, 0.35f);
            Gizmos.DrawWireSphere(Application.isPlaying ? _spawnPosition : transform.position, patrolRadius);
        }
    }
}
