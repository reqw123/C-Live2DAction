using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Live2DAction.EditorTools
{
    // Populates the area just outside GreyboxTest's boundary walls with the CC0 Quaternius
    // "Simple Nature Pack" (see Docs/ASSET_LICENSES.md) - trees/rocks/bushes/grass the player
    // can see but never reach (BoundaryWalls block movement at Ground's edge, see
    // GreyboxSceneBuilder.CreateBoundaryWalls). Purely a "背景" dressing pass around the fixed
    // arena; run after Build Greybox Test Scene, same two-step pattern as
    // FemaleStandeeSetup/EnemyHumanoidVisualSetup - this one has no ordering dependency on
    // Player's Animator, but a separate script keeps GreyboxSceneBuilder from growing yet
    // another unrelated concern.
    internal static class BackgroundSceneryStandeeSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string AssetRoot = "Assets/_Project/Environment/Placeholder/QuaterniusSimpleNature";
        private const string MaterialsFolder = "Assets/_Project/Environment/Materials/QuaterniusSimpleNature";
        private const string ParentName = "BackgroundScenery";

        private static readonly string[] PropFileNames =
        {
            "Tree1", "Tree2", "Tree3", "Tree4",
            "Rock1", "Rock2", "Rock3",
            "Bush1", "Bush2", "Bush3",
            "Grass1", "Grass2", "Grass3",
        };

        // Ring just outside the boundary walls (walls sit ~0.5 past Ground's 15-unit
        // half-extent, see GreyboxSceneBuilder.CreateBoundaryWalls) out to where
        // BackgroundTerrain still covers the ground - visible, never walkable.
        private const float InnerRadius = 17f;
        private const float OuterRadius = 26f;
        private const int PropCount = 40;

        [MenuItem("Tools/Live2DAction/Add Background Scenery")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject existing = GameObject.Find(ParentName);
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing);
            }

            var parent = new GameObject(ParentName);

            // Fixed seed so re-running this tool always lays props out identically (matches
            // the project's re-buildable-scene pattern, see GreyboxSceneBuilder) instead of
            // reshuffling the arena's surroundings on every run. Explicitly System.Random, not
            // UnityEngine.Random, so the sequence is reproducible independent of engine state.
            var random = new System.Random(20260812);

            for (int i = 0; i < PropCount; i++)
            {
                string propName = PropFileNames[random.Next(PropFileNames.Length)];
                GameObject prefab = LoadPropPrefab(propName);
                if (prefab == null)
                {
                    continue;
                }

                float angle = (float)(random.NextDouble() * System.Math.PI * 2.0);
                float radius = InnerRadius + (float)random.NextDouble() * (OuterRadius - InnerRadius);
                var position = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
                instance.name = propName + "_" + i;
                instance.transform.localPosition = position;
                instance.transform.localRotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                float scale = 0.8f + (float)random.NextDouble() * 0.6f;
                instance.transform.localScale = Vector3.one * scale;

                ConvertMaterialsToUrp(instance);
            }

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Added " + PropCount + " background scenery props around GreyboxTest's boundary.");
        }

        private static GameObject LoadPropPrefab(string name)
        {
            string path = $"{AssetRoot}/{name}.fbx";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError("Could not find background scenery FBX at " + path);
            }

            return prefab;
        }

        // The pack ships untextured (see Docs/ASSET_LICENSES.md) with legacy Built-in-RP
        // Standard-shader materials baked into each FBX - those render as magenta under URP.
        // Rebuilds an equivalent URP/Lit material per source color, same fix already applied to
        // Maya / the Universal Base Characters (see PlayerMayaVisualSetup / FemaleStandeeSetup's
        // material comments).
        private static void ConvertMaterialsToUrp(GameObject instance)
        {
            EnsureFolder(MaterialsFolder);

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                Material[] sourceMaterials = renderer.sharedMaterials;
                var converted = new Material[sourceMaterials.Length];
                for (int i = 0; i < sourceMaterials.Length; i++)
                {
                    converted[i] = ConvertToUrpLit(sourceMaterials[i]);
                }

                renderer.sharedMaterials = converted;
            }
        }

        private static Material ConvertToUrpLit(Material source)
        {
            if (source == null)
            {
                return null;
            }

            string path = $"{MaterialsFolder}/{source.name}_URP.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            var urpMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (source.HasProperty("_Color"))
            {
                urpMaterial.color = source.color;
            }

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
