using UnityEngine;
using Live2DAction.Combat;

namespace Live2DAction.Characters
{
    // Fires the Animator's Attack1/Attack2/Attack3 triggers (see CombatAnimatorSetup.cs) in
    // sync with PlayerCombat's existing frame-data-driven combo state, so the (real,
    // Mixamo-sourced) attack animations play at the same moment ComboAttackState enters a new
    // combo step. Kept as a separate component from PlayerCombat itself, same reasoning as
    // CharacterAnimatorLink - combat logic never needs to know an Animator exists (the training
    // dummy / any future non-visual enemy has PlayerCombat but no Animator at all).
    //
    // 2026-08-12: replaces AttackPoseVisualizer (explicit user request, now that real animation
    // exists) - the two would otherwise fight over the same arm bone every frame, one playing a
    // real clip and the other forcing a procedural rotation on top of it.
    [RequireComponent(typeof(PlayerCombat))]
    public class CharacterAttackAnimationLink : MonoBehaviour
    {
        [SerializeField] private Animator animator;

        private PlayerCombat _combat;
        private int _lastComboIndex = -1;

        private void Awake()
        {
            _combat = GetComponent<PlayerCombat>();
        }

        private void Update()
        {
            if (animator == null || !animator.isActiveAndEnabled || _combat == null)
            {
                return;
            }

            int comboIndex = _combat.ComboIndex;
            if (comboIndex != _lastComboIndex && comboIndex >= 0)
            {
                animator.SetTrigger(TriggerNameForComboIndex(comboIndex));
            }

            _lastComboIndex = comboIndex;
        }

        // Pure so the index->trigger-name mapping is directly EditMode-testable without a
        // live Animator (matches this codebase's established pure-logic-first pattern).
        public static string TriggerNameForComboIndex(int comboIndex)
        {
            switch (comboIndex)
            {
                case 0:
                    return "Attack1";
                case 1:
                    return "Attack2";
                default:
                    return "Attack3";
            }
        }
    }
}
