using UnityEngine;
using Live2DAction.Core;

namespace Live2DAction.Combat
{
    // Pure hit-resolution logic, kept separate from MonoBehaviour/input polling
    // so it can be exercised directly in EditMode tests without a physics step.
    public static class AttackResolver
    {
        public static int ResolveHits(Vector3 origin, AttackData attackData, Transform attackerRoot, Collider[] candidates)
        {
            int hitCount = 0;
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
                    hitCount++;
                }
            }

            return hitCount;
        }
    }
}
