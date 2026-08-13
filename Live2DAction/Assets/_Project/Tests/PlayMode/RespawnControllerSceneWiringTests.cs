using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Live2DAction.Characters;
using Live2DAction.Core;

// Regression test for a real user report (2026-08-13, "現在角色1不會復活") - renaming
// PlayerRespawnController's fields (player/playerHealth -> target/targetHealth) as part of
// generalizing it into RespawnController left Player's ALREADY-SERIALIZED component in the
// scene with null target/targetHealth (Unity serializes fields by name - the old data under
// the old names became orphaned, and the new field names had never been serialized before).
// PlayerRespawnSetup.Apply() was re-run to fix Player's instance, but nothing caught that it
// had gone stale in the first place - every other test either builds a fresh RespawnController
// (PlayerRespawnControllerTests/Player2RespawnControllerTests) or wires fields directly via
// reflection, neither of which touches the actual persisted scene data. This test loads the
// real GreyboxTest scene and checks the actual wiring, the same way WorldSpaceHealthBarTests
// does for health bars - so a future field rename that forgets to re-run the setup tool for an
// EXISTING instance fails a test instead of silently shipping a broken respawn.
public class RespawnControllerSceneWiringTests
{
    [UnityTest]
    public IEnumerator Player_Player2_And_Player4_HaveCorrectlyWiredRespawnControllersInRealScene()
    {
        SceneManager.LoadScene("GreyboxTest");
        yield return null;
        yield return null;

        GameObject player = GameObject.Find("Player");
        GameObject player2 = GameObject.Find("Player2");
        GameObject player4 = GameObject.Find("Player4");
        GameObject manager = GameObject.Find("GameManager");
        Assert.IsNotNull(player, "Player not found in GreyboxTest scene");
        Assert.IsNotNull(player2, "Player2 not found in GreyboxTest scene");
        Assert.IsNotNull(player4, "Player4 not found in GreyboxTest scene");
        Assert.IsNotNull(manager, "GameManager not found in GreyboxTest scene");

        RespawnController[] controllers = manager.GetComponents<RespawnController>();
        // 2026-08-13: Player4 added alongside Player/Player2 after the user noticed "發現敵人
        // 死了不會復活" and asked for consistency (previously a deliberate choice to leave
        // Player4 without respawn - see KNOWN_ISSUES.md's history on this).
        Assert.AreEqual(3, controllers.Length, "GameManager should have exactly 3 RespawnControllers (Player + Player2 + Player4)");

        AssertWiredTo(controllers, player, "Player");
        AssertWiredTo(controllers, player2, "Player2");
        AssertWiredTo(controllers, player4, "Player4");
    }

    private static void AssertWiredTo(RespawnController[] controllers, GameObject expectedTarget, string label)
    {
        foreach (RespawnController controller in controllers)
        {
            var target = (GameObject)GetField(controller, "target");
            if (target == expectedTarget)
            {
                var targetHealth = (Health)GetField(controller, "targetHealth");
                Assert.IsNotNull(targetHealth, $"{label}'s RespawnController has a target but null targetHealth - stale/incomplete scene wiring");
                Assert.AreSame(expectedTarget.GetComponent<Health>(), targetHealth, $"{label}'s RespawnController targetHealth should be {label}'s own Health component");
                return;
            }
        }

        Assert.Fail($"No RespawnController on GameManager has target == {label} - wiring is missing or stale (target still null after a field rename?).");
    }

    private static object GetField(object instance, string fieldName)
    {
        FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Expected a private field named '{fieldName}' on {instance.GetType().Name}");
        return field.GetValue(instance);
    }
}
