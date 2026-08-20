using UnityEngine;

namespace Live2DAction.Combat
{
    // Pure timing/geometry math for UltimateActivationBurst's one-shot activation flash, kept
    // separate from the MonoBehaviour that owns Time/LineRenderer so it's directly
    // EditMode-testable (mirrors LightningAuraUtility/EnergyBarPulseUtility's existing
    // pure-logic pattern).
    public static class UltimateBurstUtility
    {
        // Ease-out cubic: punches out fast in the first moments, then slows toward maxRadius -
        // a shockwave that expanded at a constant rate would read as a slow-growing circle,
        // not a sudden burst.
        public static float ComputeRadius(float t01, float maxRadius)
        {
            float clamped = Mathf.Clamp01(t01);
            float eased = 1f - Mathf.Pow(1f - clamped, 3f);
            return eased * maxRadius;
        }

        // Quadratic fade - stays close to full brightness at first (reads as a sudden flash)
        // and falls off faster as the burst finishes, rather than a linear fade that would
        // look like it's dimming from the very first frame.
        public static float ComputeBrightnessMultiplier(float t01)
        {
            float remaining = 1f - Mathf.Clamp01(t01);
            return remaining * remaining;
        }

        // Smooth circle points (not jagged, unlike LightningAuraUtility's bolts - this is a
        // shockwave ring, not electricity) for the expanding ring, closed loop (first and last
        // points coincide).
        public static Vector3[] BuildRingPoints(float radius, float height, int segmentCount)
        {
            var points = new Vector3[segmentCount + 1];
            for (int i = 0; i <= segmentCount; i++)
            {
                float angle = (360f / segmentCount) * i * Mathf.Deg2Rad;
                points[i] = new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);
            }

            return points;
        }

        // Unit XZ direction for the rayIndex-th of rayCount rays, evenly spaced around a full
        // circle - the straight "burst" spikes shooting outward alongside the ring.
        public static Vector3 ComputeRayDirection(int rayIndex, int rayCount)
        {
            float angle = (360f / rayCount) * rayIndex * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }
    }
}
