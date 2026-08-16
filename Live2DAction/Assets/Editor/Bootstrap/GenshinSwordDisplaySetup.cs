using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.DebugTools;

namespace Live2DAction.EditorTools
{
    // Places the user-provided "10 Genshin Impact-inspired sword" model set as a static
    // decorative display in the scene (2026-08-13, explicit user request - "當成場景物件匯
    // 入"). The source FBX's texture filenames (Equip_Sword_Narukami_01_Tex_Diffuse.png,
    // SkillObj_Shougun.png, etc.) match HoYoverse/miHoYo's own internal Genshin Impact asset
    // naming convention exactly - this looks like datamined game files, not just a fan
    // recreation (a step beyond even MechaModel_DoNotShip/WolfsGravestone's risk tier). User
    // confirmed personal prototype use only, never ship - see Docs/ASSET_LICENSES.md.
    //
    // The 10 sword meshes ship already laid out as a side-by-side display lineup (each with
    // its own large per-object localScale, ~100-108x, baked into the source file) - rather
    // than scatter them individually like BackgroundSceneryStandeeSetup does for
    // trees/rocks, this keeps that authored arrangement intact and places the whole group as
    // one set piece, decoration-ring placement (same InnerRadius/OuterRadius convention as
    // BackgroundSceneryStandeeSetup - outside the ~17-unit play boundary, not scattered
    // randomly).
    internal static class GenshinSwordDisplaySetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string FbxPath = "Assets/_Project/Environment/Placeholder/GenshinSwords/GenshinSwords.fbx";
        private const string TextureFolder = "Assets/_Project/Environment/Placeholder/GenshinSwords/Textures";
        private const string MaterialsFolder = "Assets/_Project/Environment/Placeholder/GenshinSwords/Materials";

        // Combined renderer bounds across all 10 swords (pre-additional-scale, measured from
        // the imported prefab: lowest point -4.38, highest point 11.565, see class comment
        // history) - derives the group scale/ground-offset from real geometry instead of a
        // guessed constant, same reasoning as Player5VisualSetup's MeasureHeight.
        private const float RawMinY = -4.38f;
        private const float RawMaxY = 11.565f;
        private const float TargetGroupHeight = 1.3f; // tallest sword in the lineup, meters
        private const float GroupScale = TargetGroupHeight / (RawMaxY - RawMinY);

        // Just outside BackgroundSceneryStandeeSetup's own InnerRadius (17) - visible near
        // the play area's edge without sitting inside it.
        private static readonly Vector3 PlacementXZ = new Vector3(0f, 0f, 20f);

        // Best-effort name match between each sword's display-name material (already
        // correctly split per-mesh in the source FBX - "Narukami", "Cool Steel", etc.) and
        // the loose texture files' internal dev names (e.g. "Steel", "Blunt", "Widsith") -
        // the two naming schemes don't agree, same underlying problem as Player5's material
        // wiring. Based on publicly known Genshin Impact internal/display name pairs
        // (Freedom-Sworn = Widsith, Dull Blade = Blunt, Prototype Rancour = Proto, Cool Steel
        // = Steel) - NOT independently verified against the actual textures visually in this
        // environment. "Lion's Roar" -> Rockkiller is a guess by elimination (no obvious name
        // relationship); "Mistsplitter Reforged" has no matching texture file at all in what
        // the user provided, left untextured.
        private static readonly Dictionary<string, string> MaterialToTexture = new Dictionary<string, string>
        {
            { "Bakufu", "Equip_Sword_Bakufu_02_Tex_Diffuse" },
            { "Boreas", "Equip_Sword_Boreas_01_Tex_Diffuse" },
            { "Narukami", "Equip_Sword_Narukami_01_Tex_Diffuse" },
            { "Katana", "Equip_Sword_Narukami_02_Tex_Diffuse" },
            { "Cool Steel", "Equip_Sword_Steel_01_Tex_Diffuse" },
            { "Dull Blade", "Equip_Sword_Blunt_01_Tex_Diffuse" },
            { "Freedom-Sworn", "Equip_Sword_Widsith_01_Tex_Diffuse" },
            { "Prototype Rancour", "Equip_Sword_Proto_01_Tex_Diffuse" },
            { "Lion's Roar", "Equip_Sword_Rockkiller_01_Tex_Diffuse" },
        };

