using UnityEngine;

namespace Live2DAction.Characters
{
    // 2026-08-29, explicit user request ("這個GLB應該是有骨架的 尤其是四肢，能否將四肢參照貓咪行走
    // 姿態來對應移動控制") - Cat.glb has a 43-bone auto-rig but ZERO animation clips (see
    // KNOWN_ISSUES / Docs asset notes), and retargeting a foreign quadruped rig onto a generic
    // Bone_XXX skeleton is fragile. So this drives a procedural quadruped gait directly on the four
    // leg chains, scaled by how fast the cat is actually moving (CharacterMovement's
    // CurrentHorizontalSpeed via ICharacterSpeedSource - the same speed signal CharacterAnimatorLink
    // reads for humanoid locomotion blends). Stationary => eases back to the captured rest pose.
    //
    // Each leg is a "shoulder/hip" bone (swings the whole limb fore-aft) + a "knee/elbow" bone
    // (bends). Rotation is applied as a hinge about the CAT ROOT's right axis, expressed in each
    // bone's parent space, so it works regardless of how the generic bones are individually
    // oriented: bone.localRotation = AngleAxis(deg, rightAxisInParentSpace) * restLocalRotation.
    //
    // Runs in LateUpdate (nothing else writes these bones - there's no Animator on the cat).
    public class CatProceduralWalk : MonoBehaviour
    {
        [Tooltip("The cat's root transform - its right vector is the hinge axis for every leg swing/bend. Falls back to this component's transform.")]
        [SerializeField] private Transform catRoot;

        [Tooltip("Where the movement speed comes from - the cat's CharacterMovement (ICharacterSpeedSource). If unset, resolved from catRoot / this GameObject at Start.")]
        [SerializeField] private MonoBehaviour speedSource;

        [System.Serializable]
        public struct Leg
        {
            [Tooltip("Shoulder (front) or hip (back) bone - the whole limb swings fore-aft here.")]
            public Transform swingBone;
            [Tooltip("Elbow (front) or knee (back) bone - bends as the leg lifts.")]
            public Transform bendBone;
            [Tooltip("0..1 phase within the gait cycle. Diagonal trot = FL/BR at 0, FR/BL at 0.5.")]
            [Range(0f, 1f)] public float phaseOffset;
            [Tooltip("Front legs' elbow bends opposite to back legs' knee - flip this for one pair. Typically +1 for back, -1 for front (tune by eye).")]
            public float bendSign;
        }

        [SerializeField]
        private Leg[] legs = new Leg[0];

        [Header("Gait")]
        [Tooltip("Horizontal speed (units/sec) at which the gait reaches full amplitude and reference stride rate. Match the cat's CharacterMovement.moveSpeed.")]
        [SerializeField] private float speedForFullStride = 3f;
        [Tooltip("Gait cycles per second at speedForFullStride. Phase advance is proportional to actual speed, so stride LENGTH stays roughly constant as the cat speeds up/slows down.")]
        [SerializeField] private float strideFrequency = 1.7f;
        [Tooltip("Peak fore-aft swing of each limb at the shoulder/hip, degrees.")]
        [SerializeField] private float swingDegrees = 16f;
        [Tooltip("Peak bend at the elbow/knee during the swing (lift) phase, degrees.")]
        [SerializeField] private float bendDegrees = 30f;
        [Tooltip("How fast the gait eases in when the cat starts moving and out when it stops (blend units/sec).")]
        [SerializeField] private float blendSpeed = 9f;
        [Tooltip("Small vertical body bob at twice the stride rate, degrees of pitch on the root bone. 0 disables. Optional cosmetic.")]
        [SerializeField] private float bodyBobDegrees = 0f;
        [SerializeField] private Transform bodyBobBone;

        private ICharacterSpeedSource SpeedSource => speedSource as ICharacterSpeedSource;

        private Quaternion[] _swingRest;
        private Quaternion[] _bendRest;
        private Quaternion _bodyBobRest;
        private bool _captured;
        private float _phase;
        private float _gaitBlend;

        // 2026-08-29, cat combat design (Docs/CAT_COMBAT_DESIGN.md 3.8) - CatAttackPose calls
        // SetAttackSuppression every frame: 1 while a swing is in progress (so the front legs
        // aren't being pumped by the gait AND rotated by the attack pose at the same time), 0
        // otherwise, eased so it doesn't pop. Multiplies the gait amplitude, nothing else.
        private float _attackSuppression;
        private float _attackSuppressionTarget;
        [SerializeField] private float attackSuppressionSpeed = 8f;

        // 0 = full gait, 1 = gait amplitude fully suppressed (attack pose owns the legs).
        public void SetAttackSuppression(float target)
        {
            _attackSuppressionTarget = Mathf.Clamp01(target);
        }

        private void Start()
        {
            if (catRoot == null) catRoot = transform;
            if (speedSource == null)
            {
                Transform probe = catRoot != null ? catRoot : transform;
                speedSource = probe.GetComponent<CharacterMovement>();
                if (speedSource == null) speedSource = probe.GetComponentInParent<CharacterMovement>();
            }
            CaptureRest();
        }

