using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Live2DAction.EditorTools
{
    // Speeds up the Attack1/Attack2/Attack3 AnimatorStates CombatAnimatorSetup wired into
    // Maya's NewAnimator.controller (shared by Player5 too, see Player5VisualSetup's own
    // comment on why one controller covers both) - 2026-08-13 explicit user request ("出拳
    // 更快"), chosen over sourcing new animation clips because ComboAttackState's own hit
    // timing (LightAttack1/2/3's Startup+Active+Recovery frames) already resolves well
    // inside the Mixamo clips' default 1x playback length, so the mismatch reads as "the
    // swing drags after the hit already landed" rather than the hit judgment itself being
    // slow - AnimatorState.speed is a straight visual-playback-rate multiplier, so this
    // doesn't touch AttackData/ComboAttackState at all. Only Maya's controller: Arisa's
    // (the enemy's) is left untouched since the request was about the player's own attacks.
    internal static class SpeedUpAttackAnimations
    {
        private const string MayaControllerPath = "Assets/_Project/Characters/Placeholder/MayaAnime/Animator/NewAnimator.controller";

        private static readonly (string state, float speed)[] Speeds =
        {
            ("Attack1", 1.4f),
            ("Attack2", 1.4f),
            ("Attack3", 1.3f),
        };

        [MenuItem("Tools/Live2DAction/Speed Up Player Attack Animations")]
        public static void Apply()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(MayaControllerPath);
            if (controller == null)
            {
                Debug.LogError("Could not load AnimatorController at " + MayaControllerPath);
                return;
            }

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            foreach ((string stateName, float speed) in Speeds)
            {
                AnimatorState state = FindState(stateMachine, stateName);
                if (state == null)
                {
                    Debug.LogError($"Could not find state '{stateName}' in {MayaControllerPath} - run Wire Combat Animations Into Both Animator Controllers first.");
                    continue;
                }

                state.speed = speed;
                Debug.Log($"{stateName}.speed = {speed}");
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log("Sped up Player attack animations in " + MayaControllerPath);
        }

        private static AnimatorState FindState(AnimatorStateMachine stateMachine, string name)
        {
            foreach (ChildAnimatorState child in stateMachine.states)
            {
                if (child.state.name == name)
                {
                    return child.state;
                }
            }

            return null;
        }
    }
}
