using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Live2DAction.DebugTools;

namespace Live2DAction.EditorTools
{
    // 2026-09-02, user request - wires the BossAnimationDebugMode overlay into GreyboxTest:
    // finds 武士 + 屁孩王, their Animators + AI components, reads every Base-Layer Animator state
    // name off their controllers, and points the debug camera at 守望者's Viewpoint camera.
    // Re-run any time a boss gets a new attack state.
    public static class BossAnimationDebugSetup
    {
        [MenuItem("Tools/Live2DAction/[Debug] Setup Boss Animation Debug Mode")]
        public static void Setup()
        {
            var scene = EditorSceneManager.GetActiveScene();

            var host = GameObject.Find("BossAnimationDebugMode");
            if (host == null)
            {
                host = new GameObject("BossAnimationDebugMode");
                Undo.RegisterCreatedObjectUndo(host, "create BossAnimationDebugMode");
            }
            var mode = host.GetComponent<BossAnimationDebugMode>() ?? Undo.AddComponent<BossAnimationDebugMode>(host);
            var so = new SerializedObject(mode);

            // --- debug camera = 守望者's Viewpoint camera (starts disabled) ---
            GameObject debugCam = null;
            var watcher = GameObject.Find("守望者");
            if (watcher != null)
            {
                var camT = watcher.GetComponentsInChildren<Camera>(true).FirstOrDefault();
                if (camT != null) debugCam = camT.gameObject;
            }
            if (debugCam == null)
            {
                debugCam = GameObject.Find("BossAnimDebugCamera");
                if (debugCam == null)
                {
                    debugCam = new GameObject("BossAnimDebugCamera");
                    debugCam.transform.SetParent(host.transform);
                    debugCam.AddComponent<Camera>();
                    debugCam.SetActive(false);
                    Undo.RegisterCreatedObjectUndo(debugCam, "create BossAnimDebugCamera");
                }
            }
            so.FindProperty("debugCamera").objectReferenceValue = debugCam;

            // --- targets ---
            var entries = new List<(string label, GameObject go)>
            {
                ("武士", GameObject.Find("武士")),
                ("屁孩王", GameObject.Find("屁孩王")),
            };
            var targetsProp = so.FindProperty("targets");
            targetsProp.ClearArray();
            int wired = 0;
            foreach (var (label, go) in entries)
            {
                if (go == null) { Debug.LogWarning($"[BossAnimDebugSetup] '{label}' not in scene - skipped."); continue; }
                var animator = go.GetComponentInChildren<Animator>(true);
                if (animator == null) { Debug.LogWarning($"[BossAnimDebugSetup] '{label}' has no Animator - skipped."); continue; }

                targetsProp.InsertArrayElementAtIndex(targetsProp.arraySize);
                var e = targetsProp.GetArrayElementAtIndex(targetsProp.arraySize - 1);
                e.FindPropertyRelative("label").stringValue = label;
                e.FindPropertyRelative("animator").objectReferenceValue = animator;
                e.FindPropertyRelative("pinRoot").objectReferenceValue = go.transform;

                // behaviours to switch off while debugging
                var disable = new List<Behaviour>();
                foreach (var mb in go.GetComponents<MonoBehaviour>())
                {
                    if (mb == null) continue;
                    var n = mb.GetType().Name;
                    if (n == "BossStateMachine" || n == "NavPathFollower" || n == "HealthRegeneration"
                        || n == "BossLifeNodeController" || n == "BossSignalReceiver")
                    {
                        disable.Add(mb);
                    }
                }
                var disProp = e.FindPropertyRelative("disableWhileDebugging");
                disProp.ClearArray();
                foreach (var b in disable)
                {
                    disProp.InsertArrayElementAtIndex(disProp.arraySize);
                    disProp.GetArrayElementAtIndex(disProp.arraySize - 1).objectReferenceValue = b;
                }

                // Base-Layer state names off the AnimatorController (skip Locomotion / the blend tree)
                var names = new List<string>();
                var ctrl = animator.runtimeAnimatorController as AnimatorController;
                if (ctrl != null && ctrl.layers.Length > 0)
                {
                    CollectStates(ctrl.layers[0].stateMachine, "", names);
                }
                names = names.Where(s => s != "Locomotion" && !s.Contains("Locomotion")).Distinct().ToList();
                var namesProp = e.FindPropertyRelative("stateNames");
                namesProp.ClearArray();
                foreach (var s in names)
                {
                    namesProp.InsertArrayElementAtIndex(namesProp.arraySize);
                    namesProp.GetArrayElementAtIndex(namesProp.arraySize - 1).stringValue = s;
                }
                // FSM-script vertical arcs (LeapSlam flies via BossStateMachine, not the clip)
                var arcsProp = e.FindPropertyRelative("verticalArcs");
                arcsProp.ClearArray();
                var fsm = go.GetComponents<MonoBehaviour>().FirstOrDefault(m => m != null && m.GetType().Name == "BossStateMachine");
                if (fsm != null)
                {
                    var fso = new SerializedObject(fsm);
                    var leap = fso.FindProperty("leapSlamAttack")?.objectReferenceValue;
                    var tuning = fso.FindProperty("tuning")?.objectReferenceValue;
                    if (leap != null && tuning != null)
                    {
                        var lso = new SerializedObject(leap);
                        string leapState = lso.FindProperty("clipName")?.stringValue;
                        var tso = new SerializedObject(tuning);
                        float h = tso.FindProperty("leapSlamExtraHeight")?.floatValue ?? 8f;
                        float rs = tso.FindProperty("leapSlamHeightRiseStartNormalized")?.floatValue ?? 0.05f;
                        float pk = tso.FindProperty("leapSlamHeightPeakNormalized")?.floatValue ?? 0.3f;
                        float fe = tso.FindProperty("leapSlamHeightFallEndNormalized")?.floatValue ?? 0.53f;
                        if (!string.IsNullOrEmpty(leapState) && names.Contains(leapState) && h > 0.01f)
                        {
                            arcsProp.InsertArrayElementAtIndex(0);
                            var a = arcsProp.GetArrayElementAtIndex(0);
                            a.FindPropertyRelative("stateName").stringValue = leapState;
                            a.FindPropertyRelative("peakHeight").floatValue = h;
                            a.FindPropertyRelative("riseNt").floatValue = rs;
                            a.FindPropertyRelative("peakNt").floatValue = pk;
                            a.FindPropertyRelative("fallEndNt").floatValue = fe;
                            Debug.Log($"[BossAnimDebugSetup] {label}: LeapSlam vertical arc '{leapState}' peak {h} (nt {rs}/{pk}/{fe}).");
                        }
                    }
                }

                Debug.Log($"[BossAnimDebugSetup] {label}: {names.Count} states, {disable.Count} components to disable.");
                wired++;
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(mode);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[BossAnimDebugSetup] done - {wired} target(s) wired. Press F7 in Play to toggle. Camera = '{debugCam.name}'.");
        }

        private static void CollectStates(AnimatorStateMachine sm, string prefix, List<string> into)
        {
            foreach (var cs in sm.states) into.Add(cs.state.name);
            foreach (var css in sm.stateMachines) CollectStates(css.stateMachine, prefix + css.stateMachine.name + "/", into);
        }
    }
}
