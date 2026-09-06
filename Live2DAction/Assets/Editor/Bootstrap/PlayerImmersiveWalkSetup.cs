using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Live2DAction.EditorTools
{
    // 2026-09-06, user: "速度正確但是 姿勢還是跑步 能調整與速度匹配的姿勢嗎" - the Alt walk toggle
    // now translates slowly (walkSpeed 0.55) and CharacterAnimatorLink slows the clip, but the clip
    // itself is Maya's NewWalk (0.83s cycle - barely slower than NewRun's 0.70s), so the POSE still
    // reads as a jog. Fix: an AnimatorOverrideController on the PLAYER ONLY that swaps NewWalk for
    // TC_Sword_Free_Pack's KBS_Walk_F_001 (1.17s cycle - a genuinely relaxed stroll), looped. The
    // shared NewAnimator.controller (also used by 中立者1 / 守望者) is untouched.
    //
    // Re-runnable: rebuilds the override asset, re-loops the source clip, re-assigns it on the
    // Player's Visual Animator in GreyboxTest.
    internal static class PlayerImmersiveWalkSetup
    {
        const string BaseControllerPath = "Assets/_Project/Characters/Placeholder/MayaAnime/Animator/NewAnimator.controller";
        const string OverridePath = "Assets/_Project/Characters/Placeholder/MayaAnime/Animator/NewAnimator_PlayerImmersiveWalk.overrideController";
        const string WalkFbxPath = "Assets/_Project/Characters/Placeholder/CombatAnimations/TC_Sword_Free_Pack/KBS_Walk_F_001_IP.fbx";
        const string OriginalClipName = "NewWalk";
        const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";

        [MenuItem("Tools/Live2DAction/Setup Player Immersive Walk Pose")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Exit Play Mode first - this edits an importer + a scene.");
                return;
            }

            AnimationClip strollClip = LoopSourceClip();
            if (strollClip == null) return;

            var baseController = AssetDatabase.LoadAssetAtPath<AnimatorController>(BaseControllerPath);
            if (baseController == null) { Debug.LogError("PlayerImmersiveWalkSetup: base controller missing at " + BaseControllerPath); return; }

            var original = FindClip(baseController, OriginalClipName);
            if (original == null) { Debug.LogError("PlayerImmersiveWalkSetup: '" + OriginalClipName + "' not found in " + BaseControllerPath); return; }

            var ov = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(OverridePath);
            bool isNew = ov == null;
            if (isNew) ov = new AnimatorOverrideController();
            ov.runtimeAnimatorController = baseController;
            ov[original] = strollClip;   // NewWalk -> KBS_Walk_F_001 (relaxed, looped)
            if (isNew) AssetDatabase.CreateAsset(ov, OverridePath);
            else EditorUtility.SetDirty(ov);
            AssetDatabase.SaveAssets();

            AssignToPlayer(ov);
            Debug.Log("PlayerImmersiveWalkSetup: done - Player's Visual Animator now uses " +
                      System.IO.Path.GetFileName(OverridePath) + " (NewWalk -> KBS_Walk_F_001). " +
                      "Tune the pace with CharacterAnimatorLink.walkAnimatorSpeed on the Player.");
        }

        static AnimationClip LoopSourceClip()
        {
            var imp = AssetImporter.GetAtPath(WalkFbxPath) as ModelImporter;
            if (imp == null) { Debug.LogError("PlayerImmersiveWalkSetup: " + WalkFbxPath + " not found / not a model."); return null; }

            var clips = imp.clipAnimations;
            if (clips == null || clips.Length == 0) clips = imp.defaultClipAnimations;
            bool changed = false;
            for (int i = 0; i < clips.Length; i++)
            {
                if (!clips[i].loopTime) { clips[i].loopTime = true; changed = true; }
            }
            if (changed) { imp.clipAnimations = clips; imp.SaveAndReimport(); }

            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(WalkFbxPath))
                if (a is AnimationClip c && !c.name.StartsWith("__")) return c;
            Debug.LogError("PlayerImmersiveWalkSetup: no AnimationClip sub-asset on " + WalkFbxPath);
            return null;
        }

        static AnimationClip FindClip(RuntimeAnimatorController controller, string name)
        {
            foreach (var c in controller.animationClips)
                if (c != null && c.name == name) return c;
            return null;
        }

        static void AssignToPlayer(AnimatorOverrideController ov)
        {
            var scene = EditorSceneManager.GetSceneByPath(ScenePath);
            bool opened = false;
            if (!scene.isLoaded) { scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive); opened = true; }

            var player = GameObject.Find("Player");
            Animator anim = player != null ? player.transform.Find("Visual")?.GetComponent<Animator>() : null;
            if (anim == null)
            {
                Debug.LogError("PlayerImmersiveWalkSetup: Player/Visual Animator not found in " + ScenePath);
                if (opened) EditorSceneManager.CloseScene(scene, true);
                return;
            }

            var so = new SerializedObject(anim);
            so.FindProperty("m_Controller").objectReferenceValue = ov;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(anim);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
    }
}
