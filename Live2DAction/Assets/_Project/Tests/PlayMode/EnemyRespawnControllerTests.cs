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
// asked for consistency with Player/Mecha instead. Reuses RespawnController the same way
// MechaRespawnControllerTests does; this file focuses on Enemy's own wiring.
public class EnemyRespawnControllerTests
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
    public IEnumerator EnemyHealthReachesZero_RespawnsInPlaceWithFullHealthAfterDelay()
    {
        var enemy = new GameObject("Enemy");
        enemy.transform.position = new Vector3(4f, 0.5f, -1f);
        Health enemyHealth = enemy.AddComponent<Health>();

        var managerGo = new GameObject("GameManager");
        RespawnController respawnController = managerGo.AddComponent<RespawnController>();
        SetField(respawnController, "target", enemy);
        SetField(respawnController, "targetHealth", enemyHealth);
        SetField(respawnController, "respawnDelaySeconds", 0.1f);

        Vector3 spawnPosition = enemy.transform.position;
        enemyHealth.ApplyDamage(new DamageInfo(enemyHealth.MaxHealth, Vector3.zero, Vector3.forward, null));

        yield return null; // let Health.Died's SetActive(false) actually take effect
        Assert.IsFalse(enemy.activeSelf, "Test setup expectation: Enemy should be inactive right after lethal damage");

        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < 1f && !enemy.activeSelf)
        {
            yield return null;
        }

        Assert.IsTrue(enemy.activeSelf, "Enemy should have respawned (reactivated) within 1s of dying");
        Assert.AreEqual(enemyHealth.MaxHealth, enemyHealth.CurrentHealth, "Respawned Enemy should be at full health");
        Assert.IsFalse(enemyHealth.IsDead);
        Assert.AreEqual(spawnPosition, enemy.transform.position, "Respawn should be in place, not move Enemy elsewhere");

        Object.Destroy(enemy);
        Object.Destroy(managerGo);
    }
}
