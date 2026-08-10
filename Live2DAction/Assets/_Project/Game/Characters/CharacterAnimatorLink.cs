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

        private void Awake()
        {
            _movement = GetComponent<CharacterMovement>();
            _speedParameterHash = Animator.StringToHash(speedParameterName);
        }

        private void Update()
        {
            if (animator == null)
            {
                return;
            }

            float parameterValue = ComputeSpeedParameter(_movement.CurrentHorizontalSpeed, maxAnimatorSpeed);
            animator.SetFloat(_speedParameterHash, parameterValue);
        }

        public static float ComputeSpeedParameter(float currentSpeed, float maxAnimatorSpeed)
        {
            return Mathf.Clamp(currentSpeed, 0f, maxAnimatorSpeed);
        }
    }
}
