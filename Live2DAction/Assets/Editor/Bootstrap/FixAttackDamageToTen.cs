using UnityEditor;
using UnityEngine;
using Live2DAction.Combat;

namespace Live2DAction.EditorTools
{
    // Normalizes every AttackData asset's damage to 10 (2026-08-12, explicit user request:
    // "攻擊命中一次扣10滴血" alongside the new 100-HP health bars). Previously Player's combo
    // escalated 8/10/16 across its three hits and Enemy's attack was 5 - this flattens that
    // combo-escalation design to a uniform 10 per hit on every attack, matching the request
    // literally rather than only touching whichever side happens to hit first. Damage stays
    // data-driven in these ScriptableObject assets per CLAUDE.md rule 7 - nothing hardcoded
    // in script.
    internal static class FixAttackDamageToTen
    {
        private const string CombatFolder = "Assets/_Project/Settings/Combat";

        [MenuItem("Tools/Live2DAction/[Fix] Set All Attack Damage To 10")]
        public static void Apply()
        {
            string[] guids = AssetDatabase.FindAssets("t:AttackData", new[] { CombatFolder });
            int changedCount = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var attackData = AssetDatabase.LoadAssetAtPath<AttackData>(path);
                if (attackData == null)
                {
                    continue;
                }

                var so = new SerializedObject(attackData);
                SerializedProperty damageProperty = so.FindProperty("damage");
                if (damageProperty.floatValue != 10f)
                {
                    damageProperty.floatValue = 10f;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    changedCount++;
                }

                Debug.Log($"{path}: damage = {damageProperty.floatValue}");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Set damage=10 on {changedCount} AttackData asset(s) that weren't already at 10.");
        }
    }
}
