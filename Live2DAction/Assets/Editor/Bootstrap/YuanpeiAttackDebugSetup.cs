using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.AI.Boss.Yuanpei;
using Live2DAction.DebugTools;

namespace Live2DAction.EditorTools
{
    // 2026-09-05, user request ("有沒有一種開發者模式 可讓我讓清楚看到boss的每一種攻擊手段的機制 外觀
    // ui 範圍等等 專門用來優化美術系統的") - wires YuanpeiAttackDebugMode (F8) onto yuanpei_LogoSky.
    // Expects Map_School.unity to already be loaded (additively, same as normal play - via
    // Map_School's SceneGate, or opened directly for editing); finds the boss wherever it currently
    // is and parents the new debug GameObject into the SAME scene so it streams/unloads with it.
    // Re-run any time the boss's attackPool changes - the tool reads it live at runtime anyway, this
    // setup only needs to (re)point the object references.
    public static class YuanpeiAttackDebugSetup
    {
        [MenuItem("Tools/Live2DAction/[Debug] Setup Yuanpei Attack Debug Mode")]
        public static void Setup()
        {
            var boss = Object.FindFirstObjectByType<YuanpeiBoss>();
            if (boss == null)
            {
                Debug.LogError("[YuanpeiAttackDebugSetup] No YuanpeiBoss in the loaded scenes - " +
                                "load Map_School.unity first (additive is fine).");
                return;
            }
            var attacks = boss.GetComponent<YuanpeiAttacks>();
            var hud = boss.GetComponent<YuanpeiBossHUD>();
            Scene targetScene = boss.gameObject.scene;

            var host = GameObject.Find("YuanpeiAttackDebugMode");
            if (host == null || host.scene != targetScene)
            {
                if (host != null) Undo.DestroyObjectImmediate(host);
                host = new GameObject("YuanpeiAttackDebugMode");
                SceneManager.MoveGameObjectToScene(host, targetScene);
                Undo.RegisterCreatedObjectUndo(host, "create YuanpeiAttackDebugMode");
            }

            var mode = host.GetComponent<YuanpeiAttackDebugMode>() ?? Undo.AddComponent<YuanpeiAttackDebugMode>(host);
            var so = new SerializedObject(mode);
            so.FindProperty("boss").objectReferenceValue = boss;
            so.FindProperty("attacks").objectReferenceValue = attacks;
            so.FindProperty("hud").objectReferenceValue = hud;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(host);
            EditorSceneManager.MarkSceneDirty(targetScene);
            Debug.Log("[YuanpeiAttackDebugSetup] wired - boss=" + boss.name + " attacks=" + (attacks != null)
                      + " hud=" + (hud != null) + " pool size=" + (boss.AttackPool != null ? boss.AttackPool.Count : 0)
                      + ". Press F8 in Play mode to toggle.");
        }
    }
}
