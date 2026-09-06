using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Video;
using TMPro;
using Live2DAction.World;

namespace Live2DAction.EditorTools
{
    // 2026-09-06, user request - the full-screen Boss-map video loading screen. EXTENDS
    // ScreenFader + SceneTransitionRunner (no new manager). Re-runnable.
    //
    // Does:
    //  1. Import TMP Essential Resources (if missing) - needed for the default TMP shader/settings.
    //  2. Build a dynamic TMP_FontAsset from Assets/_Project/Fonts/NotoSansTC-Regular.otf
    //     (SIL OFL 1.1 - shippable; logged in Docs/ASSET_LICENSES.md).
    //  3. Import the two loading videos + create RT_BossLoading.renderTexture (1280x720).
    //  4. Put a persistent BossLoadingScreen GameObject in GreyboxTest, wire clips / RT / font.
    //  5. Set SchoolGate_Enter.SceneGate.useLoadingScreen = true (only that gate).
    //
    // If TMP Essentials had to be imported this run, it prints "run again" - the font asset needs
    // the freshly-imported TMP shader.
    internal static class BossLoadingScreenSetup
    {
        const string ScenePath   = "Assets/_Project/Scenes/GreyboxTest.unity";
        const string LoadingDir  = "Assets/_Project/VFX/Loading";
        const string SealVideo   = LoadingDir + "/BossLoadingVideo_Seal.mp4";
        const string BloodVideo  = LoadingDir + "/BossLoadingVideo_Blood.mp4";
        const string RtPath      = LoadingDir + "/RT_BossLoading.renderTexture";
        const string FontDir     = "Assets/_Project/Fonts";
        const string SrcFontPath = FontDir + "/NotoSansTC-Regular.otf";
        const string FontAssetPath = FontDir + "/NotoSansTC SDF.asset";
        const string TmpSettings = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        const string GoName      = "BossLoadingScreen";
        const string GateName    = "SchoolGate_Enter";

        [MenuItem("Tools/Live2DAction/Setup Boss Loading Screen (Boss 地圖影片載入畫面)")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Exit Play Mode first."); return; }

            // --- 1. TMP Essentials ------------------------------------------------------------
            bool tmpJustImported = false;
            if (!File.Exists(TmpSettings))
            {
                string pkg = FindTmpEssentialsPackage();
                if (pkg == null)
                {
                    Debug.LogError("BossLoadingScreenSetup: TMP Essential Resources.unitypackage not found in the ugui package cache. " +
                                   "Import it once via Window > TextMeshPro > Import TMP Essential Resources, then re-run.");
                    return;
                }
                Debug.Log("BossLoadingScreenSetup: importing TMP Essential Resources from " + pkg);
                AssetDatabase.ImportPackage(pkg, false);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                tmpJustImported = true;
            }
            if (!File.Exists(TmpSettings))
            {
                Debug.LogWarning("BossLoadingScreenSetup: TMP Essentials import kicked off - it may finish on the next editor tick. " +
                                 "Run this menu item again to build the font asset + wire the loading screen.");
                return;
            }

            // --- 2. TMP font asset from Noto Sans TC ---------------------------------------------
            var srcFont = AssetDatabase.LoadAssetAtPath<Font>(SrcFontPath);
            if (srcFont == null)
            {
                Debug.LogError("BossLoadingScreenSetup: " + SrcFontPath + " missing - copy NotoSansTC-Regular.otf there first.");
                return;
            }
            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (fontAsset == null)
            {
                fontAsset = TMP_FontAsset.CreateFontAsset(srcFont, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                                                          1024, 1024, AtlasPopulationMode.Dynamic, true);
                fontAsset.name = "NotoSansTC SDF";
                AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

                // save the atlas texture + material as sub-assets of the font asset
                if (fontAsset.atlasTexture != null)
                {
                    fontAsset.atlasTexture.name = "NotoSansTC Atlas";
                    AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
                }
                if (fontAsset.material != null)
                {
                    fontAsset.material.name = "NotoSansTC Material";
                    AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
                }
                EditorUtility.SetDirty(fontAsset);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(FontAssetPath);
                Debug.Log("BossLoadingScreenSetup: created dynamic TMP font asset " + FontAssetPath);
            }

            // --- 3. videos + RenderTexture -----------------------------------------------------
            foreach (var v in new[] { SealVideo, BloodVideo })
            {
                if (!File.Exists(v)) { Debug.LogError("BossLoadingScreenSetup: missing " + v); return; }
                AssetDatabase.ImportAsset(v, ImportAssetOptions.ForceSynchronousImport);
            }
            var seal = AssetDatabase.LoadAssetAtPath<VideoClip>(SealVideo);
            var blood = AssetDatabase.LoadAssetAtPath<VideoClip>(BloodVideo);

            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(RtPath);
            if (rt != null) AssetDatabase.DeleteAsset(RtPath);
            rt = new RenderTexture(1280, 720, 0, RenderTextureFormat.ARGB32)
            {
                name = "RT_BossLoading",
                useMipMap = false,
                autoGenerateMips = false,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            rt.Create();
            AssetDatabase.CreateAsset(rt, RtPath);
            AssetDatabase.SaveAssets();

            // --- 4. BossLoadingScreen GameObject in GreyboxTest -------------------------------
            var scene = EditorSceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var go = scene.GetRootGameObjects().FirstOrDefault(g => g.name == GoName);
            if (go == null)
            {
                go = new GameObject(GoName);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            }
            var comp = go.GetComponent<BossLoadingScreen>() ?? go.AddComponent<BossLoadingScreen>();

            var so = new SerializedObject(comp);
            var clipsProp = so.FindProperty("clips");
            clipsProp.arraySize = 2;
            clipsProp.GetArrayElementAtIndex(0).objectReferenceValue = seal;
            clipsProp.GetArrayElementAtIndex(1).objectReferenceValue = blood;
            so.FindProperty("renderTexture").objectReferenceValue = rt;
            so.FindProperty("font").objectReferenceValue = fontAsset;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(comp);

            // --- 5. SchoolGate_Enter.useLoadingScreen = true --------------------------------------
            var gate = Object.FindObjectsByType<SceneGate>(FindObjectsSortMode.None)
                             .FirstOrDefault(g => g.gameObject.scene == scene && g.gameObject.name == GateName);
            if (gate != null)
            {
                var gso = new SerializedObject(gate);
                var uls = gso.FindProperty("useLoadingScreen");
                if (uls != null) uls.boolValue = true;
                gso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(gate);
            }
            else
            {
                Debug.LogWarning("BossLoadingScreenSetup: " + GateName + " not found - set SceneGate.useLoadingScreen by hand.");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("BossLoadingScreenSetup: done.\n" +
                      $"  TMP essentials : {(tmpJustImported ? "imported this run" : "already present")}\n" +
                      $"  Font asset     : {FontAssetPath}\n" +
                      $"  Videos         : Seal + Blood (round-robin)\n" +
                      $"  RenderTexture  : {RtPath}\n" +
                      $"  GO             : {GoName} (in GreyboxTest)\n" +
                      $"  SchoolGate_Enter.useLoadingScreen = {(gate != null)}");
        }

        static string FindTmpEssentialsPackage()
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "PackageCache"));
            if (!Directory.Exists(root)) return null;
            foreach (var dir in Directory.GetDirectories(root, "com.unity.ugui@*"))
            {
                var p = Path.Combine(dir, "Package Resources", "TMP Essential Resources.unitypackage");
                if (File.Exists(p)) return p.Replace('\\', '/');
            }
            return null;
        }
    }
}
