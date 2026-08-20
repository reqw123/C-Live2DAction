using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Live2DAction.EditorTools
{
    // 2026-08-19, explicit user request ("三個角色沒有硬直 蹲下 死亡動畫") - 中立者1 already has
    // real Staggered/Dead states (she shares Maya's controller, which DeathAnimationSetup/
    // SpecialMoveAnimatorSetup already wired long before today - verified via reflection before
    // writing this class, untouched by today's sword-showcase change) - most likely just never
    // actually observed yet since nothing has landed a real hit on her. 中立者2/3 are the real
    // gap: each runs its OWN native AnimatorController (deliberate choice - see
    // NeutralCharacterSetup.cs's own class comment for why cross-rig retargeting wasn't
    // attempted for them originally), which never had Staggered/Dead states at all - so
    // StaggerAnimationLink/DeathAnimationLink's SetBool("Staggered")/SetTrigger("Dead") calls
    // were harmless no-ops on both, exactly the accepted trade-off documented back then.
    //
    // 中立者2 (Haon/Misaki): her Avatar auto-mapped to a valid HUMANOID rig on import (confirmed
    // via reflection) - safe to reuse Maya's own KneelingDown/Dying clips directly through the
    // same cross-rig Humanoid retargeting already proven twice this session (Mixamo clips across
    // Maya/Arisa, then the TC Sword pack onto 中立者1). No animations of Haon's own pack fit
    // (searched for Down/Damage/Faint/KO - the pack is a life-sim/adventure set with no combat-
    // hit-reaction content at all), so this is the only real option short of authoring new clips.
    //
    // 中立者3 (SapphiArtchan): her Avatar is GENERIC, not Humanoid (confirmed via reflection) -
    // Maya's clips would NOT retarget onto her reliably, so this uses her OWN already-imported
    // "damage"/"KO_big" clips instead (same skeleton, zero retargeting risk). Deliberately does
    // NOT reuse her prefab's existing idle-hub transition graph (idle -> damage -> idle etc.,
    // gated on oddly-inverted param_idletoXIf0/IfNot0 bool conditions) - that graph was built to
    // be driven by the SapphiArtChan_AnimManager script this project already removed (see
    // NeutralCharacterSetup.RemoveForeignAnimationManagers) and is only reachable FROM the idle
    // state in the first place (no AnyState transitions at all), so it can't preempt whatever
    // state she happens to be in when a real stagger/death event fires. Adds independent
    // AnyState-gated states instead, wired the exact same way as everything else in this project.
    internal static class NeutralStaggerDeathAnimatorSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string MayaControllerPath = "Assets/_Project/Characters/Placeholder/MayaAnime/Animator/NewAnimator.controller";
        private const string SapphiControllerPath = "Assets/SapphiArt/SapphiArtchan/Animation/SapphiArtchanAnimController.controller";

        private const string StaggeredParam = "Staggered";
        private const string DeadParam = "Dead";

        [MenuItem("Tools/Live2DAction/Add Stagger+Death Animations To 中立者2 And 中立者3")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            AnimatorController mayaController = AssetDatabase.LoadAssetAtPath<AnimatorController>(MayaControllerPath);
            if (mayaController == null)
            {
                Debug.LogError("Could not load Maya's AnimatorController at " + MayaControllerPath);
                return;
            }
            AnimationClip kneelClip = FindStateMotion(mayaController, "Staggered");
            AnimationClip dyingClip = FindStateMotion(mayaController, "Dead");
            if (kneelClip == null || dyingClip == null)
            {
                Debug.LogError("Could not find Maya's own Staggered/Dead state motions to reuse.");
                return;
            }

            // --- 中立者2: cross-rig Humanoid retarget of Maya's own KneelingDown/Dying clips ---
            GameObject neutral2 = GameObject.Find("中立者2");
            if (neutral2 != null)
            {
                Animator animator2 = neutral2.GetComponentInChildren<Animator>();
                AnimatorController controller2 = animator2 != null ? animator2.runtimeAnimatorController as AnimatorController : null;
                if (controller2 != null)
                {
                    WireStaggerDeath(controller2, kneelClip, dyingClip, "StandB@Loop");
                }
                else
                {
                    Debug.LogError("中立者2 has no AnimatorController to wire.");
                }
            }
            else
            {
                Debug.LogError("中立者2 GameObject not found in " + ScenePath);
            }

            // --- 中立者3: her own already-imported damage/KO_big clips (same rig, no retargeting) ---
            AnimatorController sapphiController = AssetDatabase.LoadAssetAtPath<AnimatorController>(SapphiControllerPath);
            if (sapphiController != null)
            {
                AnimationClip damageClip = FindStateMotion(sapphiController, "damage");
                AnimationClip koClip = FindStateMotion(sapphiController, "KO_big");
                if (damageClip != null && koClip != null)
                {
                    WireStaggerDeath(sapphiController, damageClip, koClip, "idle");
                }
                else
                {
                    Debug.LogError("Could not find 中立者3's own 'damage'/'KO_big' state motions.");
                }
            }
            else
            {
                Debug.LogError("Could not load SapphiArtchan's AnimatorController at " + SapphiControllerPath);
            }

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Wired real Staggered (bool, kneel/hurt pose) and Dead (trigger, KO pose) states onto 中立者2 and 中立者3's own AnimatorControllers.");
        }

        private static AnimationClip FindStateMotion(AnimatorController controller, string stateName)
        {
            foreach (ChildAnimatorState cs in controller.layers[0].stateMachine.states)
            {
                if (cs.state.name == stateName)
                {
                    return cs.state.motion as AnimationClip;
                }
            }
            return null;
        }

        // Same AnyState-gated-by-parameter pattern this project already uses everywhere
        // (CombatAnimatorSetup's Attack1-4, SwordShowcaseAnimatorSetup's showcase states, Maya's
        // own Staggered/Dead) - Staggered is a bool (holds the pose for as long as
        // StancePoise.IsStaggered stays true, same reasoning as StaggerAnimationLink's own
        // comment), Dead is a Trigger (one-shot, never needs to un-fire).
        private static void WireStaggerDeath(AnimatorController controller, AnimationClip staggerClip, AnimationClip deadClip, string idleStateName)
        {
            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState idle = FindState(sm, idleStateName);
            if (idle == null)
            {
                Debug.LogError($"Could not find '{idleStateName}' state in {controller.name} to use as the return-to state.");
                return;
            }

            if (!HasParameter(controller, StaggeredParam))
            {
                controller.AddParameter(StaggeredParam, AnimatorControllerParameterType.Bool);
            }
            if (!HasParameter(controller, DeadParam))
            {
                controller.AddParameter(DeadParam, AnimatorControllerParameterType.Trigger);
            }

            AnimatorState staggerState = FindState(sm, StaggeredParam);
            if (staggerState == null)
            {
                staggerState = sm.AddState(StaggeredParam);
            }
            staggerState.motion = staggerClip;

            if (!HasAnyStateTransitionTo(sm, staggerState))
            {
                AnimatorStateTransition enter = sm.AddAnyStateTransition(staggerState);
                enter.AddCondition(AnimatorConditionMode.If, 0, StaggeredParam);
                enter.duration = 0.05f;
                enter.hasExitTime = false;
                enter.canTransitionToSelf = false;
            }
            if (!HasConditionalExitTransitionTo(staggerState, idle))
            {
                AnimatorStateTransition exit = staggerState.AddTransition(idle);
                exit.AddCondition(AnimatorConditionMode.IfNot, 0, StaggeredParam);
                exit.hasExitTime = false;
                exit.duration = 0.15f;
            }

            AnimatorState deadState = FindState(sm, DeadParam);
            if (deadState == null)
            {
                deadState = sm.AddState(DeadParam);
            }
            deadState.motion = deadClip;

            if (!HasAnyStateTransitionTo(sm, deadState))
            {
                AnimatorStateTransition enter = sm.AddAnyStateTransition(deadState);
                enter.AddCondition(AnimatorConditionMode.If, 0, DeadParam);
                enter.duration = 0.05f;
                enter.hasExitTime = false;
                enter.canTransitionToSelf = false;
            }
            // No exit transition for Dead - matches Maya's own controller (a dead character stays
            // on its last pose; DeathAnimationLink deactivates the whole GameObject shortly after
            // anyway, same as every other character).

            EditorUtility.SetDirty(controller);
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

        private static bool HasConditionalExitTransitionTo(AnimatorState from, AnimatorState target)
        {
            foreach (AnimatorStateTransition transition in from.transitions)
            {
                if (transition.destinationState == target && !transition.hasExitTime)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
