using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.Targeting;

namespace Live2DAction.EditorTools
{
    // Adds a new standalone standee GameObject ("Player4") using a free anime-style Sketchfab
    // model ("【Anime Character】Arisa (Free / Unity 3D)" by 3D動漫風角色屋 / 3D Anime Character
    // Store - the same author/store as Maya, see Docs/ASSET_LICENSES.md - CC-BY, requires
    // in-game attribution before any Build that includes it ships, same requirement as Maya).
    // Same full-Unity-package structure as Maya too (FBX + Humanoid-ready rig + Animator
    // Controller with Idle/Walk/Run/Jump/Fall clips + pre-built materials/prefab), copied in
    // with its original .meta files intact so the Prefab/Animator's internal GUID references
    // still resolve (same reasoning as PlayerMayaVisualSetup.cs's own copy). Only the
    // Script/Demo/Readme/_VRM folders from the original download were left out - the author's
    // own movement/camera scripts would just be dead code duplicating this project's
    // CharacterMovement/ThirdPersonCameraController, and _VRM/Demo/Readme aren't used here.
    //
    // Purely a static "cast" placeholder for now (2026-08-12, explicitly requested) - not
    // wired into movement/combat, but does get a Collider (so the player can't walk through
    // it, matching Player2's precedent) and a LockOnTarget (explicitly requested as a "might
    // become an enemy or a lockable target later" - LockOnTarget alone has no fields/behavior
    // beyond making TargetLockController's scan find it, so this is a safe, cheap thing to
    // add now rather than needing a second pass later; actual enemy AI/combat is NOT added
    // here, that's a separate feature to build only if asked).
    internal static class Player4AnimeVisualSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string AssetRoot = "Assets/_Project/Characters/Placeholder/ArisaAnime";
        private const string PrefabPath = AssetRoot + "/Prefabs/Arisa.prefab";
        private const string MaterialsFolder = AssetRoot + "/Materials";
        private const string StandeeName = "Player4";

        // Clear of FemaleStandee (0,0,-8) / NatsuStandee (-6,0,-8) / LucyStandee (-3,0,-8) /
        // Player2 (2.5,0,-2) - same back row as the Live2D standees, further along +X.
        private static readonly Vector3 StandeePosition = new Vector3(5f, 0f, -8f);

        [MenuItem("Tools/Live2DAction/Add Anime Character As Player4 Standee")]
        public static void Apply()
        {
            ConvertMaterialsToUrp();

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject existing = GameObject.Find(StandeeName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefabAsset == null)
            {
                Debug.LogError("Could not load prefab at " + PrefabPath);
                return;
            }

            var standee = new GameObject(StandeeName);
            standee.transform.position = StandeePosition;

            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, standee.transform);
            visual.name = "Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            Animator animator = visual.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                // Same reasoning as Maya: this project's own movement code drives position,
                // so animation-driven root motion would fight it - not that this GameObject
                // moves at all right now (static standee), but keeping it off avoids surprise
                // drift if the Animator Controller's Idle clip has any root motion and this is
                // ever wired to movement later.
                animator.applyRootMotion = false;
            }

            // Same precaution as PlayerMayaVisualSetup.cs's RemoveEmbeddedPhysicsRig - a real
            // bug found this same day (an unconstrained Rigidbody shipped in the Maya prefab's
            // root got simulated independently of its parent's Transform and visibly launched
            // into the air). Checked defensively here too even though not confirmed present.
            RemoveEmbeddedPhysicsRig(visual);
            RemoveEmbeddedCameraRig(visual);
            RemoveMissingScripts(visual);

            AddCollider(standee);
            standee.AddComponent<LockOnTarget>();

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"Added {StandeeName} using the \"Arisa\" anime placeholder (CC-BY, see ASSET_LICENSES.md).");
        }

        private static void RemoveEmbeddedPhysicsRig(GameObject visual)
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

        private static void RemoveEmbeddedCameraRig(GameObject visual)
        {
            foreach (Camera embeddedCamera in visual.GetComponentsInChildren<Camera>(true))
            {
                Object.DestroyImmediate(embeddedCamera.gameObject);
            }
        }

        // The original package's prefab has components referencing the author's own
        // PlayerBasicCode.cs/PlayerMoveCode.cs/ThirdPerson.cs (under Script/), which we
        // deliberately did not import (same reasoning as skipping Demo/Readme - this
        // project's own CharacterMovement/ThirdPersonCameraController replace them, so
        // importing would just be dead/conflicting code). Without the scripts, those
        // components deserialize as "Missing Script" placeholders (confirmed via PlayMode
        // test log spam: "The referenced script on this Behaviour (Game Object 'Visual') is
        // missing!") - strip them from every GameObject in the hierarchy so the standee is
        // clean instead of carrying broken component references.
        private static void RemoveMissingScripts(GameObject visual)
        {
            foreach (Transform child in visual.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
            }
        }

        // Rough proportions for an adult human silhouette (author's own package - unlike the
        // FBX-only characters this project builds materials/scale for from scratch, Arisa's
        // prefab already ships at a sensible authored scale, so this doesn't need to measure
        // renderer bounds the way Player2MechaVisualSetup does).
        private static void AddCollider(GameObject standee)
        {
            CapsuleCollider collider = standee.AddComponent<CapsuleCollider>();
            collider.radius = 0.3f;
            collider.height = 1.6f;
            collider.center = new Vector3(0f, 0.8f, 0f);
        }

        // Same conversion as PlayerMayaVisualSetup.cs - the package's materials use Built-in
        // RP's Standard shader (renders magenta under URP).
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
