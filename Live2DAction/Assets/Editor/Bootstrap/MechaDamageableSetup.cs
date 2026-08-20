using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.Core;

namespace Live2DAction.EditorTools
{
    // Makes Mecha (the mecha standee, DoNotShip) damageable and gives it the same red
    // health bar Player/Player4 have (2026-08-13, explicit user request: "幫我讓player2也有血
    // 條 也能受擊，但是他不會自主攻擊"). Deliberately does NOT add PlayerCombat or EnemyAI -
    // Mecha stays passive (wanders, can be locked onto, can now take damage and show it),
    // never attacks back. Mecha already has a CapsuleCollider on its own root GameObject
    // (added when collision-blocking was fixed - see KNOWN_ISSUES.md), so once it has a
    // Health component AttackResolver.ResolveHits already finds and damages it through the
    // exact same code path Player/Enemy use - no other combat wiring needed.
    internal static class MechaDamageableSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";

        [MenuItem("Tools/Live2DAction/Make Mecha Damageable (Health Bar, No Attack)")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject mecha = GameObject.Find("Mecha");
            if (mecha == null)
            {
                Debug.LogError("Mecha GameObject not found in " + ScenePath);
                return;
            }

            if (mecha.GetComponent<Health>() == null)
            {
                mecha.AddComponent<Health>();
            }

            HealthBarSetup.AddHealthBar(mecha);

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Mecha can now take damage and shows a health bar (no PlayerCombat/EnemyAI added - it still never attacks).");
        }
    }
}
