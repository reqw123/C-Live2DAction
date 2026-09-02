using UnityEngine;

namespace Live2DAction.Combat
{
    // Pure, MonoBehaviour-free three-hit combo state machine, kept separate from PlayerCombat
    // so its phase-timing and combo-chaining logic can be exercised directly in EditMode
    // tests without a Play loop (matches AttackResolver's pure-logic-first-then-MonoBehaviour
    // pattern already used in this codebase).
    public class ComboAttackState
    {
        private readonly AttackData[] _combo;
        private AttackPhase _phase = AttackPhase.Idle;
        private int _comboIndex = -1;
        private float _elapsed;
        private bool _hitResolvedThisAttack;

        // 2026-08-29, cat combat design (Docs/CAT_COMBAT_DESIGN.md 3.2/3.3) - a single-shot
        // attack outside the combo array (the cat's charged heavy / pounce). Runs the exact same
        // Startup/Active/Recovery timing off its own AttackData, but never chains into the combo
        // and always returns straight to Idle - so ComboIndex stays -1 the whole time (existing
        // tests/callers that read ComboIndex are unaffected). Set via StartOverride, cleared on
        // return to Idle.
        private AttackData _overrideAttack;

        public ComboAttackState(AttackData[] combo)
        {
            _combo = combo;
        }

        public AttackPhase Phase => _phase;
        public int ComboIndex => _comboIndex;
        public bool IsOverrideAttackActive => _overrideAttack != null;
        public AttackData CurrentAttack => _overrideAttack != null
            ? _overrideAttack
            : (_comboIndex >= 0 && _comboIndex < _combo.Length ? _combo[_comboIndex] : null);

        // Normalized (0-1) progress through the *current* phase only (not the whole attack) -
        // _elapsed accumulates from the start of the attack across all three phases, so this
        // subtracts each phase's start offset first. Used by AttackPoseVisualizer to drive a
        // procedural placeholder swing animation from the same frame data the hit-timing
        // already runs on, without that visual code needing its own timer. A phase with zero
        // duration (e.g. a combo's last hit having no recovery-into-combo-window) reports 1
        // (fully progressed) rather than dividing by zero.
        public float PhaseProgress
        {
            get
            {
                if (_phase == AttackPhase.Idle || CurrentAttack == null)
                {
                    return 0f;
                }

                AttackData current = CurrentAttack;
                float phaseStart;
                float phaseDuration;
                switch (_phase)
                {
                    case AttackPhase.Startup:
                        phaseStart = 0f;
                        phaseDuration = current.StartupSeconds;
                        break;
                    case AttackPhase.Active:
                        phaseStart = current.StartupSeconds;
                        phaseDuration = current.ActiveSeconds;
                        break;
                    case AttackPhase.Recovery:
                        phaseStart = current.StartupSeconds + current.ActiveSeconds;
                        phaseDuration = current.RecoverySeconds;
                        break;
                    default:
                        return 0f;
                }

                if (phaseDuration <= 0f)
                {
                    return 1f;
                }

                return Mathf.Clamp01((_elapsed - phaseStart) / phaseDuration);
            }
        }

        // Advances the state machine by deltaTime and returns true on the single step where
        // the active hitbox should resolve damage - PlayerCombat runs the Physics query only
        // on that step, so a multi-frame Active window still only hits once.
        public bool Tick(float deltaTime, bool attackPressed)
        {
            if (_phase == AttackPhase.Idle)
            {
                if (attackPressed && _combo.Length > 0 && _combo[0] != null)
                {
                    StartAttack(0);
                }
                return false;
            }

            _elapsed += deltaTime;
            AttackData current = CurrentAttack;
            bool didHit = false;

            switch (_phase)
            {
                case AttackPhase.Startup:
                    if (_elapsed >= current.StartupSeconds)
                    {
                        _phase = AttackPhase.Active;
                    }
                    break;

                case AttackPhase.Active:
                    if (!_hitResolvedThisAttack)
                    {
                        _hitResolvedThisAttack = true;
                        didHit = true;
                    }
                    if (_elapsed >= current.StartupSeconds + current.ActiveSeconds)
                    {
                        _phase = AttackPhase.Recovery;
                    }
                    break;

                case AttackPhase.Recovery:
                    float recoveryStart = current.StartupSeconds + current.ActiveSeconds;
                    float comboWindowEnd = recoveryStart + current.ComboWindowSeconds;
                    float recoveryEnd = recoveryStart + current.RecoverySeconds;
                    int nextIndex = _comboIndex + 1;

                    // An override attack (charged heavy / pounce) never chains into the combo -
                    // it just plays out its own recovery and returns to Idle.
                    if (_overrideAttack == null && attackPressed && _elapsed <= comboWindowEnd && nextIndex < _combo.Length && _combo[nextIndex] != null)
                    {
                        StartAttack(nextIndex);
                    }
                    else if (_elapsed >= recoveryEnd)
                    {
                        _phase = AttackPhase.Idle;
                        _comboIndex = -1;
                        _overrideAttack = null;
                    }
                    break;
            }

            return didHit;
        }

        // Starts a single-shot attack from an AttackData that is NOT in the combo array (the
        // cat's charged heavy / pounce claw). No-op unless currently Idle. Returns true if it
        // started. ComboIndex stays -1 throughout; IsOverrideAttackActive reports true.
        public bool StartOverride(AttackData attack)
        {
            if (_phase != AttackPhase.Idle || attack == null)
            {
                return false;
            }
            _overrideAttack = attack;
            _comboIndex = -1;
            _phase = AttackPhase.Startup;
            _elapsed = 0f;
            _hitResolvedThisAttack = false;
            return true;
        }

        private void StartAttack(int index)
        {
            _comboIndex = index;
            _overrideAttack = null;
            _phase = AttackPhase.Startup;
            _elapsed = 0f;
            _hitResolvedThisAttack = false;
        }
    }
}
