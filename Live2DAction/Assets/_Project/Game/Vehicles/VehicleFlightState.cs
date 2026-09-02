using UnityEngine;

namespace Live2DAction.Vehicles
{
    // 2026-08-30, vehicle flight. Pure state machine + velocity solver, no MonoBehaviour / no
    // Rigidbody - same "extract the logic so it's unit-testable" split as DodgeState vs the player
    // dodge (DodgeState is the plain class, CharacterMovement feeds it Tick() each frame).
    // VehicleFlightController is the thin MonoBehaviour that reads the keyboard and writes the
    // result onto the buggy's Rigidbody.
    //
    // Principle mirrors CharacterMovement's flight:
    //   - hold fly (Ctrl) to engage + climb; release to hover (vertical eases to 0, not a fall)
    //   - descend held (Space) dives
    //   - flight PERSISTS once engaged regardless of the key, ends only on a real landing
    //     (wheel-grounded AND not holding fly) or the energy meter hitting 0
    //   - (re-)engaging needs resumeEnergyThreshold, a genuine reserve
    public readonly struct VehicleFlightOutput
    {
        public readonly bool IsFlying;
        public readonly bool JustEnded;          // true on the single tick flight stops (landed / out of energy)
        public readonly Vector3 LinearVelocity;  // apply directly to the Rigidbody while IsFlying
        public readonly float YawDeltaDegrees;   // add to the chassis yaw this tick
        public readonly float TargetPitchDegrees;
        public readonly float EnergyToDrain;     // already multiplied by dt

        public VehicleFlightOutput(bool isFlying, bool justEnded, Vector3 linearVelocity,
            float yawDeltaDegrees, float targetPitchDegrees, float energyToDrain)
        {
            IsFlying = isFlying;
            JustEnded = justEnded;
            LinearVelocity = linearVelocity;
            YawDeltaDegrees = yawDeltaDegrees;
            TargetPitchDegrees = targetPitchDegrees;
            EnergyToDrain = energyToDrain;
        }
    }

    public class VehicleFlightState
    {
        private readonly VehicleFlightData _data;

        private bool _isFlying;
        private float _verticalVelocity;
        private float _verticalVelRef;
        private Vector3 _horizontalVelocity;
        private Vector3 _horizontalVelRef;

        public bool IsFlying => _isFlying;

        public VehicleFlightState(VehicleFlightData data)
        {
            _data = data;
        }

        // dt              : fixed timestep
        // canControl      : this character is actually the one driving right now (VehicleController
        //                   enabled). Losing it mid-flight (dismount) ends flight immediately.
        // flyHeld         : Ctrl - engage / climb
        // descendHeld     : Space - dive (only while already flying)
        // boostHeld       : Shift - faster cruise + extra drain
        // throttle        : -1..1, W/S - thrust along chassisForwardFlat
        // steer           : -1..1, A/D - yaw
        // currentVelocity : the Rigidbody's velocity right now (seeds the smoothing on engage)
        // chassisForwardFlat : transform.forward flattened to the XZ plane, normalized
        // anyWheelGrounded: from VehicleController - a wheel actually touched down
        // heightAboveGround: metres from a downward raycast (self-ignored); huge if nothing below
        // currentEnergy / hasEnergyMeter : the flight meter (hasEnergyMeter false => unlimited)
        public VehicleFlightOutput Tick(float dt, bool canControl, bool flyHeld, bool descendHeld, bool boostHeld,
            float throttle, float steer, Vector3 currentVelocity, Vector3 chassisForwardFlat,
            bool anyWheelGrounded, float heightAboveGround, float currentEnergy, bool hasEnergyMeter)
        {
            if (_data == null || !canControl)
            {
                bool wasFlying = _isFlying;
                _isFlying = false;
                _verticalVelocity = 0f; _verticalVelRef = 0f;
                _horizontalVelocity = Vector3.zero; _horizontalVelRef = Vector3.zero;
                return new VehicleFlightOutput(false, wasFlying, currentVelocity, 0f, 0f, 0f);
            }

            if (_isFlying)
            {
                // Land when you're not commanding a climb AND you're either physically on the
                // wheels or hovering low enough. `landingClearance` is small, so you won't drop
                // out of the sky by drifting low - and holding Ctrl always aborts it and climbs.
                bool nearGround = anyWheelGrounded || heightAboveGround <= _data.LandingClearance;
                bool landed = nearGround && !flyHeld;
                bool outOfEnergy = hasEnergyMeter && currentEnergy <= 0f;
                if (landed || outOfEnergy)
                {
                    _isFlying = false;
                    _verticalVelocity = 0f; _verticalVelRef = 0f;
                    _horizontalVelocity = Vector3.zero; _horizontalVelRef = Vector3.zero;
                    return new VehicleFlightOutput(false, true, currentVelocity, 0f, 0f, 0f);
                }
            }
            else
            {
                bool haveReserve = !hasEnergyMeter || currentEnergy >= _data.ResumeEnergyThreshold;
                if (flyHeld && haveReserve)
                {
                    _isFlying = true;
                    _verticalVelocity = Mathf.Max(currentVelocity.y, _data.LiftoffBoost);
                    _horizontalVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
                }
                else
                {
                    return new VehicleFlightOutput(false, false, currentVelocity, 0f, 0f, 0f);
                }
            }

            // --- flying: solve the desired velocity ---
            float verticalTarget = flyHeld ? _data.AscendSpeed : (descendHeld ? -_data.DescendSpeed : 0f);
            _verticalVelocity = Mathf.SmoothDamp(_verticalVelocity, verticalTarget, ref _verticalVelRef,
                Mathf.Max(0.001f, _data.VerticalSmoothTime), Mathf.Infinity, dt);

            float horizontalSpeed = _data.CruiseSpeed * (boostHeld ? _data.BoostMultiplier : 1f);
            Vector3 fwd = chassisForwardFlat.sqrMagnitude > 0.0001f ? chassisForwardFlat.normalized : Vector3.forward;
            Vector3 horizontalTarget = fwd * (Mathf.Clamp(throttle, -1f, 1f) * horizontalSpeed);
            _horizontalVelocity = Vector3.SmoothDamp(_horizontalVelocity, horizontalTarget, ref _horizontalVelRef,
                Mathf.Max(0.001f, _data.HorizontalSmoothTime), Mathf.Infinity, dt);

            Vector3 velocity = new Vector3(_horizontalVelocity.x, _verticalVelocity, _horizontalVelocity.z);
            float yawDelta = Mathf.Clamp(steer, -1f, 1f) * _data.YawSpeedDegrees * dt;

            float pitchFraction = Mathf.Clamp(_verticalVelocity / Mathf.Max(0.01f, _data.AscendSpeed), -1f, 1f);
            float targetPitch = -pitchFraction * _data.PitchTowardVerticalDegrees; // negative pitch = nose up

            float drain = (_data.EnergyDrainPerSecond + (boostHeld ? _data.BoostExtraDrainPerSecond : 0f)) * dt;

            return new VehicleFlightOutput(true, false, velocity, yawDelta, targetPitch, drain);
        }
    }
}
