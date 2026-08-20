using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.Core;

namespace Live2DAction.EditorTools
{
    // 2026-08-16, explicit user request: enemies (Enemy) should also carry a blue energy bar
    // stacked under their health bar, for visual consistency with Player. Adds a plain
    // UltimateEnergy (regen-only resource meter, no combat/skill knowledge of its own - see
    // that class's own comment) with no matching UltimateAbility, since EnemyAI never triggers
    // the player-only ultimate (EnemyAI.UltimatePressed is hardcoded false - see that field's
    // comment). The bar will simply fill up and sit at max once charged and never get
    // consumed - fine here, since the point is a matching visual readout, not giving Enemy
    // an actual skill to fire.
    internal static class EnemyEnergyBarSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";

        [MenuItem("Tools/Live2DAction/Add Energy Bar To Enemy (Enemy)")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject enemy = GameObject.Find("Enemy");
            if (enemy == null)
            {
                Debug.LogError("Enemy GameObject not found in " + ScenePath);
                return;
            }

            UltimateEnergy energy = enemy.GetComponent<UltimateEnergy>();
            if (energy == null)
            {
                energy = enemy.AddComponent<UltimateEnergy>();
            }

            UltimateAbilitySetup.AddEnergyBar(enemy, energy);

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Added a blue energy bar (regen-only, no R-trigger) to Enemy, stacked under its health bar.");
        }
    }
}
