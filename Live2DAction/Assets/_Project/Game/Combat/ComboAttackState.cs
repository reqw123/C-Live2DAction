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
