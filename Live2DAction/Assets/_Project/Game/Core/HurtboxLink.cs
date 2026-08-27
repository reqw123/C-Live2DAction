using UnityEngine;

namespace Live2DAction.Core
{
    // 2026-08-26, real playtested bug ("真的有考慮到雙方的受擊區域和身高差距嗎") - a generic,
    // non-Boss-specific twin of Live2DAction.Combat.Boss.BossHurtboxLink (same one-line forward to
    // a shared Health), for characters that need a SEPARATE, larger hit-detection collider from
    // their movement CharacterController. Root cause this exists to fix: the Player's own
    // CharacterController (height=1, so only world Y 0.58-1.58 given its usual stance) is a tight
    // movement-precision capsule, not a fair "did an attack's weapon swing through my body"
    // hurtbox - it doesn't even reach the top of the player's own visual head (~2.08). Against a
    // giant boss (武士, 4x scale) swinging a blade/kick through a wide vertical arc, most of a
    // real swing's actual strike height falls either above or below that narrow 1m band, so
    // attacks were readable and correctly timed but geometrically could never connect - not a
    // damage-pipeline bug (that one's already fixed, see BossHitbox.Awake()'s own comment), a
    // pure hit-region-versus-target-height mismatch.
    //
    // Deliberately its own file in Core (not reusing BossHurtboxLink directly) - functionally
    // identical, but a Player carrying a component named "Boss*" would misread as a modeling
    // mistake to the next person who opens this hierarchy.
    public class HurtboxLink : MonoBehaviour, IDamageable
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
