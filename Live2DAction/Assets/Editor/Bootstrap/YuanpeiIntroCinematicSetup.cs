using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Live2DAction.AI.Boss.Yuanpei;

namespace Live2DAction.EditorTools
{
    // 2026-09-06 (續 180), user request - wires the 6-beat yuanpei_LogoSky intro cutscene into
    // Map_School: a YuanpeiIntroCinematic component on the YuanpeiEncounter object + its same-scene
    // refs (boss, domainVfx), and YuanpeiEncounter.introCinematic pointing at it. Re-runnable.
    //
    // The player + camera control scripts are NOT wired here - the player lives in the persistent
    // GreyboxTest scene, so a cross-scene serialized reference can't be saved. YuanpeiIntroCinematic
    // resolves them by type at runtime instead.
    //
    // Run "Setup Boss Domain Screen VFX" first (it creates the night skybox + domain component the
    // cinematic drives).
    internal static class YuanpeiIntroCinematicSetup
    {
        const string ScenePath = "Assets/_Project/Scenes/Map_School.unity";

        [MenuItem("Tools/Live2DAction/Setup Yuanpei Intro Cinematic (續180)")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Exit Play Mode first."); return; }

            var scene = EditorSceneManager.GetSceneByPath(ScenePath);
            bool opened = false;
            if (!scene.isLoaded) { scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive); opened = true; }

            var encounter = Object.FindObjectsByType<YuanpeiEncounter>(FindObjectsSortMode.None)
                                  .FirstOrDefault(e => e.gameObject.scene == scene);
            var boss = Object.FindObjectsByType<YuanpeiBoss>(FindObjectsSortMode.None)
                             .FirstOrDefault(b => b.gameObject.scene == scene);
            if (encounter == null || boss == null)
            {
                Debug.LogError("YuanpeiIntroCinematicSetup: YuanpeiEncounter / YuanpeiBoss not found in " + ScenePath);
                if (opened) EditorSceneManager.CloseScene(scene, true);
                return;
            }

            var cine = encounter.GetComponent<YuanpeiIntroCinematic>()
                       ?? encounter.gameObject.AddComponent<YuanpeiIntroCinematic>();

            var domain = boss.GetComponent<BossDomainScreenVFX>();
            var so = new SerializedObject(cine);
            SetRef(so, "boss", boss);
            SetRef(so, "domainVfx", domain);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(cine);

            var eso = new SerializedObject(encounter);
            SetRef(eso, "introCinematic", cine);
            eso.ApplyModifiedProperties();
            EditorUtility.SetDirty(encounter);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (opened) EditorSceneManager.CloseScene(scene, true);
            Debug.Log("YuanpeiIntroCinematicSetup: done - YuanpeiIntroCinematic added + wired " +
                      "(boss, domainVfx, encounter.introCinematic). Player control resolved at runtime.");
        }

        static void SetRef(SerializedObject so, string prop, Object value)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.objectReferenceValue = value;
            else Debug.LogWarning("YuanpeiIntroCinematicSetup: '" + prop + "' not found on " + so.targetObject);
        }
    }
}
