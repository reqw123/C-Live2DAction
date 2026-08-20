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
        public static void ApplyToPlayer()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            WireTarget("Player");
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        // 2026-08-20, explicit user request ("敵人的移動動作採用跟玩家一樣地踏步") - Enemy's
        // Locomotion blend tree was never being driven at all (no CharacterAnimatorLink, and
        // that class used to hard-require CharacterMovement, which Enemy doesn't have - see
        // CharacterAnimatorLink's own comment for the ICharacterSpeedSource generalization that
        // made this possible). Separate menu item, same underlying helper as Player's own -
        // keeps each entry point narrowly named for what it actually targets, matching this
        // project's other per-character Setup tools.
        [MenuItem("Tools/Live2DAction/Wire Character Animator Link On Enemy")]
        public static void ApplyToEnemy()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            WireTarget("Enemy");
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static void WireTarget(string gameObjectName)
        {
            GameObject target = GameObject.Find(gameObjectName);
            if (target == null)
            {
                Debug.LogError(gameObjectName + " GameObject not found in " + ScenePath);
                return;
            }

            Animator animator = target.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                Debug.LogError("No Animator found under " + gameObjectName + " - is the visual model attached?");
                return;
            }

            CharacterAnimatorLink link = target.GetComponent<CharacterAnimatorLink>();
            if (link == null)
            {
                link = target.AddComponent<CharacterAnimatorLink>();
            }

            var so = new SerializedObject(link);
            so.FindProperty("animator").objectReferenceValue = animator;
            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log("Wired CharacterAnimatorLink on " + gameObjectName + " to " + animator.gameObject.name + "'s Animator.");
        }
    }
}
