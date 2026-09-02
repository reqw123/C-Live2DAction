using UnityEngine;

namespace Live2DAction.Combat
{
    // 2026-09-01, user request - Sekiro deflect, spec section 三 (「玩家防禦刀刃判定」).
    //
    // The Guard Collider: a trigger Capsule on the dedicated "GuardWeapon" layer, enabled only
    // while the player is defending, that the boss's swept weapon hitbox casts against
    // (BossHitbox.SweepCheck).
    //
    // WHY IT'S NOT LITERALLY THE BLADE SHAPE: the 4x 武士 swings its BladeHitbox down onto the
    // player from a y≈1.5-3.5 column, and those Meshy attack clips already connect marginally even
    // against the player's body (see Wushi_Attack_DoubleCombo's designNotes - "starts ~3 units
    // behind its root"). A blade-thin forward collider misses them entirely. So this is a generous
    // "guard coverage" volume anchored in front of + above the player, leaning toward the sword
    // hand. It STILL rotates the visible katana forward-up while guarding (so it reads as a real
    // blade guard), and the frontal cone + parry window are the real gate.
    [RequireComponent(typeof(CapsuleCollider))]
    [DefaultExecutionOrder(-40)] // position before BossHitbox.FixedUpdate sweeps
    public class PlayerGuardVolume : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("The PlayerGuard that owns this volume. Auto-found in parents if unset.")]
        [SerializeField] private PlayerGuard guard;

        [Tooltip("The katana weapon mount - rotated toward the facing while guarding, and the " +
                 "volume leans toward it.")]
        [SerializeField] private Transform weaponMount;

        [Header("Guard coverage volume")]
        [Tooltip("Height of the near end above the PLAYER's feet - low enough for a point-blank / kick.")]
        [SerializeField] private float nearHeight = 0.9f;
        [Tooltip("How far the near end sits BEHIND the player's chest (covers a body-hugging attack).")]
        [SerializeField] private float backReach = 0.35f;
        [Tooltip("How far forward the guard reaches, world metres.")]
        [SerializeField] private float reach = 1.5f;
        [Tooltip("Height the far end rises to above the player's feet - tall enough for the 4x boss.")]
        [SerializeField] private float farHeight = 3.4f;
        [Tooltip("How much the volume leans toward the sword hand (0 = straight off the player).")]
        [SerializeField, Range(0f, 1f)] private float handLean = 0.35f;
        [Tooltip("Capsule radius, world metres.")]
        [SerializeField] private float radius = 0.45f;

        [Header("Guard weapon pose (visual)")]
        [Tooltip("While guarding, rotate the katana so the blade points up-forward (a guard stance). " +
                 "Cosmetic - the collider above is what actually blocks.")]
        [SerializeField] private bool rotateWeapon = true;
        [Tooltip("How steeply the guard blade points UP vs forward. 0 = level, ~1 = 45deg.")]
        [SerializeField, Range(0f, 3f)] private float bladeRise = 1.1f;
        [SerializeField] private float poseBlendSpeed = 12f;

        [Header("Debug")]
        [SerializeField] private bool drawGizmo = true;

        private CapsuleCollider _capsule;
        private Transform _facingT;
        private float _poseWeight;

        // The volume's segment endpoints - the spec's 刀根 / 刀尖, exposed for the boss sweep + debug.
        public Vector3 BladeRoot { get; private set; }
        public Vector3 BladeTip { get; private set; }
        public float Radius => radius;
        public bool Active => _capsule != null && _capsule.enabled;

        private void Awake()
        {
            _capsule = GetComponent<CapsuleCollider>();
            _capsule.isTrigger = true;
            _capsule.enabled = false;
            if (guard == null)
            {
                guard = GetComponentInParent<PlayerGuard>();
            }
            _facingT = guard != null ? guard.transform : transform;
        }

        private bool ShouldBeActive => guard != null && guard.DefenseActionActive;

        private void FixedUpdate()
        {
            RecomputeAndPlace();
        }

        private void LateUpdate()
        {
            RecomputeAndPlace();

            if (!rotateWeapon || weaponMount == null || _facingT == null)
            {
                return;
            }
            _poseWeight = Mathf.MoveTowards(_poseWeight, ShouldBeActive ? 1f : 0f, poseBlendSpeed * Time.deltaTime);
            if (_poseWeight <= 0.001f)
            {
                return;
            }
            Vector3 fwd = _facingT.forward; fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
            Vector3 bladeDir = (fwd + Vector3.up * bladeRise).normalized;
            Vector3 currentBladeAxis = weaponMount.rotation * Vector3.up;
            Quaternion corrected = Quaternion.FromToRotation(currentBladeAxis, bladeDir) * weaponMount.rotation;
            weaponMount.rotation = Quaternion.Slerp(weaponMount.rotation, corrected, _poseWeight);
        }

        private void RecomputeAndPlace()
        {
            bool on = ShouldBeActive;
            if (_capsule != null)
            {
                _capsule.enabled = on;
            }

            if (_facingT == null)
            {
                return;
            }
            Vector3 fwd = _facingT.forward; fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
            Vector3 basePos = _facingT.position;

            Vector3 nearXZ = basePos - fwd * backReach;
            Vector3 farXZ = basePos + fwd * reach;
            if (weaponMount != null && handLean > 0f)
            {
                Vector3 handXZ = new Vector3(weaponMount.position.x, basePos.y, weaponMount.position.z);
                nearXZ = Vector3.Lerp(nearXZ, handXZ, handLean);
                farXZ = Vector3.Lerp(farXZ, handXZ + fwd * reach, handLean);
            }
            BladeRoot = new Vector3(nearXZ.x, basePos.y + nearHeight, nearXZ.z);
            BladeTip = new Vector3(farXZ.x, basePos.y + farHeight, farXZ.z);

            if (!on || _capsule == null)
            {
                return;
            }
            Vector3 a = BladeRoot, b = BladeTip;
            Vector3 axis = b - a;
            float len = axis.magnitude;
            transform.position = (a + b) * 0.5f;
            if (len > 1e-4f)
            {
                transform.rotation = Quaternion.LookRotation(axis / len);
            }
            _capsule.direction = 2;
            _capsule.center = Vector3.zero;
            _capsule.radius = radius;
            _capsule.height = Mathf.Max(len + radius * 2f, radius * 2f);
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmo || !Application.isPlaying || guard == null)
            {
                return;
            }
            bool active = Active;
            bool parry = guard.CurrentDefense == PlayerGuard.DefenseState.Parry;
            Gizmos.color = !active ? new Color(0.4f, 0.4f, 0.4f, 0.5f)
                : parry ? Color.white : new Color(0.2f, 0.6f, 1f, 1f);
            Gizmos.DrawLine(BladeRoot, BladeTip);
            Gizmos.DrawWireSphere(BladeRoot, radius);
            Gizmos.DrawWireSphere(BladeTip, radius);

            Vector3 o = guard.transform.position + Vector3.up;
            float half = guard.GuardArcDegrees * 0.5f;
            Vector3 f = guard.transform.forward;
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.5f);
            Gizmos.DrawLine(o, o + Quaternion.Euler(0f, -half, 0f) * f * 1.6f);
            Gizmos.DrawLine(o, o + Quaternion.Euler(0f, half, 0f) * f * 1.6f);
        }

        public void EditorConfigure(PlayerGuard owner, Transform mount, float capsuleRadius)
        {
            guard = owner;
            weaponMount = mount;
            radius = capsuleRadius;
        }
    }
}
