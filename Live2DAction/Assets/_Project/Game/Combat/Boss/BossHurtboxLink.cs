using UnityEngine;
using Live2DAction.Core;

namespace Live2DAction.Combat.Boss
{
    // Sits on each named hurtbox collider (Head/Chest/Pelvis/LeftArm/RightArm/LeftLeg/RightLeg -
    // see PiHaiWangBossSetup's own hurtbox construction) and forwards IDamageable.ApplyDamage to
    // the boss's single shared Health component. This project's existing damage pipeline
    // (AttackResolver.ResolveHits) already works purely by TryGetComponent<IDamageable> on
    // whatever collider a player attack's Physics query actually hit - so making every hurtbox
    // implement IDamageable itself (routing to the same underlying Health) is enough to make
    // "hit any limb" work with ZERO changes to PlayerCombat/AttackResolver, matching the spec's
    // own "沿用既有系統" requirement instead of building a parallel damage pipeline.
    //
    // Deliberately does NOT differentiate damage by body part (no headshot multiplier etc.) -
    // the design doc only asked for the hurtboxes to exist/be hittable, not for per-part damage
    // scaling; adding that now would be guessing at a rule nobody specified.
    public class BossHurtboxLink : MonoBehaviour, IDamageable
    {
        [SerializeField] private Health health;

        public void Configure(Health targetHealth)
        {
            health = targetHealth;
        }

        public void ApplyDamage(DamageInfo damageInfo)
        {
            if (health != null)
            {
                health.ApplyDamage(damageInfo);
            }
        }
    }
}
