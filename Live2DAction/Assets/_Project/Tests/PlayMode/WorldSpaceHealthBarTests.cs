using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Live2DAction.Core;
using Live2DAction.UI;
using Object = UnityEngine.Object;

// Runs the real Update/LateUpdate loop so WorldSpaceHealthBar actually writes to the Image
// component, which EditMode can't check (covered in isolation by HealthBarUtilityTests).
public class WorldSpaceHealthBarTests
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
    public IEnumerator HealthBar_UpdatesFillAmount_WhenHealthChanges()
    {
        var owner = new GameObject("Owner");
        Health health = owner.AddComponent<Health>();

        var barGo = new GameObject("HealthBar");
        barGo.transform.SetParent(owner.transform);
        var fillImage = barGo.AddComponent<Image>();
        fillImage.type = Image.Type.Filled;
        WorldSpaceHealthBar bar = barGo.AddComponent<WorldSpaceHealthBar>();
        SetField(bar, "health", health);
        SetField(bar, "fillImage", fillImage);

        yield return null;
        Assert.AreEqual(1f, fillImage.fillAmount, 0.001f, "Full health should start the bar full");

        health.ApplyDamage(new DamageInfo(10f, Vector3.zero, Vector3.forward, null));
        yield return null;

        Assert.AreEqual(0.9f, fillImage.fillAmount, 0.001f, "10/100 damage should leave the bar at 90%");

        Object.Destroy(owner);
    }

    [UnityTest]
    public IEnumerator Player_And_Player4_HaveWorldSpaceHealthBarsInRealScene()
    {
        SceneManager.LoadScene("GreyboxTest");
        yield return null;
        yield return null;

        GameObject player = GameObject.Find("Player");
        GameObject player4 = GameObject.Find("Player4");
        Assert.IsNotNull(player, "Player not found in GreyboxTest scene");
        Assert.IsNotNull(player4, "Player4 not found in GreyboxTest scene");

        AssertHasWiredHealthBar(player, "Player");
        AssertHasWiredHealthBar(player4, "Player4");
    }

    // 2026-08-13, explicit user request ("幫我讓player2也有血條 也能受擊，但是他不會自主攻擊") -
    // Player2 gets the same bar and can take damage, but deliberately has no PlayerCombat/
    // EnemyAI wired to it (it never attacks back).
    [UnityTest]
    public IEnumerator Player2_HasHealthBarAndCanBeDamaged_ButHasNoAttackCapability()
    {
        SceneManager.LoadScene("GreyboxTest");
        yield return null;
        yield return null;

        GameObject player2 = GameObject.Find("Player2");
        Assert.IsNotNull(player2, "Player2 not found in GreyboxTest scene");

        AssertHasWiredHealthBar(player2, "Player2");

        Assert.IsNull(player2.GetComponent<Live2DAction.Combat.PlayerCombat>(), "Player2 should not have PlayerCombat - it must never attack");
        Assert.IsNull(player2.GetComponent<Live2DAction.AI.EnemyAI>(), "Player2 should not have EnemyAI - it must never attack");

        Health health = player2.GetComponent<Health>();
        float before = health.CurrentHealth;
        health.ApplyDamage(new DamageInfo(10f, Vector3.zero, Vector3.forward, null));
        Assert.AreEqual(before - 10f, health.CurrentHealth, "Player2 should actually take damage when hit");
    }

    // 2026-08-13, explicit user request ("引入 Cross Punch.fbx，攻擊、動作判定、機制完全與p1一
    // 致，差別只在於他完全不會動，也不會攻擊") - Player3 shares Player's exact combat data
    // (same LightAttack1/2/3 asset references, not copies) and Maya visual, but has no input
    // source/AI (never attacks) and no CharacterController/movement component (never moves) -
    // same "damageable but passive" contract as Player2.
    [UnityTest]
    public IEnumerator Player3_SharesPlayersExactCombatData_ButNeverMovesOrAttacks()
    {
        SceneManager.LoadScene("GreyboxTest");
        yield return null;
        yield return null;

        GameObject player = GameObject.Find("Player");
        GameObject player3 = GameObject.Find("Player3");
        Assert.IsNotNull(player3, "Player3 not found in GreyboxTest scene");

        AssertHasWiredHealthBar(player3, "Player3");

        Live2DAction.Combat.PlayerCombat playerCombat = player.GetComponent<Live2DAction.Combat.PlayerCombat>();
        Live2DAction.Combat.PlayerCombat player3Combat = player3.GetComponent<Live2DAction.Combat.PlayerCombat>();
        Assert.IsNotNull(player3Combat, "Player3 should have PlayerCombat - same attack mechanism as Player");

        var comboAttacksField = typeof(Live2DAction.Combat.PlayerCombat).GetField("comboAttacks", BindingFlags.Instance | BindingFlags.NonPublic);
        var playerCombos = (Live2DAction.Combat.AttackData[])comboAttacksField.GetValue(playerCombat);
        var player3Combos = (Live2DAction.Combat.AttackData[])comboAttacksField.GetValue(player3Combat);
        Assert.AreEqual(playerCombos.Length, player3Combos.Length, "Player3 should have the same number of combo steps as Player");
        for (int i = 0; i < playerCombos.Length; i++)
        {
            Assert.AreSame(playerCombos[i], player3Combos[i], $"Player3's combo step {i} should reference the exact same AttackData asset as Player's, not a copy");
        }

        Assert.IsNull(player3.GetComponent<CharacterController>(), "Player3 should have no CharacterController - it never moves");
        Assert.IsNull(player3.GetComponent<Live2DAction.Input.PlayerInputProvider>(), "Player3 should have no PlayerInputProvider - it never attacks");
        Assert.IsNull(player3.GetComponent<Live2DAction.AI.EnemyAI>(), "Player3 should have no EnemyAI - it never attacks");

        Vector3 startPosition = player3.transform.position;
        Health player3Health = player3.GetComponent<Health>();
        float before = player3Health.CurrentHealth;

        yield return null;
        yield return null;

        Assert.AreEqual(startPosition, player3.transform.position, "Player3 should never move on its own");
        Assert.AreEqual(-1, player3Combat.ComboIndex, "Player3 should never enter an attack (ComboIndex stays -1 with no input driving it)");

        player3Health.ApplyDamage(new DamageInfo(10f, Vector3.zero, Vector3.forward, null));
        Assert.AreEqual(before - 10f, player3Health.CurrentHealth, "Player3 should actually take damage when hit, same as Player2");
    }

    // Regression test for a real user report (2026-08-12, "被攻擊時血量條貼圖不會扣...血條滿
    // 格的狀態敵人直接消失了") - the isolated test above proves fillAmount the *property*
    // updates correctly, and the scene-wiring test proves Player/Player4 each have a
    // correctly-wired bar, but neither one screenshots the actual rendered pixels. Real root
    // cause (found by screenshotting the bar during actual Play mode at 50% HP and seeing an
    // unchanged full-width bar): Image.Type.Filled has NO visual effect at all without an
    // assigned Sprite (see HealthBarSetup.CreateStretchedImage's own comment) - fillAmount
    // happily updates as a plain float the whole time, masking the bug from any test that only
    // reads the property instead of the rendered geometry. This test can't screenshot pixels
    // either, but AssertHasWiredHealthBar below now checks fillImage.sprite != null, which is
    // the actual condition that was silently broken.
    [UnityTest]
    public IEnumerator PlayerBar_UpdatesWhenPlayer4DamagesPlayer_InRealScene()
    {
        SceneManager.LoadScene("GreyboxTest");
        yield return null;
        yield return null;

        GameObject player = GameObject.Find("Player");
        GameObject player4 = GameObject.Find("Player4");
        Health playerHealth = player.GetComponent<Health>();
        WorldSpaceHealthBar bar = player.GetComponentInChildren<WorldSpaceHealthBar>(true);
        var fillImage = (Image)typeof(WorldSpaceHealthBar)
            .GetField("fillImage", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(bar);

        player.transform.position = player4.transform.position + new Vector3(1.5f, 0f, 0f);

        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < 3f && playerHealth.CurrentHealth >= playerHealth.MaxHealth)
        {
            yield return null;
        }
        // One extra frame past whichever frame CurrentHealth actually changed on - Update()
        // execution order between PlayerCombat and WorldSpaceHealthBar isn't guaranteed, so the
        // very same frame damage lands can still show the pre-damage fillAmount for that one
        // frame without it being a real bug (see class comment).
        yield return null;

        Assert.Less(playerHealth.CurrentHealth, playerHealth.MaxHealth, "Player should have taken damage from Player4 within 3s");
        Assert.AreEqual(HealthBarUtility.ComputeFillAmount(playerHealth.CurrentHealth, playerHealth.MaxHealth), fillImage.fillAmount, 0.001f,
            "Player's own health bar should reflect the damage Player4 dealt");
    }

    private static void AssertHasWiredHealthBar(GameObject owner, string label)
    {
        WorldSpaceHealthBar bar = owner.GetComponentInChildren<WorldSpaceHealthBar>(true);
        Assert.IsNotNull(bar, $"{label} should have a WorldSpaceHealthBar in its hierarchy");

        var health = (Health)typeof(WorldSpaceHealthBar)
            .GetField("health", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(bar);
        Assert.AreSame(owner.GetComponent<Health>(), health, $"{label}'s health bar should reference its own Health");

        var fillImage = (Image)typeof(WorldSpaceHealthBar)
            .GetField("fillImage", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(bar);
        Assert.IsNotNull(fillImage, $"{label}'s health bar should have a fillImage assigned");
        Assert.AreEqual(Image.Type.Filled, fillImage.type, $"{label}'s health bar fill image should be a Filled-type Image");
        // 2026-08-12 real bug: Image.Type.Filled has no visual effect at all without an
        // assigned Sprite - fillAmount still happily updates as a plain float, so this is the
        // one property check that actually would have caught it (see HealthBarSetup's own
        // comment on this).
        Assert.IsNotNull(fillImage.sprite, $"{label}'s health bar fill image needs a Sprite assigned - Image.Type.Filled silently does nothing without one, even though fillAmount still updates.");
    }
}
