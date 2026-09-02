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

        // 2026-09-01, spec WUSHI_COMBAT_ENGINEERING_SPEC.md §4 (M2 項目 3). The default SweepCheck
        // sweeps the collider CENTRE from its last pose to this one. A rotating blade barely moves at
        // the hilt while the tip carves a long arc, so a centre sweep (and the `distance < 0.0001`
        // early-out) misses the tip's path entirely. When this is on AND hitCollider is a
        // CapsuleCollider (the katana BladeHitbox), the sweep instead samples the capsule as a line
        // - root / mid / tip - and sweeps each point prev->curr, subdividing per bladeSweepMaxSampleTravel
        // (same maths as the player's PlayerWeaponHitbox / WeaponSweepUtility). OFF (the default) =
        // every existing BossHitbox behaves exactly as before - only the 武士 blade opts in.
        [SerializeField] private bool useRotationalSweep;
        [Tooltip("Rotational sweep only: a sample point moving further than this in one physics step " +
                 "is subdivided so a fast swing can't tunnel a target (spec §4.2 default 0.25).")]
        [SerializeField] private float bladeSweepMaxSampleTravel = 0.25f;

        // Scratch buffer for one sub-segment SphereCast (rotational sweep only), copied into
        // _sweepHitsBuffer. Same no-GC precedent as _sweepHitsBuffer.
        private readonly RaycastHit[] _bladeSubBuffer = new RaycastHit[8];

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
            HasSwept = false;
        }

        public bool IsActive => hitCollider != null && hitCollider.enabled;

        // 2026-09-01, Sekiro deflect debug overlay - the last translation-swept segment this
        // FixedUpdate tested (SekiroDeflectDebug draws it). Only meaningful while active.
        public Vector3 LastSweepFrom { get; private set; }
        public Vector3 LastSweepTo { get; private set; }
        public bool HasSwept { get; private set; }

        // 2026-09-01, user request (隻狼 blade-clash SFX) - lets a listener that only has the
        // resulting DamageInfo (PlayerGuardClashSfx, off PlayerGuard.Blocked) tell a blocked
        // SWORD strike apart from a blocked kick, without threading the part through DamageInfo.
        // Non-null only while this hitbox is mid-window (Activate..Deactivate).
        public BossHitboxPart? ActiveWindowPart => _activeWindow != null ? _activeWindow.part : (BossHitboxPart?)null;

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
            Vector3 direction = distance > 0.0001f ? delta / distance : Vector3.zero;

            // NonAlloc variants writing into the reused _sweepHitsBuffer - see that field's own
            // comment for why (this used to be *CastAll, allocating a fresh array every call).
            int hitCount;

            // spec §4 (M2 項目 3): rotational blade sweep. The capsule centre barely translates when
            // the blade is rotating about the wrist, so the centre-sweep below (and its early-out)
            // misses the tip's arc - sample root/mid/tip and sweep each instead.
            bool sweptThisFrame;
            if (useRotationalSweep && hitCollider is CapsuleCollider bladeCapsule)
            {
                hitCount = MultiPointBladeSweep(bladeCapsule); // sets LastSweepFrom/To itself
                sweptThisFrame = true;
            }
            else
            {
                sweptThisFrame = SweepCentreShape(distance, direction, out hitCount);
            }

            // 2026-09-01 ("踢擊離得太進...很難彈反") - even when the hitbox barely moved this frame (no
            // real swept cast) still run the clash check: TryResolveBladeClash's overlap probe catches
            // a guard volume the hitbox is already sitting inside, which a *Cast silently ignores.
            if (!sweptThisFrame && !(_activeWindow != null && IsClashablePart(_activeWindow.part)))
            {
                return; // barely moved / unsupported shape and not a clashable window - plain OnTriggerEnter covers it
            }

            // 2026-09-01, Sekiro deflect (spec 三/四) - a melee window's sweep, on the first guard
            // volume it crosses (not clearly behind the body), routes to the clash resolver instead
            // of a body hit. 2026-09-01 follow-up (user: "武士的踢擊也要能彈反") - kicks / bare
            // fists are clashable too now, only the LandingAOE shockwave and a plain Body hit are not.
            if (_activeWindow != null && IsClashablePart(_activeWindow.part)
                && TryResolveBladeClash(hitCount))
            {
                return;
            }

            for (int i = 0; i < hitCount; i++)
            {
                Collider sweptCollider = _sweepHitsBuffer[i].collider;
                if (sweptCollider == hitCollider) continue; // never self-hit
                TryResolveHit(sweptCollider);
            }
        }

        // The original centre-translation sweep. Returns false (skip resolution) if the hitbox
        // barely moved or its shape isn't supported; otherwise writes hitCount and returns true.
        private bool SweepCentreShape(float distance, Vector3 direction, out int hitCount)
        {
            hitCount = 0;
            LastSweepFrom = _previousPosition;
            LastSweepTo = transform.position;
            HasSwept = true;
            if (distance < 0.0001f)
            {
                return false; // barely moved - the ordinary OnTriggerEnter overlap check already covers this case fine
            }

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
                return false; // no supported shape (e.g. MeshCollider) - falls back to plain OnTriggerEnter only
            }

            return true;
        }

        // spec §4.4 - sample the blade capsule at root/mid/tip, sweep each point from its previous
        // world pose to this one (subdividing so a fast swing can't tunnel), and gather de-duplicated
        // hits into _sweepHitsBuffer. Returns the hit count, exactly like the centre-shape branch, so
        // the clash + TryResolveHit tail in SweepCheck is identical for both paths.
        private int MultiPointBladeSweep(CapsuleCollider cap)
        {
            CapsuleWorldEnds(cap, _previousPosition, _previousRotation,
                out Vector3 pRoot, out Vector3 pMid, out Vector3 pTip, out float worldRadius);
            CapsuleWorldEnds(cap, transform.position, transform.rotation,
                out Vector3 cRoot, out Vector3 cMid, out Vector3 cTip, out _);

            // Debug overlay (SekiroDeflectDebug): the tip's arc chord is the interesting one.
            LastSweepFrom = pTip;
            LastSweepTo = cTip;
            HasSwept = true;

            int count = 0;
            count = SweepBladeSample(pRoot, cRoot, worldRadius, count);
            count = SweepBladeSample(pMid, cMid, worldRadius, count);
            count = SweepBladeSample(pTip, cTip, worldRadius, count);
            return count;
        }

        // One blade sample point's swept SphereCast(s), previous->current, appended (de-duplicated by
        // collider, so distinct targets keep their slots) into _sweepHitsBuffer from writeIndex.
        private int SweepBladeSample(Vector3 from, Vector3 to, float radius, int writeIndex)
        {
            Vector3 d = to - from;
            float travel = d.magnitude;
            Vector3 dir = travel > 1e-5f ? d / travel : Vector3.forward;
            int subdivisions = WeaponSweepUtility.SubdivisionCount(travel, bladeSweepMaxSampleTravel);
            float subLength = WeaponSweepUtility.SubSegmentLength(travel, subdivisions);

            for (int s = 0; s < subdivisions && writeIndex < _sweepHitsBuffer.Length; s++)
            {
                Vector3 subFrom = WeaponSweepUtility.SubSegmentStart(from, to, s, subdivisions);
                int n = Physics.SphereCastNonAlloc(subFrom, radius, dir, _bladeSubBuffer,
                    Mathf.Max(subLength, 0f), ~0, QueryTriggerInteraction.Collide);
                for (int i = 0; i < n && writeIndex < _sweepHitsBuffer.Length; i++)
                {
                    RaycastHit hit = _bladeSubBuffer[i];
                    if (hit.collider == null || hit.collider == hitCollider)
                    {
                        continue;
                    }
                    bool dup = false;
                    for (int k = 0; k < writeIndex; k++)
                    {
                        if (_sweepHitsBuffer[k].collider == hit.collider) { dup = true; break; }
                    }
                    if (!dup)
                    {
                        _sweepHitsBuffer[writeIndex++] = hit;
                    }
                }
            }
            return writeIndex;
        }

        // The capsule's root / mid / tip in world space for a given pose, plus its world radius -
        // same scaling rules Unity itself uses (axis dim by that axis's lossyScale, radius by the
        // larger of the other two), matching the centre-shape capsule branch above.
        private void CapsuleWorldEnds(CapsuleCollider cap, Vector3 pos, Quaternion rot,
            out Vector3 root, out Vector3 mid, out Vector3 tip, out float worldRadius)
        {
            Vector3 ls = transform.lossyScale;
            float axisScale = cap.direction == 0 ? ls.x : cap.direction == 1 ? ls.y : ls.z;
            float radiusScale = cap.direction == 0 ? Mathf.Max(ls.y, ls.z)
                : cap.direction == 1 ? Mathf.Max(ls.x, ls.z)
                : Mathf.Max(ls.x, ls.y);
            worldRadius = cap.radius * radiusScale;
            float halfLine = Mathf.Max(0f, cap.height * 0.5f - cap.radius) * axisScale;

            Vector3 localAxis = cap.direction == 0 ? Vector3.right : cap.direction == 1 ? Vector3.up : Vector3.forward;
            Vector3 worldAxis = (rot * localAxis).normalized;
            mid = pos + rot * cap.center;
            tip = mid + worldAxis * halfLine;
            root = mid - worldAxis * halfLine;
        }

        // A generous world radius around the hitbox for the point-blank clash overlap probe - big
        // enough to cover the collider itself plus a little reach (a kick/blade "right on top of" the
        // guard should still clash even if the exact swept geometry missed).
        private float OverlapProbeRadius()
        {
            Vector3 ls = transform.lossyScale;
            float maxScale = Mathf.Max(ls.x, Mathf.Max(ls.y, ls.z));
            if (hitCollider is SphereCollider s) return s.radius * maxScale + 0.35f;
            if (hitCollider is CapsuleCollider c) return c.radius * maxScale + 0.35f;
            if (hitCollider is BoxCollider b) return Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)) * maxScale * 0.5f + 0.35f;
            return 0.6f;
        }

        private static bool IsClashablePart(BossHitboxPart part)
        {
            return part == BossHitboxPart.Weapon
                || part == BossHitboxPart.LeftHand || part == BossHitboxPart.RightHand
                || part == BossHitboxPart.LeftFoot || part == BossHitboxPart.RightFoot;
        }

        private readonly Collider[] _clashOverlapBuffer = new Collider[16];

        // Returns true if the sweep was consumed by a blade clash (parry or guard) - the caller
        // then skips the body-hit loop for this sweep.
        private bool TryResolveBladeClash(int hitCount)
        {
            if (_activeAttack == null || _activeWindow == null || _attackerRoot == null)
            {
                return false;
            }

            PlayerGuardVolume volume = null;
            float volumeDist = float.MaxValue;
            float bodyDist = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = _sweepHitsBuffer[i].collider;
                if (col == null || col == hitCollider) continue;
                if (col.transform.root == _attackerRoot) continue;
                float d = _sweepHitsBuffer[i].distance;

                PlayerGuardVolume v = col.GetComponentInParent<PlayerGuardVolume>();
                if (v != null && v.Active)
                {
                    if (d < volumeDist) { volumeDist = d; volume = v; }
                    continue;
                }
                if (col.GetComponentInParent<IDamageable>() != null && d < bodyDist)
                {
                    bodyDist = d;
                }
            }

            // 2026-09-01, user report ("踢擊離得太進或太遠都很難彈反") - a *Cast ignores a collider it
            // already overlaps at the sweep start, so a point-blank kick whose foot begins inside the
            // guard volume never registers as clashable and drops straight to a body hit. Also probe
            // the hitbox's CURRENT volume with an overlap check (distance 0 = the guard is right here).
            {
                float probeRadius = OverlapProbeRadius();
                int n = Physics.OverlapSphereNonAlloc(transform.position, probeRadius, _clashOverlapBuffer, ~0, QueryTriggerInteraction.Collide);
                for (int i = 0; i < n; i++)
                {
                    Collider col = _clashOverlapBuffer[i];
                    if (col == null || col == hitCollider || col.transform.root == _attackerRoot) continue;
                    PlayerGuardVolume v = col.GetComponentInParent<PlayerGuardVolume>();
                    if (v != null && v.Active)
                    {
                        volume = v;
                        volumeDist = 0f; // touching now - guard wins any tie
                    }
                }
            }

            if (volume == null)
            {
                return false;
            }
            // Blade reached the body clearly ahead of the guard ("繞過防禦刀刃、先命中身體") -> body hit.
            // CapsuleCast reports distance 0 for a start-overlap, so on a tie the guard wins (defense-favourable).
            if (bodyDist + 0.05f < volumeDist)
            {
                return false;
            }

            var receiver = volume.GetComponentInParent<IBladeClashReceiver>();
            if (receiver == null)
            {
                return false;
            }

            Transform targetRoot = volume.transform.root;
            if (_hitTargetsThisActivation.Contains(targetRoot))
            {
                return true; // already resolved against this target this window
            }

            Vector3 sweepDelta = transform.position - _previousPosition;
            Vector3 contact = (volumeDist > 0f && volumeDist < float.MaxValue && sweepDelta.sqrMagnitude > 1e-6f)
                ? _previousPosition + sweepDelta.normalized * volumeDist
                : volume.transform.position;

            Vector3 dir = volume.transform.position - _attackerRoot.position;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : _attackerRoot.forward;

            float healthDamage = ComputeHealthDamage(volume.GetComponentInParent<Health>());
            float poiseDamage = _activeAttack.BasePoiseDamage * _activeWindow.damageMultiplier;

            BladeClashResult result = receiver.TryResolveClash(
                new BladeClashInfo(_attackerRoot.gameObject, healthDamage, poiseDamage, contact, dir,
                    _activeWindow.deflectReaction));

            if (result == BladeClashResult.None)
            {
                return false; // guard wasn't actually valid (down / not frontal) -> let the body hit land
            }
            _hitTargetsThisActivation.Add(targetRoot);
            return true;
        }

        private float ComputeHealthDamage(Health targetHealth)
        {
            float dmg;
            if (_activeAttack.HealthDamageIsPercentOfTargetMax)
            {
                dmg = (targetHealth != null && targetHealth.MaxHealth > 0f)
                    ? targetHealth.MaxHealth * (_activeAttack.BaseHealthDamage / 100f)
                    : _activeAttack.BaseHealthDamage;
            }
            else
            {
                dmg = _activeAttack.BaseHealthDamage;
            }
            return dmg * _activeWindow.damageMultiplier;
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

            // baseHealthDamage may be a percent (5 => 5%) of the target's own max health, resolved
            // here at hit time. GetComponentInParent because the collider we hit is usually a child
            // hurtbox (PlayerHurtbox) of the object that actually carries Health. Same math the
            // blade-clash path uses - see ComputeHealthDamage.
            float healthDamage = ComputeHealthDamage(other.GetComponentInParent<Health>());
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
