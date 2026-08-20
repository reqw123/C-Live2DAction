using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Live2DAction.Core;
using Object = UnityEngine.Object;

// Runs the real Update loop - HealthRegeneration's whole mechanism is accumulating
// Time.deltaTime across frames, which only actually ticks in PlayMode (mirrors JumpTests/
// DodgeMovementTests's own reasoning for being PlayMode rather than EditMode tests).
public class HealthRegenerationTests
{
    private GameObject _character;
    private Health _health;

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

        _character = new GameObject("Character");
        _health = _character.AddComponent<Health>();
        HealthRegeneration regen = _character.AddComponent<HealthRegeneration>();
        // Shrunk from the real 10s/2-per-second design values so this test doesn't take 10+
        // real seconds to run - the timing math itself is already covered by
        // HealthRegenerationUtilityTests (EditMode), this test is about the MonoBehaviour
        // actually wiring Health <-> HealthRegeneration correctly end to end.
        SetField(regen, "health", _health);
        SetField(regen, "idleSecondsBeforeRegen", 0.1f);
        SetField(regen, "regenPerSecond", 50f);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            Object.DestroyImmediate(root);
        }
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
    public IEnumerator BeforeIdleThresholdElapses_DoesNotRegenerate()
    {
        _health.ApplyDamage(new DamageInfo(30f, Vector3.zero, Vector3.forward, null));
        float damagedHealth = _health.CurrentHealth;

        yield return RunForSeconds(0.05f); // well under the 0.1s idle threshold

        Assert.AreEqual(damagedHealth, _health.CurrentHealth,
            "Should not regenerate any health before the idle threshold has elapsed");
    }

    [UnityTest]
    public IEnumerator AfterIdleThresholdElapses_RegeneratesHealthOverTime()
    {
        _health.ApplyDamage(new DamageInfo(30f, Vector3.zero, Vector3.forward, null));
        float damagedHealth = _health.CurrentHealth;

        yield return RunForSeconds(0.3f); // past the 0.1s idle threshold, several ticks of regen

        Assert.Greater(_health.CurrentHealth, damagedHealth,
            "Should have regenerated some health once idle past the threshold");
        Assert.LessOrEqual(_health.CurrentHealth, _health.MaxHealth);
    }

    [UnityTest]
    public IEnumerator DamagedAgainDuringIdlePeriod_ResetsTimerAndDelaysRegeneration()
    {
        _health.ApplyDamage(new DamageInfo(50f, Vector3.zero, Vector3.forward, null));

        yield return RunForSeconds(0.08f); // most of the way to the 0.1s threshold, not there yet

        _health.ApplyDamage(new DamageInfo(10f, Vector3.zero, Vector3.forward, null));
        float healthAfterSecondHit = _health.CurrentHealth;

        yield return RunForSeconds(0.08f); // would have cleared the original threshold, but the timer just reset

        Assert.AreEqual(healthAfterSecondHit, _health.CurrentHealth,
            "Taking damage again should reset the idle timer, delaying regeneration further");
    }

    [UnityTest]
    public IEnumerator AtFullHealth_NeverExceedsMaxHealth()
    {
        yield return RunForSeconds(0.3f); // already full - idle from the start

        Assert.AreEqual(_health.MaxHealth, _health.CurrentHealth);
    }
}
