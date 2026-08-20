using UnityEngine;
using UnityEngine.Serialization;
using Live2DAction.Core;
using Live2DAction.Input;

namespace Live2DAction.Combat
{
    // Ultimate skill (2026-08-13, explicit user request): pressing R while UltimateEnergy is
    // full consumes the whole bar and, for durationSeconds, scales the character's held
    // weapon up by weaponScaleMultiplier and multiplies PlayerCombat's damage by
    // ultimateDamageMultiplier. Both revert automatically when the timer runs out. Can't be
    // re-triggered or extended while already active - Update returns early during the buff
    // window, and energy is fully drained on activation so it can't refill mid-buff (regen
    // only resumes from 0 after Consume()).
    //
    // 2026-08-13 revision: weapon growth is no longer instant - explicit follow-up request
    // ("不在是瞬間，而是快速遞增致最大") - it now ramps from the weapon's original scale up
    // to the full multiplier over scaleRampSeconds instead of snapping on the activation
    // frame. Only the GROWTH ramps; damage multiplier and the eventual revert-on-expiry are
    // still immediate (only the visual growth was called out as needing to not be instant).
    //
    // 2026-08-18, real bug report ("確定玩家施展必殺技時 5秒內每次攻擊都是10倍傷害嗎") - the
    // damage multiplier originally only applied to Attack1 specifically (matching the literal
    // 2026-08-13 request text), confirmed via explicit follow-up that every hit during the
    // window should be buffed instead - see PlayerCombat.UltimateDamageMultiplier's own comment
    // for the actual application-side change; this class just renamed its own field to match
    // (FormerlySerializedAs keeps the already-tuned value of 10 from being reset on reimport).
    [RequireComponent(typeof(PlayerCombat))]
    public class UltimateAbility : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour inputSource;
        [SerializeField] private UltimateEnergy energy;
        // 2026-08-16, explicit user request: 施放必殺技瞬間，身體周圍散發一瞬間的霸氣感 - optional
        // (left null-safe in Activate()) so existing tests that build a bare UltimateAbility
        // without a burst effect keep working unchanged, same pattern as EnemyAI.combat's own
        // "optional, defaults to null" field.
        [SerializeField] private UltimateActivationBurst burst;
        [SerializeField] private float weaponScaleMultiplier = 10f;
        [FormerlySerializedAs("attack1DamageMultiplier")]
        [SerializeField] private float ultimateDamageMultiplier = 10f;
        [SerializeField] private float durationSeconds = 5f;
        // "快速" (fast), not a slow buildup - reaches full size well within the 5s buff
        // window and holds there for the remainder.
        [SerializeField] private float scaleRampSeconds = 0.3f;

        // Found by name rather than wired as a serialized reference (2026-08-13) - the
        // weapon is attached by a separate, independently re-runnable Editor tool
        // (Player5WeaponSetup), which destroys and recreates this object every time it runs;
        // resolving by name here means this component never goes stale/needs re-wiring after
        // that, same "re-find after the thing it points at gets rebuilt" lesson as
        // Player5VisualSetup.RewireAnimatorLinks earlier in this project.
        private const string WeaponObjectName = "WolfsGravestone";

        private PlayerCombat _combat;
        private Transform _weapon;
        private Vector3 _weaponOriginalScale;
        private bool _active;
        private bool _ramping;
        private float _remaining;
        private float _rampElapsed;

        // Resolved on every use, not cached in Awake - same reasoning as PlayerCombat's own
        // InputCommand property (assigning inputSource after Awake, e.g. from a test, should
        // still take effect).
        private IInputCommand InputCommand => inputSource as IInputCommand;

        public bool IsActive => _active;

        private void Awake()
        {
            _combat = GetComponent<PlayerCombat>();
        }

        private void Update()
        {
            if (_active)
            {
                _remaining -= Time.deltaTime;

                if (_ramping && _weapon != null)
                {
                    _rampElapsed += Time.deltaTime;
                    float t = scaleRampSeconds > 0f ? Mathf.Clamp01(_rampElapsed / scaleRampSeconds) : 1f;
                    _weapon.localScale = Vector3.Lerp(_weaponOriginalScale, _weaponOriginalScale * weaponScaleMultiplier, t);
                    if (t >= 1f)
                    {
                        _ramping = false;
                    }
                }

                if (_remaining <= 0f)
                {
                    Deactivate();
                }

                return;
            }

            IInputCommand input = InputCommand;
            if (input != null && input.UltimatePressed && energy != null && energy.IsFull)
            {
                Activate();
            }
        }

        private void Activate()
        {
            energy.Consume();
            _active = true;
            _remaining = durationSeconds;
            _combat.UltimateDamageMultiplier = ultimateDamageMultiplier;

            if (burst != null)
            {
                burst.Play();
            }

            _weapon = FindWeapon();
            if (_weapon != null)
            {
                _weaponOriginalScale = _weapon.localScale;
                _ramping = true;
                _rampElapsed = 0f;
                // Starts the ramp at scale 1x of original (not left at whatever it happened
                // to be) so Update's Lerp always begins from a known point.
                _weapon.localScale = _weaponOriginalScale;
            }
        }

        private void Deactivate()
        {
            _active = false;
            _ramping = false;
            _combat.UltimateDamageMultiplier = 1f;

            if (_weapon != null)
            {
                _weapon.localScale = _weaponOriginalScale;
            }
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
