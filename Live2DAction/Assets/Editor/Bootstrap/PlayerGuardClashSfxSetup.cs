using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.Combat;

namespace Live2DAction.EditorTools
{
    // 2026-09-01, user request ("把 PLAYER 左鍵的音效移動到右鍵(防禦)，並且只有在防禦-玩家與武士的
    // 刀刃碰撞時"). Replaces the old "Add Player Katana Attack SFX" (PlayerMeleeSfxSetup) - the same
    // KatanaClash.mp3 clip + the same Player/GuardClashSfx child AudioSource, but the driver moves
    // from PlayerCombat.Hit (every left-click combo segment) to PlayerGuard.Blocked, gated to boss
    // sword strikes only (see PlayerGuardClashSfx). Re-runnable: rebuilds the child + re-imports
    // the clip each run, and strips the retired PlayerMeleeSfx component if it's still there.
    internal static class PlayerGuardClashSfxSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string ClipPath = "Assets/_Project/Audio/Combat/KatanaClash.mp3";
        private const string ChildName = "GuardClashSfx";
        private const string OldChildName = "MeleeSfx";

        [MenuItem("Tools/Live2DAction/Add Player Guard Clash SFX")]
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
                Debug.LogError("PlayerGuardClashSfxSetup: clip not found at " + ClipPath);
                return;
            }
            ConfigureClipImport();

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("PlayerGuardClashSfxSetup: no Player in " + ScenePath);
                return;
            }

            PlayerGuard guard = player.GetComponent<PlayerGuard>();
            if (guard == null)
            {
                Debug.LogError("PlayerGuardClashSfxSetup: Player has no PlayerGuard - run 'Add Player Katana Guard' first.");
                return;
            }

            // Retire the old MeleeSfx child (PlayerMeleeSfx on every left-click) if it's still around.
            Transform oldChild = player.transform.Find(OldChildName);
            if (oldChild != null)
            {
                Object.DestroyImmediate(oldChild.gameObject);
            }
            Transform existing = player.transform.Find(ChildName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            var go = new GameObject(ChildName);
            go.transform.SetParent(player.transform, false);
            go.transform.localPosition = new Vector3(0f, 1f, 0f); // roughly guard / blade height

            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 1f;
            src.dopplerLevel = 0f;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = 3f;
            src.maxDistance = 45f;

            var sfx = go.AddComponent<PlayerGuardClashSfx>();
            var so = new SerializedObject(sfx);
            so.FindProperty("guard").objectReferenceValue = guard;
            so.FindProperty("clashClip").objectReferenceValue = clip;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("PlayerGuardClashSfxSetup: KatanaClash.mp3 -> Player/GuardClashSfx, driven by " +
                      "PlayerGuard.Blocked (boss sword strikes only). Old MeleeSfx child removed.");
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
