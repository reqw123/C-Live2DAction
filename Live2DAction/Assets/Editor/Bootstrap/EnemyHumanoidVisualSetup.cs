using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Live2DAction.EditorTools
{
    // Swaps the Enemy (TrainingDummy)'s plain capsule visual for the CC0 "Universal Base
    // Characters" Humanoid FBX (Quaternius, see Docs/ASSET_LICENSES.md) - the same
    // ship-safe placeholder already used for Player before Maya replaced it (see
    // PlayerHumanoidVisualSetup.cs, whose material/import-setup logic this mirrors). The
    // asset itself is already imported and set to a Humanoid rig from that earlier work, so
    // this only needs to instantiate it under the Enemy and reuse the existing material.
    //
    // Keeps the child named "Visual" (same convention as Player/Player2/the old capsule) so
    // AttackPoseVisualizer's existing swingTransform reference (enemy.transform.Find("Visual"))
    // and CharacterAnimatorLink-style lookups keep working unchanged - this is a pure visual
    // swap, no combat/AI wiring is touched.
    internal static class EnemyHumanoidVisualSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string AssetRoot = "Assets/_Project/Characters/Placeholder/UniversalBaseCharacters";
        private const string FbxPath = AssetRoot + "/Models/Superhero_Male_FullBody.fbx";
        private const string MaterialPath = AssetRoot + "/Materials/Superhero_Male.mat";

        [MenuItem("Tools/Live2DAction/Replace Enemy Visual With Humanoid Placeholder")]
        public static void Apply()
        {
            GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbxAsset == null)
            {
                Debug.LogError("Could not load FBX at " + FbxPath);
                return;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                Debug.LogError("Could not load material at " + MaterialPath + " - run Replace Player Visual With Humanoid Placeholder first to create it.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject enemy = GameObject.Find("TrainingDummy");
            if (enemy == null)
            {
                Debug.LogError("TrainingDummy GameObject not found in " + ScenePath);
                return;
            }

            Transform existingVisual = enemy.transform.Find("Visual");
            if (existingVisual != null)
            {
                Object.DestroyImmediate(existingVisual.gameObject);
            }

            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset, enemy.transform);
            visual.name = "Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            SkinnedMeshRenderer[] renderers = visual.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                renderer.sharedMaterial = material;
            }

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Replaced Enemy visual with Universal Base Characters humanoid placeholder.");
        }
    }
}
