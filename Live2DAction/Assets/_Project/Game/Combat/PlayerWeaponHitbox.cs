using System.Collections.Generic;
using UnityEngine;
using Live2DAction.Combat.Boss;
using Live2DAction.Core;

namespace Live2DAction.Combat
{
    // spec WUSHI_COMBAT_ENGINEERING_SPEC.md §5 (M2 項目 4) - the player-side equivalent of BossHitbox:
    // a swept, blade-relative melee hit query that replaces PlayerCombat's Physics.OverlapCapsule from
    // the character root. The old query lands as long as the player's ROOT is close enough regardless
    // of where the katana actually is; this one only lands when the swept blade line (root / mid / tip)
    // actually crosses a target, matching BossHitbox so the two sides share one mental model.
    //
    // NOT wired into PlayerCombat yet (first, lowest-risk step of item 4 - the component + its pure
    // maths + tests). PlayerCombat still runs its OverlapCapsule; a later pass adds the
    // `useSweptBladeHitbox` feature flag + a setup menu that places the blade sample transforms and
    // enables this on the GreyboxTest Player. On any object without this component the player's melee
    // is byte-for-byte unchanged.
    //
    // While unwired this is dormant: FixedUpdate no-ops whenever `combat` is unset or no attack's
    // Active window is live. If it IS added to a Player that still has PlayerCombat's OverlapCapsule
    // running, both would resolve - which is exactly why the flag/menu step is separate.
    public class PlayerWeaponHitbox : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("The PlayerCombat whose Active window gates this sweep. Sweep runs only while " +
                 "combat.CurrentActiveAttack != null - the player-side analogue of a BossHitWindow.")]
        [SerializeField] private PlayerCombat combat;

        [Tooltip("Root transform excluded from self-hits. Defaults to this transform's root.")]
        [SerializeField] private Transform attackerRoot;

        [Header("Blade sample line")]
        [Tooltip("Hilt end of the blade.")]
        [SerializeField] private Transform bladeRoot;

        [Tooltip("Optional. Blade midpoint - if unset, the geometric middle of root..tip is used.")]
        [SerializeField] private Transform bladeMid;

        [Tooltip("Blade tip - the fast end of a swing arc.")]
        [SerializeField] private Transform bladeTip;

        [Header("Sweep")]
        [Tooltip("Blade thickness / hit tolerance for each sample point's SphereCast.")]
        [SerializeField] private float sweepRadius = 0.12f;

        [Tooltip("A sweep longer than this in one physics step is subdivided so a fast swing can't " +
                 "tunnel a target between casts (spec §4.2 default 0.25).")]
        [SerializeField] private float maxSampleTravel = 0.25f;

        [Tooltip("Layers the sweep tests against. Leave as Everything until the project has dedicated " +
                 "hurtbox layers (spec §4.5); non-IDamageable / self / friendly hits are filtered out anyway.")]
        [SerializeField] private LayerMask targetMask = ~0;

        [Tooltip("Optional impact spark spawned at the sweep contact point per landed hit - the " +
                 "swept-blade equivalent of PlayerCombat.hitEffectPrefab (billboard, rotation ignored).")]
        [SerializeField] private GameObject hitEffectPrefab;

        // Reused across all sample points and sub-segments, one physics step - same no-GC precedent as
        // BossHitbox._sweepHitsBuffer. NonAlloc truncates rather than throwing if ever exceeded.
        private readonly RaycastHit[] _sweepBuffer = new RaycastHit[16];

        // One kill per target per swing (spec §5.4 "單次揮刀對同一Boss只結算一次，但可命中多個不同敵人").
        private readonly HashSet<Transform> _hitThisAttack = new HashSet<Transform>();

        // Previous-step world positions of [root, mid, tip]; only valid while _hasPreviousPose.
        private readonly Vector3[] _previousPoints = new Vector3[3];
        private readonly Vector3[] _currentPoints = new Vector3[3];
        private bool _hasPreviousPose;
        private bool _activeLastFrame;

        // Nearest hit collected per target root within this step's sweep, before resolving.
        private readonly Dictionary<Transform, RaycastHit> _nearestPerTarget = new Dictionary<Transform, RaycastHit>();

        // 2026-09-01, spec item 4 debug - the last swept segments this FixedUpdate tested, root/mid/tip
        // (a later SekiroDeflectDebug-style overlay draws them, mirroring BossHitbox.LastSweepFrom/To).
        public Vector3[] LastSweepFrom { get; } = new Vector3[3];
        public Vector3[] LastSweepTo { get; } = new Vector3[3];
        public bool HasSwept { get; private set; }

        private Transform AttackerRoot => attackerRoot != null ? attackerRoot : transform.root;

        private void Awake()
        {
            if (attackerRoot == null)
            {
                attackerRoot = transform.root;
            }
        }

        private void FixedUpdate()
        {
            // combat.isActiveAndEnabled: when the player is cat-possessed PlayerCombat is disabled;
            // don't keep sweeping off a frozen mid-swing state.
            AttackData attack = combat != null && combat.isActiveAndEnabled ? combat.CurrentActiveAttack : null;
            if (attack == null || bladeRoot == null || bladeTip == null)
            {
                _hasPreviousPose = false;
                _activeLastFrame = false;
                HasSwept = false;
                return;
            }

            // Rising edge of an Active window: a new swing, forget the previous swing's hits and pose.
            if (!_activeLastFrame)
            {
                _hitThisAttack.Clear();
                _hasPreviousPose = false;
                _activeLastFrame = true;
            }

            Vector3 root = bladeRoot.position;
            Vector3 tip = bladeTip.position;
            _currentPoints[0] = root;
            _currentPoints[1] = WeaponSweepUtility.ResolveMidpoint(bladeMid != null,
                bladeMid != null ? bladeMid.position : Vector3.zero, root, tip);
            _currentPoints[2] = tip;

            if (_hasPreviousPose)
            {
                _nearestPerTarget.Clear();
                for (int p = 0; p < 3; p++)
                {
                    AccumulateSweep(_previousPoints[p], _currentPoints[p], p);
                }
                ResolveAccumulatedHits(attack);
            }

            for (int p = 0; p < 3; p++)
            {
                _previousPoints[p] = _currentPoints[p];
            }
            _hasPreviousPose = true;
        }

        private void AccumulateSweep(Vector3 previous, Vector3 current, int pointIndex)
        {
            LastSweepFrom[pointIndex] = previous;
            LastSweepTo[pointIndex] = current;
            HasSwept = true;

            Vector3 delta = current - previous;
            float travel = delta.magnitude;
            Vector3 direction = travel > 1e-5f ? delta / travel : Vector3.zero;

            int subdivisions = WeaponSweepUtility.SubdivisionCount(travel, maxSampleTravel);
            float subLength = WeaponSweepUtility.SubSegmentLength(travel, subdivisions);

            // Player melee windows are short and the blade is always moving during them, so the
            // swept cast is the real path. A fully stationary blade (travel 0) falls back to a
            // zero-length cast, which SphereCast does not reliably use for initial-overlap
            // detection - a dedicated OverlapSphere for the held-blade case can come with the
            // PlayerCombat wiring pass if a playtest shows it matters.
            float castDistance = Mathf.Max(subLength, 0f);

            for (int s = 0; s < subdivisions; s++)
            {
                Vector3 from = WeaponSweepUtility.SubSegmentStart(previous, current, s, subdivisions);
                int hitCount = Physics.SphereCastNonAlloc(
                    from, sweepRadius,
                    direction == Vector3.zero ? Vector3.forward : direction,
                    _sweepBuffer, castDistance, targetMask, QueryTriggerInteraction.Collide);

                for (int i = 0; i < hitCount; i++)
                {
                    RaycastHit hit = _sweepBuffer[i];
                    if (hit.collider == null)
                    {
                        continue;
                    }
                    Transform targetRoot = hit.collider.transform.root;
                    if (targetRoot == AttackerRoot)
                    {
                        continue; // never self-hit
                    }
                    // Only count a HURTBOX - a collider that carries IDamageable itself. The sweep
                    // also crosses an enemy's own outgoing attack hitboxes (the 武士's BladeHitbox /
                    // foot hitboxes carry Health only in a PARENT, not on themselves); without this
                    // filter one of those could win the per-target slot and its later IDamageable
                    // check would drop the whole target - "玩家完全傷害不到武士". Matches
                    // AttackResolver's own candidate.TryGetComponent<IDamageable> gate.
                    if (!hit.collider.TryGetComponent(out IDamageable _))
                    {
                        continue;
                    }
                    if (!_nearestPerTarget.TryGetValue(targetRoot, out RaycastHit existing)
                        || hit.distance < existing.distance)
                    {
                        _nearestPerTarget[targetRoot] = hit;
                    }
                }
            }
        }

        private void ResolveAccumulatedHits(AttackData attack)
        {
            if (_nearestPerTarget.Count == 0)
            {
                return;
            }

            Transform selfRoot = AttackerRoot;
            float multiplier = combat != null ? combat.UltimateDamageMultiplier : 1f;

            foreach (KeyValuePair<Transform, RaycastHit> pair in _nearestPerTarget)
            {
                Transform targetRoot = pair.Key;
                if (_hitThisAttack.Contains(targetRoot))
                {
                    continue; // this swing already landed on this target
                }

                Collider col = pair.Value.collider;
                if (!col.TryGetComponent(out IDamageable damageable))
                {
                    continue;
                }

                _hitThisAttack.Add(targetRoot);

                Vector3 point = pair.Value.point;
                Vector3 dir = targetRoot.position - selfRoot.position;
                dir.y = 0f;
                dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : selfRoot.forward;

                damageable.ApplyDamage(new DamageInfo(attack.Damage * multiplier, point, dir, selfRoot.gameObject));

                if (attack.KnockbackForce > 0f
                    && targetRoot.TryGetComponent(out IKnockbackReceiver knockback))
                {
                    knockback.ApplyKnockback(dir, attack.KnockbackForce, attack.KnockbackLaunches);
                }

                // Only the shared impact spark. A per-attack HitEffectOverride (e.g. a swing-arc
                // slash VFX) is swing-level, not per-hit - PlayerCombat still owns that.
                if (hitEffectPrefab != null)
                {
                    Instantiate(hitEffectPrefab, point, Quaternion.identity);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (bladeRoot == null || bladeTip == null)
            {
                return;
            }
            Vector3 root = bladeRoot.position;
            Vector3 tip = bladeTip.position;
            Vector3 mid = WeaponSweepUtility.ResolveMidpoint(bladeMid != null,
                bladeMid != null ? bladeMid.position : Vector3.zero, root, tip);

            Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.9f); // player-attack green, matches PlayerCombat's gizmo family
            Gizmos.DrawLine(root, tip);
            foreach (Vector3 sample in new[] { root, mid, tip })
            {
                Gizmos.DrawWireSphere(sample, sweepRadius);
            }
        }
    }
}
