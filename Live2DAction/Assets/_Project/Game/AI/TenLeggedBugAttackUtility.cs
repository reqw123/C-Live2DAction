using UnityEngine;

namespace Live2DAction.AI
{
    // Pure helpers for the ten-legged bug's rhino-horn stab attack, its "only strike from the
    // front" gate, and the lost-target search sweep. Separated from TenLeggedBugController for
    // the same EditMode-testability reason as TenLeggedBugGaitUtility - the horn's 3-phase timing
    // curve and the 30-degree facing cone are exactly the kind of numeric logic that's painful to
    // verify through a Play loop and trivial to verify as a function.
    public static class TenLeggedBugAttackUtility
    {
        // Spec (section 3): the bug may only start/continue a stab when the target is inside the
        // attack range AND within ~30 degrees of dead ahead. Outside the cone it must stop
        // attacking and turn to face the target first. This answers only the ANGLE half.
        // bugForward and toTarget are flattened to the XZ plane by the caller.
        public static bool TargetWithinAttackCone(Vector3 bugForwardFlat, Vector3 toTargetFlat, float maxAngleDegrees)
        {
            if (bugForwardFlat.sqrMagnitude < 0.0001f || toTargetFlat.sqrMagnitude < 0.0001f)
            {
                return false;
            }
            float angle = Vector3.Angle(bugForwardFlat, toTargetFlat);
            return angle <= maxAngleDegrees;
        }

        // The rhino-horn pitch curve, in degrees to add on top of the horn bone's rest pose,
        // as a function of normalized attack-cycle time (0..1 over attackCycleSeconds). Positive
        // = horn/head raised (wind-up and telegraph), negative = horn driven down (the stab).
        //
        // Spec timing (defaults): 0 .. raiseEndT  -> ease UP to +raiseDegrees (slow tell),
        //                         raiseEndT .. stabEndT -> whip DOWN to -stabDegrees (fast strike),
        //                         stabEndT .. 1   -> ease back to 0 (recover to ready pose).
        public static float HornPitchDegrees(float attackTime01, float raiseEndT, float stabEndT,
            float raiseDegrees, float stabDegrees)
        {
            float t = Mathf.Clamp01(attackTime01);

            if (t <= raiseEndT)
            {
                float k = raiseEndT > 0.0001f ? t / raiseEndT : 1f;
                // SmoothStep = slow, deliberate lift - this is the "clear telegraph" the spec asks for.
                return Mathf.Lerp(0f, raiseDegrees, Mathf.SmoothStep(0f, 1f, k));
            }

            if (t <= stabEndT)
            {
                float k = (t - raiseEndT) / Mathf.Max(0.0001f, stabEndT - raiseEndT);
                // Ease-in (k*k) so the down-swing accelerates - a fast, committed jab, not a glide.
                return Mathf.Lerp(raiseDegrees, -stabDegrees, k * k);
            }

            float r = (t - stabEndT) / Mathf.Max(0.0001f, 1f - stabEndT);
            return Mathf.Lerp(-stabDegrees, 0f, Mathf.SmoothStep(0f, 1f, r));
        }

        // Whether the horn hitbox should be live this instant - only across the contact frames of
        // the down-stab (a sub-window of raiseEndT..stabEndT), never merely because the target is
        // standing in range. Spec: "只有角部 Hitbox 在下刺命中幀碰到玩家時才造成傷害".
        public static bool HornStrikeIsLive(float attackTime01, float strikeStartT, float strikeEndT)
        {
            float t = Mathf.Clamp01(attackTime01);
            return t >= strikeStartT && t <= strikeEndT;
        }

        // The "anticipation" blend, 0..1, used to spread the front legs and press the head down
        // in sync with the horn. Rises during the wind-up, peaks over the strike, releases during
        // recovery - so it reads as one coordinated lunge rather than an isolated head bob.
        public static float AttackTelegraph01(float attackTime01, float raiseEndT, float stabEndT)
        {
            float t = Mathf.Clamp01(attackTime01);
            if (t <= stabEndT)
            {
                return Mathf.Clamp01(t / Mathf.Max(0.0001f, stabEndT));
            }
            float r = (t - stabEndT) / Mathf.Max(0.0001f, 1f - stabEndT);
            return 1f - Mathf.Clamp01(r);
        }

        // Horn/body yaw offset (degrees) for the lost-target search: a slow left-right sweep.
        // searchTime01 is normalized over the whole search duration; sweeps roughly 2.5 full
        // left-right passes so it visibly "looks around" before giving up.
        public static float SearchSweepDegrees(float searchTime01, float sweepAmplitudeDegrees)
        {
            return Mathf.Sin(Mathf.Clamp01(searchTime01) * Mathf.PI * 2f * 2.5f) * sweepAmplitudeDegrees;
        }
    }
}
