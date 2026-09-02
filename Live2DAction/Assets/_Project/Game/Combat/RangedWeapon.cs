using UnityEngine;
using Live2DAction.Core;
using Live2DAction.Input;

namespace Live2DAction.Combat
{
    // 2026-08-23, explicit user request ("加入瞄準-射擊機制 模擬槍戰...做簡單判定 簡單特效") -
    // deliberately minimal, layered ALONGSIDE the existing melee combo rather than replacing it:
    // hold to aim (shows a crosshair), click to fire a single straight hitscan ray from the
    // camera. No ammo, no reload, no spread/recoil, no projectile travel time - "simple judgment,
    // simple effect" was the explicit ask, not a full shooter system. Reuses the same
    // hit-effect prefab/Health.ApplyDamage pipeline melee already uses (see PlayerCombat) so a
    // gunshot reads as a consistent part of the same combat system rather than a bolted-on
    // second one.
    // 2026-08-23, explicit user request ("要給這個射擊的攻擊距離元件 獨立新的元件管理攻擊距離 並把射擊
    // 距離大幅延長") - maxRange moved out into its own RangedAttackDistance component (see that
    // class's own comment), required here the same way LineRenderer already is, so it's always
    // present rather than an optional reference that could silently be left unwired.
    [RequireComponent(typeof(LineRenderer))]
    [RequireComponent(typeof(RangedAttackDistance))]
    public class RangedWeapon : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour inputSource;
        [SerializeField] private float damage = 15f;

        // Simple fire-rate cap so holding the fire click down doesn't matter anyway (FirePressed
        // is already an edge trigger, one shot per click - this only guards against clicking
        // faster than the gun could plausibly cycle) - same per-instance-cooldown idiom
        // Portal.cs/BoundaryBlockEffect.cs already use for their own repeat-trigger guards.
        [SerializeField] private float fireCooldownSeconds = 0.25f;

        // How long the tracer line stays visible after a shot - just long enough to read as a
        // quick flash, not a lingering laser beam.
        [SerializeField] private float tracerVisibleSeconds = 0.05f;

        [SerializeField] private GameObject hitEffectPrefab;

        // Simple screen-center crosshair, shown only while actually aiming (see
        // RangedWeaponSetup for how this gets created/wired) - optional, null just means no
        // crosshair UI, doesn't affect firing itself.
        [SerializeField] private GameObject crosshair;

        // 2026-08-23, explicit user request ("希望瞄準時 這把槍會出現在手") - the AK47 model
        // (parented under the player's Rhand_Weapon2 hand socket, same one WolfsGravestone used
        // before it became a detached back accessory) sits inactive until aiming starts, same
        // "toggle alongside the crosshair" convention as that field above - optional, null just
        // means no visible weapon model, doesn't affect firing itself.
        [SerializeField] private GameObject weaponVisual;

        // 2026-08-23, explicit user request ("能先讓我自己調位置嗎") - holding right-click to keep
        // weaponVisual shown while ALSO trying to drag its Transform in the Inspector ties up the
        // same mouse for both, same problem firstPersonEyeOffset's own tuning workflow hit earlier
        // (see ThirdPersonCameraController.debugKeepVisualVisibleInFirstPerson for the precedent).
        // Checking this keeps weaponVisual forced on regardless of AimPressed, purely so the model
        // stays visible and selectable while hand-tuning its position/rotation - leave unchecked
        // for normal play.
        [SerializeField] private bool debugAlwaysShowWeaponVisual;

        // 2026-08-23, explicit user request ("想要為瞄準時射擊配上音效") - plays once per shot from
        // Fire() below. Optional/null-safe like crosshair/weaponVisual above, so firing without
        // either wired still works exactly as before.
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip fireSound;

        private IInputCommand InputCommand => inputSource as IInputCommand;
        private LineRenderer _tracer;
        private RangedAttackDistance _attackDistance;
        private float _tracerHideAt = -1f;
        private float _cooldownUntil = -1f;

        private void Awake()
        {
            _tracer = GetComponent<LineRenderer>();
            _tracer.enabled = false;
            _attackDistance = GetComponent<RangedAttackDistance>();
        }

        // Disabled mid-aim (e.g. a possession swap to the cat via
        // CameraPossessionSwitcher.playerControl, or player death) - Update stops running, so the
        // crosshair / weapon model / tracer it last turned on for aiming would stay on screen with
        // nothing to turn them back off. Clear them here.
        private void OnDisable()
        {
            if (crosshair != null && crosshair.activeSelf)
            {
                crosshair.SetActive(false);
            }
            if (weaponVisual != null && weaponVisual.activeSelf && !debugAlwaysShowWeaponVisual)
            {
                weaponVisual.SetActive(false);
            }
            if (_tracer != null)
            {
                _tracer.enabled = false;
            }
        }

        private void Update()
        {
            IInputCommand input = InputCommand;
            bool aiming = input != null && input.AimPressed;

            if (crosshair != null && crosshair.activeSelf != aiming)
            {
                crosshair.SetActive(aiming);
            }

            bool showWeaponVisual = aiming || debugAlwaysShowWeaponVisual;
            if (weaponVisual != null && weaponVisual.activeSelf != showWeaponVisual)
            {
                weaponVisual.SetActive(showWeaponVisual);
            }

            if (aiming && input.FirePressed && Time.time >= _cooldownUntil)
            {
                _cooldownUntil = Time.time + fireCooldownSeconds;
                Fire();
            }

            if (_tracer.enabled && Time.time >= _tracerHideAt)
            {
                _tracer.enabled = false;
            }
        }

        private void Fire()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            if (audioSource != null && fireSound != null)
            {
                audioSource.PlayOneShot(fireSound);
            }

            float maxRange = _attackDistance.MaxRange;
            Vector3 origin = cam.transform.position;
            Vector3 direction = cam.transform.forward;
            Vector3 endPoint = origin + direction * maxRange;

            // RaycastAll + closest-non-self, not a plain Raycast - same "ignore my own collider"
            // pattern TryGetGroundNormal/Updraft already use elsewhere in this project, needed
            // here because the camera can end up close enough to the player's own
            // CharacterController for a plain Raycast to occasionally hit yourself first.
            RaycastHit[] hits = Physics.RaycastAll(origin, direction, maxRange, ~0, QueryTriggerInteraction.Ignore);
            float closestDistance = float.MaxValue;
            RaycastHit? closestValidHit = null;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null || hit.collider.transform.root == transform.root)
                {
                    continue;
                }

                if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    closestValidHit = hit;
                }
            }

            if (closestValidHit.HasValue)
            {
                RaycastHit hit = closestValidHit.Value;
                endPoint = hit.point;

                Health health = hit.collider.GetComponentInParent<Health>();
                if (health != null)
                {
                    health.ApplyDamage(new DamageInfo(damage, hit.point, direction, gameObject));
                }

                if (hitEffectPrefab != null)
                {
                    Instantiate(hitEffectPrefab, hit.point, Quaternion.identity);
                }
            }

            _tracer.enabled = true;
            _tracer.SetPosition(0, origin);
            _tracer.SetPosition(1, endPoint);
            _tracerHideAt = Time.time + tracerVisibleSeconds;
        }
    }
}
