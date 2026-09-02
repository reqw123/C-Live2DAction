using System.Collections.Generic;
using UnityEngine;
using Live2DAction.Core;
using Live2DAction.Combat.Boss;

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
        //
        // damageMultiplier (2026-08-13, explicit user request - ultimate skill: "attack1傷害
        // 乘10倍") defaults to 1 (no effect) so every existing caller/test is unaffected.
        // Deliberately scales the damage HERE rather than having callers mutate
        // AttackData.Damage itself - AttackData is a shared ScriptableObject asset (TrainingDummy/
        // Enemy reference the exact same LightAttack1 asset object, see
        // TrainingDummySetup's own comment on why that sharing is intentional), so
        // writing a temporary buffed value into the asset would leak into every other user of
        // it and - worse - persist into the asset file after Play mode ends, since Unity
        // doesn't auto-revert ScriptableObject field edits made in Play mode the way it does
        // for scene objects.
        public static List<Vector3> ResolveHits(Vector3 origin, AttackData attackData, Transform attackerRoot, Collider[] candidates, float damageMultiplier = 1f)
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

                    // 2026-08-29, cat combat design (Docs/CAT_COMBAT_DESIGN.md 4.3) - away from
                    // the attacker, flattened to horizontal; falls back to the attacker's own
                    // facing when the two overlap exactly (point == origin). Previously always
                    // Vector3.zero - StancePoise.OnDamaged ignores Direction so existing
                    // callers are unaffected, only the new knockback path reads it.
                    Vector3 away = candidate.transform.root.position - origin;
                    away.y = 0f;
                    if (away.sqrMagnitude < 0.0001f)
                    {
                        away = attackerRoot.forward;
                        away.y = 0f;
                    }
                    Vector3 knockDir = away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.zero;

                    damageable.ApplyDamage(new DamageInfo(attackData.Damage * damageMultiplier, point, knockDir, attackerRoot.gameObject));
                    hitPoints.Add(point);

                    // Optional shove - only if this attack authored a force AND the target has a
                    // receiver. IKnockbackReceiver lives in .Boss but is a generic capability
                    // (see its own comment); the cat's KnockbackReceiver / any future one just
                    // needs to implement it.
                    if (attackData.KnockbackForce > 0f && knockDir != Vector3.zero
                        && candidate.transform.root.TryGetComponent(out IKnockbackReceiver receiver))
                    {
                        receiver.ApplyKnockback(knockDir, attackData.KnockbackForce, attackData.KnockbackLaunches);
                    }
                }
            }

            return hitPoints;
        }
    }
}
