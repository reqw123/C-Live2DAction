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
            visual.transform.localPosition = VisualFeetOffset(player);
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
            RemoveEmbeddedPhysicsRig(visual);

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Replaced Player visual with Maya (anime placeholder).");
        }

        // Player's own transform sits at the CENTER of its CharacterController's capsule
        // (center=(0,0,0), so the capsule spans from transform.y-height/2 to
        // transform.y+height/2) - it's the CENTER that's grounded at the right height (see
        // GreyboxSceneBuilder.CreatePlayer's spawn-Y formula), not the feet. Maya's own mesh
        // origin is at her feet (standard humanoid rig convention), so parenting her at a
        // flat Vector3.zero - as this used to do - puts her feet at the capsule's CENTER
        // height, floating half the capsule's height above the ground. 2026-08-12 real bug
        // report (screenshot showed feet not touching the floor after the Rigidbody fix
        // below reset this to zero): needs to be shifted down by half the capsule height,
        // derived from the actual CharacterController rather than hardcoded, so this can't
        // silently drift out of sync if height/center are ever tuned later (same reasoning as
        // GreyboxSceneBuilder's own spawn-Y formula). internal (not private) - same reuse
        // reasoning as RemoveEmbeddedCameraRig/RemoveEmbeddedPhysicsRig below: Player5's own
        // FBX root also sits at its feet (verified via bounds dump), so Player5VisualSetup
        // reuses this instead of re-deriving the same formula.
        internal static Vector3 VisualFeetOffset(GameObject player)
        {
            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller == null)
            {
                return Vector3.zero;
            }

            return new Vector3(0f, controller.center.y - controller.height / 2f, 0f);
        }

        // The Sketchfab package ships with its own preview camera rig (a "MainCamera"-tagged
        // Camera parented to the neck bone, for the asset author's own turntable renders).
        // Left in place, it collides with GameObject.FindWithTag("MainCamera") lookups (it
        // was mistaken for the scene's real camera by an earlier fix script, attaching our
        // camera controller to the character's neck bone instead - see Docs/KNOWN_ISSUES.md)
        // and would otherwise render as a second, unwanted camera every frame regardless.
        // internal (not private) so any other tool instantiating this same Maya prefab (e.g.
        // TrainingDummySetup.cs) can reuse this cleanup instead of duplicating it - same
        // reasoning as HealthBarSetup.AddHealthBar being made internal for reuse.
        internal static void RemoveEmbeddedCameraRig(GameObject visual)
        {
            foreach (Camera embeddedCamera in visual.GetComponentsInChildren<Camera>(true))
            {
                Object.DestroyImmediate(embeddedCamera.gameObject);
            }
        }

        // The Sketchfab package's root object also ships with its own Rigidbody (mass 80,
        // gravity on, position unconstrained - only rotation frozen) and CapsuleCollider,
        // presumably left over from the asset author's own turntable/preview setup - same
        // category of leftover as the embedded camera rig above. 2026-08-12 real bug report:
        // as a child of Player (whose position is driven by CharacterController, not
        // physics), this Rigidbody gets simulated independently by Unity's physics engine -
        // falling under its own gravity and colliding with Player's own CharacterController
        // capsule - and visibly launches the character's mesh up into the air, completely
        // detached from where the (correctly grounded) parent Player transform actually is.
        // This project doesn't use Rigidbody physics for characters at all (movement is
        // CharacterController-driven; combat hit/hurtboxes are separate collider data per
        // CLAUDE.md rule 4) - there's no scenario where keeping this makes sense.
        // internal for the same reuse reason as RemoveEmbeddedCameraRig above.
        internal static void RemoveEmbeddedPhysicsRig(GameObject visual)
        {
            foreach (Rigidbody rigidbody in visual.GetComponentsInChildren<Rigidbody>(true))
            {
                Object.DestroyImmediate(rigidbody);
            }

            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(collider);
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
