using UnityEngine;

namespace Live2DAction.Vehicles
{
    // 2026-08-30, user request ("car 幫我增加 ctrl 飛行功能 原理參考 player 功能綁訂車本身"). All the
    // vehicle-flight tuning lives here (project rule 7 - no balance numbers hard-coded in scripts),
    // same SO idiom as DodgeData for the player dodge. Consumed by VehicleFlightState (pure logic,
    // unit-tested) which VehicleFlightController drives from a Rigidbody buggy.
    [CreateAssetMenu(fileName = "VehicleFlightData", menuName = "Live2DAction/Vehicles/Flight Data")]
    public class VehicleFlightData : ScriptableObject
    {
        [Header("Vertical (m/s)")]
        [SerializeField] private float ascendSpeed = 8f;
        [SerializeField] private float descendSpeed = 8f;
        [Tooltip("SmoothDamp time for the vertical velocity easing toward its target (ascend / hover / descend) - mirrors CharacterMovement.flightVerticalSmoothTime.")]
        [SerializeField] private float verticalSmoothTime = 0.18f;

        [Header("Horizontal (m/s)")]
        [SerializeField] private float cruiseSpeed = 22f;
        [Tooltip("Shift-boost multiplier on cruiseSpeed while flying (same key as the ground boost).")]
        [SerializeField] private float boostMultiplier = 1.6f;
        [SerializeField] private float horizontalSmoothTime = 0.12f;

        [Header("Rotation")]
        [Tooltip("A/D yaw rate while flying (deg/s).")]
        [SerializeField] private float yawSpeedDegrees = 90f;
        [Tooltip("Nose pitches this many degrees toward the current climb/dive at full vertical speed - visual feel only, does not affect the flight path.")]
        [SerializeField] private float pitchTowardVerticalDegrees = 18f;
        [Tooltip("How fast the chassis eases toward its target pitch / zero roll while flying (exponential smoothing time constant).")]
        [SerializeField] private float levelOutSmoothTime = 0.2f;

        [Header("Liftoff / landing")]
        [Tooltip("Upward velocity the flight state seeds on engage, so the buggy visibly leaves the ground.")]
        [SerializeField] private float liftoffBoost = 4f;
        [Tooltip("Not holding Ctrl and within this height of the ground -> flight ends and the buggy drops onto its wheels. Hold Space (descend) to bring it down into this band; hold Ctrl to abort and climb away.")]
        [SerializeField] private float landingClearance = 1.6f;
        [Tooltip("Downward speed is clamped to this the instant flight ends, so a fast descent doesn't slam the suspension.")]
        [SerializeField] private float landingImpactSpeedCap = 4f;

        [Header("Energy (optional - a null UltimateEnergy meter = unlimited flight, same null-safe convention as CharacterMovement.flightEnergy)")]
        [SerializeField] private float energyDrainPerSecond = 15f;
        [Tooltip("Extra drain while boosting, ON TOP OF energyDrainPerSecond.")]
        [SerializeField] private float boostExtraDrainPerSecond = 12f;
        [Tooltip("Energy required to (re-)engage flight - a real reserve, not just > 0 - mirrors CharacterMovement.flightResumeEnergyThreshold.")]
        [SerializeField] private float resumeEnergyThreshold = 30f;

        public float AscendSpeed => ascendSpeed;
        public float DescendSpeed => descendSpeed;
        public float VerticalSmoothTime => verticalSmoothTime;
        public float CruiseSpeed => cruiseSpeed;
        public float BoostMultiplier => boostMultiplier;
        public float HorizontalSmoothTime => horizontalSmoothTime;
        public float YawSpeedDegrees => yawSpeedDegrees;
        public float PitchTowardVerticalDegrees => pitchTowardVerticalDegrees;
        public float LevelOutSmoothTime => levelOutSmoothTime;
        public float LiftoffBoost => liftoffBoost;
        public float LandingClearance => landingClearance;
        public float LandingImpactSpeedCap => landingImpactSpeedCap;
        public float EnergyDrainPerSecond => energyDrainPerSecond;
        public float BoostExtraDrainPerSecond => boostExtraDrainPerSecond;
        public float ResumeEnergyThreshold => resumeEnergyThreshold;
    }
}
