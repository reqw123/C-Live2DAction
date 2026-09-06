using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using Live2DAction.AI.Boss.Yuanpei;
using Live2DAction.VFX.Rendering;

namespace Live2DAction.EditorTools
{
    // 2026-09-06, explicit user request: one-shot wiring for the "Boss 支配領域全螢幕邊界特效"
    // (yuanpei_LogoSky). Re-runnable and idempotent. Does the four things the feature needs:
    //   1. Material  - Assets/_Project/VFX/Materials/BossDomainScreenVFX.mat from the shader,
    //                  with the spec-§4 default numbers baked in.
    //   2. Renderer  - adds BossDomainScreenVFXRendererFeature to Live2DAction_Renderer.asset
    //                  (the ONLY pipeline-wide change), replicating exactly what URP's own
    //                  ScriptableRendererDataEditor.AddComponent does.
    //   3. Scene     - adds a BossDomainScreenVFX controller onto the yuanpei_LogoSky boss in
    //                  Map_School.unity and wires sourceMaterial + bossVitals.
    //   4. Encounter - points YuanpeiEncounter.domainVfx at that controller.
    internal static class BossDomainScreenVFXSetup
    {
        const string ShaderName = "Live2DAction/VFX/BossDomainScreenVFX";
        const string MaterialPath = "Assets/_Project/VFX/Materials/BossDomainScreenVFX.mat";
        const string RendererPath = "Assets/_Project/Settings/Live2DAction_Renderer.asset";
        const string ScenePath = "Assets/_Project/Scenes/Map_School.unity";

        const string SkyShaderName = "Live2DAction/Environment/SkyboxNightPanorama";
        const string SkyTexPath = "Assets/_Project/Environment/Textures/rogland_clear_night_2k.exr";
        const string SkyMatPath = "Assets/_Project/Environment/Materials/Skybox_NightRogland.mat";

        [MenuItem("Tools/Live2DAction/Setup Boss Domain Screen VFX (yuanpei_LogoSky)")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Exit Play Mode first - this edits the URP renderer asset and a scene.");
                return;
            }

            Material mat = CreateOrUpdateMaterial();
            if (mat == null) return;

            Material skyMat = CreateOrUpdateNightSky();   // null-safe if the .exr isn't imported yet

            AddRendererFeature();
            WireScene(mat, skyMat);

            AssetDatabase.SaveAssets();
            Debug.Log("BossDomainScreenVFXSetup: done - material + renderer feature + Map_School wiring. " +
                      "Play into the yuanpei_LogoSky fight to see it fade in; it exits on victory/defeat.");
        }

        // ---------------------------------------------------------------- 1. material

