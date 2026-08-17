using UnityEngine;
using Live2DAction.AI;
using Live2DAction.Characters;

namespace Live2DAction.World
{
    // 2026-08-18, explicit user request ("上升氣流，任何人碰到這個上升氣流會快速飛向空中") - a
    // trigger volume that launches ANYTHING that enters it upward, continuously for as long as
    // it stays inside (OnTriggerStay, not just OnTriggerEnter - a single one-shot impulse would
    // get silently eaten by the character's own gravity accumulation before it ever left the
    // volume, see ApplyUpwardLaunch's own comment). Deliberately not player-only, unlike Portal/
    // HealingSpring's own PlayerInputProvider check - "任何人" means the player, 076, Player4,
    // Player2's mecha, all of them.
    //
    // Prefers each character's own vertical-velocity hook (CharacterMovement/EnemyAI's
    // ApplyUpwardLaunch, which correctly integrates with their own gravity so the character
    // keeps rising smoothly and arcs back down naturally after leaving the volume) and falls
    // back to a plain Transform push for anything with neither (e.g. Player2's mecha, which
    // uses WanderMovement - simple horizontal-only wandering with no vertical physics of its
    // own at all, so there's no velocity state to integrate with; it just gets moved straight up
    // for as long as it's inside).
    [RequireComponent(typeof(Collider))]
    public class Updraft : MonoBehaviour
    {
        [SerializeField] private float launchSpeed = 15f;

        private void Reset()
        {
            Collider trigger = GetComponent<Collider>();
            if (trigger != null)
            {
                trigger.isTrigger = true;
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (other == null || other.transform.root == transform.root)
            {
                return;
            }

            Transform root = other.transform.root;

            CharacterMovement movement = other.GetComponentInParent<CharacterMovement>();
            if (movement != null)
            {
                movement.ApplyUpwardLaunch(launchSpeed);
                return;
            }

            EnemyAI enemyAI = other.GetComponentInParent<EnemyAI>();
            if (enemyAI != null)
            {
                enemyAI.ApplyUpwardLaunch(launchSpeed);
                return;
            }

            root.position += Vector3.up * launchSpeed * Time.deltaTime;
        }
    }
}
