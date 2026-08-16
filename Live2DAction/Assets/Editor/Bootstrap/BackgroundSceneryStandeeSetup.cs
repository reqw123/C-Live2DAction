using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
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

        // 2026-08-16: this ring was originally scaled with one flat 0.8-1.4x multiplier
        // applied to every prop regardless of type - discovered (while calibrating
        // DistantMountainsSetup/MidDistanceTreeRingSetup for the same asset pack) that the
        // Quaternius FBX files import at wildly different native scales per category
        // (measured live via execute_code: Tree ~0.03 world-units of height per 1x scale,
        // Rock ~0.027, Bush ~0.0096, Grass as low as ~0.0001 for the Grass1 variant vs
        // ~0.0025 for Grass2/3) - a single uniform multiplier could never size all four
        // categories correctly at once, and in practice left this entire ring rendering as
        // sub-centimeter, effectively invisible specks since 2026-08-12. Fixed at the root
        // instead of hardcoding another guessed multiplier: measures each instantiated prop's
        // actual bounds at its native scale, then computes exactly the scale factor needed to
        // hit a randomly chosen target height within its category's design range below - self-
        // correcting regardless of how any individual asset happens to be imported, so this
        // can't quietly go stale again the way the flat multiplier did.
        private static readonly Dictionary<string, (float min, float max)> CategoryTargetHeight = new Dictionary<string, (float, float)>
        {
            { "Tree", (4f, 6f) },
            { "Rock", (1.5f, 2.5f) },
            { "Bush", (0.8f, 1.3f) },
            { "Grass", (0.3f, 0.5f) },
        };

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
                instance.transform.localScale = Vector3.one;
                instance.transform.localScale = Vector3.one * ComputeCalibratedScale(instance, propName, random);

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

        // Measures instance's actual rendered size at its current (native, scale=1) size and
        // computes exactly the multiplier needed to hit a random target size within propName's
        // category range - see CategoryTargetHeight's own comment for why this is measured
        // live per-instance instead of a hardcoded per-category constant.
        //
        // Grass calibrates against the LARGEST of the three bounding dimensions rather than
        // height - Grass1 turned out to have a completely different native aspect ratio from
        // Grass2/3 (a flat, wide patch rather than a clump), and height-only calibration scaled
        // it up into a 14-unit-wide sprawl to hit the same ~0.4-unit height target. Every other
        // category stays height-calibrated - applying the same largest-dimension rule to Tree
        // would instead calibrate off canopy width (wider than tall for this pack's tree
        // meshes) and undershoot the intended height range.
        private static float ComputeCalibratedScale(GameObject instance, string propName, System.Random random)
        {
            string category = Regex.Replace(propName, "[0-9]+$", ""); // "Tree1" -> "Tree", etc.
            if (!CategoryTargetHeight.TryGetValue(category, out (float min, float max) targetRange))
            {
                Debug.LogWarning($"No target height range registered for category '{category}' (from '{propName}') - leaving at native scale.");
                return 1f;
            }

            Renderer renderer = instance.GetComponentInChildren<Renderer>();
            float nativeSize;
            if (renderer == null)
            {
                nativeSize = 0f;
            }
            else if (category == "Grass")
            {
                nativeSize = Mathf.Max(renderer.bounds.size.x, renderer.bounds.size.y, renderer.bounds.size.z);
            }
            else
            {
                nativeSize = renderer.bounds.size.y;
            }

            // Deliberately far below Grass1's own genuine native height (~0.0001, confirmed
            // live via execute_code) - that variant's mesh really is that tiny, it's not a
            // degenerate/zero bounds case. This threshold only needs to catch an actually
            // empty/missing renderer.
            if (nativeSize <= 0.000001f)
            {
                Debug.LogWarning($"'{propName}' has no measurable renderer bounds - leaving at native scale.");
                return 1f;
            }

            float targetSize = targetRange.min + (float)random.NextDouble() * (targetRange.max - targetRange.min);
            return targetSize / nativeSize;
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
