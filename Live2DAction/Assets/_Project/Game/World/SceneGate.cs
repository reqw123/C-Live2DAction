using UnityEngine;
using UnityEngine.InputSystem;
using Live2DAction.Input;

namespace Live2DAction.World
{
    // 2026-09-03, user request ("改成在學校面前設計大門,只有在跟大門互動後,進入加載畫面,跑完後會看到
    // 新地圖的場景,玩家直接在該地圖上") - replaces the proximity MapStreamer for the school with an
    // explicit gate: walk into the portal, press E, a loading curtain covers, the target region
    // scene streams in additively, the player is teleported onto it, curtain lifts. A matching
    // gate inside the region does the reverse (teleport back + unload).
    //
    // One component drives both directions via sceneToLoad / sceneToUnload (either may be empty):
    //   ENTER gate (in the persistent scene):  sceneToLoad="Map_School",  sceneToUnload="",
    //                                          arrival = a spot inside the campus.
    //   EXIT gate  (inside Map_School):         sceneToLoad="",            sceneToUnload="Map_School",
    //                                          arrival = a spot back on the road.
    //
    // The actual load/teleport/unload sequence runs on SceneTransitionRunner (a persistent object),
    // NOT here - an exit gate that unloaded its own scene mid-coroutine used to kill the sequence.
    // This component is just the trigger + the key press. PlayerInputProvider zeroes all input
    // while ScreenFader.IsCovered. No on-screen prompt (the floating text rectangle was removed
    // 續 85 at user request - the portal itself is the affordance).
    [RequireComponent(typeof(Collider))]
    public class SceneGate : MonoBehaviour
    {
        [Header("What this gate does")]
        [Tooltip("Scene to additively load on interact (empty = load nothing). Must be in Build Settings.")]
        [SerializeField] private string sceneToLoad = "Map_School";
        [Tooltip("Scene to unload on interact, after the teleport (empty = unload nothing).")]
        [SerializeField] private string sceneToUnload = "";
        [Tooltip("World position the interacting character is placed at once the load finishes.")]
        [SerializeField] private Vector3 arrivalPosition = new Vector3(0f, 1.1f, -92f);
        [Tooltip("World Y rotation (degrees) the character faces on arrival.")]
        [SerializeField] private float arrivalYaw = 180f;

        [Header("Transition")]
        [SerializeField] private string loadingLabel = "載入中…";
        [SerializeField] private float curtainFadeSeconds = 0.4f;
        [Tooltip("Frames to hold the curtain after the scene loads, so collider cook + the first " +
                 "rendered frame + the camera catching up to the teleported player all settle.")]
        [SerializeField] private int settleFrames = 3;

        private bool _playerInside;
        private Transform _occupant;

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            var pip = other.GetComponentInParent<PlayerInputProvider>();
            if (pip == null) return;
            _playerInside = true;
            _occupant = pip.transform.root;
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponentInParent<PlayerInputProvider>() == null) return;
            _playerInside = false;
            _occupant = null;
        }

        private static bool TransitionRunning =>
            SceneTransitionRunner.Instance != null && SceneTransitionRunner.Instance.IsRunning;

        private void Update()
        {
            if (!_playerInside || _occupant == null || TransitionRunning) return;
            if (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame) return;

            if (SceneTransitionRunner.Instance == null)
            {
                Debug.LogError("[SceneGate] no SceneTransitionRunner in the scene - add one to the persistent scene.", this);
                return;
            }

            SceneTransitionRunner.Instance.Begin(sceneToLoad, sceneToUnload, _occupant,
                arrivalPosition, arrivalYaw, loadingLabel, curtainFadeSeconds, settleFrames);

            // A teleport (disabled CC) doesn't fire OnTriggerExit - clear here so a stale
            // _playerInside can't re-fire. A real walk back into the trigger re-arms it.
            _playerInside = false;
            _occupant = null;
        }
    }
}
