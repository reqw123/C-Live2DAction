using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Live2DAction.Characters;
using Live2DAction.Core;
using Object = UnityEngine.Object;

// Explicit user request (2026-08-13, "發現敵人死了不會復活") - Player4 previously kept the
// default "打倒=永久關掉" behavior on purpose (see KNOWN_ISSUES.md's history), but the user
// asked for consistency with Player/Player2 instead. Reuses RespawnController the same way
// Player2RespawnControllerTests does; this file focuses on Player4's own wiring.
public class Player4RespawnControllerTests
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
    public IEnumerator Player4HealthReachesZero_RespawnsInPlaceWithFullHealthAfterDelay()
    {
        var player4 = new GameObject("Player4");
        player4.transform.position = new Vector3(4f, 0.5f, -1f);
        Health player4Health = player4.AddComponent<Health>();

        var managerGo = new GameObject("GameManager");
        RespawnController respawnController = managerGo.AddComponent<RespawnController>();
        SetField(respawnController, "target", player4);
        SetField(respawnController, "targetHealth", player4Health);
        SetField(respawnController, "respawnDelaySeconds", 0.1f);

        Vector3 spawnPosition = player4.transform.position;
        player4Health.ApplyDamage(new DamageInfo(player4Health.MaxHealth, Vector3.zero, Vector3.forward, null));

        yield return null; // let Health.Died's SetActive(false) actually take effect
        Assert.IsFalse(player4.activeSelf, "Test setup expectation: Player4 should be inactive right after lethal damage");

        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < 1f && !player4.activeSelf)
        {
            yield return null;
        }

        Assert.IsTrue(player4.activeSelf, "Player4 should have respawned (reactivated) within 1s of dying");
        Assert.AreEqual(player4Health.MaxHealth, player4Health.CurrentHealth, "Respawned Player4 should be at full health");
        Assert.IsFalse(player4Health.IsDead);
        Assert.AreEqual(spawnPosition, player4.transform.position, "Respawn should be in place, not move Player4 elsewhere");

        Object.Destroy(player4);
        Object.Destroy(managerGo);
    }
}
