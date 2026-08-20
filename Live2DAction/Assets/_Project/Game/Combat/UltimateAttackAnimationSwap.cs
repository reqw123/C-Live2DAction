using UnityEngine;

namespace Live2DAction.Combat
{
    // 2026-08-17, explicit user request ("玩家開啟必殺技期間，攻擊動作改為Zombie Punching，結束後
    // 恢復") - swaps Attack1's clip (normally CrossPunch) for Zombie Punching for the duration of
    // UltimateAbility's buff window, then reverts. Uses an AnimatorOverrideController rather
    // than writing AnimatorState.motion directly: that write only works through
    // UnityEditor.Animations.AnimatorController, an Editor-only API not available in a build
    // (see CombatAnimatorSetup, which is an Assets/Editor/Bootstrap author-time tool for exactly
    // that reason), and even in-Editor it would mutate the shared AnimatorController ASSET
    // itself rather than just this one Animator instance. AnimatorOverrideController is the
    // runtime-safe, per-Animator way to remap a clip.
    [RequireComponent(typeof(UltimateAbility))]
    public class UltimateAttackAnimationSwap : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private AnimationClip ultimateAttack1Clip;

        // The ORIGINAL clip's name as authored on the Attack1 state (see
        // CombatAnimationImportSetup, which renames each imported clip to match its own file
        // name) - AnimatorOverrideController's indexer keys off this, not the state's name.
        [SerializeField] private string originalClipName = "CrossPunch";

        private UltimateAbility _ultimate;
        private RuntimeAnimatorController _originalController;
        private AnimatorOverrideController _overrideController;
        private bool _swapped;

        private void Awake()
        {
            _ultimate = GetComponent<UltimateAbility>();

            if (animator == null || ultimateAttack1Clip == null)
            {
                return;
            }

            _originalController = animator.runtimeAnimatorController;
            if (_originalController == null)
            {
                return;
            }

            _overrideController = new AnimatorOverrideController(_originalController);
            _overrideController[originalClipName] = ultimateAttack1Clip;
        }

        private void Update()
        {
            if (_overrideController == null)
            {
                return;
            }

            bool shouldBeSwapped = _ultimate.IsActive;
            if (shouldBeSwapped == _swapped)
            {
                return;
            }

            animator.runtimeAnimatorController = shouldBeSwapped ? _overrideController : _originalController;
            _swapped = shouldBeSwapped;
        }
    }
}
