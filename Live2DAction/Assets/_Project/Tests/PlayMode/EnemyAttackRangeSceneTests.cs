using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Live2DAction.AI;
using Live2DAction.Combat;
using Live2DAction.Core;

// Real 2026-08-13 bug report: user tuned EnemyAttack.asset's Range up to 7.5 (a long-reach
// attack) but reported "我沒有被敵人隔空打到" - Player4's EnemyAI.attackRange (a separate field
// that decides when the AI commits to attacking at all) was still at its old default of 2, so
// Player4 always walked all the way to near-melee distance before ever attacking, regardless
// of how far AttackData.Range could actually reach. A second, opposite-direction desync bug
// followed (CHANGELOG.md, "EnemyAttack.asset 又被改動" entry): attackRange(4) outlived a Range
// cut down to 1.5, so Player4 declared Attacking from a distance its actual hit capsule
// couldn't reach and had to keep closing in anyway. Both were root-caused to the same thing -
// EnemyAI.attackRange was a second, manually-synced number that could always drift stale
// against AttackData.Range/Radius - and permanently fixed by wiring EnemyAI.combat so
// ResolveEffectiveAttackRange() derives the attack-commit distance live from PrimaryAttack's
// actual Range+Radius (see EnemyAI.cs's own "combat" field comment). This test is deliberately
// NOT hardcoded to a specific "ranged" or "melee" design (EnemyAttack.asset's Range/Radius are
// the user's own combat-feel tuning and have changed several times, most recently down to
// 0.5/0.5 for a melee-feeling Player4, 2026-08-16 user decision) - it reads whatever
// Range/Radius are currently configured and asserts Player4 only ever lands a hit once within
// roughly that capsule's own reach, regardless of what that reach currently is. If attackRange
// ever desyncs from AttackData again (either direction), minDistanceObserved will land far
// outside the tolerance below and this test will catch it.
public class EnemyAttackRangeSceneTests
{
    // Generous relative to a single frame's worth of chase movement (moveSpeed=2) so normal
    // one-frame overshoot when closing the last bit of distance doesn't false-positive, while
    // still being far tighter than the multi-unit gaps both historical desync bugs produced.
    private const float CapsuleReachTolerance = 0.5f;

    [UnityTest]
    public IEnumerator Player4_AttacksPlayerOnlyOnceWithinItsOwnAttackCapsuleReach_InRealScene()
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

        PlayerCombat combat = player4.GetComponent<PlayerCombat>();
        Assert.IsNotNull(combat, "Player4 has no PlayerCombat");
        AttackData attack = combat.PrimaryAttack;
        Assert.IsNotNull(attack, "Player4's PlayerCombat has no PrimaryAttack configured");
        float effectiveRange = attack.Range + attack.Radius;

        // Comfortably beyond the current effective range so Player4 actually has to chase in,
        // whatever that range currently is - not a hardcoded "beyond old-melee" distance.
        float testDistance = effectiveRange + 4f;
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

        Assert.Less(playerHealth.CurrentHealth, startingHealth, "Player4 should have landed a hit within 3s");
        Assert.GreaterOrEqual(minDistanceObserved, effectiveRange - CapsuleReachTolerance,
            $"Player4's attack-commit distance looks desynced from its actual hit capsule reach ({effectiveRange:F2}) - closest observed: {minDistanceObserved:F2}");
    }
}
