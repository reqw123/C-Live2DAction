using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.Characters;

namespace Live2DAction.EditorTools
{
    // Wires CharacterAttackAnimationLink onto Player and Enemy (see that class's own
    // comment) and removes the old AttackPoseVisualizer it replaces (2026-08-12, explicit
    // user request - the two would otherwise fight over the same arm bone every frame).
    // Enemy never had an AttackPoseVisualizer in the first place (it was only ever wired
    // onto Player/TrainingDummy - see WireAttackPoseVisualizers.cs - and TrainingDummy is
    // gone), so there's nothing to remove there.
    internal static class WireCharacterAttackAnimationLink
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";

        [MenuItem("Tools/Live2DAction/Wire Character Attack Animation Link (Replaces Attack Pose Visualizer)")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.Find("Player");
            GameObject enemy = GameObject.Find("Enemy");
            if (player == null || enemy == null)
            {
                Debug.LogError("Player or Enemy GameObject not found in " + ScenePath);
                return;
            }

            WireCharacter(player);
            WireCharacter(enemy);

            AttackPoseVisualizer oldVisualizer = player.GetComponent<AttackPoseVisualizer>();
            if (oldVisualizer != null)
            {
                Object.DestroyImmediate(oldVisualizer);
                Debug.Log("Removed AttackPoseVisualizer from Player (replaced by real animation).");
            }

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Wired CharacterAttackAnimationLink on Player and Enemy.");
        }

        private static void WireCharacter(GameObject owner)
        {
            Animator animator = owner.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                Debug.LogError(owner.name + " has no Animator - is its Visual attached?");
                return;
            }

            CharacterAttackAnimationLink link = owner.GetComponent<CharacterAttackAnimationLink>();
            if (link == null)
            {
                link = owner.AddComponent<CharacterAttackAnimationLink>();
            }

            var so = new SerializedObject(link);
            so.FindProperty("animator").objectReferenceValue = animator;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
