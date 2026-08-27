using System.Collections;
using UnityEngine;
using Live2DAction.Core;
using Live2DAction.Input;
using Live2DAction.Targeting;

namespace Live2DAction.Combat
{
    // Ultimate skill (2026-08-13, explicit user request): pressing R while UltimateEnergy is
    // full consumes the whole bar.
    //
    // 2026-08-23, explicit user request ("我想在玩家施放r時 將大劍快速旋轉5圈後飛到空中再往敵人方向
    // 飛去並且插在腳下 扣除500血量後飛回原本位置") - REPLACES this ability's entire previous
    // behavior (weapon-scale-to-10x + 10x melee damage for 5 seconds - see git history for that
    // version). User's own explicit call when told the two effects physically conflict (the sword
    // can't be flying at an enemy AND simultaneously enlarged on the player's back): "新動畫完全
    // 取代現有效果". UltimateAttackAnimationSwap still reads IsActive unchanged - it now just spans
    // the throw sequence's duration instead of the old 5s buff window, which is harmless (that
    // component only swaps an animation clip, doesn't care why IsActive is true).
    [RequireComponent(typeof(PlayerCombat))]
    public class UltimateAbility : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour inputSource;
        [SerializeField] private UltimateEnergy energy;
        // 2026-08-16, explicit user request: 施放必殺技瞬間，身體周圍散發一瞬間的霸氣感 - still plays
        // on activation, independent of what happens to the weapon afterward.
        [SerializeField] private UltimateActivationBurst burst;

        // Reused from ThirdPersonCameraController/PlayerCombat's own "optional MonoBehaviour cast
        // to an interface" convention - who to throw the sword at. See Update's own comment for
        // what happens when nothing is currently locked on.
        [SerializeField] private MonoBehaviour lockOnSource;

        [SerializeField] private float damage = 500f;
        [SerializeField] private int spinCount = 5;
        [SerializeField] private float spinDuration = 0.6f;
        [SerializeField] private float riseHeight = 3f;
        [SerializeField] private float riseDuration = 0.3f;
        [SerializeField] private float flyToTargetDuration = 0.4f;
        // How long it stays visibly embedded in the ground before flying back - "插在腳下" reads
        // as a beat, not an instant bounce-back.
        [SerializeField] private float embedDuration = 0.6f;
        [SerializeField] private float returnDuration = 0.5f;
        [SerializeField] private GameObject hitEffectPrefab;

        // 2026-08-23, explicit user request ("如果沒有指定就飛向前方") - how far ahead of the player
        // the sword flies when nothing is locked on via the mouse-wheel lock-on.
        [SerializeField] private float noLockThrowDistance = 6f;

        // Radius to check for anyone standing at the landing point when there was no locked
        // target to ask directly - generous enough to catch someone roughly where the blade
        // visually lands without needing pixel-perfect aim on a throw that was never aimed at
        // anyone specific in the first place.
        [SerializeField] private float noLockImpactRadius = 1f;

        // Found by name rather than wired as a serialized reference (2026-08-13, carried over from
        // the previous version of this class) - see that field's own original comment for why
        // (Player5WeaponSetup destroys/recreates this object independently).
        private const string WeaponObjectName = "WolfsGravestone";

        private PlayerCombat _combat;
        private bool _active;

        // Resolved on every use, not cached in Awake - same reasoning as PlayerCombat's own
        // InputCommand property.
        private IInputCommand InputCommand => inputSource as IInputCommand;
        private ILockOnSource LockOnSource => lockOnSource as ILockOnSource;

        public bool IsActive => _active;

        private void Awake()
        {
            _combat = GetComponent<PlayerCombat>();
        }

