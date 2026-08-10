using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Live2DAction.EditorTools
{
    // Swaps the Player's visual for the CC-BY "Maya" anime-style Humanoid character
    // (see Docs/ASSET_LICENSES.md - requires in-game attribution before any Build that
    // includes it ships). Replaces the Universal Base Characters placeholder, which stays
    // in the project as a backup/spare (see Docs/ASSET_LICENSES.md).
    internal static class PlayerMayaVisualSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string AssetRoot = "Assets/_Project/Characters/Placeholder/MayaAnime";
        private const string PrefabPath = AssetRoot + "/Prefabs/Maya.prefab";
        private const string MaterialsFolder = AssetRoot + "/Materials";

        [MenuItem("Tools/Live2DAction/Replace Player Visual With Maya (Anime)")]
        public static void Apply()
        {
            ConvertMaterialsToUrp();

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("Player GameObject not found in " + ScenePath);
                return;
            }

            for (int i = player.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(player.transform.GetChild(i).gameObject);
            }

            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, player.transform);
            visual.name = "Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            Animator animator = visual.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                // The character's own CharacterController/movement code drives position,
                // so animation-driven root motion would fight it and cause visual drift.
                animator.applyRootMotion = false;
            }

            RemoveEmbeddedCameraRig(visual);

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Replaced Player visual with Maya (anime placeholder).");
        }

        // The Sketchfab package ships with its own preview camera rig (a "MainCamera"-tagged
        // Camera parented to the neck bone, for the asset author's own turntable renders).
        // Left in place, it collides with GameObject.FindWithTag("MainCamera") lookups (it
        // was mistaken for the scene's real camera by an earlier fix script, attaching our
        // camera controller to the character's neck bone instead - see Docs/KNOWN_ISSUES.md)
        // and would otherwise render as a second, unwanted camera every frame regardless.
        private static void RemoveEmbeddedCameraRig(GameObject visual)
        {
            foreach (Camera embeddedCamera in visual.GetComponentsInChildren<Camera>(true))
            {
                Object.DestroyImmediate(embeddedCamera.gameObject);
            }
        }

        private static void ConvertMaterialsToUrp()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogError("Could not find Universal Render Pipeline/Lit shader.");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { MaterialsFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || material.shader == urpLit)
                {
                    continue;
                }

                Texture mainTex = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
                Color color = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;

                material.shader = urpLit;
                if (mainTex != null)
                {
                    material.SetTexture("_BaseMap", mainTex);
                }

                material.SetColor("_BaseColor", color);
                EditorUtility.SetDirty(material);
            }
        }
    }
}
