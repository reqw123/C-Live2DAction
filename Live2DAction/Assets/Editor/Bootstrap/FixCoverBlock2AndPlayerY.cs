using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Live2DAction.EditorTools
{
    // One-time fix for the existing GreyboxTest scene (2026-08-12): two accidental edits
    // from an interactive Editor session running concurrently with a batch fix -
    // CoverBlock2 was gone entirely (matches CreateCoverBlocks' second position, (-3, 0.5,
    // -2) - see GreyboxSceneBuilder.cs) and Player's own Y had reverted to 0 (should be 0.5,
    // same recurring "accidentally dragged in the Editor" pattern this project has hit
    // several times before - see FixPlayerGroundedSpawn.cs's own history). Opens the saved
    // scene directly rather than going through GreyboxSceneBuilder.Build() (see
    // Docs/KNOWN_ISSUES.md's operating warning about that wiping the whole scene).
    internal static class FixCoverBlock2AndPlayerY
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private static readonly Vector3 CoverBlock2Position = new Vector3(-3f, 0.5f, -2f);

        [MenuItem("Tools/Live2DAction/[Fix] Restore CoverBlock2 And Player Y")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            if (GameObject.Find("CoverBlock2") == null)
            {
                GameObject cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cover.name = "CoverBlock2";
                cover.transform.position = CoverBlock2Position;
                cover.transform.localScale = Vector3.one;
                Debug.Log("Recreated CoverBlock2 at " + CoverBlock2Position);
            }
            else
            {
                Debug.Log("CoverBlock2 already present - nothing to recreate.");
            }

            GameObject player = GameObject.Find("Player");
            GameObject ground = GameObject.Find("Ground");
            if (player != null && ground != null)
            {
                CharacterController controller = player.GetComponent<CharacterController>();
                Collider groundCollider = ground.GetComponent<Collider>();
                if (controller != null && groundCollider != null)
                {
                    float groundTopY = groundCollider.bounds.max.y;
                    float requiredY = groundTopY + controller.center.y + controller.height / 2f;

                    Vector3 position = player.transform.position;
                    float previousY = position.y;
                    position.y = requiredY;
                    player.transform.position = position;
                    Debug.Log($"Moved Player spawn Y from {previousY} to {requiredY}.");
                }
            }

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
    }
}
