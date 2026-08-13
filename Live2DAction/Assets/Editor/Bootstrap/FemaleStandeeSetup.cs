using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Live2DAction.EditorTools
{
    // Adds a new standalone standee GameObject using the CC0 "Universal Base Characters"
    // Female FBX (Quaternius, see Docs/ASSET_LICENSES.md) - already sitting unused in the
    // project since Phase 1's wholesale asset-pack copy (only the Male variant was ever
    // wired to a GameObject; see PlayerHumanoidVisualSetup.cs/EnemyHumanoidVisualSetup.cs).
    // Mirrors their material/import-setup logic. Not attached to Player/Enemy/Player2 or any
    // AI/movement - purely a static "cast" placeholder standing alongside the Live2D
    // standees (see Live2DStandeeSetup.cs), same spirit as Player2's static mecha standee
    // minus the wander behaviour, since nothing asked for this one to move yet.
    internal static class FemaleStandeeSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string AssetRoot = "Assets/_Project/Characters/Placeholder/UniversalBaseCharacters";
        private const string FbxPath = AssetRoot + "/Models/Superhero_Female_FullBody.fbx";
        private const string BaseColorTexturePath = AssetRoot + "/Textures/T_Superhero_Female_Light_BaseColor.png";
        private const string NormalTexturePath = AssetRoot + "/Textures/T_Superhero_Female_Normal.png";
        private const string MaterialPath = AssetRoot + "/Materials/Superhero_Female.mat";
        private const string StandeeName = "FemaleStandee_Placeholder";
        private static readonly Vector3 StandeePosition = new Vector3(0f, 0f, -8f);

        [MenuItem("Tools/Live2DAction/Add Quaternius Female Standee")]
        public static void Apply()
        {
            SetHumanoidRig();
            Material material = CreateOrLoadMaterial();

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject existing = GameObject.Find(StandeeName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            var standee = new GameObject(StandeeName);
            standee.transform.position = StandeePosition;

            GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset, standee.transform);
            visual.name = "Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            ApplyMaterial(visual, material);

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Added " + StandeeName + " using the Universal Base Characters Female placeholder.");
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
