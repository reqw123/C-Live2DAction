using UnityEngine;

namespace Live2DAction.Vehicles
{
    // 2026-08-26, explicit user request ("車輛駕駛系統... 骨架Rotation修正") - drives ONE visual
    // wheel bone from its matching WheelCollider's real physics state (position + rpm + steer
    // angle), never a fixed/fake Rotate() call. Built specifically for AI-generated rigs whose
    // bone axes can't be assumed: measured directly on this project's imported Buggy model, the
    // four wheel bones each came out with a COMPLETELY different, non-cardinal local rotation
    // (e.g. one bone's local Y axis turned out to be the real spin axis, not X or Z) - hand-coding
    // "rotate around local X" would have looked wrong on at least three of the four wheels.
    //
    // Auto-detection approach: at Start, capture the bone's bind-pose local rotation once, then
    // ask "which of THIS bone's own local axes points closest to the vehicle's world right (spin
    // axis) and world up (steer axis) direction, given how it's actually oriented right now" via
    // Transform.InverseTransformDirection - which correctly accounts for the bone's real rotation
    // however weird it is, no cardinal-axis assumption anywhere. Steering/spin are then applied as
    // extra rotations layered on top of that captured bind pose, in the bone's own local frame, so
    // the model's original bind pose is never otherwise disturbed (spec: "不被破壞").
    public class WheelVisualSync : MonoBehaviour
    {
        public enum AxisMode { Auto, LocalX, LocalY, LocalZ, NegLocalX, NegLocalY, NegLocalZ }

        [Header("Required references")]
        [SerializeField] private WheelCollider wheelCollider;
        [SerializeField] private Transform wheelBone;
        [Tooltip("The vehicle's own root Transform - used only to read world right/up directions " +
                 "for axis auto-detection, never modified.")]
        [SerializeField] private Transform vehicleRoot;

        [Header("Steering (front wheels only - see VehicleController.DriveType note)")]
        [SerializeField] private bool isSteeringWheel;

        [Header("Axis override - per-wheel, since left/right bone axes can differ (spec 七). " +
                 "Auto (default) re-detects every Start(); only touch this if a specific wheel " +
                 "still looks wrong after checking Invert first.")]
        [SerializeField] private AxisMode spinAxisOverride = AxisMode.Auto;
        [SerializeField] private AxisMode steerAxisOverride = AxisMode.Auto;
        [SerializeField] private bool invertSpin;
        [SerializeField] private bool invertSteer;

        // Debug readout (spec 十五 - "Inspector 顯示...Wheel RPM")
        public float CurrentRpm { get; private set; }
        public float CurrentSteerAngle { get; private set; }
        public bool IsGrounded { get; private set; }

        private Quaternion _bindLocalRotation;
        private Vector3 _spinAxisLocal;
        private Vector3 _steerAxisLocal;
        private float _accumulatedSpinDegrees;

        private void Start()
        {
            if (wheelBone == null || wheelCollider == null || vehicleRoot == null)
            {
                enabled = false;
                return;
            }

            _bindLocalRotation = wheelBone.localRotation;

            _spinAxisLocal = spinAxisOverride == AxisMode.Auto
                ? wheelBone.InverseTransformDirection(vehicleRoot.right).normalized
                : AxisFromMode(spinAxisOverride);

            _steerAxisLocal = steerAxisOverride == AxisMode.Auto
                ? wheelBone.InverseTransformDirection(vehicleRoot.up).normalized
                : AxisFromMode(steerAxisOverride);
        }

        private static Vector3 AxisFromMode(AxisMode mode)
        {
            switch (mode)
            {
                case AxisMode.LocalX: return Vector3.right;
                case AxisMode.LocalY: return Vector3.up;
                case AxisMode.LocalZ: return Vector3.forward;
                case AxisMode.NegLocalX: return Vector3.left;
                case AxisMode.NegLocalY: return Vector3.down;
                case AxisMode.NegLocalZ: return Vector3.back;
                default: return Vector3.right;
            }
        }

        // Called from VehicleController's FixedUpdate/Update (not its own Update) so wheel visuals
        // update in lockstep with the physics step that just moved the WheelColliders - avoids a
        // one-frame lag between the collider's real pose and what's drawn.
        public void SyncVisual()
        {
            if (wheelBone == null || wheelCollider == null) return;

            wheelCollider.GetWorldPose(out Vector3 worldPos, out _);
            wheelBone.position = worldPos; // suspension travel (spec 八) falls out of this for free - GetWorldPose already reflects current spring compression

            IsGrounded = wheelCollider.isGrounded;
            CurrentRpm = wheelCollider.rpm;
            CurrentSteerAngle = wheelCollider.steerAngle;

            // rpm -> degrees/second -> degrees this step. Accumulated (not reset each frame) so
            // spin is continuous - reverses cleanly when rpm goes negative, holds still at rpm=0,
            // scales naturally with speed, all for free from the real physics value.
            _accumulatedSpinDegrees += wheelCollider.rpm * 6f * Time.deltaTime;
            _accumulatedSpinDegrees %= 360f;

            float spin = _accumulatedSpinDegrees * (invertSpin ? -1f : 1f);
            Quaternion spinRot = Quaternion.AngleAxis(spin, _spinAxisLocal);

            if (isSteeringWheel)
            {
                float steer = wheelCollider.steerAngle * (invertSteer ? -1f : 1f);
                Quaternion steerRot = Quaternion.AngleAxis(steer, _steerAxisLocal);
                // Steer THEN spin (both layered on the untouched bind pose) - order matters for a
                // combined rotation, but since the two axes are always near-orthogonal (right vs
                // up, whatever their real local values turned out to be) either order reads
                // correctly; steer-then-spin matches "the whole wheel assembly turns, and the tire
                // still rolls within that turned assembly" which is the physically real order.
                wheelBone.localRotation = _bindLocalRotation * steerRot * spinRot;
            }
            else
            {
                wheelBone.localRotation = _bindLocalRotation * spinRot;
            }
        }
    }
}
