using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.Characters;

namespace Live2DAction.EditorTools
{
    // One-time fix for a real Play-mode bug report: the guessed swing axis/sign in
    // WireAttackPoseVisualizers.WirePlayer (see AttackPoseVisualizer's class comment - the
    // correct direction can only be confirmed by eye) swung Maya's arm the wrong way. Flips
    // the Inspector-exposed invert toggle rather than guessing a different axis, since that's
    // exactly what it's there for.
    internal static class FixAttackPoseDirection
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";

        [MenuItem("Tools/Live2DAction/Fix Player Attack Pose Direction")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("Player GameObject not found in " + ScenePath);
                return;
            }

            AttackPoseVisualizer visualizer = player.GetComponent<AttackPoseVisualizer>();
            if (visualizer == null)
            {
                Debug.LogError("Player has no AttackPoseVisualizer - run Wire Attack Pose Visualizers first.");
                return;
            }

            var so = new SerializedObject(visualizer);
            so.FindProperty("invert").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Flipped Player's AttackPoseVisualizer.invert to true.");
        }
    }
}