        // Public so a rebuild tool (CatCharacterSetup) or a test can re-snapshot after re-wiring
        // the bone references.
        public void CaptureRest()
        {
            _swingRest = new Quaternion[legs.Length];
            _bendRest = new Quaternion[legs.Length];
            for (int i = 0; i < legs.Length; i++)
            {
                _swingRest[i] = legs[i].swingBone != null ? legs[i].swingBone.localRotation : Quaternion.identity;
                _bendRest[i] = legs[i].bendBone != null ? legs[i].bendBone.localRotation : Quaternion.identity;
            }
            _bodyBobRest = bodyBobBone != null ? bodyBobBone.localRotation : Quaternion.identity;
            _captured = true;
        }

        private void LateUpdate()
        {
            if (!_captured || legs.Length == 0)
            {
                return;
            }
            if (_swingRest == null || _swingRest.Length != legs.Length)
            {
                CaptureRest(); // bone array was resized in the Inspector at runtime
            }

            Transform root = catRoot != null ? catRoot : transform;
            float speed = SpeedSource != null ? SpeedSource.CurrentHorizontalSpeed : 0f;
            float speedNorm = Mathf.Clamp01(speed / Mathf.Max(0.01f, speedForFullStride));

            // 2026-08-29, explicit user request ("讓貓就有飛行和衝刺功能 參考player") - while flying,
            // SpeedSource.CurrentHorizontalSpeed is the (much faster) flight cruise speed, which
            // would otherwise drive the leg gait at full amplitude and read as the cat sprinting
            // through the air. Ease the gait back to the captured rest pose instead - a dedicated
            // tucked flight pose is a later slice; for now the legs simply stop pumping.
            bool flying = SpeedSource != null && SpeedSource.IsFlying;
            _gaitBlend = Mathf.MoveTowards(_gaitBlend, ComputeGaitTarget(flying, speedNorm), blendSpeed * Time.deltaTime);
            _phase = Mathf.Repeat(_phase + speedNorm * strideFrequency * Time.deltaTime, 1f);

            _attackSuppression = Mathf.MoveTowards(_attackSuppression, _attackSuppressionTarget, attackSuppressionSpeed * Time.deltaTime);
            // Attack pose (front legs especially) takes over the limbs while swinging - fade the
            // gait amplitude out so the two aren't both writing the same bones. See 3.8.
            float amp = _gaitBlend * (1f - _attackSuppression);

            Vector3 hingeWorld = root.right;

            for (int i = 0; i < legs.Length; i++)
            {
                Leg leg = legs[i];
                float p = (_phase + leg.phaseOffset) * Mathf.PI * 2f;

                if (leg.swingBone != null)
                {
                    // Fore-aft oscillation: cos so the limb is at its forward extreme at phase 0,
                    // rear extreme at 0.5. Pair that with the lift below (lift is on the half of the
                    // cycle where the foot travels rear->front) and each foot plants while it moves
                    // backward (stance push) and lifts while it moves forward (swing recovery).
                    float swing = Mathf.Cos(p) * swingDegrees * amp;
                    ApplyHinge(leg.swingBone, _swingRest[i], hingeWorld, swing);
                }
                if (leg.bendBone != null)
                {
                    // Knee/elbow folds only during the swing half (foot off the ground, recovering
                    // forward) - that's where -sin(p) is positive. bendSign flips it for the front
                    // pair, whose elbow folds opposite the back knee.
                    float bend = Mathf.Max(0f, -Mathf.Sin(p)) * bendDegrees * amp * leg.bendSign;
                    ApplyHinge(leg.bendBone, _bendRest[i], hingeWorld, bend);
                }
            }

            if (bodyBobBone != null && bodyBobDegrees != 0f)
            {
                float bob = Mathf.Sin(_phase * Mathf.PI * 4f) * bodyBobDegrees * amp;
                ApplyHinge(bodyBobBone, _bodyBobRest, hingeWorld, bob);
            }
        }

        // Pure so the "don't pump the legs while flying" rule is directly EditMode-testable
        // without a Play loop (same pure-helper-first pattern as AttackPoseUtility.ComputeSwingAngle
        // and ComboAttackState). 1 = full gait amplitude, 0 = ease back to the rest pose.
        public static float ComputeGaitTarget(bool flying, float speedNorm)
        {
            return (!flying && speedNorm > 0.02f) ? 1f : 0f;
        }

        private static void ApplyHinge(Transform bone, Quaternion restLocal, Vector3 worldAxis, float degrees)
        {
            Transform parent = bone.parent;
            Vector3 axisLocal = parent != null
                ? parent.InverseTransformDirection(worldAxis)
                : worldAxis;
            if (axisLocal.sqrMagnitude < 1e-6f)
            {
                bone.localRotation = restLocal;
                return;
            }
            bone.localRotation = Quaternion.AngleAxis(degrees, axisLocal.normalized) * restLocal;
        }
    }
}
