using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Live2DAction.Characters;
using Live2DAction.Core;
using Object = UnityEngine.Object;

// Regression test for a real user report (2026-08-12, "角色依舊消失" / "整個遊戲畫面完全沒反應") -
// Player's Health hitting zero deactivated the whole GameObject with no way back, which read
// as the game freezing (every one of Player's own scripts, including input reading, lives on
// that GameObject and stops ticking the instant it's deactivated). RespawnController (renamed
// 2026-08-13 from Player-only PlayerRespawnController so Mecha could reuse the same
// component - see MechaRespawnControllerTests for its own coverage) fixes this by respawning
// in place with full health after a short delay.
public class PlayerRespawnControllerTests
{
    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Expected a private field named '{fieldName}' on {target.GetType().Name}");
        field.SetValue(target, value);
    }

    [SetUp]
    public void SetUp()
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            Object.DestroyImmediate(root);
        }
    }

    [UnityTest]
    public IEnumerator PlayerHealthReachesZero_RespawnsInPlaceWithFullHealthAfterDelay()
    {
        var player = new GameObject("Player");
        player.transform.position = new Vector3(3f, 0.5f, -2f);
        Health playerHealth = player.AddComponent<Health>();

        // Deliberately on a separate, always-active GameObject - see the class comment for
        // why a component living on Player itself couldn't survive Player's own deactivation.
        var managerGo = new GameObject("GameManager");
        RespawnController respawnController = managerGo.AddComponent<RespawnController>();
        SetField(respawnController, "target", player);
        SetField(respawnController, "targetHealth", playerHealth);
        SetField(respawnController, "respawnDelaySeconds", 0.1f);

        Vector3 spawnPosition = player.transform.position;
        playerHealth.ApplyDamage(new DamageInfo(playerHealth.MaxHealth, Vector3.zero, Vector3.forward, null));

        yield return null; // let Health.Died's SetActive(false) actually take effect
        Assert.IsFalse(player.activeSelf, "Test setup expectation: Player should be inactive right after lethal damage");

        // Real elapsed time, not a fixed frame count - same reasoning as every other
        // RunForSeconds-style wait in this codebase (headless batchmode's per-frame
        // Time.deltaTime can be tiny).
        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < 1f && !player.activeSelf)
        {
            yield return null;
        }

        Assert.IsTrue(player.activeSelf, "Player should have respawned (reactivated) within 1s of dying");
        Assert.AreEqual(playerHealth.MaxHealth, playerHealth.CurrentHealth, "Respawned player should be at full health");
        Assert.IsFalse(playerHealth.IsDead);
        Assert.AreEqual(spawnPosition, player.transform.position, "Respawn should be in place, not move the player elsewhere");

        Object.Destroy(player);
        Object.Destroy(managerGo);
    }
}
