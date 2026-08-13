using System.Collections;
using UnityEngine;
using Live2DAction.Core;

namespace Live2DAction.Characters
{
    // Handles what happens when a character's Health reaches zero: respawns in place with full
    // health after a short beat, instead of leaving it permanently deactivated.
    // Health.ApplyDamage's generic on-death behavior (gameObject.SetActive(false)) has no
    // accompanying "what happens next" - fine for a character that's meant to stay "defeated"
    // once killed, but for one the user wants to keep fighting/wandering it just reads as
    // "the character vanished" (2026-08-12 real bug report: Player4's attacks landing reliably
    // enough - thanks to that same day's earlier stepOffset/point-blank-hit fixes - actually
    // killed the player, and with every one of Player's own scripts living on that now-inactive
    // GameObject, literally nothing responded to input anymore; not an engine hang, confirmed
    // by checking the Console - only the usual handful of pre-existing warnings, not
    // thousands). Originally built (and named PlayerRespawnController) for Player only;
    // 2026-08-13 explicit user request extended it to Player2 too (still passive - it just now
    // un-dies instead of staying dead) - nothing about the class itself is Player-specific, so
    // it was renamed rather than duplicated (the file/its .meta were renamed together so the
    // existing GameManager component in the scene keeps its GUID and doesn't turn into a
    // "Missing Script"). A second instance lives on "GameManager" wired to Player2.
    //
    // Deliberately NOT a component on the character it revives: Health.ApplyDamage calls
    // gameObject.SetActive(false) synchronously right after firing Died, and Unity kills any
    // Coroutine running on a GameObject the instant it's deactivated - a respawn coroutine
    // started from a Died handler living on the character itself would never get to resume.
    // This has to live on something that stays active the whole time (wired to "GameManager"
    // in the scene, one instance per revivable character).
    //
    // Polls Health.IsDead every frame instead of subscribing to Health.Died - deliberately, not
    // by oversight: a first version subscribed in OnEnable(), which runs synchronously the
    // moment AddComponent() executes, before either editor-tool wiring
    // (SerializedObject.ApplyModifiedProperties) or test wiring (reflection field assignment)
    // has a chance to set targetHealth - so OnEnable() always subscribed with targetHealth
    // still null, and the event never actually fired. Confirmed by this class's own tests
    // failing with the character never respawning. Polling matches this codebase's established
    // convention for exactly this reason (see CharacterMovement.InputCommand/
    // PlayerCombat.InputCommand's own "resolved on every use, not cached in Awake" comments).
    public class RespawnController : MonoBehaviour
    {
        [SerializeField] private GameObject target;
        [SerializeField] private Health targetHealth;

        // 2026-08-12: raised from 0.5s to 5s by explicit user request for Player (still no UI/
        // game-over screen - the simplest death handling, just a longer pause before it kicks
        // in). Player2 reuses the same default rather than inventing a separate value with no
        // particular reasoning behind it.
        [SerializeField] private float respawnDelaySeconds = 5f;

        private bool _wasDead;
        private bool _respawnScheduled;

        private void Update()
        {
            if (targetHealth == null)
            {
                return;
            }

            bool isDead = targetHealth.IsDead;
            if (isDead && !_wasDead && !_respawnScheduled)
            {
                _respawnScheduled = true;
                StartCoroutine(RespawnAfterDelay());
            }

            _wasDead = isDead;
        }

        private IEnumerator RespawnAfterDelay()
        {
            yield return new WaitForSeconds(respawnDelaySeconds);

            targetHealth.ResetHealth();
            target.SetActive(true);
            _respawnScheduled = false;
            _wasDead = false;
        }
    }
}