        private void Update()
        {
            if (_active)
            {
                return;
            }

            IInputCommand input = InputCommand;
            if (input == null || !input.UltimatePressed || energy == null || !energy.IsFull)
            {
                return;
            }

            Transform weapon = FindWeapon();
            if (weapon == null)
            {
                return;
            }

            energy.Consume();
            if (burst != null)
            {
                burst.Play();
            }

            // 2026-08-23, explicit user request ("r技能目標會是飛往滾輪指定的敵人 如果沒有指定就飛向
            // 前方") - REPLACES the previous "fall back to nearest LockOnTarget in the whole scene"
            // behavior (which could throw at something well off to the side or behind the player,
            // never actually "forward"). Now strictly: whatever is currently locked on via the
            // mouse-wheel lock-on (TargetLockController), or straight ahead of the player if
            // nothing is locked - never scans the scene for a substitute target.
            Transform locked = LockOnSource?.LockedTarget;
            Vector3 targetPosition = locked != null
                ? locked.position
                : transform.position + transform.forward * noLockThrowDistance;

            StartCoroutine(ThrowSequence(weapon, targetPosition, locked));
        }

        private IEnumerator ThrowSequence(Transform weapon, Vector3 targetPosition, Transform lockedTarget)
        {
            _active = true;

            Vector3 homeLocalPosition = weapon.localPosition;
            Quaternion homeLocalRotation = weapon.localRotation;

            Vector3 startWorldPos = weapon.position;
            Quaternion startWorldRot = weapon.rotation;

            // Fully owns the transform for the duration of the throw (world position/rotation set
            // directly every frame below) rather than fighting the player's own movement/rotation
            // as a child each frame - reparented back to Player at the very end.
            weapon.SetParent(null, true);

            // Phase 1: rapid spin in place around the world vertical axis - "快速旋轉5圈".
            float elapsed = 0f;
            while (elapsed < spinDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / spinDuration);
                Quaternion spin = Quaternion.AngleAxis(360f * spinCount * t, Vector3.up);
                weapon.SetPositionAndRotation(startWorldPos, spin * startWorldRot);
                yield return null;
            }

            Quaternion spinEndRotation = weapon.rotation;

