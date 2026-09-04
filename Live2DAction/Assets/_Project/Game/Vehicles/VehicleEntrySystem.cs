using UnityEngine;
using UnityEngine.InputSystem;
using Live2DAction.Characters;
using Live2DAction.CameraSystem;
using Live2DAction.Combat;

namespace Live2DAction.Vehicles
{
    // 2026-08-26, explicit user request ("靠近CAR可用F鍵進入車體 視角變為主駕駛方向 W/A/S/D移動控制
    // 正式由CAR接管"). F reads directly from Keyboard.current, same direct-input convention as
    // VehicleController/PlayerInputProvider.
    //
    // Handing WASD to the car is done by disabling a character's control CONSUMERS
    // (CharacterMovement / PlayerCombat / the cat's melee stack), never PlayerInputProvider (whose
    // public fields would freeze at their last value and be read stale forever).
    //
    // 2026-08-29 evolution:
    //   - 追加55: F enters with whichever character CameraPossessionSwitcher currently possesses.
    //   - 追加56: C still works while one character is in the car (GTA-style) - the driver stays
    //     parked in the seat, engine off, and you take over the other one; swap back to drive.
    //   - 追加57: 2-seater. F enters the DRIVER seat if it's free, else the rear-flatbed PASSENGER
    //     seat; F from any seat dismounts. Both characters can ride (one drives, one rides).
    //     "想換人開得先兩隻都下車再 F".
    //
    // State: PlayerSeat / CatSeat (None / Driver / Passenger). "Actively driving" is derived - the
    // character you currently possess holds the Driver seat. LateUpdate() reconciles the engine +
    // cameras from that every frame so the switcher (key-press only) and this can't drift.
    [DefaultExecutionOrder(-50)] // Update/LateUpdate before the camera controllers (0) and the switcher (150)
    public class VehicleEntrySystem : MonoBehaviour
    {
        public enum Occupant { None, Player, Cat }
        public enum Seat { None, Driver, Passenger }

        [Header("Vehicle")]
        [SerializeField] private VehicleController vehicleController;
        [SerializeField] private GameObject vehicleCamera;
        [SerializeField] private float enterRange = 3f;
        [Tooltip("Where a character is placed on exit, in the CAR's local space - avoids popping out inside a wheel/body collider.")]
        [SerializeField] private Vector3 exitLocalOffset = new Vector3(-1.6f, 0.2f, 0f);

        [Header("Player (on foot)")]
        [SerializeField] private Transform player;
        [SerializeField] private CharacterMovement playerMovement;
        [SerializeField] private PlayerCombat playerCombat;
        [Tooltip("2026-08-30, user (\"裁減到他下半身 不然會看到他的腳在地上\"): renderers to hide while " +
                 "the player is seated.")]
        [SerializeField] private Renderer[] playerRenderersToHide;
        [Tooltip("GameObjects SetActive(false)'d while the player is seated - the Wings (a live " +
                 "WingFlap keeps re-enabling their renderers, so toggling the object is the only " +
                 "thing that sticks). Restored on exit.")]
        [SerializeField] private GameObject[] playerHideObjectsWhileSeated;
        [Tooltip("Bones scaled to ~0 while the player is seated so the legs collapse into the hips " +
                 "(the body is one skinned mesh, can't hide half of it) - normally the two upper-leg " +
                 "bones. Restored on exit.")]
        [SerializeField] private Transform[] playerCollapseBones;
        [SerializeField] private float playerCollapseBoneScale = 0.02f;
        [Tooltip("2026-08-30, user (\"PLAYER在主駕駛時必須靜止狀態\") - disabled while the player is " +
                 "seated so the Idle animation freezes; re-enabled on exit.")]
        [SerializeField] private Animator playerAnimatorToFreeze;
        [SerializeField] private GameObject playerCamera;
        [SerializeField] private Transform driverSeatAnchor;          // player, front
        [SerializeField] private Transform playerPassengerAnchor;     // player, rear flatbed

