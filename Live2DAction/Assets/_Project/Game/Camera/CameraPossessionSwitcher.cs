using UnityEngine;
using UnityEngine.InputSystem;
using Live2DAction.Core;
using Live2DAction.Vehicles;

namespace Live2DAction.CameraSystem
{
    // 2026-08-28, explicit user request ("這是一隻貓，並請向玩家一樣提供他攝影機視角並且可將視角切換
    // 到他身上，注意他視線較低，與先前攝影機風格不同") - a possession swap between the player and the
    // Cat: press C (or call FocusCat()/FocusPlayer()) and you both SEE through and CONTROL the other
    // one. Instant hard cut, not an eased establishing pan (that's ViewFocusDirector's job for the
    // 守望者 spectator view) - this is "you are now the cat".
    //
    // Two mechanisms, both already precedented in this project:
    //   - camera swap: SetActive-toggle between the two Camera GameObjects, exactly like
    //     VehicleEntrySystem does for the on-foot vs vehicle camera. Each camera carries its own
    //     ThirdPersonCameraController (the Cat's is tuned for its low eyeline - see CatCharacterSetup),
    //     and that controller's own OnEnable/OnDisable already hands the locked cursor back and forth.
    //   - control hand-off: exactly one side's control components (CharacterMovement, ...) are enabled
    //     at a time so WASD only ever drives the character you're looking at. Unlike
    //     ViewFocusDirector.suspendWhileWatching this is a hard "this set on, that set off" - the
    //     player's and the cat's movement components exist only to be governed by possession, nothing
    //     else toggles them.
    //
    // Lives on its own always-active GameObject (NOT on a camera) so it survives either camera being
    // SetActive-toggled. [DefaultExecutionOrder] above the camera controllers (0) is not strictly
    // needed (this only acts on key-press frames) but keeps it deterministic if it ever grows.
    [DefaultExecutionOrder(150)]
    public class CameraPossessionSwitcher : MonoBehaviour
    {
        public enum Possessed { Player, Cat }

        [SerializeField] private GameObject playerCamera;
        [SerializeField] private GameObject catCamera;

        [Tooltip("Components enabled ONLY while the player is possessed - the player's CharacterMovement " +
                 "(and anything else that eats WASD), so the player stands still while you're the cat.")]
        [SerializeField] private Behaviour[] playerControl;

        [Tooltip("Components enabled ONLY while the cat is possessed - the Cat's CharacterMovement etc.")]
        [SerializeField] private Behaviour[] catControl;

        [Tooltip("Key that toggles player <-> cat. None disables the key (FocusCat()/FocusPlayer() " +
                 "still work). C was unused before this (T = 守望者 view, V = first person).")]
        [SerializeField] private Key toggleKey = Key.C;

        // 2026-08-29, user request ("讓 player 守望者/cat 三者可以互相切換視角"). Optional / null-safe.
        // While the Watcher (T) view is active it has taken over whichever camera is live - a C
        // possession swap in that moment would SetActive-swap the camera out from under the
        // director. Ignore C while watching; press T first to come back, then C.
        [SerializeField] private ViewFocusDirector viewDirector;

        // 2026-08-29, user request ("讓貓咪也可以使用車輛 F功能" -> "PLAYER和CAT在駕駛車輛時沒辦法
        // 互相切換視角嗎", GTA-style) - C keeps working while one character is in the car. When you
        // swap TO a character that VehicleEntrySystem has parked in the seat, Apply() below leaves
        // its own camera / control off (VehicleEntrySystem owns the vehicle camera + keeps the
        // parked passenger inert); swap AWAY from the driver and the car just parks itself.
        // Optional / null-safe.
        [SerializeField] private VehicleEntrySystem vehicleEntry;

        [SerializeField] private Possessed startPossessed = Possessed.Player;

        // 2026-08-29, user request ("貓咪死後5秒復活") - if the cat dies while you're possessing it,
        // its GameObject is SetActive(false)'d (Health.ApplyDamage) and you'd be stuck looking
        // through a dead CatCamera at nothing for the 5s until RespawnController brings it back.
        // Auto-drop back to the player the moment the cat dies; press C again after it respawns.
        // Optional / null-safe.
        [SerializeField] private Health catHealth;

