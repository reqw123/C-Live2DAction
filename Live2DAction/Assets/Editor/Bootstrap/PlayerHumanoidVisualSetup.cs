using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Live2DAction.EditorTools
{
    // Swaps the Player's visual for the CC0 "Universal Base Characters" Humanoid FBX
    // (Quaternius, see Docs/ASSET_LICENSES.md), replacing the earlier Live2D standee
    // experiment. This is still a Placeholder per Docs/ART_PIPELINE.md - real character
    // art comes later.
    internal static class PlayerHumanoidVisualSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string AssetRoot = "Assets/_Project/Characters/Placeholder/UniversalBaseCharacters";
        private const string FbxPath = AssetRoot + "/Models/Superhero_Male_FullBody.fbx";
        private const string BaseColorTexturePath = AssetRoot + "/Textures/T_Superhero_Male_Ligh.png";
        private const string NormalTexturePath = AssetRoot + "/Textures/T_Superhero_Male_Normal.png";
        private const string MaterialPath = AssetRoot + "/Materials/Superhero_Male.mat";

        [MenuItem("Tools/Live2DAction/Replace Player Visual With Humanoid Placeholder")]
        public static void Apply()
        {
            SetHumanoidRig();
            Material material = CreateOrLoadMaterial();

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("Player GameObject not found in " + ScenePath);
                return;
            }

            for (int i = player.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(player.transform.GetChild(i).gameObject);
            }

            GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset, player.transform);
            visual.name = "Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            ApplyMaterial(visual, material);

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Replaced Player visual with Universal Base Characters humanoid placeholder.");
        }

        private static void SetHumanoidRig()
        {
            var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("Could not find ModelImporter for " + FbxPath);
                return;
            }

            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.SaveAndReimport();
            }
        }

        private static Material CreateOrLoadMaterial()
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null)
            {
                return existing;
            }

            string materialsFolder = Path.GetDirectoryName(MaterialPath).Replace("\\", "/");
            if (!AssetDatabase.IsValidFolder(materialsFolder))
            {
                AssetDatabase.CreateFolder(AssetRoot, "Materials");
            }

            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseColorTexturePath);
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalTexturePath);

            if (baseColor != null)
            {
                material.SetTexture("_BaseMap", baseColor);
            }

            if (normal != null)
            {
                SetNormalTextureImportSettings(NormalTexturePath);
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }

            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        private static void SetNormalTextureImportSettings(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }
        }

        private static void ApplyMaterial(GameObject visual, Material material)
        {
            SkinnedMeshRenderer[] renderers = visual.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                renderer.sharedMaterial = material;
            }
        }
    }
}
