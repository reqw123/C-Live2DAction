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
    }
}
