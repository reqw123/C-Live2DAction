using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Live2DAction.AI;
using Live2DAction.Characters;
using Live2DAction.Combat;
using Live2DAction.Core;
using Live2DAction.Input;
using Object = UnityEngine.Object;

// End-to-end: verifies EnemyAI actually drives PlayerCombat (the same combo/frame-data
// pipeline the player uses) to damage the player's Health, and that dodge invulnerability
// actually blocks that damage - the two things Step 5 was meant to connect, not just that
// each piece works in isolation.
public class EnemyAttacksPlayerTests
{
    private class StubInputBehaviour : MonoBehaviour, IInputCommand
    {
        public Vector2 MoveInput { get; set; }
        public bool AttackPressed { get; set; }
        public bool DodgePressed { get; set; }
        public bool LockOnPressed { get; set; }
        public bool JumpPressed { get; set; }
        public bool UltimatePressed { get; set; }
        public bool FlyPressed { get; set; }
        public bool FlyDescendPressed { get; set; }
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Expected a private field named '{fieldName}' on {target.GetType().Name}");
        field.SetValue(target, value);
    }

    private static AttackData CreateInstantHitAttackData(float damage)
    {
        var data = ScriptableObject.CreateInstance<AttackData>();
        SetField(data, "damage", damage);
        SetField(data, "range", 1f);
        SetField(data, "radius", 1f);
        SetField(data, "startupFrames", 0);
        SetField(data, "activeFrames", 0);
        SetField(data, "recoveryFrames", 0);
        return data;
    }

    [SetUp]
    public void SetUp()
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            Object.DestroyImmediate(root);
        }
    }

    private static GameObject CreatePlayer(out Health health, out CharacterMovement movement)
    {
        var player = new GameObject("Player");
        player.transform.position = new Vector3(0f, 0f, 1f);
        // minMoveDistance=0: see CharacterMovementTests.SetUp - default 0.001 silently drops
        // sub-threshold Move() calls at the frame rates headless batchmode can hit.
        player.AddComponent<CharacterController>().minMoveDistance = 0f;
        health = player.AddComponent<Health>();
        movement = player.AddComponent<CharacterMovement>();
        SetField(movement, "gravity", 0f);
        SetField(movement, "health", health);
        return player;
    }

    private static GameObject CreateAttackingEnemy(Transform target, float damage)
    {
        var enemy = new GameObject("Enemy");
        enemy.transform.position = Vector3.zero;
        enemy.AddComponent<CharacterController>().minMoveDistance = 0f;

        EnemyAI ai = enemy.AddComponent<EnemyAI>();
        SetField(ai, "target", target);
        SetField(ai, "detectionRange", 20f);
        SetField(ai, "attackRange", 5f); // already within range at the positions used here
        SetField(ai, "gravity", 0f);

        PlayerCombat combat = enemy.AddComponent<PlayerCombat>();
        SetField(combat, "inputSource", ai);
        SetField(combat, "comboAttacks", new[] { CreateInstantHitAttackData(damage) });

        return enemy;
    }

    [UnityTest]
    public IEnumerator EnemyInAttackRange_DamagesPlayerThroughSharedCombatPipeline()
    {
        GameObject player = CreatePlayer(out Health playerHealth, out _);
        GameObject enemy = CreateAttackingEnemy(player.transform, damage: 10f);

        for (int i = 0; i < 5; i++)
        {
            yield return null; // step EnemyAI -> PlayerCombat's combo state machine through to its hit
        }

        Assert.AreEqual(playerHealth.MaxHealth - 10f, playerHealth.CurrentHealth);

        Object.Destroy(player);
        Object.Destroy(enemy);
    }

    [UnityTest]
    public IEnumerator PlayerDodging_BlocksEnemyAttackDamage()
    {
        GameObject player = CreatePlayer(out Health playerHealth, out CharacterMovement movement);
        var playerInput = player.AddComponent<StubInputBehaviour>();
        SetField(movement, "inputSource", playerInput);

        var dodgeData = ScriptableObject.CreateInstance<DodgeData>();
        SetField(dodgeData, "distance", 0f); // stay in place so the enemy remains "in range" - isolates invulnerability from repositioning
        SetField(dodgeData, "durationFrames", 30);
        SetField(dodgeData, "invulnerabilityFrames", 30);
        SetField(dodgeData, "cooldownFrames", 6);
        SetField(movement, "dodgeData", dodgeData);

        GameObject enemy = CreateAttackingEnemy(player.transform, damage: 10f);

        playerInput.DodgePressed = true;
        yield return null; // start the dodge (and let CharacterMovement sync IsInvulnerable)
        playerInput.DodgePressed = false;

        Assert.IsTrue(playerHealth.IsInvulnerable, "Test setup expectation: player should be dodge-invulnerable now");

        for (int i = 0; i < 5; i++)
        {
            yield return null; // let the enemy's attack try (and fail) to land while invulnerable
        }

        Assert.AreEqual(playerHealth.MaxHealth, playerHealth.CurrentHealth, "Damage should have been blocked by dodge invulnerability");

        Object.Destroy(player);
        Object.Destroy(enemy);
    }
}
