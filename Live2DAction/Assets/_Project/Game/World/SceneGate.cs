using UnityEngine;
using UnityEngine.InputSystem;
using Live2DAction.Core;
using Live2DAction.Input;
using Live2DAction.Vehicles;

namespace Live2DAction.World
{
    // 2026-09-03, user request ("改成在學校面前設計大門,只有在跟大門互動後,進入加載畫面,跑完後會看到
    // 新地圖的場景,玩家直接在該地圖上") - an explicit gate: walk up to the portal, press the interact
    // key, a loading curtain covers, the target region scene streams in additively, the player is
    // teleported onto it, curtain lifts. A matching gate inside the region does the reverse.
    //
    // One component drives both directions via sceneToLoad / sceneToUnload (either may be empty):
    //   ENTER gate (in the persistent scene):  sceneToLoad="Map_School",  sceneToUnload="",
    //                                          arrival = a spot inside the campus.
    //   EXIT gate  (inside Map_School):         sceneToLoad="",            sceneToUnload="Map_School",
    //                                          arrival = a spot back on the road.
    //
    // The load/teleport/unload sequence runs on SceneTransitionRunner (a persistent object), NOT
    // here - an exit gate that unloaded its own scene mid-coroutine used to kill the sequence.
    //
    // 2026-09-06, user request - a dynamic dialogue-frame prompt UI while the player is near
    // ("按下 F 進入 Boss 地圖" / "按下 F 離開元培大學"). The shared UI is a singleton
    // (PortalInteractionUIController.Instance) so a gate in ANY scene can drive it; each gate
    // supplies its own message + interact key. This component owns: which player is nearby, the
    // key press, and the SceneTransitionRunner call.
    //
    // 2026-09-06 follow-ups:
    //  - "所有傳送門互動改為 f 按鍵": interactKey default F (Portal.cs matches). F is also the
    //    vehicle enter/drive + execution key - see the seated handling below.
    //  - "駕駛車輛使用傳送門時 傳送後會掉入虛空": while seated, VehicleEntrySystem re-parents the
    //    "Player" under the car, so a naive resolve returned the CAR and Begin() teleported a
    //    950kg Rigidbody onto a still-cooking map -> void. Fix: always resolve the "Player"
    //    transform (never the vehicle), and ForceDismountAll() before Begin so an on-foot
    //    character is handed to the runner.
    //  - "當車輛與ui互動系統同時存在時，優先考慮互動系統": pressing F while driving up to a portal
    //    fires the PORTAL (dismount + teleport on foot), not a plain vehicle dismount.
    //    VehicleEntrySystem checks SceneGate.PlayerHasPortalInteraction and yields F to us.
    //  - "靠近時才顯示" + "互動框重複顯示了兩次": the prompt lifecycle no longer trusts
    //    OnTriggerExit (a CharacterController-disabled teleport never fires it, so the UI got
    //    stuck shown far from the gate) - it is driven by an actual per-frame distance check with
    //    hysteresis, so it shows only when genuinely close and never flickers Show/Hide/Show.
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

        [Header("Interaction")]
        [Tooltip("Key that fires the transition while the player is close. 2026-09-06: all portals on F.")]
        [SerializeField] private Key interactKey = Key.F;
        [Tooltip("How that key reads in the prompt ('按下 {KEY} …').")]
        [SerializeField] private string interactKeyLabel = "F";
        [Tooltip("DEPTH (metres toward/away from the portal face, this gate's local Z) at which the " +
                 "prompt UI appears. Keep <= interactRange so the prompt never shows before F works " +
                 "(2026-09-06 user: 'UI out but not yet interactable').")]
        [SerializeField] private float uiShowRange = 1.5f;
        [Tooltip("DEPTH within which the interact key fires the transition - the Blocker wall stops " +
                 "an on-foot player ~0.8 m out. A car body can't get this close, so driving up + F " +
                 "is a plain dismount.")]
        [SerializeField] private float interactRange = 1.5f;
        [Tooltip("LATERAL half-width (metres across the portal, this gate's local X) that still " +
                 "counts as 'in front of the portal'. 2026-09-06 user: the portal is wide, so " +
                 "standing off-centre in front of it must still let you interact - this covers the " +
                 "whole portal quad / road width. Depth (above) stays tight.")]
        [SerializeField] private float lateralHalfWidth = 6f;
        [Tooltip("Seconds the player must be out of range before the prompt actually hides - " +
                 "absorbs any momentary distance blip so the frame can't re-play its rise animation.")]
        [SerializeField] private float hideGraceSeconds = 0.6f;

