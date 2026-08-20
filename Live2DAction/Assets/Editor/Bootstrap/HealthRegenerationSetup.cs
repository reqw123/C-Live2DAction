using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.Core;

namespace Live2DAction.EditorTools
{
    // 2026-08-16, explicit user request: 敵我雙方閒置10秒鐘沒受到傷害時，每秒回復2點生命值 - both
    // player and enemy characters passively heal after 10s without taking damage. Rather than
    // hardcoding which GameObjects count as "our side" vs "enemy side", this wires
    // HealthRegeneration onto every GameObject in the scene that already has a Health
    // component (currently Player, Player2, Player3, Player4) - "敵我雙方" covers all of them,
    // and this way a future character just needs a Health component to automatically be
    // covered by re-running this tool, no name-based special-casing to keep in sync.
    internal static class HealthRegenerationSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const float IdleSecondsBeforeRegen = 10f;
        private const float RegenPerSecond = 2f;

        [MenuItem("Tools/Live2DAction/Add Idle Health Regeneration (All Characters)")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Health[] allHealth = Object.FindObjectsByType<Health>(FindObjectsSortMode.None);
            if (allHealth.Length == 0)
            {
                Debug.LogWarning("No GameObjects with a Health component found in " + ScenePath);
                return;
            }

            int wired = 0;
            foreach (Health health in allHealth)
            {
                GameObject owner = health.gameObject;
                HealthRegeneration regen = owner.GetComponent<HealthRegeneration>();
                if (regen == null)
                {
                    regen = owner.AddComponent<HealthRegeneration>();
                }

                var so = new SerializedObject(regen);
                so.FindProperty("health").objectReferenceValue = health;
                so.FindProperty("idleSecondsBeforeRegen").floatValue = IdleSecondsBeforeRegen;
                so.FindProperty("regenPerSecond").floatValue = RegenPerSecond;
                so.ApplyModifiedPropertiesWithoutUndo();

                wired++;
            }

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"Wired HealthRegeneration ({IdleSecondsBeforeRegen}s idle, {RegenPerSecond}/s) onto {wired} character(s) with a Health component.");
        }
    }
}
