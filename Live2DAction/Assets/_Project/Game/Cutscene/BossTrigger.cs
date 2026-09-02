using UnityEngine;

namespace Live2DAction.Cutscene
{
    // 2026-09-01, /grill-with-docs exploration — see Docs/BOSS_INTRO_EXPLORATION.md. 追加92 wired
    // it into GreyboxTest.
    //
    // A trigger volume on the path to the boss. The first time the player's body enters, it fires
    // the intro and deactivates itself so the cutscene can never re-trigger.
    //
    // Matching the player: if playerRoot is assigned (GreyboxTest - the Player object there is
    // Untagged), any collider on/under that transform counts. Otherwise it falls back to a tag
    // (the demo scene's capsule is tagged "Player").
    [RequireComponent(typeof(Collider))]
    public class BossTrigger : MonoBehaviour
    {
        [Tooltip("The intro controller to kick off.")]
        [SerializeField] private BossIntroManager introManager;

        [Tooltip("The player's root transform. When set, any collider on/under it triggers the " +
                 "intro (used in GreyboxTest, where the Player object is Untagged).")]
        [SerializeField] private Transform playerRoot;

        [Tooltip("Fallback when playerRoot is empty: the tag the entering body must carry.")]
        [SerializeField] private string playerTag = "Player";

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsPlayer(other))
            {
                return;
            }

            if (introManager != null)
            {
                introManager.StartIntro();
            }

            // One-shot: never let the intro play twice.
            gameObject.SetActive(false);
        }

        private bool IsPlayer(Collider other)
        {
            if (playerRoot != null)
            {
                return other.transform == playerRoot || other.transform.IsChildOf(playerRoot);
            }
            return other.CompareTag(playerTag);
        }

        // Setup-tool seam.
        public void EditorConfigure(BossIntroManager manager, Transform root)
        {
            introManager = manager;
            playerRoot = root;
        }
    }
}