        [Header("Interaction prompt UI (2026-09-06)")]
        [Tooltip("Show the shared dialogue-frame prompt (PortalInteractionUIController.Instance) " +
                 "while the player is within uiShowRange. Both enter and exit gates want this.")]
        [SerializeField] private bool showInteractionUI = true;
        [Tooltip("This gate's prompt line. {KEY} is replaced with interactKeyLabel. Enter and exit differ.")]
        [SerializeField] private string promptMessage = "按下 {KEY} 進入 Boss 地圖";
        [Tooltip("Use the full-screen Boss-map video loading screen (BossLoadingScreen) instead of " +
                 "the plain 'load 中…' curtain. Only SchoolGate_Enter (2026-09-06).")]
        [SerializeField] private bool useLoadingScreen = false;

        // small hysteresis band on top of uiShowRange (the hideGraceSeconds debounce is the main
        // anti-flicker; this just keeps the on/off edges from sitting exactly on each other)
        private const float RangeHysteresis = 0.7f;

        private Transform _player;          // the "Player" transform (may be a child of a vehicle while seated)
        private Health _playerHealth;
        private bool _near;                 // within uiShowRange (sticky out to uiShowRange + hysteresis)
        private bool _confirmed;            // this interaction already fired - one press, one trip
        private bool _uiShown;              // we currently hold the shared UI open
        private float _nextScan;
        private float _hideAt;              // Time.time to actually hide, 0 = no pending hide

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        // Fast path - learn about the player the instant they walk into the trigger. The lifecycle
        // (and the exit) is still owned by the per-frame distance check in Update; a teleport that
        // never fires OnTriggerEnter/Exit is picked up by the scan there.
        private void OnTriggerEnter(Collider other)
        {
            var player = ResolvePlayerRoot(other);
            if (player != null) AcquirePlayer(player);
        }

        private void OnDisable() => HideUI();
        private void OnDestroy() => HideUI();

        private void AcquirePlayer(Transform player)
        {
            if (_player == player) return;
            _player = player;
            _playerHealth = player.GetComponentInChildren<Health>();
            _confirmed = false;   // a fresh approach re-arms the gate
        }

        private void DropPlayer()
        {
            _player = null;
            _playerHealth = null;
            _near = false;
            HideUI();
        }

        // The human player's "Player" transform whether on foot or driving - mirrors
        // YuanpeiEncounter.ResolvePlayerFrom (walk up from the PlayerInputProvider to the transform
        // literally named "Player"; while seated that's a child of the vehicle).
        private static Transform ResolvePlayerRoot(Collider other)
        {
            if (other == null) return null;
            return WalkToPlayer(other.transform.root);
        }

        private static Transform WalkToPlayer(Transform root)
        {
            if (root == null) return null;
            foreach (var pip in root.GetComponentsInChildren<PlayerInputProvider>(true))
                for (var t = pip.transform; t != null; t = t.parent)
                    if (t.name == "Player") return t;
            return null;
        }

        // Robust fallback - finds the nearest "Player" transform scene-wide. Used when a teleport
        // dropped the player next to us without a trigger event, and after a force-dismount.
        private Transform ScanForPlayer()
        {
            Transform best = null;
            float bd = float.MaxValue;
            Vector3 here = transform.position; here.y = 0f;
            foreach (var pip in FindObjectsByType<PlayerInputProvider>(FindObjectsSortMode.None))
            {
                Transform p = null;
                for (var t = pip.transform; t != null; t = t.parent) if (t.name == "Player") { p = t; break; }
                if (p == null) continue;
                Vector3 q = p.position; q.y = 0f;
                float d = (q - here).sqrMagnitude;
                if (d < bd) { bd = d; best = p; }
            }
            return best;
        }

        // The player's position in this gate's local space, Y ignored - .z is DEPTH (toward/away
        // from the portal face), .x is LATERAL (across the portal width). 2026-09-06 user: the
        // trigger must be wide (whole portal / road) but shallow, not a small circle at the centre.
        private Vector3 LocalPlayerOffset()
        {
            if (_player == null) return Vector3.one * 9999f;
            Vector3 l = transform.InverseTransformPoint(_player.position);
            l.y = 0f;
            return l;
        }

        // depth <= depthRange AND lateral within lateralHalfWidth
        private bool InFront(Vector3 local, float depthRange) =>
            Mathf.Abs(local.z) <= depthRange && Mathf.Abs(local.x) <= lateralHalfWidth;

        private void HideUI()
        {
            var ui = PortalInteractionUIController.Instance;
            if (ui != null && _uiShown) ui.Hide(this);
            _uiShown = false;
            _hideAt = 0f;
        }

        private static bool TransitionRunning =>
            SceneTransitionRunner.Instance != null && SceneTransitionRunner.Instance.IsRunning;

        private static bool CurtainCovered =>
            ScreenFader.Instance != null && ScreenFader.Instance.IsCovered;

        // No re-trigger while a transition / load curtain is up, or while the player is dead.
        // NOT gated on "seated in a vehicle" - 2026-09-06 user request ("當車輛與ui互動系統同時
        // 存在時，優先考慮互動系統"): if the player drives up to a portal, F fires the portal (this
        // component dismounts them first, then teleports on foot), it does NOT dismount-only.
        // VehicleEntrySystem stands down for F when SceneGate.PlayerHasPortalInteraction is true.
        private bool Blocked =>
            _confirmed || TransitionRunning || CurtainCovered
            || (_playerHealth != null && _playerHealth.IsDead);

