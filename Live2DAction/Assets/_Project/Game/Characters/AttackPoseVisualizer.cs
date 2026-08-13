using UnityEngine;
using Live2DAction.Combat;

namespace Live2DAction.Characters
{
    // Placeholder attack visual: no authored attack animation clips exist yet for either
    // character (see AttackPoseUtility's class comment), so this drives a procedural swing
    // angle straight off the same Startup/Active/Recovery frame data the combo state machine
    // already runs on. Generic over "which Transform gets rotated" so the same component
    // works for Maya's arm bone and the enemy's whole Visual capsule - swap swingTransform
    // for a real animated rig later without touching this class.
    //
    // Runs in LateUpdate, after Animator has evaluated this frame's pose (Mecanim writes bone
    // transforms during Unity's internal animation step, which happens before LateUpdate),
    // and multiplies the swing on top of whatever rotation is already there instead of
    // caching a one-time baseline - so Maya's Idle/Walk arm sway keeps playing underneath,
    // and the swing simply adds zero rotation (identity) while no attack is in progress.
    public class AttackPoseVisualizer : MonoBehaviour
    {
        [SerializeField] private PlayerCombat combatSource;
        [SerializeField] private Transform swingTransform;
        [SerializeField] private Vector3 swingAxis = Vector3.right;
        [SerializeField] private float windUpAngleDegrees = 20f;
        [SerializeField] private float swingAngleDegrees = 60f;

        // Flips the sign of the computed angle without touching swingAxis - the correct
        // direction for a given bone/pivot can only be confirmed by eye in the Editor (same
        // reasoning as CubismBillboard's "Face Away Instead" toggle), so this exists to fix
        // it from the Inspector instead of a code change.
        [SerializeField] private bool invert;

        private void LateUpdate()
        {
            if (combatSource == null || swingTransform == null)
            {
                return;
            }

            float angle = AttackPoseUtility.ComputeSwingAngle(combatSource.CurrentPhase, combatSource.PhaseProgress, windUpAngleDegrees, swingAngleDegrees);
            if (invert)
            {
                angle = -angle;
            }

            swingTransform.localRotation *= Quaternion.AngleAxis(angle, swingAxis);
        }
    }
}
