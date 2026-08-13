using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Live2DAction.AI;
using Live2DAction.Core;

// Real 2026-08-13 bug report: user tuned EnemyAttack.asset's Range up to 7.5 (a long-reach
// attack) but reported "我沒有被敵人隔空打到" - Player4's EnemyAI.attackRange (a separate field
// that decides when the AI commits to attacking at all) was still at its old default of 2, so
// Player4 always walked all the way to near-melee distance before ever attacking, regardless
// of how far AttackData.Range could actually reach. Fixed by EnemyAttackRangeSync.cs, which
// syncs attackRange to just under EnemyAttack.asset's current Range. Range/Radius were later
// (same day) brought back down to 4.5/1 - the original 7.5 made the hit judged far beyond the
// punch animation's own visual reach ("敵人離我很遠就開始原地揮拳") - but the attackRange/Range
// coupling this test guards against is unaffected by the exact numbers. This test loads the
// real GreyboxTest scene and proves the two stay in sync where it actually matters: Player4
// lands a hit on a Player standing beyond old-melee distance, without walking all the way in.
public class EnemyAttackRangeSceneTests
{
    [UnityTest]
    public IEnumerator Player4_AttacksPlayerFromRangeWithoutClosingToMeleeDistance_InRealScene()
    {
        SceneManager.LoadScene("GreyboxTest");
        yield return null;
        yield return null;

        GameObject player = GameObject.Find("Player");
        GameObject player4 = GameObject.Find("Player4");
        Assert.IsNotNull(player, "Player not found in GreyboxTest scene");
        Assert.IsNotNull(player4, "Player4 not found in GreyboxTest scene");

        EnemyAI ai = player4.GetComponent<EnemyAI>();
        Assert.IsNotNull(ai, "Player4 has no EnemyAI");

        // A distance clearly beyond old-melee contact (~1) and beyond the old broken
        // attackRange default (2) - if EnemyAI.attackRange ever regresses back to a small
        // value without a matching AttackData.Range, Player4 would just keep Chasing instead
        // of Attacking at this distance, and this test would catch it.
        const float testDistance = 5f;
        Vector3 direction = (player.transform.position - player4.transform.position);
        direction.y = 0f;
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        player.transform.position = player4.transform.position + direction * testDistance;

        Health playerHealth = player.GetComponent<Health>();
        Assert.IsNotNull(playerHealth, "Player has no Health");
        float startingHealth = playerHealth.CurrentHealth;

        float start = Time.realtimeSinceStartup;
        float minDistanceObserved = float.MaxValue;
        while (Time.realtimeSinceStartup - start < 3f && playerHealth.CurrentHealth >= startingHealth)
        {
            float currentDistance = Vector3.Distance(player.transform.position, player4.transform.position);
            if (currentDistance < minDistanceObserved)
            {
                minDistanceObserved = currentDistance;
            }
            yield return null;
        }

        Assert.Less(playerHealth.CurrentHealth, startingHealth, "Player4 should have landed a hit within 3s at long range");
        Assert.Greater(minDistanceObserved, 2f, $"Player4 should have attacked without closing to old-melee/old-attackRange distance (closest observed: {minDistanceObserved})");
    }
}
