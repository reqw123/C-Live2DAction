using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.Characters;
using Live2DAction.Combat;

namespace Live2DAction.EditorTools
{
    // One-time fix-up applying AttackPoseVisualizer to the existing GreyboxTest scene (see
    // AttackPoseVisualizer's class comment for why this exists - placeholder attack pose,
    // no authored animation clips yet). Not called from GreyboxSceneBuilder.Build(): it needs
    // Player's Animator, which only exists after PlayerMayaVisualSetup swaps the Maya visual
    // in (a separate manual step run after building the greybox scene) - same reasoning as
    // why WireCharacterAnimatorLink isn't called from Build() either. Run this after both
    // PlayerMayaVisualSetup and WireCharacterAnimatorLink.
    internal static class WireAttackPoseVisualizers
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";

        [MenuItem("Tools/Live2DAction/Wire Attack Pose Visualizers")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("Player GameObject not found in " + ScenePath);
                return;
            }

            WirePlayer(player);

            // TrainingDummy is legitimately optional - it used to be a hard requirement here,
            // which meant re-running this after any Player-only visual swap (e.g.
            // PlayerMayaVisualSetup) silently skipped WirePlayer too, since both checks gated
            // both wire calls. Wire whichever of the two actually exists.
            //
            // 2026-08-19 naming note: "TrainingDummy" is now Player3 (see
            // TrainingDummySetup.cs, from the character-renaming pass) - a DIFFERENT, unrelated
            // object from whatever was called "TrainingDummy" when this comment was first
            // written (that one predated Player3 entirely and no longer exists). The optional-
            // find logic below still behaves correctly either way since it only checks whether
            // SOME object named "TrainingDummy" exists and has the right components
            // (PlayerCombat/Animator, both of which Player3 also has) - just be aware the local
            // variable name "enemy" here is now a misnomer for what it actually finds.
            GameObject enemy = GameObject.Find("TrainingDummy");
            if (enemy != null)
            {
                WireEnemy(enemy);
            }
            else
            {
                Debug.Log("TrainingDummy not found - skipping its AttackPoseVisualizer wiring (Player's is still wired above).");
            }

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Wired AttackPoseVisualizer on Player" + (enemy != null ? " and TrainingDummy." : "."));
        }

        internal static void WirePlayer(GameObject player)
        {
            PlayerCombat combat = player.GetComponent<PlayerCombat>();
            if (combat == null)
            {
                Debug.LogError("Player has no PlayerCombat component - cannot wire AttackPoseVisualizer.");
                return;
            }

            Animator animator = player.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                Debug.LogError("No Animator found under Player - is the Maya visual attached?");
                return;
            }

            Transform armBone = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            if (armBone == null)
            {
                Debug.LogError("Player's Animator has no RightUpperArm bone mapped - is it a Humanoid rig?");
                return;
            }

            AttackPoseVisualizer visualizer = player.GetComponent<AttackPoseVisualizer>();
            if (visualizer == null)
            {
                visualizer = player.AddComponent<AttackPoseVisualizer>();
            }

            var so = new SerializedObject(visualizer);
            so.FindProperty("combatSource").objectReferenceValue = combat;
            so.FindProperty("swingTransform").objectReferenceValue = armBone;
            so.FindProperty("swingAxis").vector3Value = Vector3.forward;
            so.FindProperty("windUpAngleDegrees").floatValue = 20f;
            so.FindProperty("swingAngleDegrees").floatValue = 70f;
            // The initial forward-axis guess swung the arm the wrong way in an actual Play
            // test (see FixAttackPoseDirection.cs, which patches an already-wired scene) -
            // true confirmed by eye, kept here so a future full re-wire starts correct.
            so.FindProperty("invert").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void WireEnemy(GameObject enemy)
        {
            PlayerCombat combat = enemy.GetComponent<PlayerCombat>();
            if (combat == null)
            {
                Debug.LogError("Enemy has no PlayerCombat component - cannot wire AttackPoseVisualizer.");
                return;
            }

            Transform visual = enemy.transform.Find("Visual");
            if (visual == null)
            {
                Debug.LogError("Enemy has no 'Visual' child - cannot wire AttackPoseVisualizer.");
                return;
            }

            AttackPoseVisualizer visualizer = enemy.GetComponent<AttackPoseVisualizer>();
            if (visualizer == null)
            {
                visualizer = enemy.AddComponent<AttackPoseVisualizer>();
            }

            var so = new SerializedObject(visualizer);
            so.FindProperty("combatSource").objectReferenceValue = combat;
            so.FindProperty("swingTransform").objectReferenceValue = visual;
            // Enemy has no arm bone (unrigged capsule - see AttackPoseVisualizer's class
            // comment), so the whole Visual capsule leans forward on a punch instead.
            so.FindProperty("swingAxis").vector3Value = Vector3.right;
            so.FindProperty("windUpAngleDegrees").floatValue = 10f;
            so.FindProperty("swingAngleDegrees").floatValue = 30f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
