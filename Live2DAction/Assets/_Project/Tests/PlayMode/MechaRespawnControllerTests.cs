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
// shared logic; this file focuses on Mecha's own wiring, one instance per revivable
// character on GameManager).
public class MechaRespawnControllerTests
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
    public IEnumerator MechaHealthReachesZero_RespawnsInPlaceWithFullHealthAfterDelay()
    {
        var mecha = new GameObject("Mecha");
        mecha.transform.position = new Vector3(-3f, 0.5f, 2f);
        Health mechaHealth = mecha.AddComponent<Health>();

        var managerGo = new GameObject("GameManager");
        RespawnController respawnController = managerGo.AddComponent<RespawnController>();
        SetField(respawnController, "target", mecha);
        SetField(respawnController, "targetHealth", mechaHealth);
        SetField(respawnController, "respawnDelaySeconds", 0.1f);

        Vector3 spawnPosition = mecha.transform.position;
        mechaHealth.ApplyDamage(new DamageInfo(mechaHealth.MaxHealth, Vector3.zero, Vector3.forward, null));

        yield return null; // let Health.Died's SetActive(false) actually take effect
        Assert.IsFalse(mecha.activeSelf, "Test setup expectation: Mecha should be inactive right after lethal damage");

        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < 1f && !mecha.activeSelf)
        {
            yield return null;
        }

        Assert.IsTrue(mecha.activeSelf, "Mecha should have respawned (reactivated) within 1s of dying");
        Assert.AreEqual(mechaHealth.MaxHealth, mechaHealth.CurrentHealth, "Respawned Mecha should be at full health");
        Assert.IsFalse(mechaHealth.IsDead);
        Assert.AreEqual(spawnPosition, mecha.transform.position, "Respawn should be in place, not move Mecha elsewhere");

        Object.Destroy(mecha);
        Object.Destroy(managerGo);
    }

    // Regression guard for GameManager hosting two RespawnController instances at once
    // (Player's and Mecha's) - each must resolve and revive only its own target, not get
    // confused between them (a plain single-component-per-GameObject assumption anywhere in
    // the wiring tools would silently wire both controllers to the same target).
    [UnityTest]
    public IEnumerator TwoRespawnControllersOnSameGameManager_EachRevivesOnlyItsOwnTarget()
    {
        var player = new GameObject("Player");
        Health playerHealth = player.AddComponent<Health>();
        var mecha = new GameObject("Mecha");
        Health mechaHealth = mecha.AddComponent<Health>();

        var managerGo = new GameObject("GameManager");
        RespawnController playerController = managerGo.AddComponent<RespawnController>();
        SetField(playerController, "target", player);
        SetField(playerController, "targetHealth", playerHealth);
        SetField(playerController, "respawnDelaySeconds", 0.1f);

        RespawnController mechaController = managerGo.AddComponent<RespawnController>();
        SetField(mechaController, "target", mecha);
        SetField(mechaController, "targetHealth", mechaHealth);
        SetField(mechaController, "respawnDelaySeconds", 0.1f);

        mechaHealth.ApplyDamage(new DamageInfo(mechaHealth.MaxHealth, Vector3.zero, Vector3.forward, null));
        yield return null;
        Assert.IsFalse(mecha.activeSelf, "Test setup expectation: Mecha should be inactive right after lethal damage");
        Assert.IsTrue(player.activeSelf, "Player was never damaged and should still be active");

        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < 1f && !mecha.activeSelf)
        {
            yield return null;
        }

        Assert.IsTrue(mecha.activeSelf, "Mecha should have respawned on its own controller");
        Assert.IsTrue(player.activeSelf, "Player should remain untouched by Mecha's respawn controller");
        Assert.AreEqual(playerHealth.MaxHealth, playerHealth.CurrentHealth, "Player was never damaged and should still be at full health");

        Object.Destroy(player);
        Object.Destroy(mecha);
        Object.Destroy(managerGo);
    }
}
