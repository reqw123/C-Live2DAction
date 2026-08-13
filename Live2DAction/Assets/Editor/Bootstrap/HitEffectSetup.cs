using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.Combat;

namespace Live2DAction.EditorTools
{
    // Builds a small procedural hit-spark particle prefab (2026-08-12, explicit user request:
    // "攻擊特效" -> clarified as hit effects specifically - a burst of sparks/flash at the
    // impact point, not a swing trail or hit-stop/screen-shake) and wires it to both Player's
    // and Player4's PlayerCombat (both can land hits on each other). No external art asset
    // needed - built entirely from a ParticleSystem using URP's built-in
    // "Universal Render Pipeline/Particles/Unlit" shader with additive blending for a bright
    // flash-like look, matching this project's established pattern of building placeholder
    // visuals procedurally when no art asset exists yet (see AttackPoseVisualizer/
    // GreyboxSceneBuilder's own skybox material).
    internal static class HitEffectSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string VfxFolder = "Assets/_Project/VFX";
        private const string MaterialPath = VfxFolder + "/HitEffect.mat";
        private const string PrefabPath = VfxFolder + "/HitEffect.prefab";

        [MenuItem("Tools/Live2DAction/Add Hit Effect To Combat")]
        public static void Apply()
        {
            GameObject prefab = CreateOrLoadHitEffectPrefab();

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            WireHitEffect("Player", prefab);
            WireHitEffect("Player4", prefab);

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Wired hit-spark particle effect into Player and Player4's PlayerCombat.");
        }

        private static void WireHitEffect(string name, GameObject prefab)
        {
            GameObject go = GameObject.Find(name);
            if (go == null)
            {
                Debug.LogError(name + " GameObject not found in " + ScenePath);
                return;
            }

            PlayerCombat combat = go.GetComponent<PlayerCombat>();
            if (combat == null)
            {
                Debug.LogError(name + " has no PlayerCombat component.");
                return;
            }

            var so = new SerializedObject(combat);
            so.FindProperty("hitEffectPrefab").objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static GameObject CreateOrLoadHitEffectPrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                return existing;
            }

            EnsureFolder(VfxFolder);

            var go = new GameObject("HitEffect");
            ParticleSystem ps = go.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = ps.main;
            main.duration = 0.3f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = 0.25f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.startColor = new Color(1f, 0.95f, 0.5f); // bright yellow-white spark
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 30;
            // Self-destructs once every particle has finished, instead of needing a separate
            // "destroy after N seconds" companion script.
            main.stopAction = ParticleSystemStopAction.Destroy;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 14, 18) });

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;

            // Fade out over the particle's short lifetime instead of popping off abruptly.
            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var alphaKeys = new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) };
            var colorKeys = new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) };
            var gradient = new Gradient();
            gradient.SetKeys(colorKeys, alphaKeys);
            colorOverLifetime.color = gradient;

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = CreateOrLoadHitEffectMaterial();

            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);

            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        private static Material CreateOrLoadHitEffectMaterial()
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                Debug.LogError("Could not find Universal Render Pipeline/Particles/Unlit shader.");
                return null;
            }

            var material = new Material(shader);
            // Additive blending reads as a bright flash/spark rather than a flat opaque blob -
            // standard choice for hit-impact VFX.
            material.SetFloat("_Surface", 1f); // Transparent
            material.SetFloat("_Blend", 1f); // Additive
            material.SetColor("_BaseColor", Color.white);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string folderName = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
