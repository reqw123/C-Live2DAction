using UnityEngine;

namespace Live2DAction.Combat.Boss
{
    // This project's existing AttackResolver/PlayerCombat pipeline has no knockback concept at
    // all (checked first) - this is a genuinely new, optional capability, not a duplicate of an
    // existing system. Implemented as an interface (rather than a concrete dependency from
    // BossHitbox on, say, CharacterController directly) so it can be added to whatever actually
    // needs to receive it (currently just KnockbackReceiver on Player) without BossHitbox caring
    // how the receiver moves.
    public interface IKnockbackReceiver
    {
        void ApplyKnockback(Vector3 horizontalDirection, float force, bool launchesUpward);
    }
}
