using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Live2DAction.AI;
using Live2DAction.Combat;
using Live2DAction.Core;
using Live2DAction.Input;
using Live2DAction.Targeting;

// Loads the real GreyboxTest scene and verifies EnemyAISetup.cs's wiring actually
// took (2026-08-12: "把 Player4 當作敵人開始製作 AI 自主攻擊模式") - not just that EnemyAI's
// own state machine works in isolation (already covered by EnemyAITests.cs), but that Enemy
// specifically ended up with the right components pointed at the right things, and that it
// actually starts chasing/attacking once the player gets close enough in the real scene
// (not a synthetic one built purely for the test).
public class EnemyIntegrationTests
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
        public bool BoostPressed { get; set; } // 2026-08-20, flight system design - interface addition, stub needs it to compile
    }

    private static object GetField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Expected a private field named '{fieldName}' on {target.GetType().Name}");
        return field.GetValue(target);
    }

    private static IEnumerator RunForSeconds(float seconds)
    {
        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < seconds)
        {
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator Enemy_IsWiredAsAnAIEnemyTargetingPlayer()
    {
        SceneManager.LoadScene("GreyboxTest");
        yield return null;
        yield return null;

        GameObject player = GameObject.Find("Player");
        GameObject enemy = GameObject.Find("Enemy");
        Assert.IsNotNull(player, "Player not found in GreyboxTest scene");
        Assert.IsNotNull(enemy, "Enemy not found in GreyboxTest scene");

        Assert.IsNull(enemy.GetComponent<CapsuleCollider>(), "Enemy should no longer have the standee's CapsuleCollider once converted to a CharacterController-driven enemy");
        Assert.IsNotNull(enemy.GetComponent<CharacterController>(), "Enemy should have a CharacterController");
        Assert.IsNotNull(enemy.GetComponent<Health>(), "Enemy should have Health");
        Assert.IsNotNull(enemy.GetComponent<LockOnTarget>(), "Enemy should still be lockable (added earlier as a standee)");

        EnemyAI ai = enemy.GetComponent<EnemyAI>();
        Assert.IsNotNull(ai, "Enemy should have EnemyAI");
        Assert.AreSame(player.transform, GetField(ai, "target"), "Enemy's EnemyAI should target Player");

        PlayerCombat combat = enemy.GetComponent<PlayerCombat>();
        Assert.IsNotNull(combat, "Enemy should have PlayerCombat");
        Assert.AreSame(ai, GetField(combat, "inputSource"), "Enemy's PlayerCombat should read input from its own EnemyAI");

        var comboAttacks = (AttackData[])GetField(combat, "comboAttacks");
        Assert.IsNotNull(comboAttacks);
        Assert.IsTrue(comboAttacks.Length > 0 && comboAttacks[0] != null, "Enemy's PlayerCombat should have at least one AttackData assigned");
    }

    [UnityTest]
    public IEnumerator Enemy_ChasesAndAttacksWhenPlayerGetsClose()
    {
        SceneManager.LoadScene("GreyboxTest");
        yield return null;
        yield return null;

        GameObject player = GameObject.Find("Player");
        GameObject enemy = GameObject.Find("Enemy");
        Assert.IsNotNull(player, "Player not found in GreyboxTest scene");
        Assert.IsNotNull(enemy, "Enemy not found in GreyboxTest scene");

        EnemyAI ai = enemy.GetComponent<EnemyAI>();
        Assert.AreEqual(EnemyState.Idle, ai.CurrentState, "Sanity check: Enemy should start Idle before the player approaches");

        // Teleport the player within Enemy's default detectionRange (8) but outside
        // attackRange (2), so the very first state transition observed is Chasing rather than
        // skipping straight to Attacking - isolates "does it notice" from "does it also close
        // the last distance and attack", the same reasoning EnemyAITests already uses.
        player.transform.position = enemy.transform.position + new Vector3(5f, 0f, 0f);

        // Real elapsed time, not a fixed frame count - see EnemyAITests.RunForSeconds for why
        // headless batchmode's per-frame deltaTime can't be trusted to cover a target duration.
        float start = Time.realtimeSinceStartup;
        bool leftIdle = false;
        while (Time.realtimeSinceStartup - start < 1f)
        {
            if (ai.CurrentState != EnemyState.Idle)
            {
                leftIdle = true;
                break;
            }
            yield return null;
        }
        Assert.IsTrue(leftIdle, "Enemy should notice the player once within detectionRange and leave Idle");

        // Now close the remaining distance and confirm it actually attacks (not just chases
        // forever) - mirrors EnemyAttacksPlayerTests' end-to-end intent but against the real
        // scene's Enemy instead of a synthetic enemy.
        yield return RunForSeconds(3f);
        Assert.AreEqual(EnemyState.Attacking, ai.CurrentState, "Enemy should close the distance and start attacking within a few seconds");
    }
}
