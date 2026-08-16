using UnityEngine;

namespace Live2DAction.UI
{
    // Pure sine-wave brightness math for WorldSpaceEnergyBar's full-state pulse, kept separate
    // from the MonoBehaviour that owns Time/Image so it's directly EditMode-testable (mirrors
    // HealthBarUtility/AttackResolver's existing pure-logic pattern).
    public static class EnergyBarPulseUtility
    {
        // Flat 1x (no pulse) while not full - only pulses once the bar is actually ready to
        // use, so the effect itself reads as "you can act now" rather than a constant idle
        // animation.
        public static float ComputePulseBrightness(bool isFull, float time, float pulseSpeed, float minBrightness, float maxBrightness)
        {
            if (!isFull)
            {
                return 1f;
            }

            float wave01 = (Mathf.Sin(time * pulseSpeed) + 1f) * 0.5f;
            return Mathf.Lerp(minBrightness, maxBrightness, wave01);
        }
    }
}
