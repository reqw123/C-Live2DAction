using UnityEngine;

namespace Live2DAction.AI
{
    // Pure, MonoBehaviour-free helpers for the ten-legged bug's procedural gait, kept separate
    // from TenLeggedBugController so the "one leg at a time, strict 1 -> N -> repeat" rule is
    // directly EditMode-testable without a Play loop (same pure-helper-first pattern this codebase
    // already uses for CatProceduralWalk.ComputeGaitTarget, AttackResolver, WanderUtility, etc.).
    //
    // The spec (section 2) is: legs are numbered 1..N (front-left = 1, front-right = 2, then
    // alternating front-to-back), and the gait steps through them strictly in that order - exactly
    // one leg is in its "lift -> swing forward -> plant" motion at any instant, every other leg is
    // planted. Faster movement = faster cycle (shorter time per leg), stride LENGTH stays roughly
    // constant. All of that is expressed here as functions of a single 0..1 "cycle phase".
    public static class TenLeggedBugGaitUtility
    {
        // Advances the gait cycle phase. moveSpeed is the bug's actual horizontal speed (units/s);
        // speedForFullRate is the speed at which the cycle runs at baseRateHz cycles/second. Phase
        // advance is proportional to actual speed so a slow crawl and a fast charge both take the
        // same number of *steps* to cover a given distance - only the timing changes.
        // Returns the new phase, wrapped into [0,1).
        public static float AdvancePhase(float currentPhase01, float moveSpeed, float speedForFullRate,
            float baseRateHz, float deltaTime)
        {
            float speedNorm = speedForFullRate > 0.0001f ? Mathf.Clamp01(moveSpeed / speedForFullRate) : 0f;
            float next = currentPhase01 + speedNorm * baseRateHz * deltaTime;
            return Mathf.Repeat(next, 1f);
        }

        // Which leg (0-based index into the leg list) is currently taking its step, given the
        // cycle phase and how many legs there are. The cycle is divided into legCount equal
        // slices; slice k belongs to leg k. Leg "1" in the spec is index 0 here.
        public static int SteppingLegIndex(float cyclePhase01, int legCount)
        {
            if (legCount <= 0)
            {
                return -1;
            }
            int i = Mathf.FloorToInt(Mathf.Repeat(cyclePhase01, 1f) * legCount);
            return Mathf.Clamp(i, 0, legCount - 1);
        }

        // 0..1 progress of the given leg WITHIN its own step slice (0 = slice just started,
        // 1 = slice about to end). 0 for every leg that isn't the one currently stepping.
        public static float LegStepProgress01(float cyclePhase01, int legCount, int legIndex)
        {
            if (legCount <= 0 || legIndex != SteppingLegIndex(cyclePhase01, legCount))
            {
                return 0f;
            }
            float p = Mathf.Repeat(cyclePhase01, 1f) * legCount;
            return Mathf.Clamp01(p - Mathf.Floor(p));
        }

        // Vertical lift factor for a leg, 0..1. A smooth bell (sin) over the stepping leg's slice
        // so it lifts off the ground, peaks mid-step, and is fully planted again by slice end.
        // Every non-stepping leg returns 0 (stays planted). legLiftHeight in the controller
        // multiplies this into a world offset / bend angle.
        public static float LegLift01(float cyclePhase01, int legCount, int legIndex)
        {
            float t = LegStepProgress01(cyclePhase01, legCount, legIndex);
            return t <= 0f ? 0f : Mathf.Sin(t * Mathf.PI);
        }

        // Fore-aft stride offset for a leg, -1..1. -1 = fully back (end of a stance push),
        // +1 = fully forward (just planted after a step). The stepping leg sweeps -1 -> +1 across
        // its slice (the recovery swing); every planted leg drifts slowly the other way (+1 -> -1)
        // across the rest of the cycle as the body moves over it (stance). Kept continuous so
        // there's no visible pop when a leg hands off from swing to stance.
        public static float LegStride(float cyclePhase01, int legCount, int legIndex)
        {
            if (legCount <= 0)
            {
                return 0f;
            }
            int stepping = SteppingLegIndex(cyclePhase01, legCount);
            if (legIndex == stepping)
            {
                // Recovery swing: back -> forward across this slice.
                float t = LegStepProgress01(cyclePhase01, legCount, legIndex);
                return Mathf.Lerp(-1f, 1f, t);
            }

            // Stance: this leg last stepped some number of slices ago; it has been drifting
            // backward ever since. Slices since its own step, as a 0..1 fraction of a full cycle.
            float slice = 1f / legCount;
            float legSliceStart = legIndex * slice;
            float sinceStep = Mathf.Repeat(cyclePhase01 - legSliceStart, 1f);
            // sinceStep in [0, slice) is the leg's own step (handled above); [slice, 1) is stance,
            // remap that to +1 (just planted) -> -1 (about to step again).
            float stanceT = Mathf.InverseLerp(slice, 1f, sinceStep);
            return Mathf.Lerp(1f, -1f, stanceT);
        }
    }
}
