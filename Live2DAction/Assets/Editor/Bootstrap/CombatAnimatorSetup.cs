using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Live2DAction.EditorTools
{
    // Wires the 3 Mixamo combo animations (see CombatAnimationImportSetup.cs) into BOTH
    // Maya's and Arisa's Animator Controllers as Attack1/Attack2/Attack3 states, driven by
    // Trigger parameters of the same names. 2026-08-12, explicit user request - replaces the
    // old AttackPoseVisualizer placeholder (procedural bone-rotation swing) now that real
    // animation exists; see CharacterAttackAnimationLink.cs for what fires these triggers in
    // sync with PlayerCombat's existing frame-data-driven combo state.
    //
    // Each Humanoid AnimationClip works with either character's Animator regardless of which
    // model's skeleton the source FBX was originally imported against - that's the entire
    // point of Unity's Humanoid retargeting system, so the same 3 clips are reused for both
    // rather than needing separate copies per character.
    internal static class CombatAnimatorSetup
    {
        private const string ClipsFolder = "Assets/_Project/Characters/Placeholder/CombatAnimations/Mixamo";
        private static readonly string[] MayaControllerPath = { "Assets/_Project/Characters/Placeholder/MayaAnime/Animator/NewAnimator.controller" };
        private static readonly string[] ArisaControllerPath = { "Assets/_Project/Characters/Placeholder/ArisaAnime/Animator/NewAnimator.controller" };

        private static readonly (string trigger, string clipName)[] ComboSteps =
        {
            ("Attack1", "CrossPunch"),
            ("Attack2", "HookPunch"),
            ("Attack3", "Uppercut"),
        };

        [MenuItem("Tools/Live2DAction/Wire Combat Animations Into Both Animator Controllers")]
        public static void Apply()
        {
            WireController(MayaControllerPath[0]);
            WireController(ArisaControllerPath[0]);
        }

        private static void WireController(string controllerPath)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                Debug.LogError("Could not load AnimatorController at " + controllerPath);
                return;
            }

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState locomotion = FindState(stateMachine, "Locomotion");
            if (locomotion == null)
            {
                Debug.LogError("Could not find 'Locomotion' state in " + controllerPath);
                return;
            }

            foreach ((string trigger, string clipName) in ComboSteps)
            {
                if (!HasParameter(controller, trigger))
                {
                    controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
                }

                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{ClipsFolder}/{clipName}.fbx");
                if (clip == null)
                {
                    Debug.LogError($"Could not load AnimationClip '{clipName}' - run [Tool] Configure Mixamo Combat Animations As Humanoid first.");
                    continue;
                }

                AnimatorState attackState = FindState(stateMachine, trigger);
                if (attackState == null)
                {
                    attackState = stateMachine.AddState(trigger);
                }
                attackState.motion = clip;

                // AnyState -> Attack, gated on the trigger - lets an attack interrupt whatever
                // the character is currently doing (idle, walking) the instant it's pressed,
                // matching how ComboAttackState's own Startup phase begins immediately on
                // input rather than waiting for a "safe" moment.
                if (!HasAnyStateTransitionTo(stateMachine, attackState))
                {
                    AnimatorStateTransition enterTransition = stateMachine.AddAnyStateTransition(attackState);
                    enterTransition.AddCondition(AnimatorConditionMode.If, 0, trigger);
                    enterTransition.duration = 0.05f;
                    enterTransition.hasExitTime = false;
                    enterTransition.canTransitionToSelf = false;
                }

                // Attack -> Locomotion once the clip has (almost) finished - no exit-time
                // condition needed beyond that, so the character always settles back into
                // Idle/Walk/Run on its own once the swing plays out, even if
                // CharacterAttackAnimationLink never explicitly tells it to leave.
                if (!HasExitTransitionTo(attackState, locomotion))
                {
                    AnimatorStateTransition exitTransition = attackState.AddTransition(locomotion);
                    exitTransition.hasExitTime = true;
                    exitTransition.exitTime = 0.9f;
                    exitTransition.duration = 0.15f;
                }
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log("Wired Attack1/Attack2/Attack3 states into " + controllerPath);
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

        private static bool HasParameter(AnimatorController controller, string name)
        {
            foreach (AnimatorControllerParameter parameter in controller.parameters)
            {
                if (parameter.name == name)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasAnyStateTransitionTo(AnimatorStateMachine stateMachine, AnimatorState target)
        {
            foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions)
            {
                if (transition.destinationState == target)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasExitTransitionTo(AnimatorState from, AnimatorState target)
        {
            foreach (AnimatorStateTransition transition in from.transitions)
            {
                if (transition.destinationState == target)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
