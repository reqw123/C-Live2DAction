using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Live2DAction.Characters;
using Live2DAction.Combat;
using Live2DAction.Core;
using Live2DAction.Input;
using Object = UnityEngine.Object;

// End-to-end for the katana guard (2026-08-31, "把滑鼠右鍵改成武士刀防禦"): a held block routes a
// frontal hit through PlayerGuard.ModifyIncoming (an IIncomingDamageModifier on the same Health)
// and cuts its HP damage, a hit from behind is unaffected, and the guard slows ground movement.
public class PlayerGuardPlayModeTests
{
    private class StubInput : MonoBehaviour, IInputCommand
    {
        public Vector2 MoveInput { get; set; }
        public bool AttackPressed { get; set; }
        public bool DodgePressed { get; set; }
        public bool LockOnPressed { get; set; }
        public bool JumpPressed { get; set; }
        public bool UltimatePressed { get; set; }
        public bool FlyPressed { get; set; }
        public bool FlyDescendPressed { get; set; }
        public bool BoostPressed { get; set; }
        public bool AimPressed { get; set; }
        public bool FirePressed { get; set; }
        public bool ViewTogglePressed { get; set; }
        public bool ZoomInPressed { get; set; }
        public bool ZoomOutPressed { get; set; }
        public bool GuardPressed { get; set; }
        public bool GuardPressedThisFrame { get; set; }
    }

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

    [TearDown]
    public void TearDown()
    {
        // A parry fires a HitStop (static Time.timeScale dip) - never let it bleed into the next test.
        HitStopController.CancelAndRestore();
    }

    private static GameObject CreateGuardedPlayer(out Health health, out PlayerGuard guard, out StubInput input, out CharacterMovement movement)
    {
        var player = new GameObject("Player");
        player.transform.position = Vector3.zero;
        player.transform.rotation = Quaternion.LookRotation(Vector3.forward); // faces +Z
        player.AddComponent<CharacterController>().minMoveDistance = 0f;
        health = player.AddComponent<Health>();
        movement = player.AddComponent<CharacterMovement>();
        SetField(movement, "gravity", 0f);
        SetField(movement, "health", health);
        input = player.AddComponent<StubInput>();
        SetField(movement, "inputSource", input);

        guard = player.AddComponent<PlayerGuard>();
        SetField(guard, "inputSource", input);
        SetField(guard, "health", health);
        SetField(guard, "movement", movement);
        SetField(guard, "blockedDamageMultiplier", 0.15f);
        SetField(guard, "poiseMultiplier", 0.2f);
        SetField(guard, "blockedSpeedMultiplier", 0.35f);
        SetField(guard, "guardArcDegrees", 150f);
        return player;
    }

    // DamageInfo.Direction = target - attacker (away from the attacker).
    private static DamageInfo HitFrom(Vector3 attackerPos, Vector3 targetPos, float amount)
    {
        return new DamageInfo(amount, targetPos, targetPos - attackerPos, null);
    }

    private static GameObject CreateGuardedPlayerWithStance(out PlayerGuard guard, out StubInput input, out StancePoise stance)
    {
        GameObject player = CreateGuardedPlayer(out Health _, out guard, out input, out _);
        stance = player.AddComponent<StancePoise>();
        SetField(guard, "stance", stance);
        SetField(guard, "clashCooldownSeconds", 0f); // resolve back-to-back clashes without a real-time wait
        return player;
    }

    private static GameObject CreateAttacker(GameObject player, out StancePoise attackerStance)
    {
        var attacker = new GameObject("Boss");
        attacker.transform.position = player.transform.position + Vector3.forward * 2f; // dead ahead of a +Z-facing player
        attacker.AddComponent<Health>();
        attackerStance = attacker.AddComponent<StancePoise>();
        return attacker;
    }

    // A frontal blade clash: direction = target - attacker points -Z toward a +Z-facing player.
    private static BladeClashInfo FrontalClash(GameObject attacker, float poiseDamage)
    {
        return new BladeClashInfo(attacker, 0f, poiseDamage, Vector3.up, Vector3.back);
    }

    // spec item 6: a plain guard costs the ATTACK's own poise damage, so a heavy strike pressures
    // the player's stance more than a light one - not a flat number for every hit.
    [UnityTest]
    public IEnumerator Guard_HeavierAttack_BuildsMorePlayerStance()
    {
        GameObject player = CreateGuardedPlayerWithStance(out PlayerGuard guard, out StubInput input, out StancePoise stance);
        GameObject attacker = CreateAttacker(player, out _);
        input.GuardPressed = true;
        yield return null; // IsBlocking true; no press edge => InParryWindow false => Guarded, not Parried

        float before = stance.CurrentStance;
        Assert.AreEqual(BladeClashResult.Guarded, guard.TryResolveClash(FrontalClash(attacker, 12f)));
        float lightGain = stance.CurrentStance - before;

        before = stance.CurrentStance;
        Assert.AreEqual(BladeClashResult.Guarded, guard.TryResolveClash(FrontalClash(attacker, 22f)));
        float heavyGain = stance.CurrentStance - before;

        Assert.AreEqual(12f, lightGain, 0.01f);
        Assert.AreEqual(22f, heavyGain, 0.01f);
    }

