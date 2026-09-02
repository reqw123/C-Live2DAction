using UnityEngine;
using Live2DAction.Combat;

namespace Live2DAction.Characters
{
    // 2026-08-29, cat combat design (Docs/CAT_COMBAT_DESIGN.md 3.7). Cat.glb has a 43-bone
    // auto-rig but zero animation clips and no Animator, so the attack visual is procedural,
    // driven straight off the same PlayerCombat Startup/Active/Recovery frame data the hit
    // timing runs on - the multi-bone equivalent of the retired single-bone AttackPoseVisualizer.
    //
    // Model: one scalar "swing" in [-1..1] over the attack's lifetime (wind up to -1 during
    // Startup, snap through to +1 during Active, settle to 0 during Recovery - see ComputeSwing,
    // kept pure so the shape is EditMode-testable, same convention as AttackPoseUtility). Each
    // configured bone hinges about its own local axis: negative swing rotates by windUpDegrees
    // one way (draw the paw back), positive swing by strikeDegrees the other (throw it forward).
    // A per-attack amplitude multiplier (swipe 1 < swipe 2 < swipe 3 < heavy/pounce) scales the
    // whole set, and each bone's pawSide lets the front-left / front-right chains alternate
    // between combo steps.
    //
    // Runs in LateUpdate at execution order 20 - AFTER CatProceduralWalk (order 0) has written
    // the gait, so this multiplies its rotation on top of the walk's. CatProceduralWalk's gait
    // amplitude is suppressed via SetAttackSuppression while a swing is in progress so the two
    // aren't fighting over the front-leg bones.
    [DefaultExecutionOrder(20)]
    public class CatAttackPose : MonoBehaviour
    {
        public enum PawSide { Both, Left, Right }

        [System.Serializable]
        public struct PoseBone
        {
            public Transform bone;
            [Tooltip("Local-space hinge axis for this bone.")]
            public Vector3 localAxis;
            [Tooltip("Degrees this bone rotates at full wind-up (negative swing).")]
            public float windUpDegrees;
            [Tooltip("Degrees this bone rotates at full strike (positive swing). Sign sets swing direction.")]
            public float strikeDegrees;
            [Tooltip("Which combo steps this bone participates in (front paws alternate).")]
            public PawSide pawSide;
        }

        [SerializeField] private PlayerCombat combatSource;
        [SerializeField] private CatProceduralWalk walk;
        [SerializeField] private PoseBone[] bones = new PoseBone[0];

        [Header("Per-attack amplitude (multiplies every bone's degrees)")]
        [SerializeField] private float swipe1Amplitude = 0.7f;
        [SerializeField] private float swipe2Amplitude = 0.85f;
        [SerializeField] private float swipe3Amplitude = 1f;
        [SerializeField] private float heavyAmplitude = 1.3f;
        [SerializeField] private float pounceAmplitude = 1.15f;

        [Tooltip("attackId of the charged heavy AttackData - matched against PlayerCombat.CurrentAttackId.")]
        [SerializeField] private string heavyAttackId = "CatHeavy";
        [Tooltip("attackId of the pounce AttackData.")]
        [SerializeField] private string pounceAttackId = "CatPounce";

        private Quaternion[] _rest;
        private bool _captured;

        private void Start()
        {
            if (combatSource == null) combatSource = GetComponent<PlayerCombat>();
            if (walk == null) walk = GetComponent<CatProceduralWalk>();
            CaptureRest();
        }

        // Public so a rebuild tool / test can re-snapshot after re-wiring the bone list.
        public void CaptureRest()
        {
            _rest = new Quaternion[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                _rest[i] = bones[i].bone != null ? bones[i].bone.localRotation : Quaternion.identity;
            }
            _captured = true;
        }

        // -1 = full wind-up, +1 = full strike, 0 = neutral. Pure (same reasoning as
        // AttackPoseUtility.ComputeSwingAngle / ComboAttackState.PhaseProgress).
        public static float ComputeSwing(AttackPhase phase, float phaseProgress)
        {
            float t = Mathf.Clamp01(phaseProgress);
            switch (phase)
            {
                case AttackPhase.Startup: return Mathf.Lerp(0f, -1f, t);
                case AttackPhase.Active: return Mathf.Lerp(-1f, 1f, t);
                case AttackPhase.Recovery: return Mathf.Lerp(1f, 0f, t);
                default: return 0f;
            }
        }

        // Which paw leads this attack. Combo swipes alternate; heavy/pounce use both.
        public static PawSide LeadPawFor(int comboIndex, bool isOverride)
        {
            if (isOverride) return PawSide.Both;
            return (comboIndex % 2 == 0) ? PawSide.Right : PawSide.Left;
        }

        private float AmplitudeFor(string attackId, int comboIndex, bool isOverride)
        {
            if (isOverride)
            {
                if (attackId == heavyAttackId) return heavyAmplitude;
                if (attackId == pounceAttackId) return pounceAmplitude;
                return heavyAmplitude;
            }
            switch (comboIndex)
            {
                case 0: return swipe1Amplitude;
                case 1: return swipe2Amplitude;
                default: return swipe3Amplitude;
            }
        }

        private void LateUpdate()
        {
            if (!_captured || bones.Length == 0 || combatSource == null)
            {
                return;
            }
            if (_rest == null || _rest.Length != bones.Length)
            {
                CaptureRest();
            }

            AttackPhase phase = combatSource.CurrentPhase;
            bool active = phase != AttackPhase.Idle;

            if (walk != null)
            {
                walk.SetAttackSuppression(active ? 1f : 0f);
            }

            // When no swing is in progress, leave every bone alone - CatProceduralWalk (which
            // ran just before this, at order 0) owns them, and overwriting with _rest here would
            // freeze the gait. This component only takes the bones over for the duration of a swing.
            if (!active)
            {
                return;
            }

            float swing = ComputeSwing(phase, combatSource.PhaseProgress);
            bool isOverride = combatSource.IsOverrideAttackActive;
            float amp = AmplitudeFor(combatSource.CurrentAttackId, combatSource.ComboIndex, isOverride);
            PawSide lead = LeadPawFor(combatSource.ComboIndex, isOverride);

            float windUpPart = Mathf.Max(0f, -swing);
            float strikePart = Mathf.Max(0f, swing);

            for (int i = 0; i < bones.Length; i++)
            {
                PoseBone pb = bones[i];
                if (pb.bone == null)
                {
                    continue;
                }

                // A left/right paw bone only moves when it (or "both") is leading this attack.
                if (pb.pawSide != PawSide.Both && lead != PawSide.Both && pb.pawSide != lead)
                {
                    pb.bone.localRotation = _rest[i];
                    continue;
                }

                float degrees = (strikePart * pb.strikeDegrees - windUpPart * pb.windUpDegrees) * amp;
                Vector3 axis = pb.localAxis.sqrMagnitude > 1e-6f ? pb.localAxis.normalized : Vector3.right;
                pb.bone.localRotation = _rest[i] * Quaternion.AngleAxis(degrees, axis);
            }
        }
    }
}
