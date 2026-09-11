using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.Characters;

namespace Live2DAction.EditorTools
{
    // 2026-09-11 follow-up to GuessWhoPlayerParitySetup.cs, three user requests:
    //   1. "猜猜看角色暫不需要有wolf大劍" - drop the cosmetic back-mounted claymore
    //      (BackGreatswordDecor, the actual Genshin "Wolf's Gravestone" greatsword model). NOT the
    //      hand-mounted weapon (also internally named "WolfsGravestone" for legacy reasons, but
    //      it's the BloodKatana mesh, not greatsword-shaped, and UltimateAbility.FindWeapon()
    //      silently no-ops the R ultimate if it can't find that exact name - removing it would
    //      quietly break the ultimate we just built for him).
    //   2. "alt靜走時請讓他把手放下來呈現伸直狀態" - HumanoidWalkArmsDown.cs (new component).
    //   3. "把猜猜看的位置移動到露營區土地上" - move him (+ his camera) from the persistent
    //      GreyboxTest scene into Map_Camp.unity, standing on 露營區's ground. Also removes the
    //      earlier non-combat placeholder instance there (GuessWhoCharacterSetup.cs) so there's
    //      only one 猜猜看.
    //
    // Consequence of #3 worth recording: CameraPossessionSwitcher lives in the ALWAYS-loaded
    // GreyboxTest scene, but 猜猜看 now only exists while Map_Camp is loaded (additive, only while
    // the player is physically at the camp) - so the switcher's guessWhoCamera/guessWhoControl
    // fields are cleared to null here and re-resolved at runtime by name
    // (CameraPossessionSwitcher.TryRelinkGuessWho(), throttled once/second) whenever that scene
    // streams in. G does nothing until you've actually walked to 露營區 once - by design, this
    // reads as "you can only take control of him near where he is."
    internal static class GuessWhoCampRelocateSetup
    {
        private const string HomeScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string CampScenePath = "Assets/_Project/Scenes/Map_Camp.unity";
        private const string CharacterName = "猜猜看";
        private const string CameraName = "GuessWhoCamera";
        private const string GroundName = "露營區";

        [MenuItem("Tools/Live2DAction/Relocate GuessWho To Camp + Drop Greatsword + Walk Pose")]
        public static void Apply()
        {
            EditorSceneManager.OpenScene(HomeScenePath, OpenSceneMode.Single);
            Scene homeScene = SceneManager.GetSceneByPath(HomeScenePath);
            Scene campScene = EditorSceneManager.OpenScene(CampScenePath, OpenSceneMode.Additive);

            GameObject gw = FindInScene(homeScene, CharacterName);
            if (gw == null)
            {
                Debug.LogError("[GuessWhoCampRelocateSetup] '" + CharacterName + "' not found in " + HomeScenePath +
                                " - run GuessWhoPlayerParitySetup first.");
                return;
            }
            GameObject cam = FindInScene(homeScene, CameraName);

            // ---- 1. Drop the back greatsword decor -------------------------------------------
            Transform decor = gw.transform.Find("BackGreatswordDecor");
            if (decor != null) Object.DestroyImmediate(decor.gameObject);

            // ---- 2. Arms-down-while-walking override ------------------------------------------
            Transform visual = gw.transform.Find("Visual");
            Animator animator = visual.GetComponent<Animator>();
            HumanoidWalkArmsDown armsDown = visual.GetComponent<HumanoidWalkArmsDown>();
            if (armsDown == null) armsDown = visual.gameObject.AddComponent<HumanoidWalkArmsDown>();
            var armsSo = new SerializedObject(armsDown);
            armsSo.FindProperty("movement").objectReferenceValue = gw.GetComponent("CharacterMovement");
            armsSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- 3. Remove the old non-combat placeholder already standing in Map_Camp --------
            GameObject oldPlaceholder = FindInScene(campScene, CharacterName);
            if (oldPlaceholder != null) Object.DestroyImmediate(oldPlaceholder);

            GameObject ground = FindInScene(campScene, GroundName);
            if (ground == null)
            {
                Debug.LogError("[GuessWhoCampRelocateSetup] '" + GroundName + "' not found in " + CampScenePath);
                return;
            }

            // ---- 4. Move 猜猜看 (+ his camera) into Map_Camp, stand him on the ground ----------
            SceneManager.MoveGameObjectToScene(gw, campScene);
            if (cam != null) SceneManager.MoveGameObjectToScene(cam, campScene);

            Collider groundCollider = ground.GetComponent<Collider>();
            float groundTopY = groundCollider != null ? groundCollider.bounds.max.y : ground.transform.position.y + 0.5f;
            CharacterController cc = gw.GetComponent<CharacterController>();
            float capsuleBottomLocalY = cc.center.y - cc.height * 0.5f;
            Vector3 spawnXZ = ground.transform.position + new Vector3(0f, 0f, 10f); // a few metres in from the north (gate) edge

            gw.transform.position = new Vector3(spawnXZ.x, groundTopY - capsuleBottomLocalY, spawnXZ.z);
            gw.transform.rotation = Quaternion.identity; // faces north/+Z - toward the gate, greeting anyone who walks in

            // ---- 5. Clear the switcher's now-cross-scene refs - runtime TryRelinkGuessWho() ----
            //         re-finds them by name whenever Map_Camp is loaded (see class comment above).
            GameObject switcherGo = FindInScene(homeScene, "CameraPossession");
            if (switcherGo != null)
            {
                Component switcher = switcherGo.GetComponent("CameraPossessionSwitcher");
                var swSo = new SerializedObject(switcher);
                swSo.FindProperty("guessWhoCamera").objectReferenceValue = null;
                swSo.FindProperty("guessWhoHealth").objectReferenceValue = null;
                SerializedProperty controlArr = swSo.FindProperty("guessWhoControl");
                controlArr.arraySize = 0;
                swSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(switcher);
            }

            EditorUtility.SetDirty(gw);
            EditorSceneManager.MarkSceneDirty(homeScene);
            EditorSceneManager.MarkSceneDirty(campScene);
            EditorSceneManager.SaveScene(homeScene);
            EditorSceneManager.SaveScene(campScene);
            AssetDatabase.SaveAssets();

            Debug.Log("[GuessWhoCampRelocateSetup] '" + CharacterName + "' moved to '" + GroundName +
                       "' in " + CampScenePath + " at " + gw.transform.position.ToString("F2") +
                       ", back greatsword removed, walk-pose override wired. G possession now only " +
                       "resolves while Map_Camp is loaded (walk there once to link it).");
        }

        private static GameObject FindInScene(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
                Transform t = FindDeep(root.transform, name);
                if (t != null) return t.gameObject;
            }
            return null;
        }

        private static Transform FindDeep(Transform parent, string name)
        {
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