        static Material CreateOrUpdateMaterial()
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError("BossDomainScreenVFXSetup: shader '" + ShaderName + "' not found (still compiling?).");
                return null;
            }

            var dir = System.IO.Path.GetDirectoryName(MaterialPath);
            if (!AssetDatabase.IsValidFolder(dir))
                System.IO.Directory.CreateDirectory(dir);

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            bool isNew = mat == null;
            if (isNew) mat = new Material(shader) { name = "BossDomainScreenVFX" };
            else if (mat.shader != shader) mat.shader = shader;

            // spec §4 default numbers (kept in sync with BossDomainScreenVFX.cs field defaults)
            mat.SetColor("_DomainColor", new Color(0.10f, 0.85f, 0.55f, 1f));
            mat.SetFloat("_MasterIntensity", 0f);   // starts fully off - the controller drives it
            mat.SetFloat("_EnterExit", 0f);
            mat.SetFloat("_Phase", 1f);
            mat.SetFloat("_Pulse", 0f);
            mat.SetFloat("_TimeSeconds", 0f);
            mat.SetFloat("_EdgeWidth", 0.12f);
            mat.SetFloat("_CornerStrength", 1.5f);
            mat.SetFloat("_FogOpacity", 0.38f);
            mat.SetFloat("_FlameIntensity", 1.1f);
            mat.SetFloat("_EmissionSpeed", 0.6f);
            mat.SetFloat("_NoiseScale", 3.2f);
            mat.SetFloat("_NoiseSpeed", 0.05f);
            mat.SetFloat("_DistortionStrength", 0.004f);
            mat.SetFloat("_RuneIntensity", 0.25f);
            mat.SetFloat("_BreathPeriod", 6.5f);
            mat.SetFloat("_BreathAmount", 0.12f);
            mat.SetFloat("_HasRuneTex", 0f);

            if (isNew) AssetDatabase.CreateAsset(mat, MaterialPath);
            else EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------------------------------------------------------------- 1b. night-sky panorama

        static Material CreateOrUpdateNightSky()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(SkyTexPath);
            if (tex == null)
            {
                Debug.LogWarning("BossDomainScreenVFXSetup: " + SkyTexPath + " not found - domain sky swap left unwired " +
                                 "(the effect still works, the arena just keeps its current sky).");
                return null;
            }

            // HDR panorama: linear data, seamless in X.
            var importer = AssetImporter.GetAtPath(SkyTexPath) as TextureImporter;
            if (importer != null)
            {
                bool dirty = false;
                if (importer.textureShape != TextureImporterShape.Texture2D) { importer.textureShape = TextureImporterShape.Texture2D; dirty = true; }
                if (importer.sRGBTexture) { importer.sRGBTexture = false; dirty = true; }
                if (importer.mipmapEnabled) { importer.mipmapEnabled = false; dirty = true; }
                if (importer.wrapMode != TextureWrapMode.Repeat) { importer.wrapMode = TextureWrapMode.Repeat; dirty = true; }
                if (importer.maxTextureSize < 2048) { importer.maxTextureSize = 2048; dirty = true; }
                if (dirty) importer.SaveAndReimport();
            }

            Shader sky = Shader.Find(SkyShaderName);
            if (sky == null) { Debug.LogError("BossDomainScreenVFXSetup: shader '" + SkyShaderName + "' not found."); return null; }

            var skyMat = AssetDatabase.LoadAssetAtPath<Material>(SkyMatPath);
            bool isNew = skyMat == null;
            if (isNew) skyMat = new Material(sky) { name = "Skybox_NightRogland" };
            else if (skyMat.shader != sky) skyMat.shader = sky;

            skyMat.SetTexture("_MainTex", tex);
            skyMat.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 1f));
            skyMat.SetFloat("_Exposure", 0.62f);        // "陰暗"
            skyMat.SetFloat("_Rotation", 205f);         // Milky Way arc toward where the boss hovers (north side)
            skyMat.SetFloat("_HorizonDarken", 0.92f);   // kill the desert-hill lower hemisphere
            skyMat.SetFloat("_HorizonHeight", 0.03f);
            skyMat.SetFloat("_HorizonSoftness", 0.32f);

            if (isNew) AssetDatabase.CreateAsset(skyMat, SkyMatPath);
            else EditorUtility.SetDirty(skyMat);
            return skyMat;
        }

        // ---------------------------------------------------------------- 2. renderer feature

        static void AddRendererFeature()
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RendererPath);
            if (rendererData == null)
            {
                Debug.LogError("BossDomainScreenVFXSetup: renderer asset not found at " + RendererPath);
                return;
            }

            if (rendererData.rendererFeatures.Any(f => f is BossDomainScreenVFXRendererFeature))
            {
                Debug.Log("BossDomainScreenVFXSetup: renderer feature already present - left as is.");
                return;
            }

            // Same steps as URP's ScriptableRendererDataEditor.AddComponent.
            var component = ScriptableObject.CreateInstance<BossDomainScreenVFXRendererFeature>();
            component.name = nameof(BossDomainScreenVFXRendererFeature);
            AssetDatabase.AddObjectToAsset(component, rendererData);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(component, out _, out long localId);

            var so = new SerializedObject(rendererData);
            var features = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");

            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = component;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(RendererPath);
            Debug.Log("BossDomainScreenVFXSetup: added BossDomainScreenVFXRendererFeature to " + RendererPath);
        }

        // ---------------------------------------------------------------- 3 + 4. scene wiring

        static void WireScene(Material mat, Material skyMat)
        {
            Scene scene = EditorSceneManager.GetSceneByPath(ScenePath);
            bool opened = false;
            if (!scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                opened = true;
            }

            var boss = Object.FindObjectsByType<YuanpeiBoss>(FindObjectsSortMode.None)
                             .FirstOrDefault(b => b.gameObject.scene == scene);
            if (boss == null)
            {
                Debug.LogError("BossDomainScreenVFXSetup: no YuanpeiBoss in " + ScenePath + " - scene not wired.");
                if (opened) EditorSceneManager.CloseScene(scene, true);
                return;
            }

            var vitals = boss.GetComponent<YuanpeiBossVitals>();
            var existing = boss.GetComponent<BossDomainScreenVFX>();
            bool freshlyAdded = existing == null;
            var domain = existing != null ? existing : boss.gameObject.AddComponent<BossDomainScreenVFX>();

            var dso = new SerializedObject(domain);
            SetRef(dso, "sourceMaterial", mat);
            SetRef(dso, "bossVitals", vitals);
            if (skyMat != null) SetRef(dso, "domainSkybox", skyMat);
            if (freshlyAdded)
            {
                // a brand-new component already carries the C# field defaults; nothing to force.
            }
            else
            {
                // an existing component from an earlier setup run may hold stale numbers - refresh
                // the shape/motion knobs to the current defaults (colour + durations left as they are
                // so a hand-tuned instance isn't stomped on every re-run).
                SetF(dso, "edgeWidth", 0.12f);
                SetF(dso, "cornerStrength", 1.5f);
                SetF(dso, "fogOpacity", 0.38f);
                SetF(dso, "flameIntensity", 1.1f);
                SetF(dso, "domainAmbientIntensity", 0.5f);
                SetF(dso, "domainAmbientColorTint", 0.35f);
            }
            dso.ApplyModifiedProperties();
            EditorUtility.SetDirty(domain);

            var encounter = Object.FindObjectsByType<YuanpeiEncounter>(FindObjectsSortMode.None)
                                  .FirstOrDefault(e => e.gameObject.scene == scene);
            if (encounter != null)
            {
                var eso = new SerializedObject(encounter);
                SetRef(eso, "domainVfx", domain);
                eso.ApplyModifiedProperties();
                EditorUtility.SetDirty(encounter);
            }
            else
            {
                Debug.LogWarning("BossDomainScreenVFXSetup: no YuanpeiEncounter found - " +
                                 "BossDomainScreenVFX added but BeginDomain/EndDomain not auto-called.");
            }

            string bossName = boss.name;   // capture before CloseScene destroys the object

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (opened) EditorSceneManager.CloseScene(scene, true);
            Debug.Log("BossDomainScreenVFXSetup: wired BossDomainScreenVFX onto '" + bossName + "' in " + ScenePath);
        }

        static void SetRef(SerializedObject so, string prop, Object value)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.objectReferenceValue = value;
            else Debug.LogWarning("BossDomainScreenVFXSetup: property '" + prop + "' not found on " + so.targetObject);
        }

        static void SetF(SerializedObject so, string prop, float value)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.floatValue = value;
        }
    }
}
