using UnityEngine;
using UnityEngine.InputSystem;
using Live2DAction.Core;

namespace Live2DAction.Vehicles
{
    // 2026-08-30, user request ("car 幫我增加 ctrl 飛行功能 原理參考 player 功能綁訂車本身").
    // Thin MonoBehaviour: reads the keyboard, feeds a VehicleFlightState (the pure, unit-tested
    // solver), and writes the result onto the buggy's Rigidbody.
    //
    // Reads Keyboard.current directly - same convention as VehicleController / VehicleEntrySystem,
    // whose own comments explain why the vehicle side doesn't use the IInputCommand interface
    // (that vocabulary is combat / character-movement). playerMovement (the IInputCommand consumer)
    // is disabled for the whole drive anyway, so Ctrl / Space are free here.
    //
    // Flight = Rigidbody.useGravity off + linearVelocity written every FixedUpdate. The wheels are
    // airborne and contribute nothing; VehicleController.FlightModeActive also makes it skip its
    // own steering/motor/brake so a stray input can't fight the flight or leave stale torque for
    // the landing.
    [RequireComponent(typeof(Rigidbody))]
    public class VehicleFlightController : MonoBehaviour
    {
        [SerializeField] private VehicleController vehicleController;
        [SerializeField] private VehicleFlightData data;
        [Tooltip("Optional flight-energy meter. Null = unlimited flight (same null-safe convention as CharacterMovement.flightEnergy).")]
        [SerializeField] private UltimateEnergy flightEnergy;
        [Tooltip("Seed the energy meter to full on Start so you can fly immediately (the player's own flight ramps up from 0, a known minor UX gap).")]
        [SerializeField] private bool startEnergyFull = true;

        private Rigidbody _rigidbody;
        private VehicleFlightState _state;

        private bool _flyHeld, _descendHeld, _boostHeld;
        private float _throttle, _steer;

        private bool _wasFlying;
        // Our own authoritative attitude while flying - never read back from _rigidbody.rotation
        // (which physics/interpolation nudges between our writes), so the pitch/level easing can't
        // feed back on itself and jitter.
        private Quaternion _flightRot;
        private float _flightYaw;

        public bool IsFlying => _state != null && _state.IsFlying;

        private void Reset() => vehicleController = GetComponent<VehicleController>();

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            if (vehicleController == null) vehicleController = GetComponent<VehicleController>();
            _state = new VehicleFlightState(data);
        }

        private void Start()
        {
            if (startEnergyFull && flightEnergy != null) flightEnergy.AddEnergy(flightEnergy.MaxEnergy);
        }

        // "You are the one driving this car right now" = VehicleController is enabled
        // (VehicleEntrySystem toggles it). Same gate everything vehicle-side keys off.
        private bool CanControl => vehicleController != null && vehicleController.enabled && data != null;

        private void Update()
        {
            Keyboard k = Keyboard.current;
            if (k == null || !CanControl)
            {
                _flyHeld = _descendHeld = _boostHeld = false;
                _throttle = _steer = 0f;
                return;
            }

            _flyHeld = k.leftCtrlKey.isPressed || k.rightCtrlKey.isPressed;
            _descendHeld = k.spaceKey.isPressed;                       // handbrake on the ground, descend in the air
            _boostHeld = k.leftShiftKey.isPressed || k.rightShiftKey.isPressed;
            _throttle = (k.wKey.isPressed || k.upArrowKey.isPressed ? 1f : 0f)
                      - (k.sKey.isPressed || k.downArrowKey.isPressed ? 1f : 0f);
            _steer = (k.dKey.isPressed || k.rightArrowKey.isPressed ? 1f : 0f)
                   - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1f : 0f);
        }

        private void FixedUpdate()
        {
            if (_state == null || data == null) return;

            float dt = Time.fixedDeltaTime;

            Vector3 forwardFlat = transform.forward;
            forwardFlat.y = 0f;

            float energy = flightEnergy != null ? flightEnergy.CurrentEnergy : 0f;
            bool grounded = vehicleController != null && vehicleController.AnyWheelGrounded;
            float height = HeightAboveGround();

            VehicleFlightOutput outp = _state.Tick(dt, CanControl, _flyHeld, _descendHeld, _boostHeld,
                _throttle, _steer, _rigidbody.linearVelocity, forwardFlat, grounded, height, energy, flightEnergy != null);

            if (vehicleController != null) vehicleController.FlightModeActive = outp.IsFlying;
            _rigidbody.useGravity = !outp.IsFlying;

            if (!outp.IsFlying)
            {
                // Soft-land: don't hand a fast descent straight to the suspension.
                if (outp.JustEnded && _rigidbody.linearVelocity.y < -data.LandingImpactSpeedCap)
                {
                    Vector3 v = _rigidbody.linearVelocity;
                    v.y = -data.LandingImpactSpeedCap;
                    _rigidbody.linearVelocity = v;
                }
                _wasFlying = false;
                return;
            }

            if (!_wasFlying)
            {
                _flightYaw = _rigidbody.rotation.eulerAngles.y;
                _flightRot = _rigidbody.rotation;
            }
            _wasFlying = true;

            _rigidbody.linearVelocity = outp.LinearVelocity;
            _rigidbody.angularVelocity = Vector3.zero;

            _flightYaw += outp.YawDeltaDegrees;
            Quaternion target = Quaternion.Euler(outp.TargetPitchDegrees, _flightYaw, 0f);
            float t = 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, data.LevelOutSmoothTime));
            _flightRot = Quaternion.Slerp(_flightRot, target, t);
            _rigidbody.MoveRotation(_flightRot);

            if (flightEnergy != null) flightEnergy.Drain(outp.EnergyToDrain);
        }

        // Downward ray from the chassis pivot, ignoring the buggy's own colliders. Used for the
        // "descend near the ground -> land" path (the wheels are disabled while flying so their
        // isGrounded can't be relied on). Big number if nothing is below.
        private float HeightAboveGround()
        {
            Vector3 origin = transform.position + Vector3.up * 0.2f;
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 500f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            foreach (RaycastHit h in hits)
            {
                if (h.collider.transform.IsChildOf(transform)) continue; // IsChildOf(self) is true
                if (h.distance < best) best = h.distance;
            }
            return best == float.MaxValue ? 999f : Mathf.Max(0f, best - 0.2f);
        }

        private void OnDisable()
        {
            // Torn down mid-flight (dismount, scene teardown) - hand the buggy back to gravity.
            if (_rigidbody != null) _rigidbody.useGravity = true;
            if (vehicleController != null) vehicleController.FlightModeActive = false;
            _wasFlying = false;
        }
    }
}
