using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Live2DAction.Combat;
using Live2DAction.Core;
using Object = UnityEngine.Object;

// spec WUSHI_COMBAT_ENGINEERING_SPEC.md §8 (M4 項目 7) - a finisher on an IExecutable target (a boss
// with life nodes) hands the whole outcome to that target: OnExecutionStarted at the windup, then
// ResolveExecution at the end, and the ordinary "50% of current HP" fallback never runs. A plain
// target with no IExecutable still gets the fallback.
public class ExecutionAbilityRoutingTests
{
    private class StubExecutable : MonoBehaviour, IExecutable
    {
        public bool canExecute = true;
        public int startedCount;
        public int resolvedCount;
        public bool CanBeExecuted(GameObject executor) => canExecute;
        public void OnExecutionStarted(GameObject executor) => startedCount++;
        public ExecutionOutcome ResolveExecution(GameObject executor)
        {
            resolvedCount++;
            return ExecutionOutcome.PhaseTransition;
        }
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo f = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(f, $"no private field '{name}' on {target.GetType().Name}");
        f.SetValue(target, value);
    }

    private static void Invoke(object target, string method, params object[] args)
    {
        MethodInfo m = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(m, $"no private method '{method}' on {target.GetType().Name}");
        m.Invoke(target, args);
    }

    [SetUp]
    public void SetUp()
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            Object.DestroyImmediate(root);
        }
    }

    private static ExecutionAbility MakePlayer()
    {
        var go = new GameObject("Player");
        var ability = go.AddComponent<ExecutionAbility>();
        SetField(ability, "executionAnimationSeconds", 0f); // resolve on the next frame
        return ability;
    }

    private static GameObject MakeStaggeredTarget(out StancePoise stance, out Health health)
    {
        var go = new GameObject("Boss");
        health = go.AddComponent<Health>();
        stance = go.AddComponent<StancePoise>();
        stance.AddPostureDamage(stance.MaxStance); // -> IsStaggered
        return go;
    }

    [UnityTest]
    public IEnumerator ExecutableTarget_GetsStartedAndResolved_AndTakesNoFallbackDamage()
    {
        ExecutionAbility ability = MakePlayer();
        GameObject boss = MakeStaggeredTarget(out StancePoise stance, out Health health);
        var stub = boss.AddComponent<StubExecutable>();
        float hpBefore = health.CurrentHealth;

        Invoke(ability, "BeginExecution", stance);
        Assert.AreEqual(1, stub.startedCount, "OnExecutionStarted fires at the windup");

        for (int i = 0; i < 4; i++) yield return null; // let TickPendingExecution resolve

        Assert.AreEqual(1, stub.resolvedCount, "ResolveExecution fires when the finisher finishes");
        Assert.AreEqual(hpBefore, health.CurrentHealth, 0.01f, "the IExecutable owns damage - no 50% fallback hit");
    }

    [UnityTest]
    public IEnumerator NonExecutableTarget_StillTakesTheFiftyPercentFallback()
    {
        ExecutionAbility ability = MakePlayer();
        GameObject enemy = MakeStaggeredTarget(out StancePoise stance, out Health health);
        // no StubExecutable
        float hpBefore = health.CurrentHealth;

        Invoke(ability, "BeginExecution", stance);
        for (int i = 0; i < 4; i++) yield return null;

        Assert.AreEqual(hpBefore * 0.5f, health.CurrentHealth, 0.01f, "default fallback = 50% of current HP");
    }

    [UnityTest]
    public IEnumerator ExecutableThatRefuses_FallsBackToTheOrdinaryPath()
    {
        ExecutionAbility ability = MakePlayer();
        GameObject boss = MakeStaggeredTarget(out StancePoise stance, out Health health);
        var stub = boss.AddComponent<StubExecutable>();
        stub.canExecute = false; // e.g. no nodes left
        float hpBefore = health.CurrentHealth;

        Invoke(ability, "BeginExecution", stance);
        for (int i = 0; i < 4; i++) yield return null;

        Assert.AreEqual(0, stub.startedCount);
        Assert.AreEqual(0, stub.resolvedCount);
        Assert.AreEqual(hpBefore * 0.5f, health.CurrentHealth, 0.01f, "refused IExecutable -> ordinary 50% path");
    }
}
