using UnityEngine;
using UnityEngine.InputSystem;
using Live2DAction.Characters;
using Live2DAction.Combat;

namespace Live2DAction.Vehicles
{
    // 2026-08-26, explicit user request ("靠近CAR可用F鍵進入車體 視角變為主駕駛方向 W/A/S/D移動控制
    // 正式由CAR接管") - proximity-gated enter/exit. Reads F directly from Keyboard.current, same
    // direct-input convention as VehicleController/PlayerInputProvider rather than the Input
    // Actions asset workflow this project doesn't use.
    //
    // Handing WASD to the car is done by disabling CharacterMovement (the ONLY thing that turns
    // MoveInput into an actual Move() call) rather than disabling PlayerInputProvider itself -
    // PlayerInputProvider's public properties are plain fields that freeze at their last value the
    // instant its Update() stops running, and PlayerCombat/other systems keep reading them every
    // frame regardless of whether the provider is enabled - disabling the provider would leave
    // stale input (e.g. a MoveInput frozen mid-press, or AttackPressed stuck true if F happened to
    // land the same frame as a click) being read forever instead of correctly reading as "nothing
    // held". Disabling the CONSUMER (CharacterMovement, PlayerCombat) is the actually-safe cut
    // point - each Component re-reads live input the next time it's re-enabled.
    public class VehicleEntrySystem : MonoBehaviour
    {
        [SerializeField] private VehicleController vehicleController;
        [SerializeField] private Transform player;
        [SerializeField] private CharacterMovement playerMovement;
        [SerializeField] private PlayerCombat playerCombat;
        [SerializeField] private Renderer[] playerRenderersToHide;
        [SerializeField] private GameObject playerCamera;
        [SerializeField] private GameObject vehicleCamera;
        [Tooltip("Where the player Transform parks (as a child of the car) while driving - purely so anything still reading Player.transform.position (lock-on, minimap, etc.) gets a sane in-the-car answer instead of stale coordinates.")]
        [SerializeField] private Transform driverSeatAnchor;
        [SerializeField] private float enterRange = 3f;
        [Tooltip("Where the player is placed on exit, relative to the car - avoids popping out inside a wheel/body collider.")]
        [SerializeField] private Vector3 exitLocalOffset = new Vector3(-1.6f, 0.2f, 0f);

        public bool IsDriving { get; private set; }

        private Transform _playerOriginalParent;
        private Vector3 _playerLocalPosBeforeEntry;
        private Quaternion _playerLocalRotBeforeEntry;

        private void Reset()
        {
            vehicleController = GetComponent<VehicleController>();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.fKey.wasPressedThisFrame) return;

            if (!IsDriving)
            {
                if (player != null && Vector3.Distance(player.position, transform.position) <= enterRange)
                {
                    EnterVehicle();
                }
            }
            else
            {
                ExitVehicle();
            }
        }

        private void EnterVehicle()
        {
            IsDriving = true;

            if (playerMovement != null) playerMovement.enabled = false;
            if (playerCombat != null) playerCombat.enabled = false;
            if (playerRenderersToHide != null)
            {
                foreach (var r in playerRenderersToHide) if (r != null) r.enabled = false;
            }

            if (player != null)
            {
                // 2026-08-26, real playtested bug ("無法用WASD控制車體移動") - root cause: the
                // Player's CharacterController is a solid (non-trigger) capsule that stayed enabled
                // after being parented into the driver seat, physically overlapping the car's own
                // MainBodyCollider/CabinCollider (also solid). PhysX depenetrated that overlap every
                // physics step, launching the Rigidbody into the air (confirmed: wheels never
                // touched ground, isGrounded stayed false, motorTorque had nothing to push against)
                // - not a WheelCollider/suspension bug, the car's own physics settles fine in
                // isolation. Disabling it here is the same "cut the CONSUMER, not the input" pattern
                // as playerMovement/playerCombat above; CharacterController.enabled=false makes
                // Unity drop its collision response entirely while parked in the seat.
                CharacterController playerCollider = player.GetComponent<CharacterController>();
                if (playerCollider != null) playerCollider.enabled = false;

                _playerOriginalParent = player.parent;
                _playerLocalPosBeforeEntry = player.localPosition;
                _playerLocalRotBeforeEntry = player.localRotation;
                Transform seat = driverSeatAnchor != null ? driverSeatAnchor : transform;
                player.SetParent(seat, false);
                player.localPosition = Vector3.zero;
                player.localRotation = Quaternion.identity;
            }

            if (playerCamera != null) playerCamera.SetActive(false);
            if (vehicleCamera != null)
            {
                vehicleCamera.SetActive(true);
                // Always start a fresh drive in chase view, never resume whatever mode the last
                // drive happened to end in (see VehicleCameraController.ResetView's own comment).
                VehicleCameraController cameraController = vehicleCamera.GetComponent<VehicleCameraController>();
                if (cameraController != null) cameraController.ResetView();
            }

            if (vehicleController != null) vehicleController.enabled = true;
        }

        private void ExitVehicle()
        {
            IsDriving = false;

            if (vehicleController != null) vehicleController.enabled = false; // OnDisable applies the parking brake, see that method's own comment

            if (player != null)
            {
                player.SetParent(_playerOriginalParent, false);
                // Exit beside the car, not back at wherever they happened to be standing before
                // entry - exitLocalOffset is in the CAR's local space at the moment of exit.
                player.position = transform.TransformPoint(exitLocalOffset);
                player.rotation = Quaternion.identity;

                CharacterController playerCollider = player.GetComponent<CharacterController>();
                if (playerCollider != null) playerCollider.enabled = true;
            }

            if (playerMovement != null) playerMovement.enabled = true;
            if (playerCombat != null) playerCombat.enabled = true;
            if (playerRenderersToHide != null)
            {
                foreach (var r in playerRenderersToHide) if (r != null) r.enabled = true;
            }

            if (vehicleCamera != null) vehicleCamera.SetActive(false);
            if (playerCamera != null) playerCamera.SetActive(true);
        }

        // Spec-adjacent debug aid, same convention as VehicleController's own OnDrawGizmosSelected.
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = IsDriving ? Color.red : Color.green;
            Gizmos.DrawWireSphere(transform.position, enterRange);
        }
    }
}