        [MenuItem("Tools/Live2DAction/Add Genshin Sword Display (Scene Decoration)")]
        public static void Apply()
        {
            ExtractAndAssignMaterials();

            GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbxAsset == null)
            {
                Debug.LogError("Could not load FBX at " + FbxPath);
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Transform existing = GameObject.Find("GenshinSwordDisplay_DoNotShip")?.transform;
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            GameObject display = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset);
            display.name = "GenshinSwordDisplay_DoNotShip";

            // "Sun" is a leftover Blender scene light/empty from the source file, not a
            // sword - has no Renderer (confirmed via diagnostic dump) and sits far outside
            // the lineup (Z=-26.82) - harmless either way but removed to keep the hierarchy
            // clean and unambiguous about what's actually on display.
            Transform sun = display.transform.Find("Sun");
            if (sun != null)
            {
                Object.DestroyImmediate(sun.gameObject);
            }

            GameObject ground = GameObject.Find("Ground");
            float groundTopY = ground != null ? ground.GetComponent<Collider>().bounds.max.y : 0f;

            display.transform.localScale = Vector3.one * GroupScale;
            // Lifts the group so its lowest point (RawMinY, scaled) rests exactly on the
            // ground instead of clipping through it - derived from the same measured bounds
            // as GroupScale rather than a separate guessed offset.
            float groundOffset = -RawMinY * GroupScale;
            display.transform.position = new Vector3(PlacementXZ.x, groundTopY + groundOffset, PlacementXZ.z);

            // 2026-08-13 explicit user request - Z/X/C/V held-key height/scale nudging in
            // Play mode, since neither of us can otherwise confirm this display's placement
            // looks right (see class comment). Idempotent: AddComponent only if missing, so
            // re-running this tool after the user has already tuned it via the adjuster
            // doesn't add a second copy.
            if (display.GetComponent<SwordDisplayAdjuster>() == null)
            {
                display.AddComponent<SwordDisplayAdjuster>();
            }

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"Added Genshin sword display. scale={GroupScale} position={display.transform.position}");
        }

        // Same "extract embedded FBX materials to persistent .mat files + AddRemap" pattern
        // as Player5VisualSetup - editing the FBX's own transient generated materials in
        // place doesn't survive reimport (see that class's own history for the empirically
        // confirmed reason).
        private static void ExtractAndAssignMaterials()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            var importer = (ModelImporter)AssetImporter.GetAtPath(FbxPath);

            if (!AssetDatabase.IsValidFolder(MaterialsFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Environment/Placeholder/GenshinSwords", "Materials");
            }

            Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(FbxPath);
            bool anyRemapped = false;

            foreach (Object asset in allAssets)
            {
                if (!(asset is Material embeddedMaterial))
                {
                    continue;
                }

                string matPath = MaterialsFolder + "/" + embeddedMaterial.name.Replace("'", "") + ".mat";
                Material persisted = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (persisted == null)
                {
                    persisted = new Material(embeddedMaterial);
                    AssetDatabase.CreateAsset(persisted, matPath);
                }

                if (urpLit != null && persisted.shader != urpLit)
                {
                    persisted.shader = urpLit;
                }

                if (embeddedMaterial.name.StartsWith("Outline"))
                {
                    // Toon-style outline shell material (inverted-normal duplicate mesh,
                    // rendered slightly larger than the base) - solid dark color, no texture
                    // needed, matches the flat-black outline look these are authored for.
                    persisted.SetColor("_BaseColor", new Color(0.05f, 0.05f, 0.05f));
                }
                else if (MaterialToTexture.TryGetValue(embeddedMaterial.name, out string textureName))
                {
                    string texturePath = TextureFolder + "/" + textureName + ".png";
                    Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                    if (texture != null)
                    {
                        persisted.SetTexture("_BaseMap", texture);
                    }
                    else
                    {
                        Debug.LogWarning("Sword material texture not found: " + texturePath);
                    }
                }

                EditorUtility.SetDirty(persisted);

                var sourceId = new AssetImporter.SourceAssetIdentifier(typeof(Material), embeddedMaterial.name);
                importer.AddRemap(sourceId, persisted);
                anyRemapped = true;
            }

            if (anyRemapped)
            {
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
                AssetDatabase.SaveAssets();
            }
        }
    }
}
