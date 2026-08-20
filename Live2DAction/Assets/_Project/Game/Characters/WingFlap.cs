using UnityEngine;
using Live2DAction.AI;

namespace Live2DAction.Characters
{
    // 2026-08-18, explicit user request ("接下來做他的振幅翅膀動作") - the wing model carries
    // zero baked animation (confirmed on import - a purely static mesh), so this procedurally
    // flaps the two wing-half meshes with a mirrored sine wave instead of needing an authored
    // clip. Rotates around each half's own local Z axis - after WingsAnchor's own rotation-
    // cancelling fix (see that setup's own history), local Z points the same way the character
    // actually faces, which is the axis a wing naturally hinges around when flapping (the tip
    // sweeps up/down through the X-Y plane while staying roughly fixed front-to-back). Mirrored
    // in sign between the two halves: both wing objects share ONE local transform origin (the
    // FBX splits left/right entirely via each mesh's own vertex data, not a transform offset -
    // see this session's own investigation), so the same +angle rotation would swing them in
    // opposite real-world directions unless negated for one side.
    public class WingFlap : MonoBehaviour
    {
        [SerializeField] private Transform leftWing;
        [SerializeField] private Transform rightWing;

        // Gentle idle flutter by default; ramps up while actually flying (see CharacterMovement.
        // IsFlying) so the wings visibly work harder exactly when they're supposed to be doing
        // something, not just decorative all the time.
        [SerializeField] private float idleAmplitudeDegrees = 8f;
        [SerializeField] private float idleFlapsPerSecond = 0.6f;
        [SerializeField] private float flyingAmplitudeDegrees = 30f;
        [SerializeField] private float flyingFlapsPerSecond = 3f;

        // Optional - null-safe below. Without either, the wings just always flap at the idle
        // rate. Two independent optional sources rather than one shared interface: a player-
        // driven character (CharacterMovement.IsFlying) and an AI-driven decorative-wings
        // character (EnemyAI.CurrentState) mean genuinely different things by "working harder" -
        // see WingFlap's own 2026-08-18 extension comment below for Enemy's case.
        [SerializeField] private CharacterMovement movement;
        [SerializeField] private EnemyAI enemyAI;

        private Quaternion _leftBaseRotation;
        private Quaternion _rightBaseRotation;
        private float _phase;

        private void Awake()
        {
            if (leftWing != null)
            {
                _leftBaseRotation = leftWing.localRotation;
            }

            if (rightWing != null)
            {
                _rightBaseRotation = rightWing.localRotation;
            }
        }

        private void Update()
        {
            // 2026-08-18 extension (flight system grilling session, Q8) - Enemy's mechanical
            // Decorative Wings have no CharacterMovement/IsFlying at all (Enemy never flies -
            // see CONTEXT.md's "Enemy flight scope" decision), so "working harder" instead means
            // EnemyAI.CurrentState == Attacking - the wings visibly rev up right as Enemy
            // commits to a swing, reading as a mechanical tell/threat cue rather than being
            // decorative all the time. Both sources are optional and independent; movement takes
            // priority if somehow both are wired (shouldn't happen in practice - one character
            // gets one or the other, never both).
            bool working = (movement != null && movement.IsFlying) ||
                           (enemyAI != null && enemyAI.CurrentState == EnemyState.Attacking);
            float amplitude = working ? flyingAmplitudeDegrees : idleAmplitudeDegrees;
            float flapsPerSecond = working ? flyingFlapsPerSecond : idleFlapsPerSecond;

            // Accumulate phase by deltaTime (not just Time.time) so a runtime change in
            // flapsPerSecond (e.g. entering/leaving flight) doesn't snap the wave to a
            // different point mid-cycle - same "continuous accumulator, not a raw
            // Time.time lookup" reasoning UltimateEnergy's own regen timer already uses.
            _phase += flapsPerSecond * 360f * Time.deltaTime;
            float angle = Mathf.Sin(_phase * Mathf.Deg2Rad) * amplitude;

            if (leftWing != null)
            {
                leftWing.localRotation = _leftBaseRotation * Quaternion.Euler(0f, 0f, angle);
            }

            if (rightWing != null)
            {
                rightWing.localRotation = _rightBaseRotation * Quaternion.Euler(0f, 0f, -angle);
            }
        }
    }
}