            // Phase 2: rise straight up - "飛到空中".
            Vector3 risenPos = startWorldPos + Vector3.up * riseHeight;
            elapsed = 0f;
            while (elapsed < riseDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / riseDuration);
                weapon.SetPositionAndRotation(Vector3.Lerp(startWorldPos, risenPos, t), spinEndRotation);
                yield return null;
            }

            // Phase 3: fly toward the target, tip leading, landing at the ground under their feet
            // (not their AimPoint's own height, which may sit at chest/knee level depending on the
            // target - "插在腳下" means the ground, not wherever the aim point happens to be).
            // Re-read live rather than using the position snapshotted back in Update - the target
            // may have moved during the spin+rise windup, and flying at a stale point would miss.
            //
            // 2026-08-25, user feedback ("有時會穿過敵人或是偏移") - two real bugs fixed here:
            // 1) impactPoint used to be computed ONCE right before the flight loop below, then
            //    lerped toward as a fixed point for the whole flight - a target that kept moving
            //    (very plausible, Boss AI never stops acting) meant the sword landed wherever they
            //    USED to be, visually passing through/missing wherever they'd actually moved to by
            //    the time it arrived. The loop below now re-samples the live position every frame.
            // 2) The X/Z used to come from lockedTarget itself, which (as of today's lock-on fix)
            //    is AimPoint - parented to the character's CHEST bone, not their root. A boss's
            //    chest bone drifts sideways from its actual root/feet position during animation
            //    (leaning, attacking), so stabbing "under the AimPoint's X/Z" could land visibly
            //    beside the character's body instead of centered under it. Now tracks the Health
            //    component's own root Transform (the character's stable center) instead, resolved
            //    once here and reused both for live tracking below and for the damage application
            //    after the loop - only falls back to the static targetPosition/a physical search at
            //    the landing point when there was no specific character locked on to track.
            Health targetHealth = lockedTarget != null ? lockedTarget.GetComponentInParent<Health>() : null;
            Transform trackTransform = targetHealth != null ? targetHealth.transform : null;

            Vector3 liveTargetPosition = trackTransform != null ? trackTransform.position : targetPosition;
            Vector3 impactPoint = liveTargetPosition;
            impactPoint.y = FindGroundY(liveTargetPosition, risenPos.y);

            Vector3 flightDirection = (impactPoint - risenPos).normalized;
            // 2026-08-23, real playtested bug ("r施放十大劍應該是劍尖的朝向敵人飛去而非劍柄") - the
            // weapon's own root pivot sits at the TIP end (established when it was first mounted
            // on the back this session: local Y=0/near-pivot is the blade tip, increasing Y walks
            // OUT toward the crossguard/pommel at the hilt), so local +Y (transform.up) points
            // tip-TO-hilt, not hilt-to-tip. Aligning +Y with flightDirection (the previous version)
            // put the hilt end forward and the tip trailing behind the pivot - backwards. Aligning
            // local -Y (Vector3.down) with flightDirection instead puts the tip - which sits right
            // at the pivot - leading into the throw, hilt trailing behind it.
            Quaternion flightRotation = flightDirection.sqrMagnitude > 0.0001f
                ? Quaternion.FromToRotation(Vector3.down, flightDirection)
                : spinEndRotation;

            elapsed = 0f;
            while (elapsed < flyToTargetDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / flyToTargetDuration);
                // Keep re-tracking the live target position every frame (see this loop's own
                // setup comment above) instead of lerping toward a stale pre-flight snapshot.
                if (trackTransform != null)
                {
                    Vector3 currentTargetPos = trackTransform.position;
                    currentTargetPos.y = FindGroundY(currentTargetPos, risenPos.y);
                    impactPoint = currentTargetPos;
                }
                weapon.SetPositionAndRotation(Vector3.Lerp(risenPos, impactPoint, t), flightRotation);
                yield return null;
            }

            // targetHealth was already resolved before the flight loop (needed there for live
            // tracking) when something was locked on. The "no lock, fly forward" case has no
            // specific Transform to track, so it checks whatever's physically standing at the
            // FINAL landing point instead - a forward throw that happens to land on someone should
            // still hit them, it just isn't steered at anyone in particular.
            if (targetHealth == null && lockedTarget == null)
            {
                targetHealth = FindHealthNear(impactPoint);
            }
            if (targetHealth != null)
            {
                targetHealth.ApplyDamage(new DamageInfo(damage, impactPoint, flightDirection, gameObject));
            }

            if (hitEffectPrefab != null)
            {
                Instantiate(hitEffectPrefab, impactPoint, Quaternion.identity);
            }

            yield return new WaitForSeconds(embedDuration);

            // Phase 4: fly back to its dock on the player's back - recomputed live every frame
            // from the player's CURRENT transform so it correctly tracks them even if they moved
            // during the whole sequence, rather than returning to a stale world point.
            Vector3 embeddedPos = weapon.position;
            Quaternion embeddedRot = weapon.rotation;
            elapsed = 0f;
            while (elapsed < returnDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / returnDuration);
                Vector3 homeWorldPos = transform.TransformPoint(homeLocalPosition);
                Quaternion homeWorldRot = transform.rotation * homeLocalRotation;
                weapon.SetPositionAndRotation(
                    Vector3.Lerp(embeddedPos, homeWorldPos, t),
                    Quaternion.Slerp(embeddedRot, homeWorldRot, t));
                yield return null;
            }

            weapon.SetParent(transform, true);
            weapon.localPosition = homeLocalPosition;
            weapon.localRotation = homeLocalRotation;

            _active = false;
        }

        // Same "ignore my own collider, only care about real ground" SphereCast/Raycast idiom
        // TryGetGroundNormal already uses elsewhere in this project.
        private float FindGroundY(Vector3 origin, float fromHeight)
        {
            Vector3 rayStart = new Vector3(origin.x, fromHeight, origin.z);
            float rayDistance = fromHeight - origin.y + 10f;
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, rayDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                return hit.point.y;
            }

            return origin.y;
        }

        // Only used for the "no lock, threw forward" case - see noLockImpactRadius's own comment.
        private Health FindHealthNear(Vector3 point)
        {
            Collider[] hits = Physics.OverlapSphere(point, noLockImpactRadius, ~0, QueryTriggerInteraction.Ignore);
            foreach (Collider hit in hits)
            {
                Health health = hit.GetComponentInParent<Health>();
                if (health != null && health.transform.root != transform.root)
                {
                    return health;
                }
            }

            return null;
        }

        private Transform FindWeapon()
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == WeaponObjectName)
                {
                    return t;
                }
            }

            return null;
        }
    }
}
