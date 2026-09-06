using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Live2DAction.AI.Boss.Yuanpei;

namespace Live2DAction.EditorTools
{
    // 續183e, user request ("boss 開場來個下馬威，長矛/雷射/六連彈三種攻擊瞄準玩家左/中/右，全命中基本
    // 必死") - creates the YuanpeiAttack_OpeningBarrage ScriptableObject (all numbers live there,
    // CLAUDE.md rule 7) and wires it onto the YuanpeiBoss in Map_School. Re-runnable.
    //
    // The barrage is NOT added to attackPool - YuanpeiBoss fires it once, scripted, at the tail of
    // IntroRoutine / BeginEncounter(playIntro:false). Toggle with the boss's `playOpeningBarrage`.
    internal static class YuanpeiOpeningBarrageSetup
    {
        const string ScenePath = "Assets/_Project/Scenes/Map_School.unity";
        const string AssetDir = "Assets/_Project/Settings/Combat/Yuanpei";
        const string AssetPath = AssetDir + "/YuanpeiAttack_OpeningBarrage.asset";

        [MenuItem("Tools/Live2DAction/Setup Yuanpei Opening Barrage (下馬威)")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Exit Play Mode first."); return; }

            // --- 1. the ScriptableObject (create or update) ---
            var def = AssetDatabase.LoadAssetAtPath<YuanpeiAttackDef>(AssetPath);
            bool created = def == null;
            if (created)
            {
                if (!Directory.Exists(AssetDir)) Directory.CreateDirectory(AssetDir);
                def = ScriptableObject.CreateInstance<YuanpeiAttackDef>();
                AssetDatabase.CreateAsset(def, AssetPath);
            }

            def.attackId = YuanpeiAttackId.OpeningBarrage;
            def.displayName = "下馬威 開場齊射";
            def.requiredPhase = 1;
            def.energyCost = 0f;              // scripted, not scheduled
            def.cooldownSeconds = 0f;
            def.minRange = 0f;
            def.maxRange = 999f;
            def.isMajorHazard = true;
            def.telegraphSeconds = 1.1f;     // 下馬威 wind-up (disc DESCENDS + looms + spin-up + red flashes + shake)
            def.windupSeconds = 0.2f;        // 續183h - spacing between spears in the volley (bullets are 2× size)
            def.activeSeconds = 1.2f;        // total volley / beam window
            def.recoverySeconds = 0.5f;
            def.healthDamage = 40f;          // reference only - streams use number1..3
            // 續183h (user: 讓每種攻擊軌跡直線對其 / 不要三種攻擊重疊 / 長矛子彈放大 2× / 間距要調):
            def.number1 = 40f;               // 長矛 per-spear damage (straight LEFT lane, volley of 6 -> 240)
            def.number2 = 18f;               // 雷射 per-tick damage (straight CENTRE beam, ~6 ticks -> ~110)
            def.number3 = 20f;               // 六連彈 per-orb damage (straight RIGHT lane, 12 orbs -> 240)  all-hit ≈ 590
            def.number4 = 0.6f;              // lane offset (m) - L/R separation of the 3 straight lanes
            def.number5 = 21f;               // projectile speed
            def.count = 12;                  // orb count (2 waves; 長矛 volley = ceil(count/2) = 6)
            EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssets();

            // --- 2. wire it onto the boss in Map_School ---
            var scene = EditorSceneManager.GetSceneByPath(ScenePath);
            bool opened = false;
            if (!scene.isLoaded) { scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive); opened = true; }

            var boss = Object.FindObjectsByType<YuanpeiBoss>(FindObjectsSortMode.None)
                             .FirstOrDefault(b => b.gameObject.scene == scene);
            if (boss == null)
            {
                Debug.LogError("YuanpeiOpeningBarrageSetup: no YuanpeiBoss in " + ScenePath + " (asset still created/updated).");
                if (opened) EditorSceneManager.CloseScene(scene, true);
                return;
            }

            var so = new SerializedObject(boss);
            var p = so.FindProperty("openingBarrageDef");
            if (p != null) p.objectReferenceValue = def;
            var pb = so.FindProperty("playOpeningBarrage");
            if (pb != null) pb.boolValue = true;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(boss);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (opened) EditorSceneManager.CloseScene(scene, true);

            Debug.Log($"YuanpeiOpeningBarrageSetup: {(created ? "created" : "updated")} {AssetPath} " +
                      "+ wired onto YuanpeiBoss (openingBarrageDef, playOpeningBarrage=true). " +
                      "續183h: 3 straight lanes (spear-L/laser-C/orb-R), offset 0.6m, all-hit ≈ 6×40 + ~6×18 + 12×20 ≈ 590 dmg.");
        }
    }
}
