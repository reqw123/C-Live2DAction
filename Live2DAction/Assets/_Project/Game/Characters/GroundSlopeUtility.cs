using UnityEngine;

namespace Live2DAction.Characters
{
    // Pure slope-geometry math for CharacterMovement's "slide off if standing somewhere too
    // steep" fix, kept separate from the MonoBehaviour that owns the actual Physics.SphereCast
    // ground probe so it's directly EditMode-testable (mirrors this codebase's established
    // pure-logic pattern, e.g. EnemyBehaviorUtility/TargetLockUtility).
    //
    // 2026-08-16, real bug report: "跳躍有機會卡在敵人頭上，需要自行下來" - jumping onto another
    // character's CharacterController capsule can land the player on its rounded top.
    // CharacterController.isGrounded reads true there (something solid is under the feet)
    // regardless of how steep/round that surface actually is - isGrounded only reflects "is
    // something there", not "is this walkable" (that's what slopeLimit is for, but
    // slopeLimit only blocks WALKING up onto a steep slope, it does nothing once you're
    // already resting on top of one via a jump's ballistic arc). CharacterMovement's own
    // gravity handling just holds vertical velocity at -1 whenever isGrounded is true, so
    // nothing ever pushes the player back off - matches "需要自行下來" (has to manually walk
    // off) exactly.
    public static class GroundSlopeUtility
    {
        // Matches CharacterController.slopeLimit's own convention: the angle (degrees) between
        // the surface normal and straight up.
        public static bool IsTooSteepToStandOn(Vector3 groundNormal, float slopeLimitDegrees)
        {
            return Vector3.Angle(groundNormal, Vector3.up) > slopeLimitDegrees;
        }

        // The downhill direction along a (steep) slope's own surface - the component of
        // straight-down that lies in the slope's plane, flattened to the horizontal (Y=0) and
        // normalized, since CharacterMovement only wants a horizontal push (vertical motion is
        // still handled by gravity separately). Returns zero for a degenerate/flat normal
        // (nothing to slide down).
        public static Vector3 ComputeSlideDirection(Vector3 groundNormal)
        {
            Vector3 alongSlope = Vector3.ProjectOnPlane(Vector3.down, groundNormal);
            alongSlope.y = 0f;
            if (alongSlope.sqrMagnitude < 0.0001f)
            {
                // Standing dead-center on top of a dome, the slope-projected "downhill"
                // direction is undefined (every horizontal direction is equally downhill) -
                // this only happens exactly at the apex, which is an unstable equilibrium in
                // practice (any tiny drift resolves it the next frame), so zero here is fine.
                return Vector3.zero;
            }

            return alongSlope.normalized;
        }

        // Fallback for ComputeSlideDirection's own documented zero-result case (standing
        // exactly on a dome's apex, where "downhill" is undefined) - horizontal direction from
        // otherPosition to selfPosition, defaulting to +X if the two happen to be exactly
        // coincident. Used specifically for the "standing on another character" case, where
        // relying on the apex resolving itself via floating-point noise or the other
        // character's own movement isn't good enough - it needs to always resolve.
        public static Vector3 ComputeFallbackAwayDirection(Vector3 selfPosition, Vector3 otherPosition)
        {
            Vector3 away = selfPosition - otherPosition;
            away.y = 0f;
            return away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.right;
        }
    }
}
