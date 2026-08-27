using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Live2DAction.EditorTools
{
    // 2026-08-20, explicit user request ("把所有光環再往y軸上方相對移動一點") - shifts the already-
    // built CheckpointGate_0..6 in the live scene by the same +1.5 offset just applied to
    // SkyIslandTimeTrialSetup.GatePositions, WITHOUT going through that class's own Apply()
    // (which destroys and rebuilds the whole SkyIslandTimeTrial hierarchy, including
    // TimeTrialController - that would orphan TimeTrialStartMechanism's serialized reference to
    // it, see that class's own comment). Repositions the existing gate GameObjects in place
    // instead, so every other wiring (TimeTrialController.checkpointsInOrder, the mechanism's
    // controller reference, the HUD) stays intact.
    internal static class RaiseSkyIslandTimeTrialGates
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const float YOffset = 1.5f;

        [MenuItem("Tools/Live2DAction/[Fix] Raise Sky Island Time Trial Gates")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            int moved = 0;
            for (int i = 0; i <= 6; i++)
            {
                GameObject gate = GameObject.Find("CheckpointGate_" + i);
                if (gate == null)
                {
                    continue;
                }

                gate.transform.position += new Vector3(0f, YOffset, 0f);
                moved++;
            }

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Raised " + moved + " sky island time trial gates by " + YOffset + " units on Y.");
        }
    }
}
