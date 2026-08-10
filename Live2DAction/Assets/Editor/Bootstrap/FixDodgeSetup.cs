using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.Characters;

namespace Live2DAction.EditorTools
{
    // Creates DodgeData and wires it into the Player's CharacterMovement in the existing
    // GreyboxTest scene. One-time because CharacterMovement.dodgeData didn't exist when the
    // scene was last saved.
    internal static class FixDodgeSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string AssetPath = "Assets/_Project/Settings/DodgeData.asset";

        [MenuItem("Tools/Live2DAction/[Fix] Wire Dodge On Player")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("Player GameObject not found in " + ScenePath);
                return;
            }

            CharacterMovement movement = player.GetComponent<CharacterMovement>();
            if (movement == null)
            {
                Debug.LogError("Player has no CharacterMovement component.");
                return;
            }

            DodgeData dodgeData = CreateOrLoadDodgeData();
            var so = new SerializedObject(movement);
            so.FindProperty("dodgeData").objectReferenceValue = dodgeData;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Wired DodgeData onto Player's CharacterMovement.");
        }

        private static DodgeData CreateOrLoadDodgeData()
        {
            var existing = AssetDatabase.LoadAssetAtPath<DodgeData>(AssetPath);
            if (existing != null)
            {
                return existing;
            }

            var data = ScriptableObject.CreateInstance<DodgeData>();
            var so = new SerializedObject(data);
            so.FindProperty("distance").floatValue = 3f;
            so.FindProperty("durationFrames").intValue = 12;
            so.FindProperty("invulnerabilityFrames").intValue = 12;
            so.FindProperty("cooldownFrames").intValue = 20;
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(data, AssetPath);
            return data;
        }
    }
}
