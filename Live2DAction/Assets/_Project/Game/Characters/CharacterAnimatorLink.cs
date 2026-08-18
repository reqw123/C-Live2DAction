using UnityEngine;

namespace Live2DAction.Characters
{
    // Drives the Animator's "Speed" parameter from CharacterMovement's actual velocity,
    // so Idle/Walk/Run blend correctly instead of the character standing still while
    // moving. Kept separate from CharacterMovement so movement logic never needs to know
    // an Animator exists (e.g. the training dummy has no visual and no Animator at all).
    //
    // Feeds the raw speed value (clamped, not rescaled) because Maya's Locomotion blend
    // tree's thresholds (0/0.4/0.8/2) are the convention used by these asset-store
    // Mixamo-style controllers for literal units-per-second, matching CharacterMovement's
    // moveSpeed - NOT an arbitrary 0-1 range requiring normalization. An earlier version of
    // this class normalized by moveSpeed and rescaled into 0-2, which silently mismatched
    // the animation's authored pace against the character's actual translation speed and
    // produced visible foot sliding (the clips have no real root motion to cross-check
    // against - they're authored in-place - so this must be tuned by eye, not derived).
    [RequireComponent(typeof(CharacterMovement))]
    public class CharacterAnimatorLink : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private string speedParameterName = "Speed";

        // Ceiling matching the top threshold of the target Animator Controller's
        // locomotion blend tree (Maya's NewAnimator tops out at 2), so a mistuned
        // moveSpeed can't drive the parameter past what any blend state was designed for.
        [SerializeField] private float maxAnimatorSpeed = 2f;

        private CharacterMovement _movement;
        private int _speedParameterHash;

        // 2026-08-18, explicit user request (flight: "接下來我想做飛行功能") - the Animator
        // Controller already had an unused "Fly" bool parameter sitting on it from the imported
        // template (see CharacterMovement.IsFlying's own comment) - this class already exists
        // specifically to drive Animator state from CharacterMovement's own data every frame, so
        // it's the natural place to also wire this one rather than a dedicated link component
        // (unlike Staggered/Attack1-4, nothing else in the project drives Jump/Grounded/Aim
        // either - Fly is just the first of that unused set to actually get consumed).
        private static readonly int FlyParameterHash = Animator.StringToHash("Fly");

        private void Awake()
        {
            _movement = GetComponent<CharacterMovement>();
            _speedParameterHash = Animator.StringToHash(speedParameterName);
        }

        private void Update()
        {
            // isActiveAndEnabled, not just a null check: Animator.SetFloat on a disabled
            // Animator (e.g. its GameObject deactivated - the "Visual" child gets toggled by
            // the first-person camera setup, see ThirdPersonCameraController) doesn't throw,
            // but logs "Animator is not playing an AnimatorController" every single call. This
            // component runs on Player, which stays active regardless, so it kept hammering a
            // disabled Animator every frame during any first-person Play session - 26,000+
            // repeated warnings in one real session, expensive enough (each logs a full stack
            // trace) to be the likely cause of a reported Editor hang. See Docs/KNOWN_ISSUES.md.
            if (animator == null || !animator.isActiveAndEnabled)
            {
                return;
            }

            float parameterValue = ComputeSpeedParameter(_movement.CurrentHorizontalSpeed, maxAnimatorSpeed);
            animator.SetFloat(_speedParameterHash, parameterValue);
            // 2026-08-18: also true while Gliding (see CharacterMovement.IsGliding's own
            // comment) - both are "airborne under wing control", not a normal fall, so the body
            // pose shouldn't suddenly read as a plain fall just because Flight Energy ran out.
            // WingFlap deliberately does NOT follow this same OR - it keys off IsFlying alone so
            // gliding still reads as a gentle idle-rate flap, not the energetic flying one.
            animator.SetBool(FlyParameterHash, _movement.IsFlying || _movement.IsGliding);
        }

        public static float ComputeSpeedParameter(float currentSpeed, float maxAnimatorSpeed)
        {
            return Mathf.Clamp(currentSpeed, 0f, maxAnimatorSpeed);
        }
    }
}
