using System.Collections.Generic;
using UnityEngine;

namespace Live2DAction.AI.Boss.Yuanpei
{
    // spec §9.4 - MultiAoE circle placement + the "必須保留至少一條可透過走路或一次閃避抵達的安全路線"
    // guarantee. Pure so it's directly EditMode-testable without a boss / coroutine.
    //
    // Given a set of candidate circle centres (already ground-projected by the caller) and the
    // arena, this: drops circles whose overlap would leave no gap, then verifies a ring of sample
    // points around the player at "one dodge" radius still has an uncovered spot. If not, it
    // removes the circle covering the largest slice of that ring until a safe point exists (or a
    // hard floor of circles is reached - a MultiAoE with 2 circles is never a trap).
    public static class YuanpeiAoePlacement
    {
        public struct Circle
        {
            public Vector2 center;   // XZ
            public float radius;
        }

        // dodgeReach ~= spec §8.3 閃避距離 3-4m; safeMargin keeps the safe spot a bit clear of the edge.
        public static List<Circle> EnsureSafeRoute(
            IReadOnlyList<Circle> candidates,
            Vector2 playerXZ,
            Vector2 arenaCenter,
            float arenaRadius,
            float dodgeReach = 3.5f,
            float safeMargin = 0.4f,
            int minCircles = 2,
            int ringSamples = 24)
        {
            var kept = new List<Circle>(candidates);

            // sample a ring of "escape" points at dodgeReach around the player, clamped to the arena
            for (int guard = 0; guard < kept.Count; guard++)
            {
                int safeIdx = FirstSafeSampleIndex(kept, playerXZ, arenaCenter, arenaRadius,
                    dodgeReach, safeMargin, ringSamples);
                if (safeIdx >= 0) break;                 // a reachable safe spot exists - done
                if (kept.Count <= minCircles) break;     // never fully trap the player

                // remove the circle that covers the most of that escape ring
                int worst = MostCoveringCircle(kept, playerXZ, dodgeReach, safeMargin, ringSamples);
                if (worst < 0) break;
                kept.RemoveAt(worst);
            }
            return kept;
        }

        // -1 if every sampled escape point is inside some circle (or outside the arena)
        public static int FirstSafeSampleIndex(
            IReadOnlyList<Circle> circles, Vector2 playerXZ, Vector2 arenaCenter, float arenaRadius,
            float dodgeReach, float safeMargin, int ringSamples)
        {
            for (int i = 0; i < ringSamples; i++)
            {
                float a = (i / (float)ringSamples) * Mathf.PI * 2f;
                Vector2 p = playerXZ + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * dodgeReach;
                if ((p - arenaCenter).magnitude > arenaRadius - safeMargin) continue;   // out of bounds
                if (!PointInAnyCircle(circles, p, safeMargin)) return i;
            }
            // also allow "stand still" if the player's current spot happens to be clear
            if ((playerXZ - arenaCenter).magnitude <= arenaRadius && !PointInAnyCircle(circles, playerXZ, safeMargin))
                return ringSamples;
            return -1;
        }

        private static int MostCoveringCircle(
            IReadOnlyList<Circle> circles, Vector2 playerXZ, float dodgeReach, float safeMargin, int ringSamples)
        {
            int best = -1, bestCount = -1;
            for (int c = 0; c < circles.Count; c++)
            {
                int count = 0;
                for (int i = 0; i < ringSamples; i++)
                {
                    float a = (i / (float)ringSamples) * Mathf.PI * 2f;
                    Vector2 p = playerXZ + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * dodgeReach;
                    if ((p - circles[c].center).magnitude <= circles[c].radius + safeMargin) count++;
                }
                if (count > bestCount) { bestCount = count; best = c; }
            }
            return best;
        }

        private static bool PointInAnyCircle(IReadOnlyList<Circle> circles, Vector2 p, float margin)
        {
            for (int i = 0; i < circles.Count; i++)
                if ((p - circles[i].center).magnitude <= circles[i].radius + margin) return true;
            return false;
        }
    }
}
