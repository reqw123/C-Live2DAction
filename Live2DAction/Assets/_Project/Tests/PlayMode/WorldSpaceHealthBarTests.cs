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
    public IEnumerator Player_And_Enemy_HaveWorldSpaceHealthBarsInRealScene()
    {
        SceneManager.LoadScene("GreyboxTest");
        yield return null;
        yield return null;

        GameObject player = GameObject.Find("Player");
        GameObject enemy = GameObject.Find("Enemy");
        Assert.IsNotNull(player, "Player not found in GreyboxTest scene");
        Assert.IsNotNull(enemy, "Enemy not found in GreyboxTest scene");

        AssertHasWiredHealthBar(player, "Player");
        AssertHasWiredHealthBar(enemy, "Enemy");
    }

    // 2026-08-13, explicit user request ("幫我讓player2也有血條 也能受擊，但是他不會自主攻擊") -
    // Mecha gets the same bar and can take damage, but deliberately has no PlayerCombat/
    // EnemyAI wired to it (it never attacks back).
    [UnityTest]
    public IEnumerator Mecha_HasHealthBarAndCanBeDamaged_ButHasNoAttackCapability()
    {
        SceneManager.LoadScene("GreyboxTest");
        yield return null;
        yield return null;

        GameObject mecha = GameObject.Find("Mecha");
        Assert.IsNotNull(mecha, "Mecha not found in GreyboxTest scene");

        AssertHasWiredHealthBar(mecha, "Mecha");

        Assert.IsNull(mecha.GetComponent<Live2DAction.Combat.PlayerCombat>(), "Mecha should not have PlayerCombat - it must never attack");
        Assert.IsNull(mecha.GetComponent<Live2DAction.AI.EnemyAI>(), "Mecha should not have EnemyAI - it must never attack");

        Health health = mecha.GetComponent<Health>();
        float before = health.CurrentHealth;
        health.ApplyDamage(new DamageInfo(10f, Vector3.zero, Vector3.forward, null));
        Assert.AreEqual(before - 10f, health.CurrentHealth, "Mecha should actually take damage when hit");
    }

    // 2026-08-13, explicit user request ("引入 Cross Punch.fbx，攻擊、動作判定、機制完全與p1一
    // 致，差別只在於他完全不會動，也不會攻擊") - Player3 shares Player's exact combat data
    // (same LightAttack1/2/3 asset references, not copies) and Maya visual, but has no input
    // source/AI (never attacks) and no CharacterController/movement component (never moves) -
    // same "damageable but passive" contract as Mecha.
    [UnityTest]
    public IEnumerator TrainingDummy_SharesPlayersExactCombatData_ButNeverMovesOrAttacks()
    {
        SceneManager.LoadScene("GreyboxTest");
        yield return null;
        yield return null;

        GameObject player = GameObject.Find("Player");
        GameObject trainingDummy = GameObject.Find("TrainingDummy");
        Assert.IsNotNull(trainingDummy, "TrainingDummy not found in GreyboxTest scene");

        AssertHasWiredHealthBar(trainingDummy, "TrainingDummy");

        Live2DAction.Combat.PlayerCombat playerCombat = player.GetComponent<Live2DAction.Combat.PlayerCombat>();
        Live2DAction.Combat.PlayerCombat trainingDummyCombat = trainingDummy.GetComponent<Live2DAction.Combat.PlayerCombat>();
        Assert.IsNotNull(trainingDummyCombat, "TrainingDummy should have PlayerCombat - same attack mechanism as Player");

        var comboAttacksField = typeof(Live2DAction.Combat.PlayerCombat).GetField("comboAttacks", BindingFlags.Instance | BindingFlags.NonPublic);
        var playerCombos = (Live2DAction.Combat.AttackData[])comboAttacksField.GetValue(playerCombat);
        var trainingDummyCombos = (Live2DAction.Combat.AttackData[])comboAttacksField.GetValue(trainingDummyCombat);
        Assert.AreEqual(playerCombos.Length, trainingDummyCombos.Length, "TrainingDummy should have the same number of combo steps as Player");
        for (int i = 0; i < playerCombos.Length; i++)
        {
            Assert.AreSame(playerCombos[i], trainingDummyCombos[i], $"TrainingDummy's combo step {i} should reference the exact same AttackData asset as Player's, not a copy");
        }

        Assert.IsNull(trainingDummy.GetComponent<CharacterController>(), "TrainingDummy should have no CharacterController - it never moves");
        Assert.IsNull(trainingDummy.GetComponent<Live2DAction.Input.PlayerInputProvider>(), "TrainingDummy should have no PlayerInputProvider - it never attacks");
        Assert.IsNull(trainingDummy.GetComponent<Live2DAction.AI.EnemyAI>(), "TrainingDummy should have no EnemyAI - it never attacks");

        Vector3 startPosition = trainingDummy.transform.position;
        Health trainingDummyHealth = trainingDummy.GetComponent<Health>();
        float before = trainingDummyHealth.CurrentHealth;

        yield return null;
        yield return null;

        Assert.AreEqual(startPosition, trainingDummy.transform.position, "TrainingDummy should never move on its own");
        Assert.AreEqual(-1, trainingDummyCombat.ComboIndex, "TrainingDummy should never enter an attack (ComboIndex stays -1 with no input driving it)");

        trainingDummyHealth.ApplyDamage(new DamageInfo(10f, Vector3.zero, Vector3.forward, null));
        Assert.AreEqual(before - 10f, trainingDummyHealth.CurrentHealth, "TrainingDummy should actually take damage when hit, same as Mecha");
    }

    // Regression test for a real user report (2026-08-12, "被攻擊時血量條貼圖不會扣...血條滿
    // 格的狀態敵人直接消失了") - the isolated test above proves fillAmount the *property*
    // updates correctly, and the scene-wiring test proves Player/Enemy each have a
    // correctly-wired bar, but neither one screenshots the actual rendered pixels. Real root
    // cause (found by screenshotting the bar during actual Play mode at 50% HP and seeing an
    // unchanged full-width bar): Image.Type.Filled has NO visual effect at all without an
    // assigned Sprite (see HealthBarSetup.CreateStretchedImage's own comment) - fillAmount
    // happily updates as a plain float the whole time, masking the bug from any test that only
    // reads the property instead of the rendered geometry. This test can't screenshot pixels
    // either, but AssertHasWiredHealthBar below now checks fillImage.sprite != null, which is
    // the actual condition that was silently broken.
    [UnityTest]
    public IEnumerator PlayerBar_UpdatesWhenEnemyDamagesPlayer_InRealScene()
    {
        SceneManager.LoadScene("GreyboxTest");
        yield return null;
        yield return null;

        GameObject player = GameObject.Find("Player");
        GameObject enemy = GameObject.Find("Enemy");
        Health playerHealth = player.GetComponent<Health>();
        WorldSpaceHealthBar bar = player.GetComponentInChildren<WorldSpaceHealthBar>(true);
        var fillImage = (Image)typeof(WorldSpaceHealthBar)
            .GetField("fillImage", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(bar);

        player.transform.position = enemy.transform.position + new Vector3(1.5f, 0f, 0f);

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

        Assert.Less(playerHealth.CurrentHealth, playerHealth.MaxHealth, "Player should have taken damage from Enemy within 3s");
        Assert.AreEqual(HealthBarUtility.ComputeFillAmount(playerHealth.CurrentHealth, playerHealth.MaxHealth), fillImage.fillAmount, 0.001f,
            "Player's own health bar should reflect the damage Enemy dealt");
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
