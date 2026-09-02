using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.Core;
using Live2DAction.Vehicles;

namespace Live2DAction.EditorTools
{
    // 2026-08-30, user request ("car 幫我增加 ctrl 飛行功能"). Bolts VehicleFlightController + a
    // dedicated flight-energy meter onto the Buggy and points them at a VehicleFlightData asset
    // (created on first run). Re-runnable (idempotent: reuses the existing component / child /
    // asset). Same "one small Tools/ menu per feature" pattern as SchoolAreaSetup, VehicleCatWiring.
    internal static class VehicleFlightSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string DataDir = "Assets/_Project/Settings/Movement/Vehicle";
        private const string DataPath = DataDir + "/VehicleFlightData.asset";

        [MenuItem("Tools/Live2DAction/Add Vehicle Flight (Ctrl)")]
        public static void Apply()
        {
            if (Application.isPlaying)
            {
                Debug.LogError("Exit Play Mode before running VehicleFlightSetup (it opens/saves the scene).");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject buggy = null;
            foreach (GameObject g in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (g != null && g.name == "Buggy" && g.transform.parent == null) { buggy = g; break; }
            }
            if (buggy == null)
            {
                Debug.LogError("VehicleFlightSetup: no root GameObject named 'Buggy' in " + ScenePath + ".");
                return;
            }

            VehicleController vc = buggy.GetComponent<VehicleController>();
            if (vc == null)
            {
                Debug.LogError("VehicleFlightSetup: 'Buggy' has no VehicleController.");
                return;
            }

            // --- flight tuning asset ---
            VehicleFlightData data = AssetDatabase.LoadAssetAtPath<VehicleFlightData>(DataPath);
            if (data == null)
            {
                EnsureFolder(DataDir);
                data = ScriptableObject.CreateInstance<VehicleFlightData>();
                AssetDatabase.CreateAsset(data, DataPath);
                AssetDatabase.SaveAssets();
            }

            // --- dedicated flight-energy meter (child, so it's clearly separate from any
            //     ultimate-skill UltimateEnergy). Same profile as CharacterMovement.flightEnergy:
            //     max 500, +5 every 1s, but only after 3s with no drain. ---
            Transform energyT = buggy.transform.Find("FlightEnergy");
            if (energyT == null)
            {
                var go = new GameObject("FlightEnergy");
                go.transform.SetParent(buggy.transform, false);
                energyT = go.transform;
            }
            UltimateEnergy energy = energyT.GetComponent<UltimateEnergy>();
            if (energy == null) energy = energyT.gameObject.AddComponent<UltimateEnergy>();
            var eSo = new SerializedObject(energy);
            eSo.FindProperty("maxEnergy").floatValue = 500f;
            eSo.FindProperty("regenAmount").floatValue = 5f;
            eSo.FindProperty("regenIntervalSeconds").floatValue = 1f;
            eSo.FindProperty("regenIdleDelaySeconds").floatValue = 3f;
            eSo.ApplyModifiedPropertiesWithoutUndo();

            // --- the flight controller ---
            VehicleFlightController flight = buggy.GetComponent<VehicleFlightController>();
            if (flight == null) flight = buggy.AddComponent<VehicleFlightController>();
            var fSo = new SerializedObject(flight);
            fSo.FindProperty("vehicleController").objectReferenceValue = vc;
            fSo.FindProperty("data").objectReferenceValue = data;
            fSo.FindProperty("flightEnergy").objectReferenceValue = energy;
            fSo.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(flight);
            EditorUtility.SetDirty(energy);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("VehicleFlightSetup: VehicleFlightController + FlightEnergy(500) on Buggy, data = " + DataPath +
                      ". Drive (F), hold Left Ctrl to fly / climb, Space to descend, Shift to boost, W/S thrust, A/D yaw.");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
