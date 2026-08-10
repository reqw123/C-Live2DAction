using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.Combat;

namespace Live2DAction.EditorTools
{
    // Creates the three-hit combo's AttackData assets and wires them into the Player's
    // PlayerCombat.comboAttacks array in the existing GreyboxTest scene. Needed one-time
    // because PlayerCombat's old single "attackData" field was renamed to the "comboAttacks"
    // array when the frame-data combo system was added - the old wiring is simply gone from
    // the saved scene rather than broken, since Unity drops serialized data for fields that
    // no longer exist without erroring.
    internal static class FixComboAttacksSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string ComboAttacksFolder = "Assets/_Project/Settings/Combat";

        [MenuItem("Tools/Live2DAction/[Fix] Wire Combo Attacks On Player")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("Player GameObject not found in " + ScenePath);
                return;
            }

            PlayerCombat combat = player.GetComponent<PlayerCombat>();
            if (combat == null)
            {
                Debug.LogError("Player has no PlayerCombat component.");
                return;
            }

            AttackData[] comboAttacks =
            {
                CreateOrLoadAttackData("LightAttack1", damage: 8f, startupFrames: 6, activeFrames: 4, recoveryFrames: 14, comboWindowFrames: 10),
                CreateOrLoadAttackData("LightAttack2", damage: 10f, startupFrames: 7, activeFrames: 4, recoveryFrames: 16, comboWindowFrames: 10),
                CreateOrLoadAttackData("LightAttack3", damage: 16f, startupFrames: 10, activeFrames: 5, recoveryFrames: 22, comboWindowFrames: 0),
            };

            var combatSo = new SerializedObject(combat);
            SerializedProperty comboProperty = combatSo.FindProperty("comboAttacks");
            comboProperty.arraySize = comboAttacks.Length;
            for (int i = 0; i < comboAttacks.Length; i++)
            {
                comboProperty.GetArrayElementAtIndex(i).objectReferenceValue = comboAttacks[i];
            }
            combatSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Wired 3-hit combo AttackData assets into Player's PlayerCombat.comboAttacks.");
        }

        private static AttackData CreateOrLoadAttackData(string assetName, float damage, int startupFrames, int activeFrames, int recoveryFrames, int comboWindowFrames)
        {
            string path = $"{ComboAttacksFolder}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<AttackData>(path);
            if (existing != null)
            {
                return existing;
            }

            if (!AssetDatabase.IsValidFolder(ComboAttacksFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Settings", "Combat");
            }

            var data = ScriptableObject.CreateInstance<AttackData>();
            var so = new SerializedObject(data);
            so.FindProperty("attackId").stringValue = assetName;
            so.FindProperty("damage").floatValue = damage;
            so.FindProperty("startupFrames").intValue = startupFrames;
            so.FindProperty("activeFrames").intValue = activeFrames;
            so.FindProperty("recoveryFrames").intValue = recoveryFrames;
            so.FindProperty("comboWindowFrames").intValue = comboWindowFrames;
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(data, path);
            return data;
        }
    }
}
