using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Live2DAction.EditorTools
{
    // 2026-08-16, explicit user request ("開放世界那樣的美麗風景"), part of the same pass as
    // DistantMountainsSetup - adds a second, sparser ring of trees between the existing near
    // scenery ring (17-26, BackgroundSceneryStandeeSetup) and the new distant mountains
    // (55-90), so the world reads as continuing in layered depth (near/mid/far) instead of
    // jumping straight from "arena dressing" to "mountains on the horizon". Trees only (not
    // rocks/bush/grass) - at this distance bushes/grass are too small to read as anything, and
    // a second rock ring would just look like more of the same near-ring rocks, not new depth.
    internal static class MidDistanceTreeRingSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string AssetRoot = "Assets/_Project/Environment/Placeholder/QuaterniusSimpleNature";
        private const string MaterialsFolder = "Assets/_Project/Environment/Materials/QuaterniusSimpleNature";
        private const string ParentName = "MidDistanceTrees";

        private static readonly string[] TreeFileNames = { "Tree1", "Tree2", "Tree3", "Tree4" };
        // Lighter atmospheric-perspective tint than the near ring's plain conversion, but not
        // as heavily hazed as DistantMountainsSetup's - this ring sits in between.
        private static readonly Color MidDistanceTint = new Color(0.75f, 0.8f, 0.88f);

        private const float InnerRadius = 30f;
        private const float OuterRadius = 48f;
        private const int TreeCount = 30;

        // 2026-08-16: same tiny-native-import-scale discovery as DistantMountainsSetup's own
        // comment - confirmed live via execute_code that Tree2 at localScale~1.03 has a
        // world-space bounds height of only ~0.07 units (ratio ~0.068 height-units per
        // scale-unit). The first version of this script used 0.9-1.7x (matching
        // BackgroundSceneryStandeeSetup's near-ring scale, which has the same unnoticed bug -
        // see this feature's own follow-up note) and produced ~0.05-0.1 unit trees, invisible
        // specks. Back-calculated to hit an actual ~3.5-6 unit tree height.
        private const float MinScale = 55f;
        private const float MaxScale = 90f;

        [MenuItem("Tools/Live2DAction/Add Mid-Distance Tree Ring")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject existing = GameObject.Find(ParentName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            var parent = new GameObject(ParentName);
            var random = new System.Random(20260816 + 1);

            for (int i = 0; i < TreeCount; i++)
            {
                string propName = TreeFileNames[random.Next(TreeFileNames.Length)];
                GameObject prefab = LoadPropPrefab(propName);
                if (prefab == null)
                {
                    continue;
                }

                float angle = (float)(random.NextDouble() * System.Math.PI * 2.0);
                float radius = InnerRadius + (float)random.NextDouble() * (OuterRadius - InnerRadius);
                var position = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
                instance.name = "MidTree_" + propName + "_" + i;
                instance.transform.localPosition = position;
                instance.transform.localRotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                float scale = MinScale + (float)random.NextDouble() * (MaxScale - MinScale);
                instance.transform.localScale = Vector3.one * scale;

                ConvertMaterialsToTintedUrp(instance);
            }

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Added " + TreeCount + " mid-distance trees between the near scenery ring and the distant mountains.");
        }

        private static GameObject LoadPropPrefab(string name)
        {
            string path = $"{AssetRoot}/{name}.fbx";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError("Could not find tree FBX at " + path);
            }

            return prefab;
        }

        private static void ConvertMaterialsToTintedUrp(GameObject instance)
        {
            EnsureFolder(MaterialsFolder);

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                Material[] sourceMaterials = renderer.sharedMaterials;
                var converted = new Material[sourceMaterials.Length];
                for (int i = 0; i < sourceMaterials.Length; i++)
                {
                    converted[i] = ConvertToTintedUrpLit(sourceMaterials[i]);
                }

                renderer.sharedMaterials = converted;
            }
        }

        private static Material ConvertToTintedUrpLit(Material source)
        {
            if (source == null)
            {
                return null;
            }

            string path = $"{MaterialsFolder}/{source.name}_MidDistance_URP.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            var urpMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            Color baseColor = source.HasProperty("_Color") ? source.color : Color.white;
            urpMaterial.color = baseColor * MidDistanceTint;
            urpMaterial.SetFloat("_Smoothness", 0.15f);

            AssetDatabase.CreateAsset(urpMaterial, path);
            return urpMaterial;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path).Replace("\\", "/");
            string folderName = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
