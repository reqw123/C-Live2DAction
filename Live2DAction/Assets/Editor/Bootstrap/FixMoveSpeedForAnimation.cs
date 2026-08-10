using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.Characters;

namespace Live2DAction.EditorTools
{
    // Fixes reported foot-sliding: Player's moveSpeed (5) far exceeded the pace Maya's
    // Run clip was authored for. Since the clips have no usable root motion to derive the
    // "correct" speed from (checked: their RootT curves are just a small in-place sway,
    // not real stride displacement), this drops moveSpeed to match the Locomotion blend
    // tree's top threshold (2) as a reasoned starting point - still needs visual
    // confirmation, not a derived/proven-correct value.
    internal static class FixMoveSpeedForAnimation
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const float NewMoveSpeed = 2f;

        [MenuItem("Tools/Live2DAction/[Fix] Match Move Speed To Animation Pace")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("Could not find Player in " + ScenePath);
                return;
            }

            CharacterMovement movement = player.GetComponent<CharacterMovement>();
            var so = new SerializedObject(movement);
            so.FindProperty("moveSpeed").floatValue = NewMoveSpeed;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"Set Player moveSpeed to {NewMoveSpeed} to match Maya's animation pace.");
        }
    }
}
