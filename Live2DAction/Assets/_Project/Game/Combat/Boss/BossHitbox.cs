using System.Collections.Generic;
using UnityEngine;
using Live2DAction.Core;

namespace Live2DAction.Combat.Boss
{
    // One physical attack hitbox (LeftHand/RightHand/LeftFoot/RightFoot/Body/LandingAOE) - a
    // trigger Collider parented to the matching bone, disabled by default (spec: "攻擊Hitbox只能
    //在實際有效攻擊幀啟用"). BossStateMachine calls Activate() at a HitWindow's startNormalized
    // and Deactivate() at its endNormalized; nothing else ever toggles enabled directly, so a
    // window's own lifetime is the single source of truth for whether this can hit anything.
    //
    // Each Activate() call gets a fresh "already hit" set keyed by target root Transform - the
    // spec's own "同一個HitWindow對同一目標只結算一次,禁止因OnTrigger停留而每個物理幀重複扣血"
    // requirement. A HitWindow that reactivates later in the SAME clip (shouldn't normally happen
    // within one attack, but guards against it anyway) gets its own fresh set rather than
    // remembering hits from a previous activation.
    [RequireComponent(typeof(Collider))]
    public class BossHitbox : MonoBehaviour
    {
        [SerializeField] private Collider hitCollider;

        private Transform _attackerRoot;
        private string _attackerTeam;
        private BossAttackDefinition _activeAttack;
        private BossHitWindow _activeWindow;
        private readonly HashSet<Transform> _hitTargetsThisActivation = new HashSet<Transform>();

        // 2026-08-26, real playtested bug ("刀亮紅的時機正確 但...始終沒能產生碰撞") - see the swept-
        // check block below for the actual root cause/fix; these two just track enough state to
        // build a swept test between "where the hitbox was last physics step" and "where it is
        // now".
        private Vector3 _previousPosition;
        private Quaternion _previousRotation;
        private bool _hasPreviousPose;

        // Perf fix (2026-08-27, playtest report - "play mode模式下遊戲會卡頓") - SweepCheck below
        // used to call Physics.*CastAll every FixedUpdate a hitbox is active, which allocates a
        // brand-new managed array on every single call; with multiple attacks (including the new
        // periodic Breakdance/Leap Slam specials) landing during combat, that's a steady stream of
        // GC garbage right when the game is busiest. Reused fixed-size buffer + the *NonAlloc
        // overloads instead - same query semantics as before (mask/QueryTriggerInteraction
        // unchanged - the mask stays ~0 rather than being narrowed to a specific layer because this
        // project has no dedicated Player/hurtbox layer set up yet, see TagManager.asset; TryResolveHit
        // already filters non-IDamageable/self/same-team hits afterward, so ~0 only costs extra
        // physics query time against scenery, not correctness), zero allocation. 16 is generous for
        // a single swept weapon hitbox; NonAlloc just truncates instead of throwing if it's ever
        // exceeded.
        private readonly RaycastHit[] _sweepHitsBuffer = new RaycastHit[16];

        private void Reset()
        {
            hitCollider = GetComponent<Collider>();
        }

        private void Awake()
        {
            if (hitCollider == null)
            {
                hitCollider = GetComponent<Collider>();
            }
            hitCollider.isTrigger = true;
            hitCollider.enabled = false;

            // 2026-08-26, real playtested bug ("玩家沒有受到攻擊和傷害") - OnTriggerEnter below was
            // never firing at all, on ANY hit, despite Activate()/Deactivate() correctly toggling
            // hitCollider.enabled at exactly the right normalized-time windows (confirmed via a
            // forced Play Mode test: bladeActive=True, player standing well inside blade reach,
            // zero HP lost). Root cause: Unity only sends trigger callbacks for a collider pair if
            // AT LEAST ONE side has a Rigidbody - neither this hitbox's own hierarchy (parented to
            // an Animator-driven bone, moved by animation curves, not physics) nor the target
            // (Player/Enemy, using a CharacterController - which does NOT itself satisfy this rule
            // for a stationary target being swept into by an animated trigger) has one anywhere.
            // This silently affected every BossHitbox instance that's ever existed in this project
            // (PiHaiWangV2's own hitboxes have the identical gap - just never caught, since PW2 was
            // never actually instantiated in a scene to hit anything - see PiHaiWangHealthBarSetup's
            // own comment on that). Self-healing here, same precedent as Configure() being re-run
            // every Awake() in BossStateMachine (its own comment explains why that pattern exists),
            // rather than requiring every hitbox to be hand-configured correctly in the Inspector.
            // Kinematic so it never falls under gravity or gets shoved by physics forces - it only
            // exists to make the trigger pair valid, never to actually simulate.
            var rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }
            rb.isKinematic = true;
            rb.useGravity = false;

