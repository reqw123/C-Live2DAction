using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Live2DAction.CameraSystem
{
    // 2026-08-31, user request ("我想要新增一個按鍵得到他的攝影機視角(純粹觀看不影響行為)") - a
    // VIEW-ONLY camera toggle. Unlike CameraPossessionSwitcher (player <-> cat: swaps view AND
    // hands over WASD control), this only swaps which camera you see through. The subject
    // (the ten-legged bug) keeps running its own AI untouched - nothing here references its
    // controller.
    //
    // Toggle behaviour: pressing the key the FIRST time snapshots every camera GameObject that is
    // currently active, disables them all, and enables the spectator camera. Pressing it again
    // re-enables exactly that snapshot and disables the spectator. So it composes correctly with
    // whatever camera happened to be live (player / cat / vehicle / 守望者) - you always land back
    // on the one you left.
    //
    // Lives on its own always-active GameObject (not on a camera) so it survives any camera being
    // SetActive-toggled, same as CameraPossessionSwitcher.
    [DefaultExecutionOrder(150)]
    public class SpectatorCameraToggle : MonoBehaviour
    {
        [Tooltip("The spectator camera GameObject (a Camera rig aimed at the subject). Starts disabled.")]
        [SerializeField] private GameObject spectatorCamera;

        [Tooltip("Key that toggles into / out of the spectator view. Default B (was unused).")]
        [SerializeField] private Key toggleKey = Key.B;

        public bool IsSpectating { get; private set; }

        private readonly List<GameObject> _restoreOnExit = new List<GameObject>();
        private readonly HashSet<GameObject> _restoreSet = new HashSet<GameObject>();

        private void Start()
        {
            if (spectatorCamera != null && spectatorCamera.activeSelf)
            {
                spectatorCamera.SetActive(false);
            }
        }

        private void OnDisable()
        {
            // Torn down while spectating - don't leave the player with no camera.
            if (IsSpectating)
            {
                ExitSpectator();
            }
        }

        private void Update()
        {
            if (toggleKey == Key.None || Keyboard.current == null)
            {
                return;
            }
            if (Keyboard.current[toggleKey].wasPressedThisFrame)
            {
                Toggle();
            }
        }

        // Re-assert the spectator view every LateUpdate while active. Needed because other camera
        // owners re-enable their own camera every frame from their own LateUpdate - notably
        // VehicleEntrySystem (SetActiveSafe(vehicleCamera, youDrive) while seated), which is
        // exactly the "can't switch to the bug view while driving" bug. This component's
        // DefaultExecutionOrder (150) puts this LateUpdate AFTER those (order 0), so whatever they
        // turned back on this frame gets turned off again before the frame renders - no flicker.
        // Any newly-active camera we clobber is added to the restore list so exiting brings the
        // right one back.
        private void LateUpdate()
        {
            if (!IsSpectating || spectatorCamera == null)
            {
                return;
            }

            foreach (Camera cam in Camera.allCameras)
            {
                GameObject go = cam.gameObject;
                if (go == spectatorCamera || !go.activeSelf)
                {
                    continue;
                }
                if (_restoreSet.Add(go))
                {
                    _restoreOnExit.Add(go);
                }
                go.SetActive(false);
            }

            if (!spectatorCamera.activeSelf)
            {
                spectatorCamera.SetActive(true);
            }
        }

        // Public so a test / scripted event can drive it too.
        public void Toggle()
        {
            if (IsSpectating)
            {
                ExitSpectator();
            }
            else
            {
                EnterSpectator();
            }
        }

        private void EnterSpectator()
        {
            if (spectatorCamera == null)
            {
                Debug.LogWarning("[SpectatorCameraToggle] no spectator camera assigned.");
                return;
            }

            _restoreOnExit.Clear();
            _restoreSet.Clear();
            foreach (Camera cam in Camera.allCameras)
            {
                GameObject go = cam.gameObject;
                if (go == spectatorCamera || !go.activeSelf)
                {
                    continue;
                }
                _restoreOnExit.Add(go);
                _restoreSet.Add(go);
                go.SetActive(false);
            }

            spectatorCamera.SetActive(true);
            IsSpectating = true;
            Debug.Log("[SpectatorCameraToggle] " + toggleKey + " -> spectator view ON (bug camera)");
        }

        private void ExitSpectator()
        {
            if (spectatorCamera != null)
            {
                spectatorCamera.SetActive(false);
            }
            foreach (GameObject go in _restoreOnExit)
            {
                if (go != null)
                {
                    go.SetActive(true);
                }
            }
            _restoreOnExit.Clear();
            _restoreSet.Clear();
            IsSpectating = false;
            Debug.Log("[SpectatorCameraToggle] " + toggleKey + " -> spectator view OFF");
        }
    }
}
