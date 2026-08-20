using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.Characters;
using Live2DAction.Targeting;

namespace Live2DAction.EditorTools
{
    // Static "Mecha" standee using an unverified-provenance mecha model the user
    // explicitly accepted the risk on (see Docs/ASSET_LICENSES.md). No rig, so this is
    // a static prop only - never wire this into playable/combat logic, and it must never
    // ship in any Build shared with anyone.
    internal static class MechaVisualSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string FbxPath = "Assets/_Project/Characters/Placeholder/MechaModel_DoNotShip/MechaCharacter2.fbx";
        private const float TargetHeightMeters = 2.2f;

        [MenuItem("Tools/Live2DAction/Add Mecha Standee")]
        public static void Apply()
        {
            GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbxAsset == null)
            {
                Debug.LogError("Could not load FBX at " + FbxPath);
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Transform existing = GameObject.Find("Mecha")?.transform;
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            var mecha = new GameObject("Mecha");
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset, mecha.transform);
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

            mecha.transform.position = new Vector3(2.5f, 0f, -2f);

            // Mecha originally had no Collider at all and the player just walked straight
            // through it (see Docs/CHANGELOG.md 2026-08-11) - added here (not just as a
            // one-off Fix script) so it can't regress again the next time this tool
            // recreates Mecha from scratch. Radius/height match that fix's own values
            // (roughly matching TargetHeightMeters above); pivot is at ground level (position
            // above is a bare Y=0, not TargetHeightMeters/2), so center is offset up by half
            // the height rather than sitting at the transform's own origin.
            CapsuleCollider collider = mecha.AddComponent<CapsuleCollider>();
            collider.radius = 0.6f;
            collider.height = 2.2f;
            collider.center = new Vector3(0f, 1.1f, 0f);

            // Same "fold into source" reasoning as the collider above (see
            // Docs/CHANGELOG.md 2026-08-11 "Player2 隨機漫遊") - decorative wandering so
            // Mecha isn't just a static mannequin; all default field values, no per-field
            // overrides were used originally.
            mecha.AddComponent<WanderMovement>();

            // Lets the player Q-lock Mecha as a stand-in enemy (see Docs/CHANGELOG.md
            // 2026-08-11 "Player2 補上鎖定") - no fields to configure,
            // TargetLockController finds it via FindObjectsByType<LockOnTarget> without any
            // extra registration.
            mecha.AddComponent<LockOnTarget>();

            AssignFallbackMaterial(renderers);

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Mecha mecha] scale={scale} rawHeight={rawHeight}");
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
