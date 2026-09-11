using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Live2DAction.CameraSystem;

namespace Live2DAction.EditorTools
{
    // 2026-09-11, user request ("給個按鍵 像t/c一樣給猜猜看攝影機視角，且他必須擁有跟player一樣的機制，
    // 包括移動、攻擊，f架勢 r必殺技等等" - confirmed key: G).
    //
    // Turns 猜猜看 (placed on 露營區's ground earlier, GuessWhoCharacterSetup.cs) into a THIRD
    // possessable character living in the PERSISTENT GreyboxTest scene alongside Player/Cat - a
    // possession key only makes sense if it works immediately without first walking all the way to
    // Map_Camp, exactly like C (cat) and T (watcher) already do everywhere. The 露營區 instance is
    // left alone as ambient set-dressing; this creates a SEPARATE, fully-rigged instance here.
    //
    // Strategy: clone "Player" wholesale (Object.Instantiate deep-copies every component AND
    // auto-remaps intra-hierarchy SerializedObject references - confirmed by dumping every combat
    // component's object-reference fields first: only camera fields (CharacterMovement.
    // cameraYawSource, TargetLockController.viewOrigin) and PlayerGuard's two arm-bone fields point
    // OUTSIDE the Player hierarchy or AT bones that won't exist on the new skeleton - everything
    // else (animator refs -> "Visual", canvases, hurtbox, guard volume, clash/attack sfx) resolves
    // inside the clone automatically). Then swap only the VISUAL: delete the old lacrimosa mesh +
    // Bip001 skeleton under Visual, drop in 猜猜看's own Humanoid-converted mesh/skeleton (see
    // GuessWhoCharacterSetup.cs's earlier Generic->Human reimport), and re-point the handful of
    // fields above to the new rig/camera. This is the ONLY way to get full Player-parity (movement,
    // combo attacks, F guard/parry, R ultimate, execution) without hand-authoring 6+ components'
    // worth of tuned values from scratch on an incompatible skeleton.
    //
    // Humanoid retargeting is what makes this possible at all: the shared NewAnimator_
    // PlayerImmersiveWalk controller plays muscle-space clips, so the SAME controller drives BOTH
    // Player's Bip001 rig and 猜猜看's Mixamo-style rig once each has its own valid Avatar - no new
    // animations needed.
    internal static class GuessWhoPlayerParitySetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string GuessWhoFbxPath = "Assets/_Project/Characters/Placeholder/GuessWho/FBX/GuessWho_Walking.fbx";
        private const string GuessWhoMaterialPath = "Assets/_Project/Characters/Placeholder/GuessWho/Materials/GuessWho_Body.mat";
        private const string CharacterName = "猜猜看";
        private const string CameraName = "GuessWhoCamera";
        private const Key PossessionKey = Key.G;

        // Same six components CameraPossessionSwitcher.playerControl already lists for Player -
        // mirrored 1:1 so 猜猜看 gets identical control gating.
        private static readonly string[] ControlComponentTypes =
        {
            "CharacterMovement", "PlayerCombat", "TargetLockController",
            "UltimateAbility", "ExecutionAbility", "PlayerGuard",
        };

        [MenuItem("Tools/Live2DAction/Give GuessWho Full Player Parity (G possession)")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("[GuessWhoPlayerParitySetup] 'Player' not found in " + ScenePath);
                return;
            }

            GameObject existing = FindInSceneIncludingInactive(CharacterName);
            if (existing != null) Object.DestroyImmediate(existing);
            GameObject existingCam = FindInSceneIncludingInactive(CameraName);
            if (existingCam != null) Object.DestroyImmediate(existingCam);

            // ---- 1. Deep-clone Player, reskin the Visual -------------------------------------
            GameObject gw = Object.Instantiate(player);
            gw.name = CharacterName;
            gw.transform.position = player.transform.position + new Vector3(0f, 0f, -2f);
            gw.transform.rotation = player.transform.rotation;

            Transform visual = gw.transform.Find("Visual");
            Animator animator = visual.GetComponent<Animator>();

