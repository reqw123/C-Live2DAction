using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.Characters;
using Live2DAction.Combat;
using Live2DAction.Core;
using Live2DAction.Input;

namespace Live2DAction.EditorTools
{
    // 2026-08-31, user request ("現在我要把滑鼠右鍵改成武士刀防禦"). Adds PlayerGuard to the Player
    // and wires it: input (PlayerInputProvider), CharacterMovement, Health, StancePoise, and the
    // sword-arm bone for the procedural block pose (Bip001-R-Forearm via the Humanoid rig).
    //
    // Re-runnable. Paired with the retirement of the shooting system - RangedWeapon /
    // RangedAttackDistance / the tracer LineRenderer / the crosshair HUD / the AK47 hand instance
    // were removed from the Player in the same change (the AK47 FBX + RangedWeapon.cs stay on disk;
    // "Add Ranged Weapon To Player" can put it all back).
    internal static class PlayerGuardSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";

        [MenuItem("Tools/Live2DAction/Add Player Katana Guard")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Exit Play Mode first - this edits the scene.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("PlayerGuardSetup: no Player in " + ScenePath);
                return;
            }

            var input = player.GetComponent<PlayerInputProvider>();
            var movement = player.GetComponent<CharacterMovement>();
            var health = player.GetComponent<Health>();
            var stance = player.GetComponent<StancePoise>();
            if (input == null || health == null)
            {
                Debug.LogError("PlayerGuardSetup: Player is missing PlayerInputProvider / Health.");
                return;
            }

            var guard = player.GetComponent<PlayerGuard>();
            if (guard == null)
            {
                guard = player.AddComponent<PlayerGuard>();
            }

            Transform swordArm = FindSwordArmBone(player);
            Transform upperArm = FindHumanBone(player, HumanBodyBones.RightUpperArm);

            var so = new SerializedObject(guard);
            so.FindProperty("inputSource").objectReferenceValue = input;
            so.FindProperty("movement").objectReferenceValue = movement;
            so.FindProperty("health").objectReferenceValue = health;
            so.FindProperty("stance").objectReferenceValue = stance;
            so.FindProperty("swordArmBone").objectReferenceValue = swordArm;
            so.FindProperty("upperArmBone").objectReferenceValue = upperArm;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(guard);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("PlayerGuardSetup: PlayerGuard wired. Hold right mouse to block. " +
                      "Forearm = " + (swordArm != null ? swordArm.name : "<none>") +
                      ", upper arm = " + (upperArm != null ? upperArm.name : "<none>") + ".");
        }

        // Prefer the Humanoid right lower arm (Bip001-R-Forearm). Fall back to the parent of the
        // right-hand weapon mount if the rig isn't Humanoid for some reason.
        private static Transform FindSwordArmBone(GameObject player)
        {
            Transform forearm = FindHumanBone(player, HumanBodyBones.RightLowerArm);
            if (forearm != null)
            {
                return forearm;
            }

            foreach (Transform t in player.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Rhand_Weapon2")
                {
                    return t.parent; // the hand bone
                }
            }
            return null;
        }

        private static Transform FindHumanBone(GameObject player, HumanBodyBones bone)
        {
            var anim = player.GetComponentInChildren<Animator>();
            return anim != null && anim.isHuman ? anim.GetBoneTransform(bone) : null;
        }
    }
}
