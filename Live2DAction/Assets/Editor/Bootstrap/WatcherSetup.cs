using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Live2DAction.CameraSystem;
using Live2DAction.Characters;
using Live2DAction.Vehicles;

namespace Live2DAction.EditorTools
{
    // 2026-08-27, explicit user request ("有一個角色紫色頭髮穿著泳裝 我想讓他在空中(本地正上方)待著,
    // 在他身上掛攝影機 提供我一個方式可以將視角從player轉向守望者(給他的新名字)" + follow-ups:
    // key(T) + code API both; framing = the Watcher's own POV looking DOWN at the battlefield;
    // "讓他可用w/a/s/d移動攝影機視角"; "player駕駛車輛狀態也必須支援t按鍵視角轉換").
    //
    // Repeatable (delete-and-rebuild, same convention as GreyboxSceneBuilder / TrainingDummySetup):
    //   - "守望者" : root at LOCAL-area centre, high in the air. Maya prefab as its "Visual" child
    //     (purple-hair swimsuit placeholder - CC-BY, attribution required before ship, see
    //     Docs/ASSET_LICENSES.md), embedded Sketchfab camera/physics rigs stripped exactly like
    //     PlayerMayaVisualSetup does. No collider, no Rigidbody - she just hovers.
    //   - "守望者/Viewpoint" : a disabled Camera marking the Watcher-view pose. ViewFocusDirector
    //     only copies its Transform; the Camera component is purely an in-Editor framing aid
    //     (select it -> GameObject > Align View to Selected).
    //   - "ViewDirector" : standalone GameObject holding ViewFocusDirector, wired to BOTH the
    //     on-foot camera (Main Camera + ThirdPersonCameraController) and the vehicle camera
    //     (VehicleCamera + VehicleCameraController) so T works whether the player is on foot or
    //     driving. suspendWhileWatching = the player's CharacterMovement + the vehicle's
    //     VehicleController, so W/A/S/D in the Watcher view only pans the camera.
    //
    // Height / pitch are deliberately plain consts here (not a fancy solve) - the intended workflow
    // is to run this once, enter Play, trigger a LeapSlam, and nudge Viewpoint's local transform in
    // the Inspector until the apex sits where you want. Re-running this tool resets to these values.
    internal static class WatcherSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string MayaPrefabPath = "Assets/_Project/Characters/Placeholder/MayaAnime/Prefabs/Maya.prefab";

        private const string WatcherName = "守望者";
        private const string DirectorName = "ViewDirector";
        private const string ViewConfigPath = "Assets/_Project/Settings/WatcherViewConfig.asset";

        // 2026-08-28, user request ("放大3倍" -> "放大2倍" -> "放大5倍"). Scales the whole 守望者 root,
        // so Maya AND the Viewpoint child's local offset scale together (camera stays at her scaled
        // head).
        private const float WatcherScale = 5f;

        // "本地正上方" - directly above the Ground map's centre (Ground is 30x30 centred on origin,
        // surface y=0.5; boundary walls top out at y=6.5).
        // 2026-08-27, user feedback: "太高了 ... 那守望者應該40?" -> then "守望者 Position Y =33".
        // Wushi's LeapSlam root rises leapSlamExtraHeight=30 world units (apex root-Y ~30.6), so at
        // 33 the leap reads as him rising right up toward her.
        private static readonly Vector3 WatcherPosition = new Vector3(0f, 33f, 0f);

        // Viewpoint local to the Watcher root: at head height, a touch forward, pitched steeply
        // down toward the arena below. Yaw 0 = facing +Z (BoundaryWall_North) - arbitrary but
        // deterministic since she sits dead-centre.
        private static readonly Vector3 ViewpointLocalPosition = new Vector3(0f, 1.62f, 0.25f);
        private static readonly Vector3 ViewpointLocalEuler = new Vector3(72f, 0f, 0f);
        private const float ViewpointFieldOfView = 70f;

        [MenuItem("Tools/Live2DAction/Add Watcher (Sky Observer + View Focus)")]
        public static void Apply()
        {
            if (Application.isPlaying)
            {
                Debug.LogError("Exit Play Mode before running WatcherSetup (it opens/saves the scene).");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // --- repeatable: clear anything a previous run left ---
            for (int pass = 0; pass < 2; pass++)
            {
                GameObject existingWatcher = GameObject.Find(WatcherName);
                if (existingWatcher != null) Object.DestroyImmediate(existingWatcher);
                GameObject existingDirector = GameObject.Find(DirectorName);
                if (existingDirector != null) Object.DestroyImmediate(existingDirector);
            }
            // Older builds put ViewFocusDirector directly on Main Camera - strip that too.
            GameObject mainCameraGo = GameObject.Find("Main Camera");
            if (mainCameraGo != null)
            {
                var stale = mainCameraGo.GetComponent<ViewFocusDirector>();
                if (stale != null) Object.DestroyImmediate(stale);
            }

            // --- Watcher root + Maya visual ---
            var watcher = new GameObject(WatcherName);
            watcher.transform.position = WatcherPosition;
            watcher.transform.rotation = Quaternion.identity;
            watcher.transform.localScale = Vector3.one * WatcherScale;

            GameObject mayaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MayaPrefabPath);
            if (mayaPrefab == null)
            {
                Debug.LogError("Could not load Maya prefab at " + MayaPrefabPath);
                Object.DestroyImmediate(watcher);
                return;
            }

            var visual = (GameObject)PrefabUtility.InstantiatePrefab(mayaPrefab, watcher.transform);
            visual.name = "Visual";
            visual.transform.localPosition = Vector3.zero; // no CharacterController here - Maya's own foot-origin sits right on the root
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            Animator animator = visual.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false; // she never translates; keep her Idle clip playing in place
            }