            Transform oldMesh = visual.Find("player_004_lacrimosa_skin_LOD1");
            Transform oldSkeleton = visual.Find("player_004_lacrimosa_skin_LOD1_Skeleton");
            Transform weapon = oldSkeleton != null ? FindDeep(oldSkeleton, "WolfsGravestone") : null;
            if (weapon != null) weapon.SetParent(visual, worldPositionStays: false); // survive the skeleton delete below

            if (oldMesh != null) Object.DestroyImmediate(oldMesh.gameObject);
            if (oldSkeleton != null) Object.DestroyImmediate(oldSkeleton.gameObject);

            // Reset Visual to identity BEFORE parenting the new model - it currently carries the
            // 0.00728 scale tuned for the old (huge, cm-scale) lacrimosa import, which would shrink
            // 猜猜看 (already meter-scale) to a speck.
            visual.localScale = Vector3.one;
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;

            // Instantiate the new model at world origin FIRST (clean, unscaled bounds measurement),
            // measure its feet offset, THEN move its parts into Visual - avoids measuring bounds
            // through a parent whose scale is still being changed.
            GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(GuessWhoFbxPath);
            GameObject temp = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset);
            // A PrefabUtility instance refuses to have its children reparented out (Unity logs
            // "Setting the parent of a transform which resides in a Prefab instance is not
            // possible" and silently no-ops) - unpack it into a plain hierarchy first.
            PrefabUtility.UnpackPrefabInstance(temp, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            temp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            temp.transform.localScale = Vector3.one;

            Transform armature = temp.transform.Find("Armature");
            Transform meshT = temp.transform.Find("output_unwrapped");
            SkinnedMeshRenderer smr = meshT.GetComponent<SkinnedMeshRenderer>();
            float feetLocalY = smr.bounds.min.y;

            Material bodyMat = AssetDatabase.LoadAssetAtPath<Material>(GuessWhoMaterialPath);
            if (bodyMat != null) smr.sharedMaterial = bodyMat;

            armature.SetParent(visual, false);
            meshT.SetParent(visual, false);
            // "Icosphere" (scale 100, radius ~1, fully engulfing the character) is a Meshy export
            // artifact - not part of the actual model. Left behind on temp, destroyed with it.
            // (First discovered 2026-09-11: it washed out every screenshot pure white/grey - the
            // camera was rendering from inside a giant grey sphere.)
            Object.DestroyImmediate(temp);

            UnityEngine.Object[] avatarAssets = AssetDatabase.LoadAllAssetsAtPath(GuessWhoFbxPath);
            Avatar avatar = null;
            foreach (var o in avatarAssets) if (o is Avatar a) avatar = a;
            animator.avatar = avatar;
            animator.applyRootMotion = false; // matches Player

            // Feet flush with the CharacterController's own capsule bottom (matches how Visual's
            // Y offset already related to Player's CC before the reskin).
            CharacterController cc = gw.GetComponent<CharacterController>();
            float capsuleBottomLocalY = cc.center.y - cc.height * 0.5f;
            visual.localPosition = new Vector3(0f, capsuleBottomLocalY - feetLocalY, 0f);

            // ---- 2. Weapon: move from the old Bip001 hand bone to 猜猜看's own Humanoid hand ---
            Transform rightHand = FindDeep(armature, "RightHand");
            if (weapon != null && rightHand != null)
            {
                weapon.SetParent(rightHand, false);
                weapon.localPosition = Vector3.zero;
                weapon.localRotation = Quaternion.identity;
                weapon.localScale = Vector3.one * 0.03f; // starting guess, same order as Player's grip scale - needs visual tuning
            }
            else if (weapon != null)
            {
                Debug.LogWarning("[GuessWhoPlayerParitySetup] 'RightHand' bone not found - weapon left under Visual, unparented from any hand.");
                weapon.SetParent(visual, false);
            }

            // ---- 3. PlayerGuard's two arm-bone refs (pointed at bones that no longer exist) ----
            Component guard = gw.GetComponent("PlayerGuard");
            var guardSo = new SerializedObject(guard);
            Transform rightForeArm = FindDeep(armature, "RightForeArm");
            Transform rightArm = FindDeep(armature, "RightArm");
            guardSo.FindProperty("swordArmBone").objectReferenceValue = rightForeArm;
            guardSo.FindProperty("upperArmBone").objectReferenceValue = rightArm;
            guardSo.ApplyModifiedPropertiesWithoutUndo();

            // Debug overlay is conceptually Player-only (F9 dump) - drop it on the clone.
            var deflectDebug = gw.GetComponent("SekiroDeflectDebug");
            if (deflectDebug != null) Object.DestroyImmediate(deflectDebug);

            // ---- 4. Camera: clone Main Camera (keeps every tuned distance/FOV/duel value), then
            //         re-point only the handful of fields that must follow 猜猜看 specifically -----
            GameObject mainCamera = FindInSceneIncludingInactive("Main Camera");
            GameObject cam = Object.Instantiate(mainCamera);
            cam.name = CameraName;
            cam.transform.SetParent(null);
            cam.SetActive(false); // G turns it on

            var tpc = cam.GetComponent("ThirdPersonCameraController");
            var camSo = new SerializedObject(tpc);
            camSo.FindProperty("target").objectReferenceValue = gw.transform;
            camSo.FindProperty("lockOnSource").objectReferenceValue = gw.GetComponent("TargetLockController");
            camSo.FindProperty("inputSource").objectReferenceValue = gw.GetComponent("PlayerInputProvider");
            Transform gwBackDecor = gw.transform.Find("BackGreatswordDecor");
            camSo.FindProperty("firstPersonHiddenAccessory").objectReferenceValue = gwBackDecor;
            camSo.ApplyModifiedPropertiesWithoutUndo();

            // CharacterMovement.cameraYawSource / TargetLockController.viewOrigin still reference
            // Main Camera's components (untouched by the clone) - swap to the SAME component TYPE
            // on the new camera so whatever interface/type they're serialized as still matches.
            RepointToNewCamera(gw.GetComponent("CharacterMovement"), "cameraYawSource", mainCamera, cam);
            RepointToNewCamera(gw.GetComponent("TargetLockController"), "viewOrigin", mainCamera, cam);

            // ---- 5. Wire the possession switcher (3-way: Player / Cat / GuessWho, G = GuessWho) -
            GameObject switcherGo = FindInSceneIncludingInactive("CameraPossession");
            Component switcher = switcherGo.GetComponent("CameraPossessionSwitcher");
            var swSo = new SerializedObject(switcher);
            swSo.FindProperty("guessWhoCamera").objectReferenceValue = cam;
            swSo.FindProperty("guessWhoToggleKey").enumValueIndex = System.Array.IndexOf(
                swSo.FindProperty("guessWhoToggleKey").enumNames, PossessionKey.ToString());
            swSo.FindProperty("guessWhoHealth").objectReferenceValue = gw.GetComponent("Health");

            SerializedProperty controlArr = swSo.FindProperty("guessWhoControl");
            controlArr.arraySize = ControlComponentTypes.Length;
            for (int i = 0; i < ControlComponentTypes.Length; i++)
            {
                controlArr.GetArrayElementAtIndex(i).objectReferenceValue = gw.GetComponent(ControlComponentTypes[i]);
            }
            swSo.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(gw);
            EditorUtility.SetDirty(cam);
            EditorUtility.SetDirty(switcher);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[GuessWhoPlayerParitySetup] '" + CharacterName + "' is now a full Player-parity " +
                      "possessable character (movement/combo attacks/F guard/R ultimate/execution), " +
                      "possession key G. Own camera '" + CameraName + "'. Needs visual tuning in " +
                      "focused Play: weapon grip fit, camera distance for its proportions, guard arm " +
                      "bone orientation (retargeted onto a different skeleton than Player's).");
        }

        private static void RepointToNewCamera(Component target, string fieldName, GameObject oldCamera, GameObject newCamera)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            Object current = prop.objectReferenceValue;
            if (current == null)
            {
                Debug.LogWarning($"[GuessWhoPlayerParitySetup] {target.GetType().Name}.{fieldName} was already null - nothing to repoint.");
                return;
            }
            System.Type componentType = current.GetType();
            prop.objectReferenceValue = newCamera.GetComponent(componentType);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject FindInSceneIncludingInactive(string name)
        {
            foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t.name == name && t.gameObject.scene.IsValid() && t.gameObject.scene.isLoaded)
                {
                    return t.gameObject;
                }
            }
            return null;
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            if (parent == null) return null;
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform r = FindDeep(parent.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