        [Header("Cat (optional - needs `possession` wired too)")]
        [Tooltip("Source of truth for who you're currently controlling. Null = player-only, original behaviour.")]
        [SerializeField] private CameraPossessionSwitcher possession;
        [SerializeField] private Transform cat;
        [Tooltip("The cat's control consumers to disable while seated - CharacterMovement, PlayerCombat, CatChargeAttack, CatPounce, CatAerialJudgment.")]
        [SerializeField] private Behaviour[] catControlToDisable;
        [SerializeField] private GameObject catCamera;
        [SerializeField] private Transform catDriverSeatAnchor;       // cat, front (pitched up so the chase cam sees its face)
        [SerializeField] private Transform catPassengerAnchor;        // cat, rear flatbed
        [Tooltip("Normally EMPTY - the user wants the cat visible in the seat.")]
        [SerializeField] private Renderer[] catRenderersToHide;
        [Tooltip("Optional - while the 守望者 (T) view is active it owns the cameras; the per-frame reconcile stands down so it doesn't fight the director.")]
        [SerializeField] private ViewFocusDirector viewDirector;

        public Seat PlayerSeat { get; private set; } = Seat.None;
        public Seat CatSeat { get; private set; } = Seat.None;

        private Vector3[] _collapseBoneOriginalScales;
        private VehicleCameraController _vehicleCam;
        private Occupant _fpHiddenOccupant = Occupant.None; // driver currently hidden for first-person

        public bool IsSeated(Occupant o) => SeatOf(o) != Seat.None;
        // The character currently behind the wheel (in the Driver seat), or None.
        public Occupant DriverOccupant =>
            PlayerSeat == Seat.Driver ? Occupant.Player : CatSeat == Seat.Driver ? Occupant.Cat : Occupant.None;
        // "You are the one driving right now" = you possess whoever holds the Driver seat.
        public bool IsDriving => DriverOccupant != Occupant.None && DriverOccupant == CurrentPossessed();

        private void Reset() => vehicleController = GetComponent<VehicleController>();

        private Occupant CurrentPossessed()
        {
            if (possession == null) return Occupant.Player; // player-only mode
            return possession.Current == CameraPossessionSwitcher.Possessed.Cat ? Occupant.Cat : Occupant.Player;
        }

        private Seat SeatOf(Occupant o) => o == Occupant.Player ? PlayerSeat : o == Occupant.Cat ? CatSeat : Seat.None;
        private void SetSeat(Occupant o, Seat s) { if (o == Occupant.Player) PlayerSeat = s; else if (o == Occupant.Cat) CatSeat = s; }
        private Transform TransformOf(Occupant o) => o == Occupant.Cat ? cat : player;

        private Transform AnchorFor(Occupant o, Seat s)
        {
            Transform a = o == Occupant.Cat
                ? (s == Seat.Passenger ? catPassengerAnchor : catDriverSeatAnchor)
                : (s == Seat.Passenger ? playerPassengerAnchor : driverSeatAnchor);
            return a != null ? a : transform;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.fKey.wasPressedThisFrame) return;

            Occupant me = CurrentPossessed();

            if (SeatOf(me) != Seat.None)
            {
                Dismount(me);
                return;
            }

            Transform t = TransformOf(me);
            if (t == null || Vector3.Distance(t.position, transform.position) > enterRange) return;

            // Driver seat if free, else the rear flatbed, else the car is full.
            Seat target = DriverOccupant == Occupant.None ? Seat.Driver
                        : (PlayerSeat != Seat.Passenger && CatSeat != Seat.Passenger) ? Seat.Passenger
                        : Seat.None;
            if (target != Seat.None) Mount(me, target);
        }

        private void Mount(Occupant o, Seat s)
        {
            SetSeat(o, s);
            bool cat = o == Occupant.Cat;

            SetControl(cat, false);
            SetRenderers(cat ? catRenderersToHide : playerRenderersToHide, false);
            if (!cat)
            {
                SetPlayerLegsCollapsed(true);
                SetActiveAll(playerHideObjectsWhileSeated, false);
                if (playerAnimatorToFreeze != null) playerAnimatorToFreeze.enabled = false; // 靜止
            }

            Transform character = TransformOf(o);
            if (character != null)
            {
                // 2026-08-26 bug ("無法用WASD控制車體移動") - a solid CharacterController capsule in the
                // seat overlaps the car body colliders; PhysX depenetrates it every step and launches
                // the Rigidbody. Disable it while parked.
                CharacterController cc = character.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                Transform anchor = AnchorFor(o, s);
                character.SetParent(anchor, false);
                character.localPosition = Vector3.zero;
                character.localRotation = Quaternion.identity; // inherit the anchor's pose (cat driver anchor is pitched up)
            }

            if (s == Seat.Driver && vehicleCamera != null)
            {
                // Fresh drive always starts in chase view - see VehicleCameraController.ResetView.
                VehicleCameraController vcc = vehicleCamera.GetComponent<VehicleCameraController>();
                if (vcc != null) vcc.ResetView();
            }
            // Engine + camera applied by LateUpdate this same frame.
        }

