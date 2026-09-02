using System.Collections.Generic;
using UnityEngine;

namespace Live2DAction.Targeting
{
    // Pure candidate-selection and angle math, kept separate from TargetLockController/
    // MonoBehaviour polling so it's directly EditMode-testable (mirrors AttackResolver's
    // existing pure-logic pattern in this codebase).
    public static class TargetLockUtility
    {
        // Picks the closest candidate within maxRange and within maxAngleDegrees of
        // viewDirection; ignores null/inactive candidates. Returns null if none qualify.
        public static Transform FindBestTarget(Vector3 fromPosition, Vector3 viewDirection, float maxRange, float maxAngleDegrees, IEnumerable<Transform> candidates)
        {
            Transform best = null;
            float bestDistance = float.MaxValue;

            foreach (Transform candidate in candidates)
            {
                if (candidate == null || !candidate.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector3 offset = candidate.position - fromPosition;
                float distance = offset.magnitude;
                if (distance > maxRange || distance < 0.0001f)
                {
                    continue;
                }

                float angle = Vector3.Angle(viewDirection, offset);
                if (angle > maxAngleDegrees)
                {
                    continue;
                }

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            return best;
        }

        // A locked target becomes invalid if it was destroyed, deactivated (Health.ApplyDamage
        // already deactivates the GameObject on death, so this covers "died" for free without
        // TargetLockController needing to know about Health at all), or has drifted past
        // breakRange.
        //
        // 2026-08-29, user report ("武士在起飛後 玩家如果是鎖定視角會瞬間丟失") - the break check is
        // now HORIZONTAL distance only. A boss that leaps or flies straight up is still "right
        // there" and gaining altitude alone must not drop the lock; before, a ~15-20m leap over a
        // boss already near the old 20m 3D breakRange tipped it past the edge and the lock
        // vanished mid-attack. Horizontal-only matches how action games keep lock through a jump.
        public static bool IsStillValid(Vector3 fromPosition, Transform target, float breakRange)
        {
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                return false;
            }

            Vector3 horizontalOffset = target.position - fromPosition;
            horizontalOffset.y = 0f;
            return horizontalOffset.magnitude <= breakRange;
        }

        // Converts a look-from-position -> target-position direction into the same
        // (pitch, yaw) representation ThirdPersonCameraController's mouse-look already uses
        // (Quaternion.Euler(pitch, yaw, 0) * Vector3.forward), so switching between mouse-look
        // and lock-on only changes where yaw/pitch come from, not how they're consumed.
        public static void ComputeLockOnYawPitch(Vector3 fromPosition, Vector3 targetPosition, float minPitch, float maxPitch, out float yaw, out float pitch)
        {
            Vector3 direction = targetPosition - fromPosition;
            if (direction.sqrMagnitude < 0.0001f)
            {
                yaw = 0f;
                pitch = 0f;
                return;
            }

            yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

            Vector3 flat = new Vector3(direction.x, 0f, direction.z);
            float horizontalDistance = flat.magnitude;
            pitch = Mathf.Clamp(-Mathf.Atan2(direction.y, horizontalDistance) * Mathf.Rad2Deg, minPitch, maxPitch);
        }
    }
}