        // Would pressing the interact key right now fire this gate (whether the player is on foot
        // or driving)? Used by VehicleEntrySystem to yield F to the portal instead of dismounting.
        public bool CanInteractNow(Transform player)
        {
            if (player == null || _confirmed || TransitionRunning || CurtainCovered) return false;
            var h = player.GetComponentInChildren<Health>();
            if (h != null && h.IsDead) return false;
            Vector3 l = transform.InverseTransformPoint(player.position); l.y = 0f;
            return InFront(l, interactRange);
        }

        public static bool PlayerHasPortalInteraction(Transform player)
        {
            if (player == null) return false;
            foreach (var g in FindObjectsByType<SceneGate>(FindObjectsSortMode.None))
                if (g.isActiveAndEnabled && g.CanInteractNow(player)) return true;
            return false;
        }

        private void Update()
        {
            // --- resolve / re-resolve the player, robust to teleports ---
            if (_player == null && Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + 1f;
                var found = ScanForPlayer();
                if (found != null)
                {
                    Vector3 l = transform.InverseTransformPoint(found.position); l.y = 0f;
                    if (Mathf.Abs(l.z) <= uiShowRange + RangeHysteresis + 3f
                        && Mathf.Abs(l.x) <= lateralHalfWidth + 3f) AcquirePlayer(found);
                }
            }

            Vector3 local = LocalPlayerOffset();

            // player wandered off (or a teleport moved them without an OnTriggerExit) - let go
            if (_player == null
                || Mathf.Abs(local.z) > uiShowRange + RangeHysteresis + 4f
                || Mathf.Abs(local.x) > lateralHalfWidth + 3f)
            {
                if (_player != null || _uiShown) DropPlayer();
                return;
            }

            // --- proximity (wide lateral band, tight depth) with hysteresis on the depth edge ---
            bool laterallyInFront = Mathf.Abs(local.x) <= lateralHalfWidth;
            if (!_near && laterallyInFront && Mathf.Abs(local.z) <= uiShowRange) _near = true;
            else if (_near && (!laterallyInFront || Mathf.Abs(local.z) >= uiShowRange + RangeHysteresis)) _near = false;

            // --- prompt UI (with a hide grace so a momentary distance blip can't restart the
            //     frame's rise animation) ---
            var ui = PortalInteractionUIController.Instance;
            if (ui != null)
            {
                bool wantUI = showInteractionUI && _near && !Blocked;
                if (wantUI)
                {
                    _hideAt = 0f;
                    if (!_uiShown) { ui.Show(this, interactKeyLabel, promptMessage); _uiShown = true; }
                }
                else if (_uiShown)
                {
                    if (_hideAt <= 0f) _hideAt = Time.time + hideGraceSeconds;
                    else if (Time.time >= _hideAt) { ui.Hide(this); _uiShown = false; _hideAt = 0f; }
                }
            }

            // --- key press: fire the existing transition exactly once ---
            // Fires whether on foot or driving - VehicleEntrySystem yields F to us (via
            // PlayerHasPortalInteraction) when we're in range, and the teleport block below
            // dismounts the player before handing an on-foot character to the runner. Gated on the
            // raw in-front test (not _near, which lags a frame / carries UI hysteresis) so the
            // press can never be swallowed by VehicleEntrySystem without us picking it up.
            if (_player == null || !InFront(local, interactRange) || Blocked) return;
            if (Keyboard.current == null || interactKey == Key.None
                || !Keyboard.current[interactKey].wasPressedThisFrame) return;

            if (SceneTransitionRunner.Instance == null)
            {
                Debug.LogError("[SceneGate] no SceneTransitionRunner in the scene - add one to the persistent scene.", this);
                return;
            }

            _confirmed = true;
            if (ui != null) ui.Confirm(this);
            _uiShown = false;
            _hideAt = 0f;

            // Only an on-foot character is ever handed to the runner - a Rigidbody vehicle
            // teleported onto a freshly-loaded map tunnels into the void. Mirrors YuanpeiEncounter.
            var occupant = _player;
            foreach (var ves in FindObjectsByType<VehicleEntrySystem>(FindObjectsSortMode.None))
                if (ves.PlayerSeat != VehicleEntrySystem.Seat.None)
                {
                    ves.ForceDismountAll();
                    occupant = ScanForPlayer() ?? occupant;
                }

            SceneTransitionRunner.Instance.Begin(sceneToLoad, sceneToUnload, occupant,
                arrivalPosition, arrivalYaw, loadingLabel, curtainFadeSeconds, settleFrames, useLoadingScreen);

            // Force a fresh re-acquire after the trip (the teleport won't fire trigger events).
            _player = null;
            _playerHealth = null;
            _near = false;
        }
    }
}