        public Possessed Current { get; private set; } = Possessed.Player;

        private bool _applied;

        private void Start()
        {
            Apply(startPossessed, force: true);
        }

        private void OnDisable()
        {
            // Torn down mid-swap - don't leave the player's control permanently disabled.
            if (_applied && Current == Possessed.Cat)
            {
                Apply(Possessed.Player, force: true);
            }
        }

        private void Update()
        {
            // Cat died while possessed -> hand control/view back to the player (see catHealth).
            if (Current == Possessed.Cat && catHealth != null && catHealth.IsDead)
            {
                FocusPlayer();
                return;
            }

            if (toggleKey != Key.None && Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
            {
                if (viewDirector != null && viewDirector.IsFocusedOnWatcher)
                {
                    // 2026-08-29, user report ("可以從 c 轉回 t 但反過來不行") - C used to be a no-op in
                    // the 守望者 view, so pressing C to "get back into my character" did nothing.
                    // Now it leaves the Watcher view the same as T does, back to whoever you were
                    // possessing. A second C then swaps player <-> cat as usual. (Not a
                    // leave-and-swap in one press: ViewFocusDirector's return path restores the
                    // pre-Watcher control snapshot in LateUpdate, which would stomp a same-frame
                    // possession swap done here in Update.)
                    Debug.Log("[CameraPossession] " + toggleKey + " -> leaving 守望者 view (back to " + Current + ")");
                    viewDirector.FocusPlayer();
                }
                else
                {
                    // 2026-08-29, user report ("C按鍵並沒有對應在貓身上") - a visible Console line every
                    // time the key registers, so it's obvious whether the press is being seen at all
                    // (vs a focus / key-conflict problem) and which side you're now controlling.
                    Debug.Log("[CameraPossession] " + toggleKey + " pressed -> switching to " + Other(Current));
                    Toggle();
                }
            }
        }

        // ---- public API (key binding + cutscenes / scripted events / tests) ----

        public void Toggle() => Apply(Other(Current));
        public void FocusCat() => Apply(Possessed.Cat);
        public void FocusPlayer() => Apply(Possessed.Player);

        // Pure, so the flip is directly EditMode-testable (same convention as
        // ViewFocusDirector.BlendPose / ThirdPersonCameraController.ComputeCameraPosition).
        public static Possessed Other(Possessed p) => p == Possessed.Player ? Possessed.Cat : Possessed.Player;

        // ---- internals ----

        private void Apply(Possessed who, bool force = false)
        {
            if (_applied && !force && who == Current)
            {
                return;
            }
            Current = who;
            _applied = true;
            bool cat = who == Possessed.Cat;

            // Vehicle awareness (2026-08-29): the DRIVER's view is the vehicle camera
            // (VehicleEntrySystem owns it), so don't turn that character's own third-person camera
            // on. A PASSENGER keeps their own camera (you see them riding on the flatbed). Anyone
            // seated - driver or passenger - has their control consumers held off by
            // VehicleEntrySystem; don't re-enable them here.
            bool whoIsDriver = vehicleEntry != null && vehicleEntry.DriverOccupant ==
                (cat ? VehicleEntrySystem.Occupant.Cat : VehicleEntrySystem.Occupant.Player);
            bool playerSeated = vehicleEntry != null && vehicleEntry.PlayerSeat != VehicleEntrySystem.Seat.None;
            bool catSeated = vehicleEntry != null && vehicleEntry.CatSeat != VehicleEntrySystem.Seat.None;

            SetActiveSafe(playerCamera, !cat && !whoIsDriver);
            SetActiveSafe(catCamera, cat && !whoIsDriver);

            SetEnabled(playerControl, !cat && !playerSeated);
            SetEnabled(catControl, cat && !catSeated);
        }

        private static void SetActiveSafe(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
            {
                go.SetActive(active);
            }
        }

        private static void SetEnabled(Behaviour[] set, bool enabled)
        {
            if (set == null)
            {
                return;
            }
            foreach (Behaviour b in set)
            {
                if (b != null && b.enabled != enabled)
                {
                    b.enabled = enabled;
                }
            }
        }
    }
}
