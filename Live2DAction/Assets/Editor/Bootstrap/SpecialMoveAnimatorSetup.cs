using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Live2DAction.EditorTools
{
    // Wires one-off special-move Animator states that, unlike CombatAnimatorSetup's Attack1-4,
    // don't automatically belong to both characters.
    //
    // "AttackUltimate" (Arisa/Player4 only, 2026-08-17) - EnemyUltimateAbility's Breakdance
    // finisher. Player4-exclusive mechanic (this session's own scoping decision), so wiring it
    // into Maya's controller too would just be permanently unused dead weight.
    //
    // "Execute" (2026-08-17, originally Maya/player only for ExecutionAbility's Flying Kick
    // deathblow; 2026-08-17 follow-up explicit user request "敵我雙方都套用...處刑" made the
    // stagger/execution mechanic symmetric - EnemyExecutionAbility reuses this same trigger/clip
    // on Player4 too, so this now wires into BOTH controllers, same "one clip, both rigs" reuse
    // CombatAnimatorSetup's Attack1-4 already established).
    //
    // "Staggered" (2026-08-17, explicit user request "陷入僵直時採用蹲下動作", now on both
    // characters per the same follow-up) - a BOOL, not a Trigger, wired into both controllers;
    // see WireBoolState's own comment for why this needs different transition wiring than
    // every trigger-driven state above.
    internal static class SpecialMoveAnimatorSetup
    {
        private const string ClipsFolder = "Assets/_Project/Characters/Placeholder/CombatAnimations/Mixamo";
        private const string MayaControllerPath = "Assets/_Project/Characters/Placeholder/MayaAnime/Animator/NewAnimator.controller";
        private const string ArisaControllerPath = "Assets/_Project/Characters/Placeholder/ArisaAnime/Animator/NewAnimator.controller";

        [MenuItem("Tools/Live2DAction/Wire Special Move Animations")]
        public static void Apply()
        {
            // BreakdanceUltimate is a short (0.5s raw) flourish, not a strike with an obvious
            // impact frame (see EnemyUltimateAttack.asset's own comment/measurement) - slowed to
            // 0.6x so it reads with a bit more weight as an "ultimate" rather than a blink-and-
            // miss-it flick.
            WireState(ArisaControllerPath, "AttackUltimate", "BreakdanceUltimate", speed: 0.6f);
            WireState(MayaControllerPath, "Execute", "FlyingKick", speed: 1f);
            WireState(ArisaControllerPath, "Execute", "FlyingKick", speed: 1f);

            WireBoolState(MayaControllerPath, "Staggered", "KneelingDown");
            WireBoolState(ArisaControllerPath, "Staggered", "KneelingDown");
        }

        private static void WireState(string controllerPath, string trigger, string clipName, float speed)
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

            if (!HasParameter(controller, trigger))
            {
                controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
            }

            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{ClipsFolder}/{clipName}.fbx");
            if (clip == null)
            {
                Debug.LogError($"Could not load AnimationClip '{clipName}' - run [Tool] Configure Mixamo Combat Animations As Humanoid first.");
                return;
            }

            AnimatorState state = FindState(stateMachine, trigger);
            if (state == null)
            {
                state = stateMachine.AddState(trigger);
            }
            state.motion = clip;
            state.speed = speed;

            if (!HasAnyStateTransitionTo(stateMachine, state))
            {
                AnimatorStateTransition enterTransition = stateMachine.AddAnyStateTransition(state);
                enterTransition.AddCondition(AnimatorConditionMode.If, 0, trigger);
                enterTransition.duration = 0.05f;
                enterTransition.hasExitTime = false;
                enterTransition.canTransitionToSelf = false;
            }

            if (!HasExitTransitionTo(state, locomotion))
            {
                AnimatorStateTransition exitTransition = state.AddTransition(locomotion);
                exitTransition.hasExitTime = true;
                exitTransition.exitTime = 0.9f;
                exitTransition.duration = 0.15f;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log($"Wired '{trigger}' ({clipName}, speed={speed}) into {controllerPath}");
        }

        // Bool-driven, unlike WireState's Trigger-driven states above: KneelingDown needs to
        // hold for as long as StancePoise.IsStaggered stays true (an unknown, StancePoise-owned
        // duration - see StaggerAnimationLink), not play once and hand back control after a
        // fixed exitTime fraction the way a one-shot swing does. So this wires BOTH directions
        // explicitly instead of relying on WireState's fixed-exitTime return transition: AnyState
        // -> Staggered while the bool is true (can interrupt literally anything, including a
        // mid-swing attack), and Staggered -> Locomotion the instant the bool goes false again
        // (hasExitTime=false - StancePoise, not the Animator, decides when that happens).
        private static void WireBoolState(string controllerPath, string boolParam, string clipName)
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

            if (!HasParameter(controller, boolParam))
            {
                controller.AddParameter(boolParam, AnimatorControllerParameterType.Bool);
            }

            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{ClipsFolder}/{clipName}.fbx");
            if (clip == null)
            {
                Debug.LogError($"Could not load AnimationClip '{clipName}' - run [Tool] Configure Mixamo Combat Animations As Humanoid first.");
                return;
            }

            AnimatorState state = FindState(stateMachine, boolParam);
            if (state == null)
            {
                state = stateMachine.AddState(boolParam);
            }
            state.motion = clip;

            if (!HasAnyStateTransitionTo(stateMachine, state))
            {
                AnimatorStateTransition enterTransition = stateMachine.AddAnyStateTransition(state);
                enterTransition.AddCondition(AnimatorConditionMode.If, 0, boolParam);
                enterTransition.duration = 0.1f;
                enterTransition.hasExitTime = false;
                enterTransition.canTransitionToSelf = false;
            }

            if (!HasExitTransitionTo(state, locomotion))
            {
                AnimatorStateTransition exitTransition = state.AddTransition(locomotion);
                exitTransition.AddCondition(AnimatorConditionMode.IfNot, 0, boolParam);
                exitTransition.hasExitTime = false;
                exitTransition.duration = 0.15f;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log($"Wired bool '{boolParam}' ({clipName}) into {controllerPath}");
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
