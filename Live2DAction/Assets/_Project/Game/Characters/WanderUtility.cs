using System;
using UnityEngine;

namespace Live2DAction.Characters
{
    // Pure direction-decision logic for WanderMovement, kept separate from the MonoBehaviour
    // so it's directly EditMode-testable (mirrors EnemyBehaviorUtility/TargetLockUtility's
    // existing pure-logic-first pattern in this codebase). randomAngleDegrees is injected
    // rather than called directly on UnityEngine.Random so tests can supply a fixed value.
    public static class WanderUtility
    {
        // Picks the next horizontal (Y=0) direction to wander in. Near the boundary, steers
        // back towards the origin instead of picking a random angle, so a wandering character
        // never needs to actually touch a boundary wall to turn around.
        public static Vector3 ComputeDirection(Vector3 position, Vector3 currentDirection, float boundaryHalfExtent, Func<float> randomAngleDegrees)
        {
            bool pastBoundary = Mathf.Abs(position.x) > boundaryHalfExtent || Mathf.Abs(position.z) > boundaryHalfExtent;
            if (pastBoundary)
            {
                Vector3 towardCenter = new Vector3(-position.x, 0f, -position.z);
                return towardCenter.sqrMagnitude > 0.0001f ? towardCenter.normalized : currentDirection;
            }

            float angleRadians = randomAngleDegrees() * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(angleRadians), 0f, Mathf.Cos(angleRadians));
        }
    }
}
