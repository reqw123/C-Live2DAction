using UnityEngine;

namespace Live2DAction.UI
{
    // Pure geometry/timing math for UltimateReadyAura's single coiling lightning bolt, kept
    // separate from the MonoBehaviour that owns Time/LineRenderer so it's directly
    // EditMode-testable (mirrors EnergyBarPulseUtility/HealthBarUtility's existing pure-logic
    // pattern).
    //
    // 2026-08-16 rewrite, explicit user request ("閃電改為只有一條，從角色底部任意往上環繞，循環，
    // 就像是動漫獵人x獵人的奇犽一樣") - replaces the previous version's N-bolt orbiting ring
    // (ComputeBoltOrbitPosition/ComputeJitterOffsets/BuildBoltPoints) with a single bolt that
    // spirals up from the character's feet, grows, holds, fades, and loops - reference is
    // Killua's electric aura (Hunter x Hunter): one continuously crackling arc climbing the
    // body, not a static ring of separate bolts.
    public static class LightningAuraUtility
    {
        // Un-jittered point on the spiral at parameter s in [0,1] (0 = bottom/feet, 1 = top of
        // the climb) - spiralTurns full rotations happen over the whole 0-1 climb.
        public static Vector3 ComputeSpiralPoint(float s, float baseHeight, float totalHeight, float radius, float spiralTurns)
        {
            float height = baseHeight + s * totalHeight;
            float angleRadians = s * spiralTurns * 360f * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(angleRadians) * radius, height, Mathf.Sin(angleRadians) * radius);
        }

        // 0-1 position within one grow/hold/fade cycle, wrapping every loopDurationSeconds -
        // this is what makes the bolt "循環" (loop) instead of climbing once and stopping.
        public static float ComputeLoopProgress(float elapsedSeconds, float loopDurationSeconds)
        {
            if (loopDurationSeconds <= 0f)
            {
                return 0f;
            }

            float wrapped = elapsedSeconds % loopDurationSeconds;
            return wrapped / loopDurationSeconds;
        }

        // How far up the spiral (0-1) is currently drawn: ramps from 0 to 1 over the first
        // growFraction of the loop, then holds fully grown for the remainder - a bolt that's
        // always fully drawn wouldn't read as "climbing from the base."
        public static float ComputeGrowthAmount(float loopProgress01, float growFraction)
        {
            if (growFraction <= 0f)
            {
                return 1f;
            }

            return Mathf.Clamp01(loopProgress01 / growFraction);
        }

        // Fades to 0 over the last (1-fadeStart01) of the loop - so the loop wrapping back to
        // the start reads as a smooth fade-out/fade-in, not an abrupt pop.
        public static float ComputeBrightnessMultiplier(float loopProgress01, float fadeStart01)
        {
            if (loopProgress01 < fadeStart01)
            {
                return 1f;
            }

            float fadeSpan = 1f - fadeStart01;
            float fadeT = fadeSpan > 0f ? (loopProgress01 - fadeStart01) / fadeSpan : 1f;
            return 1f - Mathf.Clamp01(fadeT);
        }

        // Random small XZ jitter per spiral point (segmentCount+1 points, endpoints included)
        // for the jagged "crackle" texture on top of the otherwise-smooth spiral curve.
        public static Vector2[] ComputeJitterOffsets(int segmentCount, float jitterAmount, System.Random random)
        {
            var offsets = new Vector2[segmentCount + 1];
            for (int i = 0; i <= segmentCount; i++)
            {
                float x = ((float)random.NextDouble() * 2f - 1f) * jitterAmount;
                float z = ((float)random.NextDouble() * 2f - 1f) * jitterAmount;
                offsets[i] = new Vector2(x, z);
            }

            return offsets;
        }

        // Builds the currently-visible portion of the spiral (from the base up to
        // growthAmount01), jittered point-by-point - the array returned is only as long as
        // what's actually grown so far, so the LineRenderer's own positionCount naturally
        // shrinks/grows with it.
        public static Vector3[] BuildSpiralPoints(float growthAmount01, float baseHeight, float totalHeight, float radius, float spiralTurns, Vector2[] jitterOffsets)
        {
            int segmentCount = jitterOffsets.Length - 1;
            int visibleCount = segmentCount > 0
                ? Mathf.Clamp(Mathf.CeilToInt(growthAmount01 * segmentCount) + 1, 1, jitterOffsets.Length)
                : 1;

            var points = new Vector3[visibleCount];
            for (int i = 0; i < visibleCount; i++)
            {
                float s = segmentCount > 0 ? Mathf.Min((float)i / segmentCount, growthAmount01) : 0f;
                Vector3 clean = ComputeSpiralPoint(s, baseHeight, totalHeight, radius, spiralTurns);
                points[i] = clean + new Vector3(jitterOffsets[i].x, 0f, jitterOffsets[i].y);
            }

            return points;
        }
    }
}
