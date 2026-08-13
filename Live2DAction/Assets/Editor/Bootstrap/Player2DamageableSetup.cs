using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.Core;

namespace Live2DAction.EditorTools
{
    // Makes Player2 (the mecha standee, DoNotShip) damageable and gives it the same red
    // health bar Player/Player4 have (2026-08-13, explicit user request: "幫我讓player2也有血
    // 條 也能受擊，但是他不會自主攻擊"). Deliberately does NOT add PlayerCombat or EnemyAI -
    // Player2 stays passive (wanders, can be locked onto, can now take damage and show it),
    // never attacks back. Player2 already has a CapsuleCollider on its own root GameObject
    // (added when collision-blocking was fixed - see KNOWN_ISSUES.md), so once it has a
    // Health component AttackResolver.ResolveHits already finds and damages it through the
    // exact same code path Player/Player4 use - no other combat wiring needed.
    internal static class Player2DamageableSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";

        [MenuItem("Tools/Live2DAction/Make Player2 Damageable (Health Bar, No Attack)")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject player2 = GameObject.Find("Player2");
            if (player2 == null)
            {
                Debug.LogError("Player2 GameObject not found in " + ScenePath);
                return;
            }

            if (player2.GetComponent<Health>() == null)
            {
                player2.AddComponent<Health>();
            }

            HealthBarSetup.AddHealthBar(player2);

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Player2 can now take damage and shows a health bar (no PlayerCombat/EnemyAI added - it still never attacks).");
        }
    }
}
