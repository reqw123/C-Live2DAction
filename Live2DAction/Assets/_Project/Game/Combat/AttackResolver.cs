using System.Collections.Generic;
using UnityEngine;
using Live2DAction.Core;

namespace Live2DAction.Combat
{
    // Pure hit-resolution logic, kept separate from MonoBehaviour/input polling
    // so it can be exercised directly in EditMode tests without a physics step.
    public static class AttackResolver
    {
        // Returns the world-space point of every landed hit (empty list if none) - callers
        // that only care about the count can just read .Count. 2026-08-12: changed from a
        // plain int so PlayerCombat can spawn a hit-effect at each actual impact point (see
        // its own comment) without a second, redundant Physics query.
        public static List<Vector3> ResolveHits(Vector3 origin, AttackData attackData, Transform attackerRoot, Collider[] candidates)
        {
            var hitPoints = new List<Vector3>();
            foreach (Collider candidate in candidates)
            {
                if (candidate == null)
                {
                    continue;
                }

                if (candidate.transform.root == attackerRoot)
                {
                    continue;
                }

                if (candidate.TryGetComponent(out IDamageable damageable))
                {
                    Vector3 point = candidate.ClosestPoint(origin);
                    damageable.ApplyDamage(new DamageInfo(attackData.Damage, point, Vector3.zero, attackerRoot.gameObject));
                    hitPoints.Add(point);
                }
            }

            return hitPoints;
        }
    }
}
