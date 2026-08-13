using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2D.Cubism.Core;
using Live2DAction.Characters;

namespace Live2DAction.EditorTools
{
    // Fixes the reported "can't rename the two Live2D standees in the Hierarchy" issue.
    // Investigation found their root GameObjects' names had actually gone blank in the saved
    // scene, not just failed to update - matching an already-documented Cubism SDK quirk
    // (see Docs/KNOWN_ISSUES.md: ToModel()-produced GameObjects have unstable names,
    // previously only seen going blank at Play time, apparently now also persisting into a
    // saved scene). GameObject.Find(name) can't locate a blank-named object, so this matches
    // by spawn position instead (set by Live2DStandeeSetup.cs and stable regardless of name)
    // rather than by the name it may or may not still have.
    //
    // Keeps the "_DoNotShip" suffix rather than renaming to bare "076"/"077" as asked
    // literally - CLAUDE.md rule 2 is a hard requirement that these never end up in a Build
    // handed to anyone, and the suffix is what makes that obvious at a glance in the
    // Hierarchy instead of only in a comment. Both names still start with "076"/"077" as
    // requested. Renaming from an editor script (same mechanism Live2DStandeeSetup.cs
    // already used to set the original names) does stick, unlike renaming interactively in
    // the Hierarchy - don't rely on F2/double-click renaming these particular GameObjects.
    internal static class FixLive2DStandeeNames
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";

        // Must match Live2DStandeeSetup.Standees' positions.
        private static readonly Vector3 Position076 = new Vector3(-6f, 0f, -8f);
        private static readonly Vector3 Position077 = new Vector3(-3f, 0f, -8f);

        [MenuItem("Tools/Live2DAction/[Fix] Rename Live2D Standees To 076-077")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            CubismModel[] models = Object.FindObjectsByType<CubismModel>(FindObjectsSortMode.None);
            int renamedCount = 0;
            foreach (CubismModel model in models)
            {
                Transform root = model.transform;
                if (root.parent != null || root.GetComponent<CubismBillboard>() == null)
                {
                    continue; // only the standalone standee roots (skip Player's old visual etc.)
                }

                // Compare X/Z only - Y drifts slightly from the spawn position's 0 (billboard
                // positioning adjusts height relative to the canvas pivot), which isn't
                // useful for telling the two standees apart anyway.
                Vector2 flatPos = new Vector2(root.position.x, root.position.z);
                if (Vector2.Distance(flatPos, new Vector2(Position076.x, Position076.z)) < 0.5f)
                {
                    RenameTo(root.gameObject, "076_DoNotShip");
                    renamedCount++;
                }
                else if (Vector2.Distance(flatPos, new Vector2(Position077.x, Position077.z)) < 0.5f)
                {
                    RenameTo(root.gameObject, "077_DoNotShip");
                    renamedCount++;
                }
            }

            if (renamedCount < 2)
            {
                Debug.LogWarning($"Only matched {renamedCount}/2 standees by position - one may have been moved. Check the Hierarchy for a CubismModel root near {Position076} or {Position077}.");
            }

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static void RenameTo(GameObject go, string newName)
        {
            string previousName = string.IsNullOrEmpty(go.name) ? "(blank)" : go.name;
            go.name = newName;
            Debug.Log($"Renamed standee at {go.transform.position} from '{previousName}' to '{newName}'.");
        }
    }
}
