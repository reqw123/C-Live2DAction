using UnityEngine;
using Live2DAction.Combat;
using Live2DAction.Input;

namespace Live2DAction.Characters
{
    // 2026-08-29, cat combat design (Docs/CAT_COMBAT_DESIGN.md 3.3). A pounce = a forward lunge
    // that lands a claw attack. Triggered by pressing melee while grounded AND moving (a still
    // press is a normal swipe / charge - handled by CatChargeAttack). Deliberately NOT a new
    // branch in CharacterMovement.Update (that file is already dense with flight/dodge/slide/
    // gravity) - it reuses CharacterMovement.ApplyDash, the existing one-shot forward-burst
    // primitive (CheckpointGate / Updraft / the boss KnockbackReceiver all already call it),
    // plus an override AttackData on PlayerCombat for the claw itself.
    //
    // Runs before CatChargeAttack (order -8 < -5) so a moving press becomes a pounce, not a
    // swipe; it tells CatChargeAttack it consumed the press via NotifyPounceConsumedPress.
    [DefaultExecutionOrder(-8)]
    [RequireComponent(typeof(PlayerCombat))]
    public class CatPounce : MonoBehaviour
    {
        [SerializeField] private PlayerInputProvider input;
        [SerializeField] private CharacterMovement movement;
        [SerializeField] private CatChargeAttack chargeAttack;
        [SerializeField] private AttackData pounceAttack;

        [Tooltip("Forward lunge distance. Speed = distance / durationSeconds.")]
        [SerializeField] private float pounceDistance = 3.5f;
        [SerializeField] private float pounceDurationSeconds = 0.28f;
        [Tooltip("Pounce needs actual running speed >= this fraction of moveSpeed, not just a held key - " +
                 "so a standing/adjusting tap-attack can never pounce (2026-08-29, '有時普通攻擊也會衝刺').")]
        [SerializeField] private float pounceMinSpeedFraction = 0.7f;
        [Tooltip("Fraction of the lunge applied as an instant CharacterController.Move snap on trigger.")]
        [SerializeField] private float instantFraction = 0.2f;
        [Tooltip("Seconds before another pounce can start.")]
        [SerializeField] private float cooldownSeconds = 0.7f;

        private PlayerCombat _combat;
        private float _cooldownTimer;
        private bool _wasHeld;

        public bool OnCooldown => _cooldownTimer > 0f;

        private void Awake()
        {
            _combat = GetComponent<PlayerCombat>();
        }

        private void Start()
        {
            if (input == null) input = GetComponent<PlayerInputProvider>();
            if (movement == null) movement = GetComponent<CharacterMovement>();
            if (chargeAttack == null) chargeAttack = GetComponent<CatChargeAttack>();
        }

        private void Update()
        {
            if (_cooldownTimer > 0f)
            {
                _cooldownTimer -= Time.deltaTime;
            }

            if (_combat == null || input == null || movement == null)
            {
                return;
            }

            bool held = input.AttackHeld;
            bool pressedThisFrame = held && !_wasHeld;
            _wasHeld = held;

            if (!pressedThisFrame)
            {
                return;
            }

            if (!ShouldPounce(_cooldownTimer <= 0f, _combat.IsIdle, movement.IsGrounded, movement.IsFlying,
                    input.MoveInput, movement.CurrentHorizontalSpeed, movement.MoveSpeed, pounceMinSpeedFraction))
            {
                return; // stays a normal swipe/charge - CatChargeAttack handles the press
            }

            if (!_combat.TryStartOverrideAttack(pounceAttack))
            {
                return;
            }

            Vector3 dir = transform.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f)
            {
                dir = Vector3.forward;
            }
            dir.Normalize();

            float speed = pounceDistance / Mathf.Max(0.01f, pounceDurationSeconds);
            movement.ApplyDash(dir, speed, pounceDistance * instantFraction);

            _cooldownTimer = cooldownSeconds;
            if (chargeAttack != null)
            {
                chargeAttack.NotifyPounceConsumedPress();
            }
        }

        // Pure so the "how fast does the lunge move" arithmetic is EditMode-testable, same
        // convention as DodgeData.Speed / AttackPoseUtility.
        public static float LungeSpeed(float distance, float durationSeconds)
        {
            return distance / Mathf.Max(0.01f, durationSeconds);
        }

        // The full "is this melee press a pounce, or a normal swipe/charge" rule, pure so the
        // exclusions are locked by CatCombatTests. A pounce requires: off cooldown, combat Idle,
        // grounded, not flying, a held move direction (intent) AND real running speed >=
        // minSpeedFraction * moveSpeed (commitment). 2026-08-29: the speed clause is what stops a
        // standing / adjusting / just-started tap-attack from pouncing.
        public static bool ShouldPounce(bool offCooldown, bool combatIdle, bool grounded, bool flying,
            Vector2 moveInput, float horizontalSpeed, float moveSpeed, float minSpeedFraction)
        {
            if (!offCooldown || !combatIdle || !grounded || flying)
            {
                return false;
            }
            if (moveInput.sqrMagnitude < 0.01f)
            {
                return false;
            }
            return horizontalSpeed >= moveSpeed * minSpeedFraction;
        }
    }
}