        // Force everyone out of the vehicle right now, no F press (續 124: YuanpeiEncounter uses
        // this so a player who drove into the boss arena fights on foot and the boss never targets
        // the car). No-op for empty seats.
        public void ForceDismountAll()
        {
            if (PlayerSeat != Seat.None) Dismount(Occupant.Player);
            if (CatSeat != Seat.None) Dismount(Occupant.Cat);
        }

        private void Dismount(Occupant o)
        {
            bool wasDriver = SeatOf(o) == Seat.Driver;
            SetSeat(o, Seat.None);
            bool cat = o == Occupant.Cat;

            if (wasDriver)
            {
                if (vehicleController != null) vehicleController.enabled = false; // OnDisable = parking brake
                SetActiveSafe(vehicleCamera, false);
            }

            Transform character = TransformOf(o);
            if (character != null)
            {
                character.SetParent(null, false);
                character.position = transform.TransformPoint(exitLocalOffset);
                character.rotation = Quaternion.identity;
                CharacterController cc = character.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = true;
            }

            SetControl(cat, true);
            if (_fpHiddenOccupant == o) _fpHiddenOccupant = Occupant.None; // dismounting mid-cockpit-view
            SetAllRenderers(o, true); // everything back on (except the seated hide-list, which the next line clears anyway)
            SetRenderers(cat ? catRenderersToHide : playerRenderersToHide, true);
            if (!cat)
            {
                SetPlayerLegsCollapsed(false);
                SetActiveAll(playerHideObjectsWhileSeated, true);
                if (playerAnimatorToFreeze != null) playerAnimatorToFreeze.enabled = true;
            }

            // Back to that character's on-foot camera if you're still possessing them (the switcher
            // only acts on key-presses).
            if (CurrentPossessed() == o) SetActiveSafe(cat ? catCamera : playerCamera, true);
        }

        // Every frame while anyone is aboard: keep the engine + cameras consistent with who you
        // possess, and hold every seated character inert (control + collider off).
        private void LateUpdate()
        {
            if (PlayerSeat == Seat.None && CatSeat == Seat.None) return;

            if (viewDirector != null && viewDirector.IsFocusedOnWatcher)
            {
                if (PlayerSeat != Seat.None) SetControl(false, false);
                if (CatSeat != Seat.None) SetControl(true, false);
                return;
            }

            bool youDrive = IsDriving;

            if (vehicleController != null && vehicleController.enabled != youDrive) vehicleController.enabled = youDrive;
            SetActiveSafe(vehicleCamera, youDrive);
            if (youDrive)
            {
                SetActiveSafe(playerCamera, false);
                SetActiveSafe(catCamera, false);
            }
            // else: you're on foot or a passenger; the switcher already has the right char camera on.

            // 2026-08-30 - hide the driver model while the cockpit (V) view is active, otherwise you
            // stare straight at the cat's / player's face.
            if (_vehicleCam == null && vehicleCamera != null) _vehicleCam = vehicleCamera.GetComponent<VehicleCameraController>();
            Occupant fpHide = (youDrive && _vehicleCam != null && _vehicleCam.IsFirstPerson) ? DriverOccupant : Occupant.None;
            if (fpHide != _fpHiddenOccupant)
            {
                if (_fpHiddenOccupant != Occupant.None) SetAllRenderers(_fpHiddenOccupant, true);
                if (fpHide != Occupant.None) SetAllRenderers(fpHide, false);
                _fpHiddenOccupant = fpHide;
            }

            // Every seated character stays a parked occupant regardless of who you possess.
            if (PlayerSeat != Seat.None) HoldSeated(Occupant.Player);
            if (CatSeat != Seat.None) HoldSeated(Occupant.Cat);
        }

