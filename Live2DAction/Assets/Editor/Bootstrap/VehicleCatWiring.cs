using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.CameraSystem;
using Live2DAction.Characters;
using Live2DAction.Combat;
using Live2DAction.Vehicles;

namespace Live2DAction.EditorTools
{
    // 2026-08-29, user request ("讓貓咪也可以使用車輛 F功能 以及模型塞進車裡"). Cross-wires the Cat
    // into the on-foot VehicleEntrySystem so F enters/exits the car while possessing the cat too:
    //   - VehicleEntrySystem.possession -> CameraPossessionSwitcher (F reads switcher.Current)
    //   - VehicleEntrySystem.cat / catControlToDisable / catCamera / catDriverSeatAnchor
    //   - CameraPossessionSwitcher.vehicleEntry -> the entry system (C is ignored while driving)
    //   - a "CatDriverSeatAnchor" child on the car, positioned for the (visible) 0.45-scale cat
    //
    // Order-independent: also called from the end of CatCharacterSetup (the cat is rebuilt every
    // run there, so its components need re-pointing). No-op if the vehicle isn't in the scene.
    internal static class VehicleCatWiring
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";

        // All car-local. Per-position starting points for the VISIBLE occupants, roughed in against
        // real geometry via edit-mode screenshots (2026-08-30). Buggy: CabinCollider centre y 0.95
        // / top 1.375 (open roll cage, no solid roof); MainBodyCollider top y 0.57 = the rear
        // flatbed surface, behind the cabin at z -1.65..-1.05. Player root pivot sits ~0.6 above its
        // feet; Cat root pivot ~0.38 above its feet (Visual child offset).
        //
        // 2026-08-29→30, user requests:
        //   - "PLAYER駕駛時不再隱藏人物": playerRenderersToHide cleared; player stands in the open
        //     cabin / on the flatbed (head pokes out the top - fine for an open buggy).
        //   - "貓咪駕駛時...模型上仰望 不然看不到臉": the cat faces the chase camera (yaw 180 vs the
        //     car) with a slight look-up - a forward-facing quadruped shows only its back to a rear
        //     chase cam, and it has no "sit" pose to fix that.
        //   - "不同位置下的座標...不然看起來會很奇怪": driver vs flatbed anchors are separate and
        //     tuned independently below.
        // The Play-mode look (esp. the player's animated pose) is still the final check - drag the
        // 4 anchors under Buggy in Play.
        // 2026-08-30 user requests:
        //   - player legs collapsed + wings hidden + Animator frozen while seated
        //   - cat DRIVER: faces forward, pitched well up so its face is visible ("上仰望至窗戶可看
        //     見的角度")
        //   - cat PASSENGER: faces the OPPOSITE way ("後座時則朝向反方向" - yaw 180, looking back)
        // Roughed in against the buggy via edit-mode screenshots.
        private static readonly Vector3 PlayerSeatLocalPos = new Vector3(0f, 0.62f, 0.12f);   // driver, in the cabin
        private static readonly Vector3 PlayerSeatLocalEuler = Vector3.zero;
        private static readonly Vector3 CatSeatLocalPos = new Vector3(0f, 0.72f, -0.05f);     // driver, forward + face up
        private static readonly Vector3 CatSeatLocalEuler = new Vector3(-40f, 0f, 0f);
        // Rear flatbed passengers sit ON the green rear deck panel (visual top ~world 2.0-2.2, well
        // above the 0.57-local box collider) - user: "沒有坐在綠色板子上".
        private static readonly Vector3 PlayerPassengerLocalPos = new Vector3(0f, 0.90f, -1.30f);
        private static readonly Vector3 PlayerPassengerLocalEuler = Vector3.zero;
        private static readonly Vector3 CatPassengerLocalPos = new Vector3(0f, 1.15f, -1.30f);
        private static readonly Vector3 CatPassengerLocalEuler = new Vector3(-8f, 180f, 0f);

