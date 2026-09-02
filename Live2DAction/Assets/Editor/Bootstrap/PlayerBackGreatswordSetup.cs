using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Live2DAction.EditorTools
{
    // 2026-08-31, explicit user request ("player 背上那把 wolf 大劍裝飾品消失了") - puts the Wolf's
    // Gravestone claymore back on the player as a PURELY COSMETIC back accessory. It vanished in
    // 追加77 when PlayerKatanaSetup replaced the hand weapon (that tool deletes any object literally
    // named "WolfsGravestone", which is what the user's hand-arranged back sword was called).
    //
    // 追加81 續 4, user: "檢查幾個版本前的大件裝飾品是如何擺放在玩家背後 ... 應該是劍柄左上刀劍右下
    // 擺放，請以現版本來修正". Restored VERBATIM from the arrangement committed in the scene up to
    // 0830 (git d735761 / 8ecb5fb) - hand-tuned by the user on 2026-08-23, and it already IS
    // "劍柄左上刀劍右下":
    //   - parented DIRECTLY to the Player root (NOT a spine bone, NOT under Visual) - a rigid back
    //     accessory, scale 1. The Player root is unscaled; a spine bone has ~80x lossy scale that
    //     would multiply a plain localPosition and fling the sword metres away (hit this in the
    //     first 追加81 pass).
    //   - localPosition (1, -0.80115217, -0.2), localRotation Euler(0, 0, 43), localScale 1.
    //   - The FBX's GRIP is at the model's +Y end (mesh pCylinder5 at local Y≈2.37, not the
    //     origin); the origin end is the blade tip. So Euler(0,0,43) sends the grip up toward the
    //     LEFT shoulder and the pivot/tip sits at the lower right - hilt upper-left, blade
    //     lower-right, exactly as asked.
    // These values are the authored authority (same status as the camera distance / weapon grip
    // values). Re-tune by editing the consts + re-running this menu, then eyeball it.
    //
    // Deliberately NOT named "WolfsGravestone": UltimateAbility.FindWeapon() throws the object by
    // that exact name, and the R ultimate throws the KATANA in the hand, not this decoration.
    //
    // ⚠ Genshin_WGS.fbx is a direct Genshin Impact / HoYoverse weapon reproduction, marked
    // DoNotShip (Docs/ASSET_LICENSES.md, DoNotShipBuildGuard.cs). Internal-prototype placeholder
    // ONLY - never in any build handed to anyone (CLAUDE.md rule 2). Swap for an original model.
    //
    // Wired into ThirdPersonCameraController.firstPersonHiddenAccessory so it's hidden while
    // aiming / first-person (it would otherwise sit right on the lens). Carries its own
    // MeshBoundsFixer - the FBX imports with degenerate bounds, same as the katana, so without it
    // the whole renderer frustum-culls away the moment its pivot leaves screen centre.
    internal static class PlayerBackGreatswordSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string FbxPath = "Assets/_Project/Characters/Placeholder/Weapons/WolfsGravestone/Genshin_WGS.fbx";
        private const string TopMatPath = "Assets/_Project/Characters/Placeholder/Weapons/WolfsGravestone/Materials/WGS_Top.mat";
        private const string BottomMatPath = "Assets/_Project/Characters/Placeholder/Weapons/WolfsGravestone/Materials/WGS_Bottom.mat";
        private const string DecorObjectName = "BackGreatswordDecor";

        // The 2026-08-23 hand-tuned pose, restored verbatim from the scene at git d735761 / 8ecb5fb.
        // Parented to the Player root. Grip is the model's +Y end; Euler(0,0,43) lays it diagonally
        // - hilt up toward the left shoulder, blade down toward the right hip ("劍柄左上刀劍右下").
        private static readonly Vector3 DecorLocalPosition = new Vector3(1f, -0.80115217f, -0.2f);
        private static readonly Vector3 DecorLocalEuler = new Vector3(0f, 0f, 43f);
        private static readonly Vector3 DecorLocalScale = Vector3.one;

        [MenuItem("Tools/Live2DAction/Attach Wolf's Gravestone As Back Decoration")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Exit Play Mode first - this edits the scene.");
                return;
            }

            GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbxAsset == null)
            {
                Debug.LogError("PlayerBackGreatswordSetup: could not load FBX at " + FbxPath);
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("PlayerBackGreatswordSetup: no Player in " + ScenePath);
                return;
            }

            foreach (Transform t in player.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && t.name == DecorObjectName)
                {
                    Object.DestroyImmediate(t.gameObject);
                }
            }

            GameObject decor = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset, player.transform);
            decor.name = DecorObjectName;
            decor.transform.localScale = DecorLocalScale;
            decor.transform.localPosition = DecorLocalPosition;
            decor.transform.localRotation = Quaternion.Euler(DecorLocalEuler);

            ApplyMaterials(decor);

            if (decor.GetComponent<Live2DAction.World.MeshBoundsFixer>() == null)
            {
                decor.AddComponent<Live2DAction.World.MeshBoundsFixer>();
            }
            foreach (MeshFilter mf in decor.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh != null)
                {
                    mf.sharedMesh.RecalculateBounds();
                }
            }

            WireFirstPersonHidden(decor.transform);

            EditorUtility.SetDirty(player);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"PlayerBackGreatswordSetup: mounted '{DecorObjectName}' on the Player root " +
                      $"(pos {DecorLocalPosition}, euler {DecorLocalEuler}, scale {DecorLocalScale.x}). " +
                      "Cosmetic only - NOT thrown by the R ultimate.");
        }

        private static void ApplyMaterials(GameObject decor)
        {
            Material top = AssetDatabase.LoadAssetAtPath<Material>(TopMatPath);
            Material bottom = AssetDatabase.LoadAssetAtPath<Material>(BottomMatPath);
            if (top == null || bottom == null)
            {
                Debug.LogWarning("PlayerBackGreatswordSetup: WGS_Top/WGS_Bottom .mat not found - run " +
                                 "'Attach Wolf's Gravestone Weapon To Player5' once to build them. Leaving FBX default mats.");
                return;
            }

            foreach (Renderer renderer in decor.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = renderer.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    mats[i] = mats[i].name.Contains("Top") ? top : bottom;
                }
                renderer.sharedMaterials = mats;
            }
        }

        private static void WireFirstPersonHidden(Transform decor)
        {
            // There are TWO ThirdPersonCameraControllers in the scene - one on "Main Camera" (the
            // player's) and one on "CatCamera" (inactive, used while possessing the cat). Wire
            // BOTH: an early return here would stop at whichever the iteration hit first and leave
            // the other with an empty field.
            int wired = 0;
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mb == null || mb.GetType().Name != "ThirdPersonCameraController") continue;
                var so = new SerializedObject(mb);
                SerializedProperty p = so.FindProperty("firstPersonHiddenAccessory");
                if (p != null)
                {
                    p.objectReferenceValue = decor;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(mb);
                    wired++;
                }
            }
            if (wired == 0)
            {
                Debug.LogWarning("PlayerBackGreatswordSetup: no ThirdPersonCameraController found - " +
                                 "back sword won't auto-hide in first person.");
            }
        }
    }
}
