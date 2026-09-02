using UnityEngine;
using Live2DAction.Combat;

namespace Live2DAction.Characters
{
    // 2026-08-29, cat combat design (Docs/CAT_COMBAT_DESIGN.md 3.4). The cat has no lock-on, so
    // PlayerCombat can't auto-detect a vertically-offset target the way the player does (via
    // lockOnSource). This just flips PlayerCombat.UseSphericalJudgment on whenever the cat is
    // flying or otherwise off the ground - so an air swipe uses an omnidirectional sphere around
    // attackOrigin (lands regardless of body pitch/facing) instead of the forward capsule, then
    // switches back to the directional capsule the instant the cat lands. Same mechanism the
    // player's aerial combat already uses, driven by a different condition.
    //
    // Separate one-line component (not folded into PlayerCombat) so PlayerCombat stays free of
    // any movement dependency - same "combat never needs to know an Animator/CharacterMovement
    // exists" convention as CharacterAttackAnimationLink / CharacterAnimatorLink.
    [DefaultExecutionOrder(-10)] // set the judgment shape before PlayerCombat.Update reads it
    [RequireComponent(typeof(PlayerCombat))]
    public class CatAerialJudgment : MonoBehaviour
    {
        [Tooltip("The cat's CharacterMovement (ICharacterSpeedSource). If unset, resolved from this GameObject at Start.")]
        [SerializeField] private MonoBehaviour speedSource;

        private PlayerCombat _combat;
        private ICharacterSpeedSource Speed => speedSource as ICharacterSpeedSource;

        private void Awake()
        {
            _combat = GetComponent<PlayerCombat>();
        }

        private void Start()
        {
            if (speedSource == null)
            {
                speedSource = GetComponent<CharacterMovement>();
            }
        }

        private void Update()
        {
            if (_combat == null || Speed == null)
            {
                return;
            }
            _combat.UseSphericalJudgment = Speed.IsFlying || !Speed.IsGrounded;
        }
    }
}