        [MenuItem("Tools/Live2DAction/Wire Cat Into Vehicle")]
        public static void ApplyMenu()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (Wire())
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        // Returns true if it actually wired something (so callers know whether to save).
        public static bool Wire()
        {
            VehicleEntrySystem entry = FindInScene<VehicleEntrySystem>();
            CameraPossessionSwitcher switcher = FindInScene<CameraPossessionSwitcher>();
            ViewFocusDirector director = FindInScene<ViewFocusDirector>();
            GameObject catGo = FindRootByName("Cat");
            GameObject catCameraGo = FindRootByName("CatCamera");

            if (entry == null)
            {
                Debug.Log("VehicleCatWiring: no VehicleEntrySystem in the scene - nothing to wire.");
                return false;
            }

            var entrySo = new SerializedObject(entry);

            // possession <-> vehicleEntry back-reference (so C is ignored while driving).
            if (switcher != null)
            {
                entrySo.FindProperty("possession").objectReferenceValue = switcher;

                var swSo = new SerializedObject(switcher);
                swSo.FindProperty("vehicleEntry").objectReferenceValue = entry;
                swSo.ApplyModifiedPropertiesWithoutUndo();
            }

            if (catGo != null)
            {
                entrySo.FindProperty("cat").objectReferenceValue = catGo.transform;

                // The cat's control consumers - same set CameraPossessionSwitcher toggles.
                var control = new List<Behaviour>();
                void Add<T>() where T : Behaviour
                {
                    var c = catGo.GetComponent<T>();
                    if (c != null) control.Add(c);
                }
                Add<CharacterMovement>();
                Add<PlayerCombat>();
                Add<CatChargeAttack>();
                Add<CatPounce>();
                Add<CatAerialJudgment>();

                SerializedProperty arr = entrySo.FindProperty("catControlToDisable");
                arr.arraySize = control.Count;
                for (int i = 0; i < control.Count; i++)
                {
                    arr.GetArrayElementAtIndex(i).objectReferenceValue = control[i];
                }

                // User wants the cat VISIBLE in the seat - hide nothing.
                entrySo.FindProperty("catRenderersToHide").arraySize = 0;

                // Cat seat anchors: children of the car (the entry system lives on the car root).
                // Driver anchor is pitched up so the chase camera can see the cat's face.
                entrySo.FindProperty("catDriverSeatAnchor").objectReferenceValue =
                    EnsureChild(entry.transform, "CatDriverSeatAnchor", CatSeatLocalPos, CatSeatLocalEuler);
                entrySo.FindProperty("catPassengerAnchor").objectReferenceValue =
                    EnsureChild(entry.transform, "CatPassengerAnchor", CatPassengerLocalPos, CatPassengerLocalEuler);
            }

            // 2026-08-29→30: player is visible while driving ("PLAYER駕駛時不再隱藏人物"), but the
            // bits that hang below the car get cropped ("裁減到他下半身 不然會看到他的腳在地上"):
            //   - playerRenderersToHide = the wing renderers (they droop below the chassis)
            //   - playerCollapseBones = the two upper-leg bones (scaled ~0 while seated so the legs
            //     fold into the hips - the body is one skinned mesh, can't hide half of it)
            var pgo = FindRootByName("Player");
            GameObject wingsGo = null;
            Transform[] collapseBones = System.Array.Empty<Transform>();
            Animator playerAnim = null;
            if (pgo != null)
            {
                Transform wingsT = FindDescendant(pgo.transform, "Wings");
                if (wingsT != null) wingsGo = wingsT.gameObject; // SetActive(false)'d while seated (WingFlap re-enables renderers otherwise)

                playerAnim = pgo.GetComponentInChildren<Animator>(true);
                if (playerAnim != null && playerAnim.isHuman)
                {
                    var lut = playerAnim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                    var rut = playerAnim.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                    if (lut != null && rut != null) collapseBones = new[] { lut, rut };
                }
            }
            entrySo.FindProperty("playerAnimatorToFreeze").objectReferenceValue = playerAnim;
            entrySo.FindProperty("playerRenderersToHide").arraySize = 0;
            SerializedProperty hideObjArr = entrySo.FindProperty("playerHideObjectsWhileSeated");
            hideObjArr.arraySize = wingsGo != null ? 1 : 0;
            if (wingsGo != null) hideObjArr.GetArrayElementAtIndex(0).objectReferenceValue = wingsGo;

            SerializedProperty boneArr = entrySo.FindProperty("playerCollapseBones");
            boneArr.arraySize = collapseBones.Length;
            for (int i = 0; i < collapseBones.Length; i++) boneArr.GetArrayElementAtIndex(i).objectReferenceValue = collapseBones[i];

            SerializedProperty playerSeatProp = entrySo.FindProperty("driverSeatAnchor");
            Transform playerSeat = playerSeatProp.objectReferenceValue as Transform;
            if (playerSeat == null)
            {
                playerSeat = EnsureChild(entry.transform, "DriverSeatAnchor", PlayerSeatLocalPos, PlayerSeatLocalEuler);
                playerSeatProp.objectReferenceValue = playerSeat;
            }
            else
            {
                playerSeat.localPosition = PlayerSeatLocalPos;
                playerSeat.localEulerAngles = PlayerSeatLocalEuler;
            }

            entrySo.FindProperty("playerPassengerAnchor").objectReferenceValue =
                EnsureChild(entry.transform, "PlayerPassengerAnchor", PlayerPassengerLocalPos, PlayerPassengerLocalEuler);

            if (catCameraGo != null)
            {
                entrySo.FindProperty("catCamera").objectReferenceValue = catCameraGo;
            }

            if (director != null)
            {
                entrySo.FindProperty("viewDirector").objectReferenceValue = director;
            }

            entrySo.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log("VehicleCatWiring: 2-seater wired. F = driver seat if free, else rear flatbed. " +
                      "Player visible (not hidden), cat driver/passenger anchors pitched up for the " +
                      "chase cam. 4 seat anchors are children of Buggy - hand-tune positions in Play.");
            return true;
        }

        private static Transform EnsureChild(Transform parent, string name, Vector3 localPos, Vector3 localEuler)
        {
            Transform existing = parent.Find(name);
            if (existing == null)
            {
                var go = new GameObject(name);
                existing = go.transform;
                existing.SetParent(parent, false);
            }
            existing.localPosition = localPos;
            existing.localEulerAngles = localEuler;
            existing.localScale = Vector3.one;
            return existing;
        }

        private static T FindInScene<T>() where T : Component
        {
            foreach (T c in Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                return c;
            }
            return null;
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        private static GameObject FindRootByName(string name)
        {
            foreach (GameObject g in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (g != null && g.name == name && g.transform.parent == null)
                {
                    return g;
                }
            }
            return null;
        }
    }
}
