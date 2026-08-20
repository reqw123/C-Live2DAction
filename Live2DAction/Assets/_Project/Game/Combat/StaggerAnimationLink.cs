using UnityEngine;

namespace Live2DAction.Combat
{
    // 2026-08-17, explicit user request ("陷入僵直時採用蹲下動作") - drives the Animator's
    // "Staggered" bool from StancePoise.IsStaggered every frame. A bool rather than a Trigger
    // (unlike Attack1-4/AttackUltimate/Execute, which are one-shot swings) - the kneeling pose
    // needs to hold for as long as the stagger window stays open (its own duration is decided
    // by StancePoise, not fixed), and a bool naturally drives the Animator back to Locomotion
    // the instant it clears without this class needing to know how long that takes. Kept as its
    // own small component rather than folded into StancePoise itself - same reasoning as
    // CharacterAttackAnimationLink being separate from PlayerCombat: combat/stance logic never
    // needs to know an Animator exists.
    [RequireComponent(typeof(StancePoise))]
    public class StaggerAnimationLink : MonoBehaviour
    {
        [SerializeField] private Animator animator;

        private StancePoise _stance;
        private static readonly int StaggeredParam = Animator.StringToHash("Staggered");

        private void Awake()
        {
            _stance = GetComponent<StancePoise>();
        }

        private void Update()
        {
            if (animator == null || !animator.isActiveAndEnabled)
            {
                return;
            }

            animator.SetBool(StaggeredParam, _stance.IsStaggered);
        }
    }
}
