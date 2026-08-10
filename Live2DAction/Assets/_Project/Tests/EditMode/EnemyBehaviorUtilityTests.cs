using NUnit.Framework;
using Live2DAction.AI;

public class EnemyBehaviorUtilityTests
{
    [Test]
    public void DetermineState_BeyondDetectionRange_ReturnsIdle()
    {
        EnemyState state = EnemyBehaviorUtility.DetermineState(distanceToTarget: 20f, detectionRange: 8f, attackRange: 2f);

        Assert.AreEqual(EnemyState.Idle, state);
    }

    [Test]
    public void DetermineState_WithinDetectionButOutsideAttackRange_ReturnsChasing()
    {
        EnemyState state = EnemyBehaviorUtility.DetermineState(distanceToTarget: 5f, detectionRange: 8f, attackRange: 2f);

        Assert.AreEqual(EnemyState.Chasing, state);
    }

    [Test]
    public void DetermineState_WithinAttackRange_ReturnsAttacking()
    {
        EnemyState state = EnemyBehaviorUtility.DetermineState(distanceToTarget: 1f, detectionRange: 8f, attackRange: 2f);

        Assert.AreEqual(EnemyState.Attacking, state);
    }

    [Test]
    public void DetermineState_ExactlyAtDetectionRange_ReturnsChasing()
    {
        EnemyState state = EnemyBehaviorUtility.DetermineState(distanceToTarget: 8f, detectionRange: 8f, attackRange: 2f);

        Assert.AreEqual(EnemyState.Chasing, state);
    }

    [Test]
    public void DetermineState_ExactlyAtAttackRange_ReturnsAttacking()
    {
        EnemyState state = EnemyBehaviorUtility.DetermineState(distanceToTarget: 2f, detectionRange: 8f, attackRange: 2f);

        Assert.AreEqual(EnemyState.Attacking, state);
    }
}
