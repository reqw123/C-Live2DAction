using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Live2DAction.Characters;
using Live2DAction.Core;
using Object = UnityEngine.Object;

// Explicit user request (2026-08-13, "設計player2可以復活") - Player2 should revive the same way
// Player does, reusing RespawnController (generalized this same day from Player-only
// PlayerRespawnController - see PlayerRespawnControllerTests for the original coverage of the
// shared logic; this file focuses on Player2's own wiring, one instance per revivable
// character on GameManager).
public class Player2RespawnControllerTests
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
    public IEnumerator Player2HealthReachesZero_RespawnsInPlaceWithFullHealthAfterDelay()
    {
        var player2 = new GameObject("Player2");
        player2.transform.position = new Vector3(-3f, 0.5f, 2f);
        Health player2Health = player2.AddComponent<Health>();

        var managerGo = new GameObject("GameManager");
        RespawnController respawnController = managerGo.AddComponent<RespawnController>();
        SetField(respawnController, "target", player2);
        SetField(respawnController, "targetHealth", player2Health);
        SetField(respawnController, "respawnDelaySeconds", 0.1f);

        Vector3 spawnPosition = player2.transform.position;
        player2Health.ApplyDamage(new DamageInfo(player2Health.MaxHealth, Vector3.zero, Vector3.forward, null));

        yield return null; // let Health.Died's SetActive(false) actually take effect
        Assert.IsFalse(player2.activeSelf, "Test setup expectation: Player2 should be inactive right after lethal damage");

        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < 1f && !player2.activeSelf)
        {
            yield return null;
        }

        Assert.IsTrue(player2.activeSelf, "Player2 should have respawned (reactivated) within 1s of dying");
        Assert.AreEqual(player2Health.MaxHealth, player2Health.CurrentHealth, "Respawned Player2 should be at full health");
        Assert.IsFalse(player2Health.IsDead);
        Assert.AreEqual(spawnPosition, player2.transform.position, "Respawn should be in place, not move Player2 elsewhere");

        Object.Destroy(player2);
        Object.Destroy(managerGo);
    }

    // Regression guard for GameManager hosting two RespawnController instances at once
    // (Player's and Player2's) - each must resolve and revive only its own target, not get
    // confused between them (a plain single-component-per-GameObject assumption anywhere in
    // the wiring tools would silently wire both controllers to the same target).
    [UnityTest]
    public IEnumerator TwoRespawnControllersOnSameGameManager_EachRevivesOnlyItsOwnTarget()
    {
        var player = new GameObject("Player");
        Health playerHealth = player.AddComponent<Health>();
        var player2 = new GameObject("Player2");
        Health player2Health = player2.AddComponent<Health>();

        var managerGo = new GameObject("GameManager");
        RespawnController playerController = managerGo.AddComponent<RespawnController>();
        SetField(playerController, "target", player);
        SetField(playerController, "targetHealth", playerHealth);
        SetField(playerController, "respawnDelaySeconds", 0.1f);

        RespawnController player2Controller = managerGo.AddComponent<RespawnController>();
        SetField(player2Controller, "target", player2);
        SetField(player2Controller, "targetHealth", player2Health);
        SetField(player2Controller, "respawnDelaySeconds", 0.1f);

        player2Health.ApplyDamage(new DamageInfo(player2Health.MaxHealth, Vector3.zero, Vector3.forward, null));
        yield return null;
        Assert.IsFalse(player2.activeSelf, "Test setup expectation: Player2 should be inactive right after lethal damage");
        Assert.IsTrue(player.activeSelf, "Player was never damaged and should still be active");

        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < 1f && !player2.activeSelf)
        {
            yield return null;
        }

        Assert.IsTrue(player2.activeSelf, "Player2 should have respawned on its own controller");
        Assert.IsTrue(player.activeSelf, "Player should remain untouched by Player2's respawn controller");
        Assert.AreEqual(playerHealth.MaxHealth, playerHealth.CurrentHealth, "Player was never damaged and should still be at full health");

        Object.Destroy(player);
        Object.Destroy(player2);
        Object.Destroy(managerGo);
    }
}
