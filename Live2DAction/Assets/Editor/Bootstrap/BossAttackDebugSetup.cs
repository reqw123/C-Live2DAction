using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Live2DAction.DebugTools;
using Live2DAction.AI.Boss;

namespace Live2DAction.EditorTools
{
    // 2026-09-06, user request ("把元培boss f8模式裡面的機制設計 用同樣形式修改武士boss的f7模式") -
    // wires the reworked BossAttackDebugMode (F7) into GreyboxTest: finds 武士 + 屁孩王 and their
    // BossStateMachine components. Replaces the old BossAnimationDebugSetup (which also collected
    // Animator state names + a 守望者 debug camera - the reworked F7 keeps the player in control
    // and fires real pool attacks, so none of that is needed any more).
    public static class BossAttackDebugSetup
    {
        [MenuItem("Tools/Live2DAction/[Debug] Setup Boss Attack Debug Mode (F7)")]
        public static void Setup()
        {
            var scene = EditorSceneManager.GetActiveScene();

            // clean up the old tool's GameObject if it's still around
            var old = GameObject.Find("BossAnimationDebugMode");
            if (old != null) Undo.DestroyObjectImmediate(old);

            var host = GameObject.Find("BossAttackDebugMode");
            if (host == null)
            {
                host = new GameObject("BossAttackDebugMode");
                Undo.RegisterCreatedObjectUndo(host, "create BossAttackDebugMode");
            }
            var mode = host.GetComponent<BossAttackDebugMode>() ?? Undo.AddComponent<BossAttackDebugMode>(host);
            var so = new SerializedObject(mode);

            var entries = new[]
            {
                ("武士", GameObject.Find("武士")),
                ("屁孩王", GameObject.Find("屁孩王")),
            };

            var targetsProp = so.FindProperty("targets");
            targetsProp.ClearArray();
            int wired = 0;
            foreach (var (label, go) in entries)
            {
                if (go == null) { Debug.LogWarning($"[BossAttackDebugSetup] '{label}' not in scene - skipped."); continue; }
                var fsm = go.GetComponent<BossStateMachine>() ?? go.GetComponentInChildren<BossStateMachine>(true);
                if (fsm == null) { Debug.LogWarning($"[BossAttackDebugSetup] '{label}' has no BossStateMachine - skipped."); continue; }

                targetsProp.InsertArrayElementAtIndex(targetsProp.arraySize);
                var e = targetsProp.GetArrayElementAtIndex(targetsProp.arraySize - 1);
                e.FindPropertyRelative("label").stringValue = label;
                e.FindPropertyRelative("boss").objectReferenceValue = fsm;

                int poolCount = fsm.DebugAttackPool != null ? fsm.DebugAttackPool.Count : 0;
                Debug.Log($"[BossAttackDebugSetup] {label}: BossStateMachine wired, {poolCount} pool attacks.");
                wired++;
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(mode);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[BossAttackDebugSetup] done - {wired} boss(es) wired. Press F7 in Play to toggle; " +
                      "digits fire the real attack pool at the dummy, Tab cycles boss, G toggles rings.");
        }
    }
}
