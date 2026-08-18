using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.Core;
using Live2DAction.Combat;

namespace Live2DAction.EditorTools
{
    // 2026-08-18, explicit user request ("將這個動作作為所有角色死亡時的共同動作") - wires
    // DeathAnimationLink onto every character with a compatible Humanoid rig (Player/Player3/
    // Player4, all sharing Maya's or Arisa's Animator Controller - see
    // SpecialMoveAnimatorSetup.cs's own "Dead" wiring) and flips Health.
    // deferDeactivationToDeathAnimation on for each, so their death is played out via the Dying
    // clip instead of Health's original synchronous SetActive(false).
    //
    // Player2 (no Animator at all - see this session's own "沒有貼圖的機甲戰士" placeholder
    // status) and 076/077 (Live2D Cubism billboards, a completely different animation system,
    // not Humanoid Mecanim) are deliberately left untouched here - Health's default
    // (deferDeactivationToDeathAnimation=false) keeps their original immediate-deactivation
    // behavior, so nothing about them regresses just because this class exists.
    internal static class DeathAnimationSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private static readonly string[] TargetNames = { "Player", "Player3", "Player4" };

        [MenuItem("Tools/Live2DAction/Add Death Animation To Humanoid Characters")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            int wired = 0;
            foreach (string name in TargetNames)
            {
                GameObject owner = GameObject.Find(name);
                if (owner == null)
                {
                    Debug.LogError(name + " GameObject not found in " + ScenePath);
                    continue;
                }

                if (AddDeathAnimation(owner))
                {
                    wired++;
                }
            }

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Wired death animation onto " + wired + " character(s).");
        }

        private static bool AddDeathAnimation(GameObject owner)
        {
            Health health = owner.GetComponent<Health>();
            if (health == null)
            {
                Debug.LogError(owner.name + " has no Health component - cannot wire a death animation to it.");
                return false;
            }

            Transform visual = owner.transform.Find("Visual");
            Animator animator = visual != null ? visual.GetComponent<Animator>() : null;
            if (animator == null)
            {
                Debug.LogError(owner.name + " has no Animator on its 'Visual' child - cannot wire a death animation to it.");
                return false;
            }

            DeathAnimationLink existing = owner.GetComponent<DeathAnimationLink>();
            if (existing == null)
            {
                existing = owner.gameObject.AddComponent<DeathAnimationLink>();
            }

            var soLink = new SerializedObject(existing);
            soLink.FindProperty("animator").objectReferenceValue = animator;
            soLink.ApplyModifiedPropertiesWithoutUndo();

            var soHealth = new SerializedObject(health);
            soHealth.FindProperty("deferDeactivationToDeathAnimation").boolValue = true;
            soHealth.ApplyModifiedPropertiesWithoutUndo();

            return true;
        }
    }
}
