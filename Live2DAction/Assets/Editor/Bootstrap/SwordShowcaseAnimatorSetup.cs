using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.Characters;

namespace Live2DAction.EditorTools
{
    // 2026-08-19, explicit user request - imports the 4 usable animations from the downloaded
    // "MCO_TC_Sword_Free_Pack_01.zip" (free MotusMan-rig sword pack from mocaponline.com - the
    // zip's UE4_Pack folder is Unreal-only .uasset content, not usable here at all and skipped
    // entirely; only FBX_Pack's 4 real clips - Ready_Idle/Walk/Run/Sword_ATK_Combo, "in place"
    // variants where available since 中立者1 never moves - were extracted into
    // Assets/_Project/Characters/Placeholder/CombatAnimations/TC_Sword_Free_Pack/) and wires them
    // into Maya's shared AnimatorController as 4 new states, same AnyState-transition-gated-by-
    // Trigger pattern CombatAnimatorSetup already established for Attack1-4 - MotusMan's rig
    // auto-mapped to a valid Humanoid Avatar on import (confirmed via reflection before writing
    // this), so these retarget onto 中立者1 the same proven way the Mixamo clips already do,
    // no repeat of the Bake-Axis-Conversion investigation needed this time.
    //
    // 只套用在中立者1 (explicit user choice from a clarifying question - she already runs Maya's
    // shared Humanoid controller with proven-working retargeting; 中立者2/3 run their own native,
    // unrelated controllers and were never in scope for this).
    internal static class SwordShowcaseAnimatorSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string MayaControllerPath = "Assets/_Project/Characters/Placeholder/MayaAnime/Animator/NewAnimator.controller";
        private const string ClipsFolder = "Assets/_Project/Characters/Placeholder/CombatAnimations/TC_Sword_Free_Pack";

        private static readonly (string trigger, string clipFileName)[] ShowcaseSteps =
        {
            ("IdleSword", "KBS_Ready_Idle_001"),
            ("WalkSword", "KBS_Walk_F_001_IP"),
            ("RunSword", "KBS_Run_F_001_IP"),
            ("AttackComboSword", "KBS_Sword_ATK_Combo_01_001_IP"),
        };

        [MenuItem("Tools/Live2DAction/Add Sword Showcase To 中立者1")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject neutral1 = GameObject.Find("中立者1");
            if (neutral1 == null)
            {
                Debug.LogError("中立者1 GameObject not found in " + ScenePath);
                return;
            }

            Animator animator = neutral1.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                Debug.LogError("中立者1 has no Animator on its Visual hierarchy.");
                return;
            }

            var controllerPath = ResolveMayaControllerPath();
            if (controllerPath == null)
            {
                return;
            }

            var clips = new AnimationClip[ShowcaseSteps.Length];
            for (int i = 0; i < ShowcaseSteps.Length; i++)
            {
                string fbxPath = $"{ClipsFolder}/{ShowcaseSteps[i].clipFileName}.fbx";
                AnimationClip clip = LoadRealClip(fbxPath, ShowcaseSteps[i].clipFileName);
                if (clip == null)
                {
                    Debug.LogError($"Could not load AnimationClip '{ShowcaseSteps[i].clipFileName}' at {fbxPath}.");
                    return;
                }
                clips[i] = clip;
            }

            WireController(controllerPath, clips);

            AnimationShowcasePlayer player = neutral1.GetComponent<AnimationShowcasePlayer>();
            if (player == null)
            {
                player = neutral1.AddComponent<AnimationShowcasePlayer>();
            }

            var so = new SerializedObject(player);
            so.FindProperty("animator").objectReferenceValue = animator;

            SerializedProperty clipsProp = so.FindProperty("clips");
            clipsProp.arraySize = ShowcaseSteps.Length;
            SerializedProperty triggersProp = so.FindProperty("triggerNames");
            triggersProp.arraySize = ShowcaseSteps.Length;
            for (int i = 0; i < ShowcaseSteps.Length; i++)
            {
                clipsProp.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
                triggersProp.GetArrayElementAtIndex(i).stringValue = ShowcaseSteps[i].trigger;
            }
            so.FindProperty("pauseBetweenSeconds").floatValue = 0.5f;
            so.FindProperty("playOnStart").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Wired TC Sword Free Pack showcase (Idle/Walk/Run/AttackCombo, 0.5s pause between) onto 中立者1 - plays automatically once on Start, ends back at Idle.");
        }

        // The shared Maya controller's actual project path drifted once already this project
        // (see other Setup scripts' own MayaControllerPath constants) - resolve it by asset
        // search instead of trusting a single hardcoded guess, so this doesn't silently fail if
        // it moves again.
        private static string ResolveMayaControllerPath()
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(MayaControllerPath) != null)
            {
                return MayaControllerPath;
            }

            string[] guids = AssetDatabase.FindAssets("NewAnimator t:AnimatorController");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("MayaAnime"))
                {
                    return path;
                }
            }

            Debug.LogError("Could not locate Maya's shared NewAnimator.controller.");
            return null;
        }

        // FBX sub-assets include a "__preview__"-prefixed duplicate of every real clip (used by
        // the Inspector's own preview player) - LoadAllAssetsAtPath returns both, so this filters
        // to the one whose name matches the file's own base name exactly.
        private static AnimationClip LoadRealClip(string fbxPath, string expectedName)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (asset is AnimationClip clip && clip.name == expectedName)
                {
                    return clip;
                }
            }
            return null;
        }

        private static void WireController(string controllerPath, AnimationClip[] clips)
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

            for (int i = 0; i < ShowcaseSteps.Length; i++)
            {
                (string trigger, string _) = ShowcaseSteps[i];
                AnimationClip clip = clips[i];

                if (!HasParameter(controller, trigger))
                {
                    controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
                }

                AnimatorState state = FindState(stateMachine, trigger);
                if (state == null)
                {
                    state = stateMachine.AddState(trigger);
                }
                state.motion = clip;

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
                    exitTransition.exitTime = 0.95f;
                    exitTransition.duration = 0.15f;
                }
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
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