            // 2026-08-26, real playtested bug ("刀亮紅的時機正確 但...始終沒能產生碰撞") - measured
            // directly (AnimationMode.SampleAnimationClip at real 0.02s fixedDeltaTime spacing, not
            // just normalized-time steps): BladeHitbox moves up to 2.5 WORLD UNITS in a single
            // physics step at the peak of a fast swing (~97 units/s instantaneous blade-tip speed,
            // Wushi's 4x scale included) - several times the hitbox's own size and bigger than a
            // standing player's hurtbox diameter. Kept as extra insurance for slow-moving hitboxes
            // (kick follow-through, held poses), but CONFIRMED BY DIRECT TEST that this alone does
            // NOT fix the fast-swing case: even via Rigidbody.MovePosition (the officially
            // recommended way to move a kinematic body for CCD) across the exact measured 2.5-unit
            // jump, OnTriggerEnter still never fired. Unity's continuous collision detection
            // guarantees apply to preventing solid interpenetration, not to reliably raising trigger
            // callbacks for a fast-moving trigger volume - a real, documented gap, not a
            // misconfiguration. See FixedUpdate's own swept check below for the actual fix.
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        public void Configure(Transform attackerRoot, string attackerTeam)
        {
            _attackerRoot = attackerRoot;
            _attackerTeam = attackerTeam;
        }

        public void Activate(BossAttackDefinition attack, BossHitWindow window)
        {
            _activeAttack = attack;
            _activeWindow = window;
            _hitTargetsThisActivation.Clear();
            hitCollider.enabled = true;
            // Start the sweep fresh from THIS frame's pose, not wherever the bone happened to be
            // sitting the last time this same hitbox was active (a prior attack, frames ago) - that
            // stale position would draw a huge, meaningless "sweep" from the old attack's pose to
            // this one's on the very first FixedUpdate after activating.
            _hasPreviousPose = false;
        }

        public void Deactivate()
        {
            hitCollider.enabled = false;
            _activeAttack = null;
            _activeWindow = null;
            _hasPreviousPose = false;
        }

        public bool IsActive => hitCollider != null && hitCollider.enabled;

        // 2026-08-26, real playtested bug ("刀亮紅的時機正確 但由於角色身高 和彼此間隔距離 刀尖角度等等
        // 因素始終沒能產生碰撞") - the actual fix. OnTriggerEnter only ever sees discrete overlap
        // states at each physics step's END position; a fast weapon can be entirely on one side of
        // a target at step N and entirely on the other side at step N+1, tunneling straight through
        // with no frame where the two shapes ever geometrically overlapped at all - confirmed by
        // direct test that Unity's own CCD (ContinuousSpeculative, see Awake's own comment) does not
        // reliably solve this for TRIGGER volumes specifically. Standard fix for fast weapon hitboxes
        // in games that don't want to hand-author physical Rigidbody sword sweeps: every FixedUpdate
        // this is active, manually sweep-test a Box/SphereCast from last frame's pose to this
        // frame's pose (a translation-only approximation of the true swept volume - doesn't account
        // for in-between ROTATION, but the box already covers a real linear span so a moderate swing
        // arc within one physics step is still caught in practice) and resolve hits found that way
        // through the exact same TryResolveHit path OnTriggerEnter uses, so a target caught by
        // either path is deduplicated by the same _hitTargetsThisActivation set.
        private void FixedUpdate()
        {
            if (!IsActive)
            {
                _hasPreviousPose = false;
                return;
            }

            if (_hasPreviousPose)
            {
                SweepCheck();
            }

            _previousPosition = transform.position;
            _previousRotation = transform.rotation;
            _hasPreviousPose = true;
        }

