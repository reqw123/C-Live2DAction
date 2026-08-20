using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.Core;
using Live2DAction.UI;

namespace Live2DAction.EditorTools
{
    // 2026-08-16, explicit user follow-up to the energy bar pulse ("不夠炫 加個閃電繞圈的特效") -
    // builds the LineRenderer UltimateReadyAura drives. Player-only, see that class's own
    // comment for why this isn't also wired to Enemy.
    //
    // 2026-08-16 rewrite ("閃電改為只有一條，從角色底部任意往上環繞，循環，就像是動漫獵人x獵人的
    // 奇犽一樣") - was 8 (then 6) separate ring bolts, now a single coiling LineRenderer. This
    // Apply() explicitly re-syncs every tuning field via SerializedObject on every run, not
    // just energy/bolt - see the previous ring version's own note on why (Unity keeps a
    // component's serialized values once AddComponent has run once; new class defaults only
    // apply to fresh instances, so re-running this tool is the one place a tuning change
    // actually has to work).
    internal static class UltimateReadyAuraSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string MaterialPath = "Assets/_Project/VFX/LightningBolt.mat";
        private const string RootName = "UltimateReadyAura";

        private const float Radius = 0.55f;
        private const float BaseHeight = 0.05f;
        // See UltimateReadyAura's own field comment - re-measured with the weapon excluded,
        // actual head height is ~0.83 above Player's transform, not the ~1.35 this was
        // originally (wrongly) calibrated against.
        private const float ClimbHeight = 0.78f;
        private const float SpiralTurns = 2.5f;
        private const float LoopDurationSeconds = 1.2f;
        private const float GrowFraction = 0.5f;
        private const float FadeStart01 = 0.8f;
        private const float CrackleIntervalSeconds = 0.06f;
        private const float JitterAmount = 0.05f;
        private const int SegmentCount = 24;
        // Bright enough (well past 1.0) to catch the Bloom volume added earlier, same trick as
        // the energy bar's own full-state pulse.
        private static readonly Color BoltColor = new Color(0.6f, 0.9f, 2.2f);

        [MenuItem("Tools/Live2DAction/Add Ultimate Ready Lightning Aura")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("Player GameObject not found in " + ScenePath);
                return;
            }

            UltimateEnergy energy = player.GetComponent<UltimateEnergy>();
            if (energy == null)
            {
                Debug.LogError("Player has no UltimateEnergy - run 'Add Ultimate Ability' first.");
                return;
            }

            Transform existingRoot = player.transform.Find(RootName);
            if (existingRoot != null)
            {
                Object.DestroyImmediate(existingRoot.gameObject);
            }

            var root = new GameObject(RootName);
            root.transform.SetParent(player.transform, false);

            Material material = CreateOrLoadBoltMaterial();
            LineRenderer bolt = CreateBolt(root.transform, material);
            bolt.gameObject.SetActive(false); // UltimateReadyAura turns this on only while energy.IsFull

            UltimateReadyAura aura = player.GetComponent<UltimateReadyAura>();
            if (aura == null)
            {
                aura = player.AddComponent<UltimateReadyAura>();
            }

            var so = new SerializedObject(aura);
            so.FindProperty("energy").objectReferenceValue = energy;
            so.FindProperty("bolt").objectReferenceValue = bolt;
            so.FindProperty("radius").floatValue = Radius;
            so.FindProperty("baseHeight").floatValue = BaseHeight;
            so.FindProperty("climbHeight").floatValue = ClimbHeight;
            so.FindProperty("spiralTurns").floatValue = SpiralTurns;
            so.FindProperty("loopDurationSeconds").floatValue = LoopDurationSeconds;
            so.FindProperty("growFraction").floatValue = GrowFraction;
            so.FindProperty("fadeStart01").floatValue = FadeStart01;
            so.FindProperty("crackleIntervalSeconds").floatValue = CrackleIntervalSeconds;
            so.FindProperty("jitterAmount").floatValue = JitterAmount;
            so.FindProperty("segmentCount").intValue = SegmentCount;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Added a single coiling lightning bolt to Player, climbing from the feet and looping while the ultimate is ready.");
        }

        private static LineRenderer CreateBolt(Transform parent, Material material)
        {
            var go = new GameObject("Bolt");
            go.transform.SetParent(parent, false);

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.material = material;
            line.startColor = BoltColor;
            line.endColor = BoltColor;
            line.startWidth = 0.04f;
            line.endWidth = 0.02f;
            line.numCapVertices = 0;
            line.numCornerVertices = 0;
            // Sharp jagged corners read as "lightning", not smoothed-out shading.
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;

            return line;
        }

        private static Material CreateOrLoadBoltMaterial()
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
            material.SetColor("_BaseColor", Color.white); // bolt color itself comes from LineRenderer.startColor/endColor via vertex color
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }
    }
}