            PlayerMayaVisualSetup.RemoveEmbeddedCameraRig(visual);
            PlayerMayaVisualSetup.RemoveEmbeddedPhysicsRig(visual);

            // The Maya prefab root also ships 2 missing-script components from its Sketchfab import
            // (harmless, but they spam "referenced script ... is missing" on every scene load).
            foreach (Transform child in visual.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
            }

            // --- Viewpoint (disabled Camera used only as a pose marker + framing aid) ---
            var viewpoint = new GameObject("Viewpoint");
            viewpoint.transform.SetParent(watcher.transform, false);
            viewpoint.transform.localPosition = ViewpointLocalPosition;
            viewpoint.transform.localEulerAngles = ViewpointLocalEuler;

            var viewCam = viewpoint.AddComponent<Camera>();
            viewCam.fieldOfView = ViewpointFieldOfView;
            viewCam.farClipPlane = 1000f;
            viewCam.nearClipPlane = 0.1f;
            viewCam.enabled = false; // ViewFocusDirector drives the real live camera; this never renders
            viewpoint.AddComponent<UniversalAdditionalCameraData>();
            viewpoint.tag = "Untagged";

            // --- resolve the two camera rigs + what to suspend ---
            if (mainCameraGo == null)
            {
                Debug.LogError("Main Camera not found in " + ScenePath);
                return;
            }
            var onFootCamera = mainCameraGo.GetComponent<Camera>();
            var onFootController = mainCameraGo.GetComponent<ThirdPersonCameraController>();

            Camera vehicleCamera = null;
            VehicleCameraController vehicleCameraController = null;
            GameObject vehicleCameraGo = GameObject.Find("VehicleCamera");
            if (vehicleCameraGo == null)
            {
                foreach (var vcc in Object.FindObjectsByType<VehicleCameraController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    vehicleCameraGo = vcc.gameObject;
                    break;
                }
            }
            if (vehicleCameraGo != null)
            {
                vehicleCamera = vehicleCameraGo.GetComponent<Camera>();
                vehicleCameraController = vehicleCameraGo.GetComponent<VehicleCameraController>();
            }

            var suspend = new List<Behaviour>();
            GameObject playerGo = GameObject.Find("Player");
            if (playerGo != null)
            {
                var move = playerGo.GetComponent<CharacterMovement>();
                if (move != null) suspend.Add(move);
            }
            foreach (var vc in Object.FindObjectsByType<VehicleController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                suspend.Add(vc);
            }

            // --- persistable "save the Watcher view" asset. Re-running this tool is a "reset to
            //     the authored framing" action, so the saved view is CLEARED here (hasSavedView =
            //     false) - the observer will start from the scene's Viewpoint again until you
            //     fly + save a new view. This also recovers from a corrupt saved pose. ---
            var viewConfig = AssetDatabase.LoadAssetAtPath<WatcherViewConfig>(ViewConfigPath);
            if (viewConfig == null)
            {
                viewConfig = ScriptableObject.CreateInstance<WatcherViewConfig>();
                AssetDatabase.CreateAsset(viewConfig, ViewConfigPath);
            }
            viewConfig.hasSavedView = false;
            viewConfig.rootPosition = Vector3.zero;
            viewConfig.rootYaw = 0f;
            viewConfig.cameraPitch = 0f;
            viewConfig.fieldOfView = 0f;

            // --- ViewDirector GameObject ---
            var directorGo = new GameObject(DirectorName);
            var director = directorGo.AddComponent<ViewFocusDirector>();

            var so = new SerializedObject(director);
            so.FindProperty("onFootCamera").objectReferenceValue = onFootCamera;
            so.FindProperty("onFootController").objectReferenceValue = onFootController;
            so.FindProperty("vehicleCamera").objectReferenceValue = vehicleCamera;
            so.FindProperty("vehicleController").objectReferenceValue = vehicleCameraController;
            so.FindProperty("watcherViewpoint").objectReferenceValue = viewpoint.transform;
            so.FindProperty("watcherVisualRoot").objectReferenceValue = visual.transform;
            so.FindProperty("watcherFieldOfView").floatValue = ViewpointFieldOfView;
            so.FindProperty("viewConfig").objectReferenceValue = viewConfig;

            SerializedProperty suspendProp = so.FindProperty("suspendWhileWatching");
            suspendProp.arraySize = suspend.Count;
            for (int i = 0; i < suspend.Count; i++)
            {
                suspendProp.GetArrayElementAtIndex(i).objectReferenceValue = suspend[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            // 2026-08-29, user request ("讓 player 守望者/cat 三者可以互相切換視角") - if the cat is
            // already in the scene, link it into the director now (no-op otherwise; CatCharacterSetup
            // also calls this, so whichever menu runs second completes the link).
            WatcherCatWiring.Wire();

            EditorUtility.SetDirty(viewConfig);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"WatcherSetup: '{WatcherName}' at {WatcherPosition} (scale x{WatcherScale}), Viewpoint pitch {ViewpointLocalEuler.x} deg / FOV {ViewpointFieldOfView}. " +
                      $"'{DirectorName}' wired: onFoot={(onFootCamera != null)}, vehicle={(vehicleCamera != null)}, suspend={suspend.Count}, viewConfig={ViewConfigPath}. " +
                      "Press T in Play (on foot OR driving); in the Watcher view: mouse=look, W/A/S/D+E/Q=fly, scroll=zoom, K=save the view. Code API: FocusWatcher()/FocusPlayer()/Toggle().");
        }
    }
}