        private void SweepCheck()
        {
            Vector3 currentPosition = transform.position;
            Vector3 delta = currentPosition - _previousPosition;
            float distance = delta.magnitude;
            if (distance < 0.0001f)
            {
                return; // barely moved - the ordinary OnTriggerEnter overlap check already covers this case fine
            }
            Vector3 direction = delta / distance;

            // NonAlloc variants writing into the reused _sweepHitsBuffer - see that field's own
            // comment for why (this used to be *CastAll, allocating a fresh array every call).
            int hitCount;
            if (hitCollider is BoxCollider box)
            {
                Vector3 halfExtents = Vector3.Scale(box.size, transform.lossyScale) * 0.5f;
                hitCount = Physics.BoxCastNonAlloc(_previousPosition + box.center, halfExtents, direction, _sweepHitsBuffer, _previousRotation, distance, ~0, QueryTriggerInteraction.Collide);
            }
            else if (hitCollider is SphereCollider sphere)
            {
                float radius = sphere.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
                hitCount = Physics.SphereCastNonAlloc(_previousPosition + sphere.center, radius, direction, _sweepHitsBuffer, distance, ~0, QueryTriggerInteraction.Collide);
            }
            else if (hitCollider is CapsuleCollider capsule)
            {
                // 2026-08-26, explicit user request ("刀受擊區不能只是表面 而是一個立體膠囊") -
                // BladeHitbox is now a CapsuleCollider running along the blade's length, specifically
                // BECAUSE a capsule presents a consistent round cross-section from any swing angle
                // (a thin box's coverage collapses to almost nothing if the blade happens to be
                // edge-on to the swing direction at that instant - "橫向持刀就容易打不到"). Scaling
                // matches how Unity itself scales a CapsuleCollider: the axis-aligned dimension by
                // that axis's own lossyScale component, the radius by the larger of the OTHER two.
                float axisScale = capsule.direction == 0 ? transform.lossyScale.x : capsule.direction == 1 ? transform.lossyScale.y : transform.lossyScale.z;
                float radiusScale = capsule.direction == 0
                    ? Mathf.Max(transform.lossyScale.y, transform.lossyScale.z)
                    : capsule.direction == 1
                        ? Mathf.Max(transform.lossyScale.x, transform.lossyScale.z)
                        : Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);
                float worldRadius = capsule.radius * radiusScale;
                float halfLine = Mathf.Max(0f, capsule.height * 0.5f - capsule.radius) * axisScale;

                Vector3 localAxis = capsule.direction == 0 ? Vector3.right : capsule.direction == 1 ? Vector3.up : Vector3.forward;
                Vector3 worldAxis = (_previousRotation * localAxis).normalized;
                Vector3 worldCenter = _previousPosition + _previousRotation * capsule.center;
                Vector3 point1 = worldCenter + worldAxis * halfLine;
                Vector3 point2 = worldCenter - worldAxis * halfLine;

                hitCount = Physics.CapsuleCastNonAlloc(point1, point2, worldRadius, direction, _sweepHitsBuffer, distance, ~0, QueryTriggerInteraction.Collide);
            }
            else
            {
                return; // no supported shape (e.g. MeshCollider) - falls back to plain OnTriggerEnter only
            }

            for (int i = 0; i < hitCount; i++)
            {
                Collider sweptCollider = _sweepHitsBuffer[i].collider;
                if (sweptCollider == hitCollider) continue; // never self-hit
                TryResolveHit(sweptCollider);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            TryResolveHit(other);
        }

        private void TryResolveHit(Collider other)
        {
            if (_activeAttack == null || _activeWindow == null || _attackerRoot == null)
            {
                return;
            }

            if (other.transform.root == _attackerRoot)
            {
                return; // never hits the boss's own hurtboxes/collider
            }

            Transform targetRoot = other.transform.root;
            if (_hitTargetsThisActivation.Contains(targetRoot))
            {
                return; // this window already resolved against this target
            }

            if (!other.TryGetComponent(out IDamageable damageable))
            {
                return;
            }

            var teamMember = other.GetComponentInParent<BossTeamMember>();
            if (teamMember != null && teamMember.Team == _attackerTeam)
            {
                return; // no friendly fire between same-team boss hurtboxes
            }

            _hitTargetsThisActivation.Add(targetRoot);

            float healthDamage = _activeAttack.BaseHealthDamage * _activeWindow.damageMultiplier;
            float poiseDamage = _activeAttack.BasePoiseDamage * _activeWindow.damageMultiplier;

            // Guarding (Boxing_Guard_Right_Straight_Kick's stance) reduces incoming HEALTH damage
            // only - poise still accumulates in full, per spec ("仍正常累積架勢傷害,不得完全無敵").
            // This only matters when the BOSS itself is the one being hit while guarding, which
            // isn't this hitbox's own concern (this is the boss's OUTGOING hitbox) - left here as
            // a documented non-goal so it's clear guarding is handled on the receiving side
            // (BossStateMachine's own incoming-damage multiplier), not here.

            Vector3 point = other.ClosestPoint(transform.position);
            Vector3 direction = (other.transform.position - _attackerRoot.position);
            direction.y = 0f;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : _attackerRoot.forward;

            damageable.ApplyDamage(new DamageInfo(healthDamage, point, direction, _attackerRoot.gameObject, poiseDamage));

            var knockback = other.GetComponentInParent<IKnockbackReceiver>();
            if (knockback != null)
            {
                knockback.ApplyKnockback(direction, _activeAttack.KnockbackForce, _activeAttack.LaunchesTarget);
            }
        }
    }
}
