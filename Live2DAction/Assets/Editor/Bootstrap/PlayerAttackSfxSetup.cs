using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.Combat;

namespace Live2DAction.EditorTools
{
    // 2026-09-01, user request ("把 9月1日.mp3 做為左鍵攻擊音效"). Adds a Player/AttackSfx child
    // (AudioSource + PlayerAttackSfx) that plays KatanaSwing.mp3 on every PlayerCombat.Hit segment -
    // the left-click swing sound that went missing when 追加85's KatanaClash.mp3 moved to the guard
    // (PlayerGuardClashSfx). Re-runnable: rebuilds the child + re-imports the clip each run.
    internal static class PlayerAttackSfxSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string ClipPath = "Assets/_Project/Audio/Combat/KatanaSwing.mp3";
        private const string ChildName = "AttackSfx";

        [MenuItem("Tools/Live2DAction/Add Player Katana Attack SFX")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Exit Play Mode first - this touches the AssetDatabase and the scene.");
                return;
            }

            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);
            if (clip == null)
            {
                Debug.LogError("PlayerAttackSfxSetup: clip not found at " + ClipPath);
                return;
            }
            ConfigureClipImport();

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("PlayerAttackSfxSetup: no Player in " + ScenePath);
                return;
            }

            PlayerCombat combat = player.GetComponent<PlayerCombat>();
            if (combat == null)
            {
                Debug.LogError("PlayerAttackSfxSetup: Player has no PlayerCombat.");
                return;
            }

            Transform existing = player.transform.Find(ChildName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            var go = new GameObject(ChildName);
            go.transform.SetParent(player.transform, false);
            go.transform.localPosition = new Vector3(0f, 1f, 0f); // roughly blade height

            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 1f;
            src.dopplerLevel = 0f;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = 3f;
            src.maxDistance = 45f;

            var sfx = go.AddComponent<PlayerAttackSfx>();
            var so = new SerializedObject(sfx);
            so.FindProperty("combat").objectReferenceValue = combat;
            so.FindProperty("swingClip").objectReferenceValue = clip;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("PlayerAttackSfxSetup: KatanaSwing.mp3 -> Player/AttackSfx, driven by PlayerCombat.Hit (every left-click swing).");
        }

        private static void ConfigureClipImport()
        {
            var importer = AssetImporter.GetAtPath(ClipPath) as AudioImporter;
            if (importer == null) return;

            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = true;
            importer.loadInBackground = false;
            importer.SaveAndReimport();
        }
    }
}
