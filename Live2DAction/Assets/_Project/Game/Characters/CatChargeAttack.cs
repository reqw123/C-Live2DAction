using UnityEngine;
using Live2DAction.Combat;
using Live2DAction.Input;

namespace Live2DAction.Characters
{
    // 2026-08-29, cat combat design (Docs/CAT_COMBAT_DESIGN.md 3.2). The cat's melee button is
    // release-triggered: a short tap fires a normal combo swipe, holding past a threshold and
    // releasing fires the charged heavy (an override AttackData outside the combo array). So the
    // cat's PlayerCombat.inputSource is left null and this component owns the button, calling
    // PlayerCombat.FeedAttackPressed() / TryStartOverrideAttack() itself.
    //
    // Charging can only START from Idle - mid-combo a fresh press is always just a combo-chain
    // press, never a charge (see the design doc). Reads PlayerInputProvider.AttackHeld directly
    // (concrete type, not IInputCommand) because charging is player-only: the enemy cat never
    // charges, so nothing about the shared input interface changes.
    //
    // Runs before PlayerCombat.Update (order -5) so FeedAttackPressed lands the same frame.
    // CatPounce (order -8) gets first refusal on a moving press.
    [DefaultExecutionOrder(-5)]
    [RequireComponent(typeof(PlayerCombat))]
    public class CatChargeAttack : MonoBehaviour
    {
        [SerializeField] private PlayerInputProvider input;
        [SerializeField] private AttackData heavyAttack;

        [Tooltip("Seconds the melee button must be held before release fires the heavy instead of a swipe.")]
        [SerializeField] private float chargeThresholdSeconds = 0.35f;

        // Set by CatPounce (earlier execution order) when it consumed this frame's press - so
        // this component doesn't also fire a swipe/charge from the same press. Cleared each frame.
        private bool _consumedByPounce;

        private PlayerCombat _combat;
        private bool _wasHeld;
        private float _chargeTime;

        // 0-1 for CatAttackPose's charged-pose blend; 1 = fully charged.
        public float ChargeNormalized { get; private set; }
        public bool IsCharging { get; private set; }

        // Called by CatPounce (same GameObject, earlier execution order) when it starts a pounce
        // off this frame's press, so this component doesn't also fire a swipe from it.
        public void NotifyPounceConsumedPress()
        {
            _consumedByPounce = true;
        }

        private void Awake()
        {
            _combat = GetComponent<PlayerCombat>();
        }

        private void Start()
        {
            if (input == null) input = GetComponent<PlayerInputProvider>();
        }

        private void Update()
        {
            if (_combat == null || input == null)
            {
                return;
            }

            bool held = input.AttackHeld;
            bool released = _wasHeld && !held;

            if (_combat.IsIdle)
            {
                if (held && !_consumedByPounce)
                {
                    _chargeTime += Time.deltaTime;
                }

                if (released)
                {
                    if (!_consumedByPounce)
                    {
                        if (_chargeTime >= chargeThresholdSeconds && heavyAttack != null)
                        {
                            _combat.TryStartOverrideAttack(heavyAttack);
                        }
                        else
                        {
                            _combat.FeedAttackPressed(); // normal combo swipe 1
                        }
                    }
                    _chargeTime = 0f;
                }
            }
            else
            {
                // Mid-combo: a fresh press is a chain press, never a charge.
                bool pressedThisFrame = held && !_wasHeld;
                if (pressedThisFrame && !_consumedByPounce)
                {
                    _combat.FeedAttackPressed();
                }
                _chargeTime = 0f;
            }

            IsCharging = _combat.IsIdle && held && _chargeTime > 0f && !_consumedByPounce;
            ChargeNormalized = Mathf.Clamp01(_chargeTime / Mathf.Max(0.0001f, chargeThresholdSeconds));

            _wasHeld = held;
            _consumedByPounce = false;
        }
    }
}