        // Toggle every renderer under a character. visible=true re-applies the seated hide-list
        // (wings stay hidden); visible=false hides everything (first-person cockpit view).
        private void SetAllRenderers(Occupant o, bool visible)
        {
            Transform t = TransformOf(o);
            if (t == null) return;
            Renderer[] hideList = o == Occupant.Cat ? catRenderersToHide : playerRenderersToHide;
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
            {
                bool inHideList = false;
                if (hideList != null)
                    for (int i = 0; i < hideList.Length; i++) if (hideList[i] == r) { inHideList = true; break; }
                bool want = visible && !inHideList;
                if (r.enabled != want) r.enabled = want;
            }
        }

        private void HoldSeated(Occupant o)
        {
            SetControl(o == Occupant.Cat, false);
            Transform t = TransformOf(o);
            if (t != null)
            {
                CharacterController cc = t.GetComponent<CharacterController>();
                if (cc != null && cc.enabled) cc.enabled = false;
            }
            if (o == Occupant.Player) SetPlayerLegsCollapsed(true); // re-assert (cheap; Mecanim doesn't touch scale, but be safe)
        }

        // 2026-08-30, user ("裁減到他下半身 不然會看到他的腳在地上") - the player body is a single
        // skinned mesh, so hiding just the legs isn't a renderer toggle. Scale the upper-leg bones
        // to ~0 while seated: the calves/feet collapse into the pelvis and the car body / seat cover
        // the join. Restored to the captured original scale on dismount.
        private void SetPlayerLegsCollapsed(bool collapsed)
        {
            if (playerCollapseBones == null || playerCollapseBones.Length == 0) return;

            if (collapsed)
            {
                if (_collapseBoneOriginalScales == null || _collapseBoneOriginalScales.Length != playerCollapseBones.Length)
                {
                    _collapseBoneOriginalScales = new Vector3[playerCollapseBones.Length];
                    for (int i = 0; i < playerCollapseBones.Length; i++)
                        if (playerCollapseBones[i] != null) _collapseBoneOriginalScales[i] = playerCollapseBones[i].localScale;
                }
                Vector3 s = Vector3.one * Mathf.Max(0.001f, playerCollapseBoneScale);
                for (int i = 0; i < playerCollapseBones.Length; i++)
                    if (playerCollapseBones[i] != null && playerCollapseBones[i].localScale != s) playerCollapseBones[i].localScale = s;
            }
            else if (_collapseBoneOriginalScales != null)
            {
                for (int i = 0; i < playerCollapseBones.Length && i < _collapseBoneOriginalScales.Length; i++)
                    if (playerCollapseBones[i] != null) playerCollapseBones[i].localScale = _collapseBoneOriginalScales[i];
                _collapseBoneOriginalScales = null;
            }
        }

        private void OnDisable()
        {
            // Torn down while an occupant is mid-drive - undo the seated-state overrides.
            if (_fpHiddenOccupant != Occupant.None) { SetAllRenderers(_fpHiddenOccupant, true); _fpHiddenOccupant = Occupant.None; }
            if (PlayerSeat != Seat.None)
            {
                SetPlayerLegsCollapsed(false);
                SetActiveAll(playerHideObjectsWhileSeated, true);
                if (playerAnimatorToFreeze != null) playerAnimatorToFreeze.enabled = true;
            }
        }

        private static void SetActiveAll(GameObject[] gos, bool active)
        {
            if (gos == null) return;
            foreach (GameObject go in gos) if (go != null && go.activeSelf != active) go.SetActive(active);
        }

        private void SetControl(bool cat, bool enabled)
        {
            if (cat)
            {
                if (catControlToDisable != null)
                    foreach (Behaviour b in catControlToDisable) if (b != null && b.enabled != enabled) b.enabled = enabled;
            }
            else
            {
                if (playerMovement != null && playerMovement.enabled != enabled) playerMovement.enabled = enabled;
                if (playerCombat != null && playerCombat.enabled != enabled) playerCombat.enabled = enabled;
            }
        }

        private static void SetRenderers(Renderer[] renderers, bool enabled)
        {
            if (renderers == null) return;
            foreach (Renderer r in renderers) if (r != null) r.enabled = enabled;
        }

        private static void SetActiveSafe(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active) go.SetActive(active);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = IsDriving ? Color.red : Color.green;
            Gizmos.DrawWireSphere(transform.position, enterRange);
        }
    }
}
