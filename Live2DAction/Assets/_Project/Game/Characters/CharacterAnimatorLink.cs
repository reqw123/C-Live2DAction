using UnityEngine;

namespace Live2DAction.Characters
{
    // Drives the Animator's "Speed" parameter from the character's actual velocity, so
    // Idle/Walk/Run blend correctly instead of the character standing still while moving.
    // Kept separate from the movement classes themselves so movement logic never needs to
    // know an Animator exists (e.g. the training dummy has no visual and no Animator at all).
    //
    // Feeds the raw speed value (clamped, not rescaled) because Maya's Locomotion blend
    // tree's thresholds (0/0.4/0.8/2) are the convention used by these asset-store
    // Mixamo-style controllers for literal units-per-second, matching CharacterMovement's
    // moveSpeed - NOT an arbitrary 0-1 range requiring normalization. An earlier version of
    // this class normalized by moveSpeed and rescaled into 0-2, which silently mismatched
    // the animation's authored pace against the character's actual translation speed and
    // produced visible foot sliding (the clips have no real root motion to cross-check
    // against - they're authored in-place - so this must be tuned by eye, not derived).
    //
    // 2026-08-20, explicit user request ("敵人的移動動作採用跟玩家一樣地踏步") - used to
    // RequireComponent(CharacterMovement) directly and read from it by concrete type, which
    // meant only Player could ever use this (Enemy has its own entirely separate movement
    // implementation inside EnemyAI, deliberately not CharacterMovement). Generalized to
    // ICharacterSpeedSource - resolved via GetComponent<T>() against an interface, which Unity
    // supports the same as any concrete type, so this now works on either character unchanged.
    public class CharacterAnimatorLink : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private string speedParameterName = "Speed";

        // Ceiling matching the top threshold of the target Animator Controller's
        // locomotion blend tree (Maya's NewAnimator tops out at 2), so a mistuned
        // moveSpeed can't drive the parameter past what any blend state was designed for.
        [SerializeField] private float maxAnimatorSpeed = 2f;

        private ICharacterSpeedSource _speedSource;
        private int _speedParameterHash;

        // 2026-08-18, explicit user request (flight: "接下來我想做飛行功能") - the Animator
        // Controller already had an unused "Fly" bool parameter sitting on it from the imported
        // template (see CharacterMovement.IsFlying's own comment) - this class already exists
        // specifically to drive Animator state from CharacterMovement's own data every frame, so
        // it's the natural place to also wire this one rather than a dedicated link component
        // (unlike Staggered/Attack1-4, nothing else in the project drives Jump/Grounded/Aim
        // either - Fly is just the first of that unused set to actually get consumed).
        private static readonly int FlyParameterHash = Animator.StringToHash("Fly");

        // 2026-08-25, real playtested bug ("動作有哪些以及觸發時機" investigation found the
        // Animator's "Grounded" bool had no writer anywhere in the project - it just sat at its
        // default (true) forever, which made the Fall/Jump states (both gated on Grounded
        // transitions) permanently unreachable dead states. Same wiring pattern as Fly right
        // above - ICharacterSpeedSource.IsGrounded now exposes whichever movement system's own
        // CharacterController.isGrounded, same idiom.
        private static readonly int GroundedParameterHash = Animator.StringToHash("Grounded");

        private void Awake()
        {
            _speedSource = GetComponent<ICharacterSpeedSource>();
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
            if (animator == null || !animator.isActiveAndEnabled || _speedSource == null)
            {
                return;
            }

            float parameterValue = ComputeSpeedParameter(_speedSource.CurrentHorizontalSpeed, maxAnimatorSpeed);
            animator.SetFloat(_speedParameterHash, parameterValue);
            // 2026-08-20, real playtested feedback ("體力條歸0時要快速掉落到地面 停止飛行") - used
            // to also read true while Gliding, but that whole fallback state has been removed
            // (see CharacterMovement.UpdateFlightState's own comment) - running out of energy
            // now falls under normal gravity like any other fall, so the Fly pose should turn
            // off right along with it, not linger through a soft-glide pose that no longer exists.
            // Always false for Enemy (ICharacterSpeedSource.IsFlying) - harmless, Enemy's
            // Animator Controller has the same unused "Fly" bool sitting on it as Player's.
            animator.SetBool(FlyParameterHash, _speedSource.IsFlying);
            // 2026-08-25, user feedback ("在空中時身體好像在比手畫腳...要的是保持直立的姿勢") - the
            // Controller has no dedicated Flying state at all (Fly above is still unused - nothing
            // transitions on it), so as soon as today's Grounded wiring made Fall/Jump reachable,
            // active flight (isGrounded=false, same as any other airborne moment) started dropping
            // into the Fall state's tumbling/flailing-arms clip - correct for an actual
            // uncontrolled fall, wrong for deliberate flight. Feeding IsFlying into Grounded too
            // keeps flight in Locomotion/Idle (already the composed, upright pose the ground fix
            // uses) instead of Fall, while a real fall (not holding flight, e.g. after energy runs
            // out or a knockback) still correctly shows Fall.
            animator.SetBool(GroundedParameterHash, _speedSource.IsGrounded || _speedSource.IsFlying);
        }

        public static float ComputeSpeedParameter(float currentSpeed, float maxAnimatorSpeed)
        {
            return Mathf.Clamp(currentSpeed, 0f, maxAnimatorSpeed);
        }
    }
}
