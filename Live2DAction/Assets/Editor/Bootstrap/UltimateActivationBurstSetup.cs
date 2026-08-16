using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.Combat;

namespace Live2DAction.EditorTools
{
    // 2026-08-16, explicit user request: 施放必殺技瞬間，身體周圍散發一瞬間的霸氣感 - builds the
    // ring + rays UltimateActivationBurst drives, and wires it into UltimateAbility.burst so it
    // actually plays when the ability activates.
    internal static class UltimateActivationBurstSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string MaterialPath = "Assets/_Project/VFX/UltimateBurst.mat";
        private const string RootName = "UltimateActivationBurst";

        private const int RaySpikeCount = 12;
        // Warm gold-orange, deliberately distinct from UltimateReadyAura's cool electric blue -
        // that effect reads as "ready and waiting" (continuous), this one needs to read as a
        // sudden release of power ("霸氣") rather than the same electricity firing twice. Well
        // past 1.0 to catch the Bloom volume, same trick as the ready aura/energy bar pulse.
        private static readonly Color BurstColor = new Color(2.2f, 1.6f, 0.3f);

        [MenuItem("Tools/Live2DAction/Add Ultimate Activation Burst")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("Player GameObject not found in " + ScenePath);
                return;
            }

            UltimateAbility ability = player.GetComponent<UltimateAbility>();
            if (ability == null)
            {
                Debug.LogError("Player has no UltimateAbility - run 'Add Ultimate Ability' first.");
                return;
            }

            Transform existingRoot = player.transform.Find(RootName);
            if (existingRoot != null)
            {
                Object.DestroyImmediate(existingRoot.gameObject);
            }

            var root = new GameObject(RootName);
            root.transform.SetParent(player.transform, false);

            Material material = CreateOrLoadBurstMaterial();
            LineRenderer ring = CreateLine(root.transform, material, "Ring", 0.08f);
            var rays = new LineRenderer[RaySpikeCount];
            for (int i = 0; i < RaySpikeCount; i++)
            {
                rays[i] = CreateLine(root.transform, material, "Ray_" + i, 0.04f);
                rays[i].positionCount = 2;
            }

            UltimateActivationBurst burst = player.GetComponent<UltimateActivationBurst>();
            if (burst == null)
            {
                burst = player.AddComponent<UltimateActivationBurst>();
            }

            var burstSo = new SerializedObject(burst);
            burstSo.FindProperty("ring").objectReferenceValue = ring;
            SerializedProperty raysProp = burstSo.FindProperty("rays");
            raysProp.arraySize = rays.Length;
            for (int i = 0; i < rays.Length; i++)
            {
                raysProp.GetArrayElementAtIndex(i).objectReferenceValue = rays[i];
            }
            burstSo.ApplyModifiedPropertiesWithoutUndo();

            var abilitySo = new SerializedObject(ability);
            abilitySo.FindProperty("burst").objectReferenceValue = burst;
            abilitySo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Added an activation burst (shockwave ring + " + RaySpikeCount + " rays) to Player, wired to fire when the ultimate activates.");
        }

        private static LineRenderer CreateLine(Transform parent, Material material, string name, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.material = material;
            line.startColor = BurstColor;
            line.endColor = BurstColor;
            line.startWidth = width;
            line.endWidth = width;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            go.SetActive(false); // UltimateActivationBurst.Play() turns these on

            return line;
        }

        private static Material CreateOrLoadBurstMaterial()
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("Live2DAction/VFX/AdditiveUnlit");
            if (shader == null)
            {
                Debug.LogError("Could not find shader Live2DAction/VFX/AdditiveUnlit");
                return null;
            }

            var material = new Material(shader);
            material.SetColor("_BaseColor", Color.white); // actual color comes from LineRenderer.startColor/endColor via vertex color
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }
    }
}
