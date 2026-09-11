using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Live2DAction.EditorTools
{
    // 2026-09-11, user request ("讓這個作為露營區的地基 讓他水平擴大填滿整個營地" - user-supplied
    // 營區地板.zip, a Meshy AI "Raised Wooden Platform" model) - a decorative wood-platform mesh
    // stretched to visually cover 露營區's whole 60x60 plot.
    //
    // The existing 露營區 Cube keeps doing the actual collision (a perfectly flat BoxCollider top
    // at world y=0.5 is a far more reliable walking surface than a 2.24M-vertex "raised platform"
    // mesh, which may not even be flat) - its MeshRenderer is just switched off so this new mesh
    // is what's actually seen. Same "invisible collision box + decorative mesh on top" split this
    // project already uses for the yuanpei_* campus buildings' own _Collision proxies.
    //
    // Import quirk (same family as VoidmoonGate/YuanpeiUniversityBuilding): the FBX's own
    // fileScale (0.01) shrinks it to ~0.02 units - useFileScale=false undoes that, and the mesh
    // turns out authored with local Z as "up" (a thin ~1.9 x 1.9 x 0.256 slab), so it needs a
    // -90 (270) X rotation to lie flat, same non-Y-up story as every other Meshy building import
    // in this project.
    internal static class CampFloorSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Map_Camp.unity";
        private const string FbxPath = "Assets/_Project/Environment/Meshy/CampFloor/Meshy_AI_CampFloor_texture.fbx";
        private const string TextureFolder = "Assets/_Project/Environment/Meshy/CampFloor";
        private const string MaterialPath = TextureFolder + "/CampFloor.mat";
        private const string GroundName = "露營區";
        private const string FloorName = "CampFloor";

        // Plot is 60x60 (GroundName's own transform.localScale). The platform's own natural
        // footprint (measured with useFileScale=false, before any correction) is ~1.903 x 1.906 -
        // scaling both local X and Y (which become world X/Z once rotated flat) by the same factor
        // keeps it square and fills the plot edge-to-edge.
        private const float SourceFootprint = 1.905f; // average of the two measured local axes
        private const float SourceThickness = 0.256f; // local Z, becomes world Y (height) after rotation

        [MenuItem("Tools/Live2DAction/Build Camp Floor Platform (露營區)")]
        public static void Apply()
        {
            ConfigureTextureImports();
            Material mat = BuildMaterial();

            GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbxAsset == null)
            {
                Debug.LogError("[CampFloorSetup] Could not load FBX at " + FbxPath);
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject ground = GameObject.Find(GroundName);
            if (ground == null)
            {
                Debug.LogError("[CampFloorSetup] '" + GroundName + "' not found in " + ScenePath);
                return;
            }

            GameObject existing = GameObject.Find(FloorName);
            if (existing != null) Object.DestroyImmediate(existing);

            GameObject floor = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset);
            floor.name = FloorName;

            float plotSize = ground.transform.localScale.x; // 60
            float horizontalScale = plotSize / SourceFootprint;

            float groundTopY = ground.GetComponent<Collider>() != null
                ? ground.GetComponent<Collider>().bounds.max.y
                : ground.transform.position.y + 0.5f;
            float halfThicknessWorld = SourceThickness * 0.5f; // vertical (local Z) left un-scaled - "水平擴大" only

            floor.transform.SetParent(null);
            floor.transform.position = new Vector3(ground.transform.position.x, groundTopY - halfThicknessWorld, ground.transform.position.z);
            floor.transform.rotation = Quaternion.Euler(270f, 0f, 0f); // local Z (thickness) -> world Y
            floor.transform.localScale = new Vector3(horizontalScale, horizontalScale, 1f);

            Renderer renderer = floor.GetComponentInChildren<Renderer>();
            if (renderer != null) renderer.sharedMaterial = mat;

            // Collision stays on the flat Cube - hide its visual, keep its BoxCollider.
            MeshRenderer groundRenderer = ground.GetComponent<MeshRenderer>();
            if (groundRenderer != null) groundRenderer.enabled = false;

            EditorUtility.SetDirty(floor);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[CampFloorSetup] '" + FloorName + "' placed at " + floor.transform.position.ToString("F2") +
                       " scale=" + floor.transform.localScale.ToString("F2") + ", '" + GroundName +
                       "' MeshRenderer disabled (collider kept). Needs a visual check - the source " +
                       "mesh's own top-surface offset from its bounding-box centre is unverified.");
        }

        private static void ConfigureTextureImports()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { TextureFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (importer == null) continue;

                bool changed = false;
                if (path.Contains("_Normal") && importer.textureType != TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    changed = true;
                }
                else if ((path.Contains("_Metallic") || path.Contains("_Roughness")) && importer.sRGBTexture)
                {
                    importer.sRGBTexture = false;
                    changed = true;
                }

                if (changed)
                {
                    EditorUtility.SetDirty(importer);
                    importer.SaveAndReimport();
                }
            }
        }

        // BaseColor + Normal wired; Metallic/Roughness approximated as flat values (same call as
        // GuessWho_Body.mat / Player5WeaponSetup - packing two source textures into URP/Lit's
        // single _MetallicGlossMap isn't worth it for a greybox environment prop).
        private static Material BuildMaterial()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            bool isNew = material == null;
            if (isNew) material = new Material(urpLit);

            Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "/CampFloor_BaseColor.png");
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "/CampFloor_Normal.png");

            if (baseColor != null) material.SetTexture("_BaseMap", baseColor);
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }

            material.SetFloat("_Metallic", 0.05f);
            material.SetFloat("_Smoothness", 0.35f);

            if (isNew) AssetDatabase.CreateAsset(material, MaterialPath);
            else EditorUtility.SetDirty(material);

            return material;
        }
    }
}
