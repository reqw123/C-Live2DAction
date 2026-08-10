using UnityEngine;

namespace Live2DAction.Characters
{
    // Pure, MonoBehaviour-free dodge state machine (Idle -> Dodging -> Cooldown -> Idle),
    // mirrors Combat/ComboAttackState's pattern so timing/invulnerability logic is directly
    // EditMode-testable without a Play loop.
    public class DodgeState
    {
        private readonly DodgeData _data;
        private DodgePhase _phase = DodgePhase.Idle;
        private float _elapsed;
        private Vector3 _direction;

        public DodgeState(DodgeData data)
        {
            _data = data;
        }

        public DodgePhase Phase => _phase;
        public Vector3 Direction => _direction;
        public bool IsInvulnerable => _data != null && _phase == DodgePhase.Dodging && _elapsed <= _data.InvulnerabilitySeconds;

        // Returns the world-space planar velocity to apply this step - non-zero only while
        // Dodging. desiredDirectionIfStarting is only consulted on the step a new dodge
        // begins (from Idle); it's the caller's job to decide what that direction should be
        // (e.g. current move input, or backward if none) since only CharacterMovement knows
        // about camera-relative input and facing.
        public Vector3 Tick(float deltaTime, bool dodgePressed, Vector3 desiredDirectionIfStarting)
        {
            if (_data == null)
            {
                return Vector3.zero;
            }

            switch (_phase)
            {
                case DodgePhase.Idle:
                    if (dodgePressed)
                    {
                        StartDodge(desiredDirectionIfStarting);
                        return _direction * _data.Speed;
                    }
                    return Vector3.zero;

                case DodgePhase.Dodging:
                    _elapsed += deltaTime;
                    if (_elapsed >= _data.DurationSeconds)
                    {
                        _phase = DodgePhase.Cooldown;
                        _elapsed = 0f;
                        return Vector3.zero;
                    }
                    return _direction * _data.Speed;

                case DodgePhase.Cooldown:
                    _elapsed += deltaTime;
                    if (_elapsed >= _data.CooldownSeconds)
                    {
                        _phase = DodgePhase.Idle;
                        _elapsed = 0f;
                    }
                    return Vector3.zero;

                default:
                    return Vector3.zero;
            }
        }

        private void StartDodge(Vector3 direction)
        {
            _phase = DodgePhase.Dodging;
            _elapsed = 0f;
            _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
        }
    }
}
