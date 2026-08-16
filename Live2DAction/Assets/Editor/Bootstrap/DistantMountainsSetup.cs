using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Live2DAction.EditorTools
{
    // 2026-08-16, explicit user request ("開放世界那樣的美麗風景") - option 2 of the two offered
    // (extend backdrop dressing rather than building a real Terrain-based open world, which is
    // a much bigger undertaking out of scope for this prototype phase). Reuses the existing CC0
    // Quaternius Rock1-3 meshes (already in the project, see BackgroundSceneryStandeeSetup),
    // scaled way up (10-18x) and pushed far past the existing near scenery ring (17-26) to read
    // as distant mountain silhouettes rather than boulders. Tinted hazy blue-grey - real distant
    // mountains desaturate and blue-shift from atmospheric scattering, and this also helps them
    // blend into EnableAtmosphericFogSetup's fog color instead of reading as oversized rocks.
    // BackgroundTerrain is 300x300 (150 half-extent, see GreyboxSceneBuilder.CreateBackgroundTerrain)
    // so radius 55-90 is comfortably still on solid ground, and the Main Camera's far clip plane
    // (1000) has plenty of room to spare.
    internal static class DistantMountainsSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string AssetRoot = "Assets/_Project/Environment/Placeholder/QuaterniusSimpleNature";
        private const string MaterialsFolder = "Assets/_Project/Environment/Materials/QuaterniusSimpleNature";
        private const string ParentName = "DistantMountains";

        private static readonly string[] RockFileNames = { "Rock1", "Rock2", "Rock3" };
        private static readonly Color HazyMountainTint = new Color(0.55f, 0.62f, 0.72f);

        private const float InnerRadius = 55f;
        private const float OuterRadius = 90f;
        private const int MountainCount = 16;

        // 2026-08-16: the Quaternius Rock FBX assets import at a genuinely tiny native scale -
        // confirmed live via execute_code (Renderer.bounds on the actually-instantiated
        // GameObject, not the prefab asset, which reported misleadingly larger bounds): at
        // localScale=500, Rock3's world-space height is only ~13.3 units (ratio ~0.0266
        // height-units per scale-unit). The first version of this script used 10-18x thinking
        // that would tower over the near scenery ring - it actually produced ~0.3-unit rocks,
        // invisible from any normal viewing distance. These constants are back-calculated from
        // that measured ratio to hit an actual ~20-35 unit mountain silhouette height.
        private const float MinScale = 700f;
        private const float MaxScale = 1300f;

        [MenuItem("Tools/Live2DAction/Add Distant Mountains")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject existing = GameObject.Find(ParentName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            var parent = new GameObject(ParentName);

            // Separate fixed seed from BackgroundSceneryStandeeSetup's so the two rings don't
            // end up mirroring each other's angular pattern.
            var random = new System.Random(20260816);

            for (int i = 0; i < MountainCount; i++)
            {
                string propName = RockFileNames[random.Next(RockFileNames.Length)];
                GameObject prefab = LoadPropPrefab(propName);
                if (prefab == null)
                {
                    continue;
                }

                float angle = (float)(random.NextDouble() * System.Math.PI * 2.0);
                float radius = InnerRadius + (float)random.NextDouble() * (OuterRadius - InnerRadius);
                var position = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
                instance.name = "Mountain_" + propName + "_" + i;
                instance.transform.localPosition = position;
                instance.transform.localRotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                float scale = MinScale + (float)random.NextDouble() * (MaxScale - MinScale);
                instance.transform.localScale = Vector3.one * scale;

                ConvertMaterialsToHazyUrp(instance);
            }

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Added " + MountainCount + " distant mountain silhouettes beyond the background scenery ring.");
        }

        private static GameObject LoadPropPrefab(string name)
        {
            string path = $"{AssetRoot}/{name}.fbx";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError("Could not find rock FBX at " + path);
            }

            return prefab;
        }

        private static void ConvertMaterialsToHazyUrp(GameObject instance)
        {
            EnsureFolder(MaterialsFolder);

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                Material[] sourceMaterials = renderer.sharedMaterials;
                var converted = new Material[sourceMaterials.Length];
                for (int i = 0; i < sourceMaterials.Length; i++)
                {
                    converted[i] = ConvertToHazyUrpLit(sourceMaterials[i]);
                }

                renderer.sharedMaterials = converted;
            }
        }

        private static Material ConvertToHazyUrpLit(Material source)
        {
            if (source == null)
            {
                return null;
            }

            string path = $"{MaterialsFolder}/{source.name}_Mountain_URP.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            var urpMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            // Assigned directly, not multiplied against the source rock's own albedo - the
            // source color turned out to be near-black (~0.09,0.1,0.11), confirmed live via
            // execute_code, so a multiply produced an almost-invisible near-black silhouette
            // regardless of tint instead of the intended hazy blue-grey.
            urpMaterial.color = HazyMountainTint;
            urpMaterial.SetFloat("_Smoothness", 0.05f);

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
