using UnityEngine;

namespace Live2DAction.Characters
{
    // Drives the Animator's "Speed" parameter from CharacterMovement's actual velocity,
    // so Idle/Walk/Run blend correctly instead of the character standing still while
    // moving. Kept separate from CharacterMovement so movement logic never needs to know
    // an Animator exists (e.g. the training dummy has no visual and no Animator at all).
    [RequireComponent(typeof(CharacterMovement))]
    public class CharacterAnimatorLink : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private string speedParameterName = "Speed";

        // Value the Animator's Speed parameter should read when CharacterMovement is at
        // its full moveSpeed - matches whatever top threshold the Animator Controller's
        // locomotion blend tree actually uses (Maya's NewAnimator tops out at 2).
        [SerializeField] private float speedParameterScale = 2f;

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

            float parameterValue = ComputeSpeedParameter(_movement.CurrentHorizontalSpeed, _movement.MoveSpeed, speedParameterScale);
            animator.SetFloat(_speedParameterHash, parameterValue);
        }

        public static float ComputeSpeedParameter(float currentSpeed, float moveSpeed, float speedParameterScale)
        {
            if (moveSpeed <= 0.0001f)
            {
                return 0f;
            }

            float normalized = Mathf.Clamp01(currentSpeed / moveSpeed);
            return normalized * speedParameterScale;
        }
    }
}
