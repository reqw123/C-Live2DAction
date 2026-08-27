using UnityEngine;

namespace Live2DAction.UI
{
    // Pure math for PlayerHealthBarFx's tween/delay/shake/spark visuals, kept separate from the
    // MonoBehaviour that owns Time/Image so it's directly EditMode-testable (mirrors
    // HealthBarUtility/EnergyBarPulseUtility/LightningAuraUtility's existing pure-logic pattern).
    public static class HealthBarTweenUtility
    {
        // Frame-rate independent exponential approach - unlike Mathf.Lerp(a,b,fixedT) this
        // converges to the same visual speed regardless of the caller's deltaTime.
        public static float SmoothApproach(float current, float target, float speed, float deltaTime)
        {
            if (speed <= 0f)
            {
                return target;
            }

            float t = 1f - Mathf.Exp(-speed * deltaTime);
            return Mathf.Lerp(current, target, t);
        }

        // The "delayed/ghost" bar: snaps up immediately on heal (targetFill >= delayedFill, so a
        // heal is never dampened by a stale ghost sitting below it), holds at its old value for
        // delaySeconds after the last drop, then chases the new (lower) target down at
        // catchUpSpeed fill-units/second - never overshoots past targetFill.
        public static float ComputeDelayedFill(float delayedFill, float targetFill, float timeSinceDamage, float delaySeconds, float catchUpSpeed, float deltaTime)
        {
            if (targetFill >= delayedFill)
            {
                return targetFill;
            }

            if (timeSinceDamage < delaySeconds)
            {
                return delayedFill;
            }

            return Mathf.Max(targetFill, delayedFill - catchUpSpeed * deltaTime);
        }

        // 0 at/above threshold, ramping to 1 as fillAmount approaches empty - drives the
        // shader's low-HP glow pulse without ever touching the underlying HP value itself.
        public static float ComputeLowHealthIntensity(float fillAmount, float threshold)
        {
            if (threshold <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01((threshold - fillAmount) / threshold);
        }

        // Two decorrelated Perlin walks (not Random.value, which jitters uselessly between
        // completely unrelated values frame to frame) so the shake reads as a continuous
        // wobble that eases out as intensity decays, not white noise.
        public static Vector2 ComputeShakeOffset(float intensity, float time, float magnitude)
        {
            if (intensity <= 0f)
            {
                return Vector2.zero;
            }

            float x = (Mathf.PerlinNoise(time * 37.1f, 0.5f) - 0.5f) * 2f;
            float y = (Mathf.PerlinNoise(0.5f, time * 41.7f) - 0.5f) * 2f;
            return new Vector2(x, y) * magnitude * intensity;
        }

        // Local X (within a track of trackWidth, inset by insetLeft/insetRight to match the
        // fill's own padding) for the edge-glow node at the given fill amount - 0 fill sits at
        // the left inset, 1 fill sits at the right inset, matching where Image.Type.Filled's
        // own partial-fill edge actually renders.
        public static float ComputeEdgeGlowLocalX(float fillAmount, float trackWidth, float insetLeft, float insetRight)
        {
            float usableWidth = Mathf.Max(0f, trackWidth - insetLeft - insetRight);
            return insetLeft + usableWidth * Mathf.Clamp01(fillAmount);
        }

        // Simple ballistic arc (constant horizontal speed, downward gravity) for one spark's
        // offset from the burst origin at normalized lifetime t01 - cheap enough to run per
        // spark per frame without a physics/particle system.
        public static Vector2 ComputeSparkOffset(float t01, float angleRadians, float speed, float gravity)
        {
            float x = Mathf.Cos(angleRadians) * speed * t01;
            float y = Mathf.Sin(angleRadians) * speed * t01 - 0.5f * gravity * t01 * t01;
            return new Vector2(x, y);
        }
    }
}
