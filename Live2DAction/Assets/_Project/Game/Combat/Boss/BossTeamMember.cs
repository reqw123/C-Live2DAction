using UnityEngine;

namespace Live2DAction.Combat.Boss
{
    // Cheap team tag so BossHitbox can skip friendly-fire against the boss's own hurtboxes/other
    // same-team hitboxes without relying on transform.root comparisons alone (needed once a boss
    // has multiple child hurtbox colliders spread across bones rather than one root collider).
    public class BossTeamMember : MonoBehaviour
    {
        [SerializeField] private string team = "Boss";
        public string Team => team;
    }
}
