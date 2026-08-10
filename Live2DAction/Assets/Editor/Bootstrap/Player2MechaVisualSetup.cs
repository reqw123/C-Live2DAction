using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Live2DAction.EditorTools
{
    // Static "Player2" standee using an unverified-provenance mecha model the user
    // explicitly accepted the risk on (see Docs/ASSET_LICENSES.md). No rig, so this is
    // a static prop only - never wire this into playable/combat logic, and it must never
    // ship in any Build shared with anyone.
    internal static class Player2MechaVisualSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string FbxPath = "Assets/_Project/Characters/Placeholder/MechaModel_DoNotShip/MechaCharacter2.fbx";
        private const float TargetHeightMeters = 2.2f;

        [MenuItem("Tools/Live2DAction/Add Mecha As Player2 Standee")]
        public static void Apply()
        {
            GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbxAsset == null)
            {
                Debug.LogError("Could not load FBX at " + FbxPath);
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Transform existing = GameObject.Find("Player2")?.transform;
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            var player2 = new GameObject("Player2");
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset, player2.transform);
            visual.name = "Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
            Bounds combined = renderers.Length > 0 ? renderers[0].bounds : new Bounds(visual.transform.position, Vector3.zero);
            foreach (Renderer r in renderers)
            {
                combined.Encapsulate(r.bounds);
            }

            float rawHeight = Mathf.Max(combined.size.y, 0.0001f);
            float scale = TargetHeightMeters / rawHeight;
            visual.transform.localScale = Vector3.one * scale;

            player2.transform.position = new Vector3(2.5f, 0f, -2f);

            AssignFallbackMaterial(renderers);

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Player2 mecha] scale={scale} rawHeight={rawHeight}");
        }

        private static void AssignFallbackMaterial(Renderer[] renderers)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                return;
            }

            foreach (Renderer r in renderers)
            {
                if (r.sharedMaterial == null || r.sharedMaterial.shader.name == "Hidden/InternalErrorShader")
                {
                    r.sharedMaterial = new Material(urpLit);
                }
                else if (r.sharedMaterial.shader != urpLit)
                {
                    r.sharedMaterial.shader = urpLit;
                }
            }
        }
    }
}
