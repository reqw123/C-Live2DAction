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

// Loads the real GreyboxTest scene and verifies Player4EnemyAISetup.cs's wiring actually
// took (2026-08-12: "把 Player4 當作敵人開始製作 AI 自主攻擊模式") - not just that EnemyAI's
// own state machine works in isolation (already covered by EnemyAITests.cs), but that Player4
// specifically ended up with the right components pointed at the right things, and that it
// actually starts chasing/attacking once the player gets close enough in the real scene
// (not a synthetic one built purely for the test).
public class Player4EnemyIntegrationTests
{
    private class StubInputBehaviour : MonoBehaviour, IInputCommand
    {
        public Vector2 MoveInput { get; set; }
        public bool AttackPressed { get; set; }
        public bool DodgePressed { get; set; }
        public bool LockOnPressed { get; set; }
        public bool JumpPressed { get; set; }
        public bool UltimatePressed { get; set; }
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
    public IEnumerator Player4_IsWiredAsAnAIEnemyTargetingPlayer()
    {
        SceneManager.LoadScene("GreyboxTest");
        yield return null;
        yield return null;

        GameObject player = GameObject.Find("Player");
        GameObject player4 = GameObject.Find("Player4");
        Assert.IsNotNull(player, "Player not found in GreyboxTest scene");
        Assert.IsNotNull(player4, "Player4 not found in GreyboxTest scene");

        Assert.IsNull(player4.GetComponent<CapsuleCollider>(), "Player4 should no longer have the standee's CapsuleCollider once converted to a CharacterController-driven enemy");
        Assert.IsNotNull(player4.GetComponent<CharacterController>(), "Player4 should have a CharacterController");
        Assert.IsNotNull(player4.GetComponent<Health>(), "Player4 should have Health");
        Assert.IsNotNull(player4.GetComponent<LockOnTarget>(), "Player4 should still be lockable (added earlier as a standee)");

        EnemyAI ai = player4.GetComponent<EnemyAI>();
        Assert.IsNotNull(ai, "Player4 should have EnemyAI");
        Assert.AreSame(player.transform, GetField(ai, "target"), "Player4's EnemyAI should target Player");

        PlayerCombat combat = player4.GetComponent<PlayerCombat>();
        Assert.IsNotNull(combat, "Player4 should have PlayerCombat");
        Assert.AreSame(ai, GetField(combat, "inputSource"), "Player4's PlayerCombat should read input from its own EnemyAI");

        var comboAttacks = (AttackData[])GetField(combat, "comboAttacks");
        Assert.IsNotNull(comboAttacks);
        Assert.IsTrue(comboAttacks.Length > 0 && comboAttacks[0] != null, "Player4's PlayerCombat should have at least one AttackData assigned");
    }

    [UnityTest]
    public IEnumerator Player4_ChasesAndAttacksWhenPlayerGetsClose()
    {
        SceneManager.LoadScene("GreyboxTest");
        yield return null;
        yield return null;

        GameObject player = GameObject.Find("Player");
        GameObject player4 = GameObject.Find("Player4");
        Assert.IsNotNull(player, "Player not found in GreyboxTest scene");
        Assert.IsNotNull(player4, "Player4 not found in GreyboxTest scene");

        EnemyAI ai = player4.GetComponent<EnemyAI>();
        Assert.AreEqual(EnemyState.Idle, ai.CurrentState, "Sanity check: Player4 should start Idle before the player approaches");

        // Teleport the player within Player4's default detectionRange (8) but outside
        // attackRange (2), so the very first state transition observed is Chasing rather than
        // skipping straight to Attacking - isolates "does it notice" from "does it also close
        // the last distance and attack", the same reasoning EnemyAITests already uses.
        player.transform.position = player4.transform.position + new Vector3(5f, 0f, 0f);

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
        Assert.IsTrue(leftIdle, "Player4 should notice the player once within detectionRange and leave Idle");

        // Now close the remaining distance and confirm it actually attacks (not just chases
        // forever) - mirrors EnemyAttacksPlayerTests' end-to-end intent but against the real
        // scene's Player4 instead of a synthetic enemy.
        yield return RunForSeconds(3f);
        Assert.AreEqual(EnemyState.Attacking, ai.CurrentState, "Player4 should close the distance and start attacking within a few seconds");
    }
}
