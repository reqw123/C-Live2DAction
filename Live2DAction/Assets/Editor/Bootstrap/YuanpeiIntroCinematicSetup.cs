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
    // 續183 reworked beats 4-6 (slow-mo approach + normal slash -> boss back/forward RAM -> player
    // flung + lands FACING the boss in the "Dying" fall clip -> scrub that clip in reverse to get
    // up -> real fight). New serialized fields (slashStandoff / bossRamClose / downedHoldSeconds /
    // getUpSeconds) all carry sensible C# defaults, so re-running this is optional - it only needs
    // to run again if the component is missing or its refs got cleared.
    //
    // 續183d adds a Full (≈22s, the signed-off version) / Short (≈15s milestone cut) length preset.
    // The two "Yuanpei Intro Length" menu items below just flip that enum in Map_School and save;
    // they never touch the Full tuning fields.
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

        // ---- 續183d length preset toggle ------------------------------------------------------------

        [MenuItem("Tools/Live2DAction/Yuanpei Intro Length ▸ Short (~15s)")]
        public static void SetShort() => SetLength(YuanpeiIntroLength.Short);

        [MenuItem("Tools/Live2DAction/Yuanpei Intro Length ▸ Full (~22s)")]
        public static void SetFull() => SetLength(YuanpeiIntroLength.Full);

        static void SetLength(YuanpeiIntroLength len)
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Exit Play Mode first."); return; }

            var scene = EditorSceneManager.GetSceneByPath(ScenePath);
            bool opened = false;
            if (!scene.isLoaded) { scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive); opened = true; }

            var cine = Object.FindObjectsByType<YuanpeiIntroCinematic>(FindObjectsSortMode.None)
                             .FirstOrDefault(c => c.gameObject.scene == scene);
            if (cine == null)
            {
                Debug.LogError("Yuanpei Intro Length: no YuanpeiIntroCinematic in " + ScenePath +
                               " - run 'Setup Yuanpei Intro Cinematic' first.");
                if (opened) EditorSceneManager.CloseScene(scene, true);
                return;
            }

            var so = new SerializedObject(cine);
            var p = so.FindProperty("length");
            if (p == null) { Debug.LogError("Yuanpei Intro Length: 'length' field not found."); if (opened) EditorSceneManager.CloseScene(scene, true); return; }
            p.enumValueIndex = (int)len;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(cine);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (opened) EditorSceneManager.CloseScene(scene, true);
            Debug.Log("Yuanpei Intro Length set to " + len + " in " + ScenePath +
                      (len == YuanpeiIntroLength.Short ? " (~15s milestone cut)" : " (~22s full)") + ".");
        }
    }
}
