using UnityEngine;

namespace Live2DAction.Characters
{
    // 2026-08-18, explicit user request (aerial combat grilling session, Q3/Q5) - both the
    // player (CharacterMovement) and Enemy (EnemyAI) need to aim up/down at a vertically-
    // offset target, clamped to a maximum pitch so a character directly below/above its target
    // doesn't contort toward straight up/down. Pure static utility (no MonoBehaviour state)
    // shared by both, matching this codebase's established pattern for cross-character math
    // (GroundSlopeUtility, WanderUtility, HealthRegenerationUtility).
    public static class AimUtility
    {
        // Takes a raw (unclamped) world-space direction to the target and returns a normalized
        // direction with the same yaw but a pitch clamped to +/-maxPitchDegrees - feed this
        // directly into Quaternion.LookRotation to get a rotation that aims at the target
        // without ever tipping past the clamp.
        public static Vector3 ClampedAimDirection(Vector3 toTarget, float maxPitchDegrees)
        {
            Vector2 horizontal = new Vector2(toTarget.x, toTarget.z);
            float horizontalDistance = horizontal.magnitude;

            if (horizontalDistance < 0.0001f && Mathf.Abs(toTarget.y) < 0.0001f)
            {
                return Vector3.forward;
            }

            float rawPitchDegrees = Mathf.Atan2(toTarget.y, horizontalDistance) * Mathf.Rad2Deg;
            float clampedPitchDegrees = Mathf.Clamp(rawPitchDegrees, -maxPitchDegrees, maxPitchDegrees);

            if (horizontalDistance < 0.0001f)
            {
                // Target is (near-)directly above/below with no horizontal component to preserve
                // a yaw from - fall back to straight forward/back tilted by the clamped pitch,
                // rather than an undefined horizontal direction.
                float clampedYFromForward = Mathf.Tan(clampedPitchDegrees * Mathf.Deg2Rad);
                return new Vector3(0f, clampedYFromForward, 1f).normalized;
            }

            float clampedY = horizontalDistance * Mathf.Tan(clampedPitchDegrees * Mathf.Deg2Rad);
            return new Vector3(horizontal.x, clampedY, horizontal.y).normalized;
        }

        // The clamped pitch angle alone (degrees, positive = looking up), for callers that need
        // to compose it into an Euler rotation directly (CharacterMovement's own yaw is already
        // smoothed separately via SmoothDampAngle, so it recombines pitch+yaw itself rather than
        // consuming ClampedAimDirection's combined vector the way EnemyAI's LookRotation does).
        public static float ClampedPitchDegrees(Vector3 toTarget, float maxPitchDegrees)
        {
            Vector2 horizontal = new Vector2(toTarget.x, toTarget.z);
            float horizontalDistance = horizontal.magnitude;
            float rawPitchDegrees = Mathf.Atan2(toTarget.y, horizontalDistance) * Mathf.Rad2Deg;
            return Mathf.Clamp(rawPitchDegrees, -maxPitchDegrees, maxPitchDegrees);
        }
    }
}
