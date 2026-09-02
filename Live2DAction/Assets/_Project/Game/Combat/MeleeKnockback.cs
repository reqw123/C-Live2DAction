using UnityEngine;
using Live2DAction.Characters;
using Live2DAction.Combat.Boss;

namespace Live2DAction.Combat
{
    // 2026-08-29, cat combat design (Docs/CAT_COMBAT_DESIGN.md 4.3). A general-purpose knockback
    // receiver for the melee pipeline (AttackResolver dispatches to IKnockbackReceiver when an
    // AttackData authored a KnockbackForce). The boss's own KnockbackReceiver requires a
    // CharacterMovement; this one handles all three melee targets:
    //   - CharacterMovement present  -> route through ApplyDash / ApplyUpwardLaunch (one
    //     authoritative velocity pipeline, same as the boss receiver) - the player cat.
    //   - CharacterController only    -> decaying controller.Move each frame - the enemy cat.
    //   - neither                     -> decaying transform translation - the training dummy.
    //
    // Named MeleeKnockback (not KnockbackReceiver) to avoid colliding with the boss class of
    // that name in the sibling namespace.
    public class MeleeKnockback : MonoBehaviour, IKnockbackReceiver
    {
        [SerializeField] private float decaySeconds = 0.35f;
        [SerializeField] private float instantFraction = 0.15f;
        [SerializeField] private float launchUpwardSpeed = 4f;

        private CharacterMovement _movement;
        private CharacterController _controller;
        private Vector3 _velocity;
        private float _timer;

        private void Awake()
        {
            _movement = GetComponent<CharacterMovement>();
            _controller = GetComponent<CharacterController>();
        }

        public void ApplyKnockback(Vector3 horizontalDirection, float force, bool launchesUpward)
        {
            Vector3 dir = horizontalDirection;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f || force <= 0f)
            {
                return;
            }
            dir.Normalize();

            if (_movement != null)
            {
                _movement.ApplyDash(dir, force, force * instantFraction);
                if (launchesUpward)
                {
                    _movement.ApplyUpwardLaunch(launchUpwardSpeed);
                }
                return;
            }

            _velocity = dir * force;
            _timer = decaySeconds;

            // A guaranteed immediate snap on trigger, same idiom as CharacterMovement.ApplyDash's
            // instantDisplacement, so the hit reads as displacement even before the decay runs.
            Vector3 snap = dir * (force * instantFraction);
            if (_controller != null && _controller.enabled)
            {
                _controller.Move(snap);
            }
            else
            {
                transform.position += snap;
            }
        }

        private void Update()
        {
            if (_timer <= 0f)
            {
                return;
            }
            _timer -= Time.deltaTime;
            float k = Mathf.Clamp01(_timer / Mathf.Max(0.001f, decaySeconds));
            Vector3 step = _velocity * k * Time.deltaTime;

            if (_controller != null && _controller.enabled)
            {
                _controller.Move(step);
            }
            else
            {
                transform.position += step;
            }

            if (_timer <= 0f)
            {
                _velocity = Vector3.zero;
            }
        }

        // Pure so the decay shape is EditMode-testable.
        public static float DecayFactor(float timerRemaining, float decaySeconds)
        {
            return Mathf.Clamp01(timerRemaining / Mathf.Max(0.001f, decaySeconds));
        }
    }
}
