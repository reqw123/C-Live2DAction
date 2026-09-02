using UnityEngine;

namespace Live2DAction.Combat.Boss
{
    // 2026-09-02, spec WUSHI_COMBAT_ENGINEERING_SPEC.md §10.4 (M5 項目 9) acceptance condition:
    // "調整 state speed 後，自動重新計算並顯示實際首次接觸與有效窗毫秒數".
    //
    // Item 9's whole point (spec §10.1) is that tuning must be data-driven, not "watch the Animator
    // and guess". §10.2's groundwork was the session metrics overlay (SekiroDeflectDebug, 續 32);
    // this is §10.4's: pure timing math that turns an attack's (clip length, Animator state speed,
    // normalized hit windows) into real wall-clock first-contact seconds and effective window
    // milliseconds - the numbers the §10.3 tuning order actually adjusts. Pulled out of the editor
    // report tool so it is unit-testable without a live Animator.
    public static class BossAttackTimingUtility
    {
        // spec §10 locked baseline until item 9's own A/B pass (§10.3 step 7).
        public const float PlayerParryWindowSeconds = 0.20f;

        // Real wall-clock length of a clip once the Animator state's speed multiplier is applied.
        // A non-positive speed is treated as 1 (Unity clamps a 0-speed state to "paused", which is
        // never what an attack asset wants).
        public static float RealClipSeconds(float clipLength, float stateSpeed)
        {
            if (clipLength <= 0f) return 0f;
            float s = stateSpeed <= 0.0001f ? 1f : stateSpeed;
            return clipLength / s;
        }

        // Seconds from the clip's start to a normalized-time point, at the effective speed.
        public static float NormalizedToSeconds(float normalized, float realClipSeconds)
        {
            return Mathf.Clamp01(normalized) * Mathf.Max(0f, realClipSeconds);
        }

        // Duration of a [start, end] normalized window in milliseconds, at the effective speed.
        // A reversed or empty window is 0, never negative.
        public static float WindowMilliseconds(float startNormalized, float endNormalized, float realClipSeconds)
        {
            float span = Mathf.Max(0f, Mathf.Clamp01(endNormalized) - Mathf.Clamp01(startNormalized));
            return span * Mathf.Max(0f, realClipSeconds) * 1000f;
        }

        // How a hit window compares to the player's parry window. 1 = same size; >1 = wider than the
        // parry window (comfortable to time); <1 = tighter, so the telegraph has to carry it (spec
        // §10.3 step 2: "調整 Hit Window 位置，不先擴大 Window 長度補償漏判"). 0 when there is no window.
        public static float ParryDifficultyRatio(float windowMilliseconds)
        {
            if (windowMilliseconds <= 0f) return 0f;
            return windowMilliseconds / (PlayerParryWindowSeconds * 1000f);
        }
    }
}
