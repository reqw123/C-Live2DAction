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

        public ComboAttackState(AttackData[] combo)
        {
            _combo = combo;
        }

        public AttackPhase Phase => _phase;
        public int ComboIndex => _comboIndex;
        public AttackData CurrentAttack => _comboIndex >= 0 && _comboIndex < _combo.Length ? _combo[_comboIndex] : null;

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
            AttackData current = _combo[_comboIndex];
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

                    if (attackPressed && _elapsed <= comboWindowEnd && nextIndex < _combo.Length && _combo[nextIndex] != null)
                    {
                        StartAttack(nextIndex);
                    }
                    else if (_elapsed >= recoveryEnd)
                    {
                        _phase = AttackPhase.Idle;
                        _comboIndex = -1;
                    }
                    break;
            }

            return didHit;
        }

        private void StartAttack(int index)
        {
            _comboIndex = index;
            _phase = AttackPhase.Startup;
            _elapsed = 0f;
            _hitResolvedThisAttack = false;
        }
    }
}
