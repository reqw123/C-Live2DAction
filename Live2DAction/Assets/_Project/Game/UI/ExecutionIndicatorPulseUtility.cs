using UnityEngine;

namespace Live2DAction.UI
{
    // Pure alpha-animation math for ExecutionReadyIndicator, split out the same way
    // EnergyBarPulseUtility already is from WorldSpaceEnergyBar - directly EditMode-testable
    // without a MonoBehaviour/Time dependency.
    public static class ExecutionIndicatorPulseUtility
    {
        // Slow, smooth "breathing" glow for the outer ring - present the whole time the target
        // is executable, gentle enough not to fight the inner dot's own sharper blink for
        // attention (see ComputeDotBlinkAlpha's own comment for why that one needs a different
        // shape).
        public static float ComputeRingAlpha(float time, float pulseSpeed, float minAlpha, float maxAlpha)
        {
            float wave01 = (Mathf.Sin(time * pulseSpeed) + 1f) * 0.5f;
            return Mathf.Lerp(minAlpha, maxAlpha, wave01);
        }

        // Sharper on/off "warning light" blink for the inner dot (2026-08-18, explicit user
        // request: "圓圈內有個紅色小點在閃爍"). A plain sine reads as gentle breathing - fine for
        // the ring, wrong for something the user specifically called a blink - so this reshapes
        // the same wave with a power curve to spend most of its time down near minAlpha and snap
        // briefly up to maxAlpha, closer to how a real blinking indicator light looks rather than
        // smoothly fading in and out.
        public static float ComputeDotBlinkAlpha(float time, float blinkSpeed, float minAlpha, float maxAlpha)
        {
            float wave01 = (Mathf.Sin(time * blinkSpeed) + 1f) * 0.5f;
            float shaped = Mathf.Pow(wave01, 4f);
            return Mathf.Lerp(minAlpha, maxAlpha, shaped);
        }

        // 2026-08-19, explicit user request ("心臟 人的核心部位，你要做出那種要被處決的恐懼感，
        // 紅色 鮮豔 脈動") - a plain sine "breathes" evenly the whole cycle, which reads as calm.
        // A real heartbeat is TWO close-together beats ("lub-dub") separated by a much longer
        // quiet gap - most of the cycle is rest, with a sharp double-spike of intensity. Modeled
        // as two narrow Gaussian bumps inside one period (beatsPerMinute controls the cycle
        // length): the first ("lub") slightly stronger than the second ("dub"), matching a real
        // cardiac cycle's own asymmetry. Shared by both the ring's glow/scale AND the dot's
        // blink/scale (see ExecutionReadyIndicator's own Update/LateUpdate) so everything pulses
        // in lockstep as ONE heartbeat rather than several independently-timed effects competing
        // for attention - the whole point is "this target's heart is racing", not generic
        // decoration.
        public static float ComputeHeartbeatAlpha(float time, float beatsPerMinute, float minAlpha, float maxAlpha)
        {
            float wave01 = HeartbeatWave01(time, beatsPerMinute);
            return Mathf.Lerp(minAlpha, maxAlpha, wave01);
        }

        // Same heartbeat timing as ComputeHeartbeatAlpha, remapped to a scale multiplier instead
        // of alpha - drives a subtle "throb" (ring/dot swell slightly bigger on each beat) so the
        // dread reads in silhouette/size, not just brightness. minScale/maxScale are the
        // multipliers applied to the sprite's own base size at rest vs. the peak of a beat.
        public static float ComputeHeartbeatScale(float time, float beatsPerMinute, float minScale, float maxScale)
        {
            float wave01 = HeartbeatWave01(time, beatsPerMinute);
            return Mathf.Lerp(minScale, maxScale, wave01);
        }

        private static float HeartbeatWave01(float time, float beatsPerMinute)
        {
            float period = 60f / Mathf.Max(1f, beatsPerMinute);
            float phase = Mathf.Repeat(time, period) / period; // 0-1 through one cardiac cycle

            // "Lub" - the strong primary beat, early in the cycle.
            float lub = GaussianBump(phase, 0.08f, 0.045f) * 1f;
            // "Dub" - the softer secondary beat, close behind it (real hearts: S1 then S2).
            float dub = GaussianBump(phase, 0.22f, 0.05f) * 0.65f;

            return Mathf.Clamp01(Mathf.Max(lub, dub));
        }

        private static float GaussianBump(float phase01, float center01, float width01)
        {
            float d = phase01 - center01;
            return Mathf.Exp(-(d * d) / (2f * width01 * width01));
        }
    }
}
