using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.Characters;

namespace Live2DAction.EditorTools
{
    internal static class WireCharacterAnimatorLink
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";

        [MenuItem("Tools/Live2DAction/Wire Character Animator Link On Player")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("Player GameObject not found in " + ScenePath);
                return;
            }

            Animator animator = player.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                Debug.LogError("No Animator found under Player - is the Maya visual attached?");
                return;
            }

            CharacterAnimatorLink link = player.GetComponent<CharacterAnimatorLink>();
            if (link == null)
            {
                link = player.AddComponent<CharacterAnimatorLink>();
            }

            var so = new SerializedObject(link);
            so.FindProperty("animator").objectReferenceValue = animator;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Wired CharacterAnimatorLink on Player to " + animator.gameObject.name + "'s Animator.");
        }
    }
}
