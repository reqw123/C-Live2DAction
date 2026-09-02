using UnityEngine;

namespace Live2DAction.Combat
{
    // Pure geometry for a multi-point weapon sweep (spec WUSHI_COMBAT_ENGINEERING_SPEC.md §4 - M2
    // 項目 3/4). A rotating blade barely moves near the hilt while the tip carves a long arc, so a
    // single centre-to-centre cast (what BossHitbox / PlayerCombat's OverlapCapsule do today) misses
    // the tip's real path. The fix is to sample the blade as a line (root / mid / tip), sweep each
    // sample point from its previous pose to this one, and subdivide that sweep when a single physics
    // step moved it further than one blade-width - so a fast swing can't tunnel a target.
    //
    // MonoBehaviour-free on purpose: the subdivision maths and midpoint fallback are exactly the bits
    // worth unit-testing without a physics step (mirrors AttackResolver / PlayerGuardUtility). The
    // actual SphereCast lives in PlayerWeaponHitbox.
    public static class WeaponSweepUtility
    {
        // How many equal sub-segments prev->curr should be split into so no sub-cast skips more than
        // maxSampleTravel of ground. Always at least 1 (a stationary or barely-moved sample still
        // gets one cast). A non-positive maxSampleTravel disables subdivision (1 segment).
        public static int SubdivisionCount(float travelDistance, float maxSampleTravel)
        {
            if (maxSampleTravel <= 0f || travelDistance <= maxSampleTravel)
            {
                return 1;
            }
            return Mathf.Max(1, Mathf.CeilToInt(travelDistance / maxSampleTravel));
        }

        // Start point of sub-segment `index` (0-based) in a `count`-way even split of previous->current.
        // index 0 => previous, index >= count => current.
        public static Vector3 SubSegmentStart(Vector3 previous, Vector3 current, int index, int count)
        {
            if (count <= 1 || index <= 0)
            {
                return previous;
            }
            if (index >= count)
            {
                return current;
            }
            return Vector3.Lerp(previous, current, index / (float)count);
        }

        // Cast length for one sub-segment of a `count`-way even split of a `travelDistance` sweep.
        public static float SubSegmentLength(float travelDistance, int count)
        {
            float clamped = Mathf.Max(0f, travelDistance);
            return count <= 1 ? clamped : clamped / count;
        }

        // spec §4.2: "若沒有獨立 bladeMid Transform，可用 Vector3.Lerp(bladeRoot, bladeTip, 0.5f)".
        public static Vector3 ResolveMidpoint(bool hasExplicitMid, Vector3 explicitMid, Vector3 root, Vector3 tip)
        {
            return hasExplicitMid ? explicitMid : Vector3.Lerp(root, tip, 0.5f);
        }
    }
}
