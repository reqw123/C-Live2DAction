using System.Collections.Generic;
using UnityEngine;
using Live2DAction.Core;

namespace Live2DAction.AI
{
    // The bug's horn strike hitbox - one trigger collider parented to (a child of) the horn bone,
    // DISABLED by default. TenLeggedBugController calls Activate() only across the down-stab's
    // contact frames and Deactivate() the instant they end, so the collider's own enabled state is
    // the single source of truth for "can this hurt the player right now" (spec section 3/5:
    // "鼻角獨立使用 Trigger Hitbox，僅在刺擊命中幀啟用" / "不可只因玩家待在範圍內就自動持續扣血").
    //
    // Deliberately a trimmed copy of the pattern in Combat/Boss/BossHitbox rather than reusing that
    // class: BossHitbox is wired into the boss attack-definition / hit-window / team system and
    // carries damage-percent, poise, knockback-force plumbing this simple 10-flat-HP jab doesn't
    // need. What IS copied (because BossHitbox's own comments explain they're real playtested bugs,
    // not optional):
    //   - a kinematic Rigidbody, or Unity never raises trigger callbacks for an animated trigger
    //     vs. a CharacterController target that both lack a Rigidbody;
    //   - a per-activation "already hit" set so one strike resolves once per target even though
    //     OnTriggerStay/repeated FixedUpdates would otherwise re-hit every physics frame;
    //   - a swept BoxCast each FixedUpdate, because a fast horn can tunnel through a standing
    //     hurtbox between two discrete physics steps with no frame where the shapes overlap.
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    public class TenLeggedBugHornHitbox : MonoBehaviour
    {
        [Tooltip("The trigger collider that defines the horn strike volume. Auto-filled from this GameObject.")]
        [SerializeField] private Collider hitCollider;

        [Tooltip("HP removed from the player on a clean strike. Spec: 10.")]
        [SerializeField] private float damage = 10f;

        private Transform _attackerRoot;
        private readonly HashSet<Transform> _hitThisActivation = new HashSet<Transform>();
        private readonly RaycastHit[] _sweepBuffer = new RaycastHit[8];
        private Vector3 _previousPosition;
        private bool _hasPreviousPose;

        public bool IsActive => hitCollider != null && hitCollider.enabled;
        public float Damage { get => damage; set => damage = value; }

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

            // Kinematic, no gravity - it exists only to make the trigger pair valid, never to
            // physically simulate. See BossHitbox.Awake for the full "why a Rigidbody at all" note.
            var rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            // If the controller never got a chance to Configure() us (e.g. a bare test rig),
            // fall back to our own root so self-hits are still filtered.
            if (_attackerRoot == null)
            {
                _attackerRoot = transform.root;
            }
        }

        // Called once by TenLeggedBugController.Awake so hits against the bug's own body/legs are
        // ignored.
        public void Configure(Transform attackerRoot)
        {
            _attackerRoot = attackerRoot;
        }

        // Opens the strike window. Fresh "already hit" set every call so a second stab later can
        // hit the same target again, but a single stab can't double-dip.
        public void Activate()
        {
            _hitThisActivation.Clear();
            _hasPreviousPose = false;
            hitCollider.enabled = true;
        }

        public void Deactivate()
        {
            hitCollider.enabled = false;
            _hasPreviousPose = false;
        }

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
            else
            {
                // First active frame: the bug is usually already jammed against the player, so
                // the horn box can be overlapping the hurtbox the instant the window opens - a
                // case neither the swept cast (no previous pose / no movement yet) nor
                // OnTriggerEnter (no "enter" for an already-overlapping collider that was just
                // enabled) reliably catches. Resolve a static overlap once, here.
                StaticOverlapCheck();
            }
            _previousPosition = transform.position;
            _hasPreviousPose = true;
        }

        private void StaticOverlapCheck()
        {
            if (!(hitCollider is BoxCollider box))
            {
                return;
            }
            Vector3 half = Vector3.Scale(box.size, transform.lossyScale) * 0.5f;
            int count = Physics.OverlapBoxNonAlloc(
                transform.position + transform.rotation * box.center, half,
                _overlapBuffer, transform.rotation, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                Collider c = _overlapBuffer[i];
                if (c != null && c != hitCollider)
                {
                    TryHit(c);
                }
            }
        }

        private readonly Collider[] _overlapBuffer = new Collider[8];

        private void SweepCheck()
        {
            Vector3 current = transform.position;
            Vector3 delta = current - _previousPosition;
            float distance = delta.magnitude;
            if (distance < 0.0001f)
            {
                return; // barely moved - ordinary OnTriggerEnter already covers a static overlap
            }

            Vector3 dir = delta / distance;
            int count;
            if (hitCollider is BoxCollider box)
            {
                Vector3 half = Vector3.Scale(box.size, transform.lossyScale) * 0.5f;
                count = Physics.BoxCastNonAlloc(_previousPosition + transform.rotation * box.center, half, dir,
                    _sweepBuffer, transform.rotation, distance, ~0, QueryTriggerInteraction.Collide);
            }
            else if (hitCollider is SphereCollider sphere)
            {
                float r = sphere.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
                count = Physics.SphereCastNonAlloc(_previousPosition + sphere.center, r, dir,
                    _sweepBuffer, distance, ~0, QueryTriggerInteraction.Collide);
            }
            else
            {
                return; // capsule/mesh not used for the horn - plain OnTriggerEnter still applies
            }

            for (int i = 0; i < count; i++)
            {
                Collider c = _sweepBuffer[i].collider;
                if (c != null && c != hitCollider)
                {
                    TryHit(c);
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            TryHit(other);
        }

        private void TryHit(Collider other)
        {
            if (!IsActive || _attackerRoot == null)
            {
                return;
            }
            if (other.transform.root == _attackerRoot)
            {
                return; // never the bug's own colliders
            }

            Transform targetRoot = other.transform.root;
            if (_hitThisActivation.Contains(targetRoot))
            {
                return; // this strike already resolved against this target
            }
            if (!other.TryGetComponent(out IDamageable damageable))
            {
                return;
            }

            _hitThisActivation.Add(targetRoot);

            Vector3 point = other.ClosestPoint(transform.position);
            Vector3 knockDir = other.transform.position - _attackerRoot.position;
            knockDir.y = 0f;
            knockDir = knockDir.sqrMagnitude > 0.0001f ? knockDir.normalized : _attackerRoot.forward;

            damageable.ApplyDamage(new DamageInfo(damage, point, knockDir, _attackerRoot.gameObject));
        }
    }
}
