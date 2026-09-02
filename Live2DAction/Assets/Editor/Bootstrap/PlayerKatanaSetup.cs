using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Live2DAction.EditorTools
{
    // 2026-08-31, explicit user request ("將 blood_katana_retextured.glb 此武士刀銜接在 player
    // 右手上握著，幫我調整尺寸") - REPLACES Player5WeaponSetup's "Wolf's Gravestone" claymore
    // (a direct Genshin Impact reproduction, DoNotShip - see Docs/ASSET_LICENSES.md) with an
    // original blood-katana model held in Player5's right hand.
    //
    // The mounted GameObject is still NAMED "WolfsGravestone" on purpose: UltimateAbility.cs
    // finds the thrown weapon purely by that name (WeaponObjectName const, FindWeapon()), so
    // keeping the name means the R-ultimate throw sequence keeps working with zero code change.
    //
    // Structure:
    //   Rhand_Weapon2 (hand bone, ~80x lossy scale)
    //     └─ WolfsGravestone            <- wrapper, this is what UltimateAbility throws
    //          └─ BladeMesh             <- BloodKatana.glb instance
    //
    // Why the wrapper: UltimateAbility.ThrowSequence assumes the thrown transform's pivot sits
    // at the blade TIP and that local -Y points tip-first (it does
    // Quaternion.FromToRotation(Vector3.down, flightDirection) to orient the throw). The GLB's
    // own pivot is near the blade's centre with the blade along mesh -X, so the wrapper
    // re-homes the pivot to the grip/hand and orients its local -Y down the blade toward the
    // tip. BladeMesh then carries a fixed offset that puts the katana's grip at the wrapper
    // origin (= the fist) with the blade running out along the wrapper's -Y.
    //
    // The wrapper localRotation and the BladeMesh offset below are USER-TUNED-style authored
    // values (blade held forward and angled ~19 deg down - a "sword lowered, ready" idle),
    // NOT a formula. Same "authoritative until the user says otherwise" status as the camera
    // distance / CharacterController.stepOffset values - don't "fix" them back toward a
    // recomputed grip formula without asking. Re-tune by editing the constants and re-running
    // this menu item, then eyeball it in Play mode.
    internal static class PlayerKatanaSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string GlbPath = "Assets/_Project/Characters/Weapons/BloodKatana/BloodKatana.glb";
        private const string HandBoneName = "Rhand_Weapon2";
        private const string WeaponObjectName = "WolfsGravestone"; // must match UltimateAbility.WeaponObjectName
        private const string BladeChildName = "BladeMesh";

        // Wrapper sits at the hand bone origin; its local -Y runs down the blade toward the tip.
        private static readonly Vector3 WrapperLocalPosition = Vector3.zero;
        private static readonly Quaternion WrapperLocalRotation =
            new Quaternion(0.638202f, 0.000000f, -0.565351f, 0.522568f);

        // BloodKatana.glb: blade along mesh -X, grip along mesh +X (grip centre ~ mesh (6.3, 0, 0.1)).
        // Euler (0,0,90) maps mesh +X -> wrapper +Y so the blade (mesh -X) runs along wrapper -Y;
        // the position offset then slides the grip centre back onto the wrapper origin.
        private static readonly Vector3 BladeLocalPosition = new Vector3(0f, -0.004725f, -0.000075f);
        private static readonly Vector3 BladeLocalEuler = new Vector3(0f, 0f, 90f);
        private static readonly Vector3 BladeLocalScale = new Vector3(0.00075f, 0.00075f, 0.00075f);

        [MenuItem("Tools/Live2DAction/Attach Blood Katana To Player Hand")]
        public static void Apply()
        {
            GameObject glbAsset = AssetDatabase.LoadAssetAtPath<GameObject>(GlbPath);
            if (glbAsset == null)
            {
                Debug.LogError("Could not load katana GLB at " + GlbPath);
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("Player GameObject not found in " + ScenePath);
                return;
            }

            Transform handBone = FindDeepChild(player.transform, HandBoneName);
            if (handBone == null)
            {
                Debug.LogError($"Could not find '{HandBoneName}' bone under Player.");
                return;
            }

            // Clear any prior weapon mount (the old Genshin claymore, or a re-run of this tool),
            // wherever it currently lives under the Player hierarchy.
            foreach (Transform t in player.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && t.name == WeaponObjectName)
                {
                    Object.DestroyImmediate(t.gameObject);
                }
            }

            GameObject wrapper = new GameObject(WeaponObjectName);
            wrapper.transform.SetParent(handBone, false);
            wrapper.transform.localPosition = WrapperLocalPosition;
            wrapper.transform.localRotation = WrapperLocalRotation;
            wrapper.transform.localScale = Vector3.one;

            GameObject blade = (GameObject)PrefabUtility.InstantiatePrefab(glbAsset, wrapper.transform);
            blade.name = BladeChildName;
            blade.transform.localPosition = BladeLocalPosition;
            blade.transform.localRotation = Quaternion.Euler(BladeLocalEuler);
            blade.transform.localScale = BladeLocalScale;

            // BloodKatana.glb imports with degenerate (0,0,0) mesh bounds (the glTFast pipeline
            // never runs RecalculateBounds) - and the blade parts are plain MeshRenderers, so
            // `updateWhenOffscreen` (SkinnedMeshRenderer-only) doesn't help. A one-off
            // RecalculateBounds() here does NOT stick: mesh bounds are sub-asset data, regenerated
            // broken on every reimport and reloaded fresh every time the scene loads. So the katana
            // was getting whole-renderer frustum-culled the instant the hand left screen centre
            // (2026-08-31, user "katana 武士刀應該被握在 player 右手手上" - it was there, just
            // invisible in Play). Fix: a MeshBoundsFixer on the wrapper (same component the sky
            // island / cat use) re-runs RecalculateBounds at load, [ExecuteAlways] so the Editor
            // preview self-heals too.
            if (wrapper.GetComponent<Live2DAction.World.MeshBoundsFixer>() == null)
            {
                wrapper.AddComponent<Live2DAction.World.MeshBoundsFixer>();
            }
            foreach (MeshFilter mf in blade.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh != null)
                {
                    mf.sharedMesh.RecalculateBounds();
                }
            }

            EditorUtility.SetDirty(player);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"Attached Blood Katana ('{WeaponObjectName}' > '{BladeChildName}') to Player's {HandBoneName} bone.");
        }

        private static Transform FindDeepChild(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child;
                }
            }

            return null;
        }
    }
}