    // A perfect parry still costs the player 0 stance and feeds the boss's posture.
    [UnityTest]
    public IEnumerator Parry_LeavesPlayerStanceUntouched_AndDamagesBossPosture()
    {
        GameObject player = CreateGuardedPlayerWithStance(out PlayerGuard guard, out StubInput input, out StancePoise playerStance);
        GameObject attacker = CreateAttacker(player, out StancePoise bossPosture);

        input.GuardPressed = true;
        input.GuardPressedThisFrame = true;
        yield return null; // Update() records the press edge => parry window open
        input.GuardPressedThisFrame = false;

        float playerBefore = playerStance.CurrentStance;
        Assert.AreEqual(BladeClashResult.Parried, guard.TryResolveClash(FrontalClash(attacker, 22f)));

        Assert.AreEqual(playerBefore, playerStance.CurrentStance, 0.01f);
        Assert.Greater(bossPosture.CurrentStance, 0f);
    }

    // spec item 2: a quick tap that's already RELEASED still counts as an active defensive action
    // for the whole tap-guard window - the bug was the Animator / volume disagreeing with each other.
    [UnityTest]
    public IEnumerator ReleasedTap_StillCountsAsAnActiveDefense_ForTheWholeTapWindow()
    {
        CreateGuardedPlayerWithStance(out PlayerGuard guard, out StubInput input, out _);

        input.GuardPressed = true;
        input.GuardPressedThisFrame = true;
        yield return null;                 // press edge recorded
        input.GuardPressed = false;        // released almost immediately (a tap)
        input.GuardPressedThisFrame = false;

        // Simulate being 0.3s into the tap window (past the 0.2s parry window, inside the 0.55s tap
        // window) without a real-time wait - frozen play mode never advances WaitForSeconds.
        SetField(guard, "_guardStartTime", Time.time - 0.3f);
        yield return null;

        Assert.IsFalse(guard.IsBlocking, "button was released");
        Assert.IsTrue(guard.DefenseActionActive, "still inside the tap-guard window");
        Assert.AreEqual(PlayerGuard.DefenseState.Guard, guard.CurrentDefense);
    }

    [UnityTest]
    public IEnumerator Staggered_NoDefenseActionEvenWithButtonHeld()
    {
        CreateGuardedPlayerWithStance(out PlayerGuard guard, out StubInput input, out StancePoise stance);
        input.GuardPressed = true;
        input.GuardPressedThisFrame = true;
        yield return null;

        stance.AddPostureDamage(stance.MaxStance); // -> IsStaggered
        yield return null;

        Assert.IsTrue(stance.IsStaggered);
        Assert.IsFalse(guard.DefenseActionActive);
        Assert.AreEqual(PlayerGuard.DefenseState.None, guard.CurrentDefense);
    }

    [UnityTest]
    public IEnumerator CancelDefenseAction_EndsItEvenWhileHeld_ThenRecoversOnRelease()
    {
        CreateGuardedPlayerWithStance(out PlayerGuard guard, out StubInput input, out _);
        input.GuardPressed = true;
        yield return null;
        Assert.IsTrue(guard.IsBlocking);

        guard.CancelDefenseAction();
        yield return null;
        Assert.IsFalse(guard.IsBlocking, "suppressed even though the button is still held");
        Assert.IsFalse(guard.DefenseActionActive);

        input.GuardPressed = false;
        yield return null;                 // release lifts the suppression
        input.GuardPressed = true;
        input.GuardPressedThisFrame = true;
        yield return null;
        input.GuardPressedThisFrame = false;
        Assert.IsTrue(guard.IsBlocking, "a fresh press works again");
    }

    [UnityTest]
    public IEnumerator HeldGuard_FrontalHit_IsMitigated()
    {
        GameObject player = CreateGuardedPlayer(out Health health, out _, out StubInput input, out _);
        input.GuardPressed = true;
        yield return null; // PlayerGuard.Update runs, IsBlocking becomes true

        health.ApplyDamage(HitFrom(new Vector3(0f, 0f, 5f), player.transform.position, 100f));

        Assert.AreEqual(health.MaxHealth - 15f, health.CurrentHealth, 0.01f);
    }

    [UnityTest]
    public IEnumerator HeldGuard_HitFromBehind_IsNotMitigated()
    {
        GameObject player = CreateGuardedPlayer(out Health health, out _, out StubInput input, out _);
        input.GuardPressed = true;
        yield return null;

        health.ApplyDamage(HitFrom(new Vector3(0f, 0f, -5f), player.transform.position, 100f));

        Assert.AreEqual(health.MaxHealth - 100f, health.CurrentHealth, 0.01f);
    }

    [UnityTest]
    public IEnumerator NoGuard_FrontalHit_TakesFullDamage()
    {
        GameObject player = CreateGuardedPlayer(out Health health, out _, out StubInput input, out _);
        input.GuardPressed = false;
        yield return null;

        health.ApplyDamage(HitFrom(new Vector3(0f, 0f, 5f), player.transform.position, 100f));

        Assert.AreEqual(health.MaxHealth - 100f, health.CurrentHealth, 0.01f);
    }

    [UnityTest]
    public IEnumerator Guard_SlowsThenRestoresGroundSpeedMultiplier()
    {
        CreateGuardedPlayer(out _, out _, out StubInput input, out CharacterMovement movement);

        input.GuardPressed = true;
        yield return null;
        Assert.AreEqual(0.35f, movement.ExternalSpeedMultiplier, 0.001f);

        input.GuardPressed = false;
        yield return null;
        Assert.AreEqual(1f, movement.ExternalSpeedMultiplier, 0.001f);
    }
}
