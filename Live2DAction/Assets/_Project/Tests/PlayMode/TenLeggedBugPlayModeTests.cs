using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Live2DAction.AI;
using Live2DAction.Core;
using Object = UnityEngine.Object;

// End-to-end behaviour of the ten-legged bug enemy against a stand-in player:
//   * the horn deals damage ONLY on a live strike frame, exactly once, never per-physics-frame
//     just because the player is standing in the trigger (spec section 3 / 5);
//   * a player behind the bug (outside the ~30-degree cone) takes no hit and the bug turns to face;
//   * HP 0 shuts down the controller and every hitbox (spec section 5).
// Built in code (no scene dependency) the same way EnemyAttacksPlayerTests does.
public class TenLeggedBugPlayModeTests
{
    private static void SetField(object target, string name, object value)
    {
        FieldInfo f = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(f, $"missing private field '{name}' on {target.GetType().Name}");
        f.SetValue(target, value);
    }

    private static T GetField<T>(object target, string name)
    {
        FieldInfo f = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        return (T)f.GetValue(target);
    }

    [SetUp]
    public void SetUp()
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            Object.DestroyImmediate(root);
        }
        // A floor so the CharacterController is grounded.
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.localScale = new Vector3(40f, 1f, 40f);
        floor.transform.position = new Vector3(0f, -0.5f, 0f);
    }

    private static GameObject CreatePlayer(Vector3 pos, out Health health)
    {
        var player = new GameObject("Player");
        player.transform.position = pos;
        var cap = player.AddComponent<CapsuleCollider>();
        cap.height = 1.8f;
        cap.center = new Vector3(0f, 0.9f, 0f);
        health = player.AddComponent<Health>();
        return player;
    }

    // A minimal but real bug rig: root (CC + Health + controller) -> body -> horn -> HornHitbox,
    // plus four leg-root bones so the gait code has something to drive.
    private static GameObject CreateBug(Vector3 pos, Transform target,
        out TenLeggedBugController controller, out TenLeggedBugHornHitbox hornHitbox, out Health health)
    {
        var root = new GameObject("Bug");
        root.transform.position = pos;
        var cc = root.AddComponent<CharacterController>();
        cc.height = 1.7f;
        cc.radius = 0.4f;
        cc.center = new Vector3(0f, 0.85f, 0f);
        cc.minMoveDistance = 0f;
        cc.stepOffset = 0f;
        health = root.AddComponent<Health>();

        var body = new GameObject("Body"); body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        var horn = new GameObject("Horn"); horn.transform.SetParent(body.transform, false);
        horn.transform.localPosition = new Vector3(0f, 0.3f, 0.4f);

        var hbGo = new GameObject("HornHitbox"); hbGo.transform.SetParent(horn.transform, false);
        hbGo.transform.localPosition = new Vector3(0f, 0f, 0.3f);
        var col = hbGo.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.enabled = false;
        col.size = new Vector3(0.6f, 0.6f, 1.2f);
        var rb = hbGo.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        hornHitbox = hbGo.AddComponent<TenLeggedBugHornHitbox>();
        SetField(hornHitbox, "damage", 10f);

        var legRoots = new List<Transform>();
        for (int i = 0; i < 4; i++)
        {
            var hip = new GameObject("Hip" + i); hip.transform.SetParent(body.transform, false);
            var knee = new GameObject("Knee" + i); knee.transform.SetParent(hip.transform, false);
            legRoots.Add(hip.transform);
        }

        controller = root.AddComponent<TenLeggedBugController>();
        SetField(controller, "target", target);
        SetField(controller, "bodyRootBone", body.transform);
        SetField(controller, "hornBone", horn.transform);
        SetField(controller, "hornHitbox", hornHitbox);
        SetField(controller, "legRootBones", legRoots);
        SetField(controller, "detectionRange", 12f);
        SetField(controller, "loseTargetRange", 16f);
        SetField(controller, "attackRange", 2.5f);
        SetField(controller, "attackConeAngleDegrees", 30f);
        SetField(controller, "attackCycleSeconds", 1f);
        SetField(controller, "attacksBeforeStagger", 3);
        SetField(controller, "rotationSpeedDegrees", 720f);
        SetField(controller, "gravity", -10f);
        return root;
    }

    // ---------------------------------------------------------------------------------------

    [UnityTest]
    public IEnumerator HornStrike_DamagesPlayerExactlyOnce_NotEveryFrameInTrigger()
    {
        GameObject player = CreatePlayer(new Vector3(0f, 0f, 1.6f), out Health playerHealth);
        GameObject bug = CreateBug(Vector3.zero, player.transform,
            out TenLeggedBugController controller, out TenLeggedBugHornHitbox hornHitbox, out _);
        bug.transform.rotation = Quaternion.identity; // already facing +Z, straight at the player

        // Run ~2 full attack cycles' worth of frames.
        float elapsed = 0f;
        bool sawAttackState = false;
        while (elapsed < 2.3f)
        {
            if (controller.State == TenLeggedBugController.BugState.Attack) sawAttackState = true;
            elapsed += Time.deltaTime;
            yield return null;
        }

        Assert.IsTrue(sawAttackState, "bug should have entered the Attack state");
        float lost = playerHealth.MaxHealth - playerHealth.CurrentHealth;
        // Two cycles => at most two clean strikes => 20. Crucially NOT tens-per-frame.
        Assert.Greater(lost, 0f, "a live horn strike should have connected");
        Assert.LessOrEqual(lost, 20f + 0.01f,
            "damage must be per-strike, not per-physics-frame the player sits in the trigger");
        Assert.AreEqual(0f, lost % 10f, 0.001f, "each strike is a flat 10");

        Object.Destroy(player);
        Object.Destroy(bug);
    }

    [UnityTest]
    public IEnumerator PlayerBehindBug_TakesNoDamage_AndBugTurnsToFace()
    {
        // Player directly behind the bug (bug faces +Z, player at -Z) - well outside the 30 cone.
        GameObject player = CreatePlayer(new Vector3(0f, 0f, -1.8f), out Health playerHealth);
        GameObject bug = CreateBug(Vector3.zero, player.transform,
            out TenLeggedBugController controller, out _, out _);
        bug.transform.rotation = Quaternion.identity;

        float startYawToPlayer = Vector3.Angle(bug.transform.forward, (player.transform.position - bug.transform.position));

        for (float t = 0f; t < 0.6f; t += Time.deltaTime)
        {
            yield return null;
        }

        Assert.AreEqual(playerHealth.MaxHealth, playerHealth.CurrentHealth,
            "the bug must not stab a player outside its frontal cone");
        float nowYawToPlayer = Vector3.Angle(bug.transform.forward, (player.transform.position - bug.transform.position));
        Assert.Less(nowYawToPlayer, startYawToPlayer - 20f, "the bug should be turning to face the player");

        Object.Destroy(player);
        Object.Destroy(bug);
    }

    [UnityTest]
    public IEnumerator HpZero_DisablesControllerAndHornHitbox()
    {
        GameObject player = CreatePlayer(new Vector3(0f, 0f, 1.6f), out _);
        GameObject bug = CreateBug(Vector3.zero, player.transform,
            out TenLeggedBugController controller, out TenLeggedBugHornHitbox hornHitbox, out Health bugHealth);

        yield return null; // let it spin up / possibly enter Attack

        bugHealth.ApplyDamage(new DamageInfo(999f, bug.transform.position, Vector3.forward, player));
        yield return null;
        yield return new WaitForFixedUpdate();

        Assert.IsTrue(GetField<bool>(controller, "_dead"), "controller should register the death");
        Assert.IsFalse(bug.GetComponent<CharacterController>().enabled, "pathing/gravity stops at HP 0");
        Assert.IsFalse(hornHitbox.IsActive, "the horn hitbox must be off once dead");

        // player takes no further damage while the corpse lies there
        yield return null;
        Object.Destroy(player);
        if (bug != null) Object.Destroy(bug);
    }

    [UnityTest]
    public IEnumerator KilledBug_RevivesWithFullHealthAndResumesPatrolling()
    {
        GameObject player = CreatePlayer(new Vector3(0f, 0f, 30f), out _); // far away - it will patrol
        GameObject bug = CreateBug(Vector3.zero, player.transform,
            out TenLeggedBugController controller, out _, out Health bugHealth);
        // Shorten the death window so the test doesn't sit for 5 real seconds.
        SetField(controller, "respawnDelaySeconds", 0.6f);
        SetField(controller, "flipOverSeconds", 0.15f);
        SetField(controller, "getUpSeconds", 0.15f);

        yield return null;
        bugHealth.ApplyDamage(new DamageInfo(999f, bug.transform.position, Vector3.forward, player));
        yield return null;
        Assert.IsTrue(GetField<bool>(controller, "_dead"), "dead right after HP hits 0");
        Assert.IsTrue(bugHealth.IsDead);

        // Wait out the revive window plus a margin.
        float waited = 0f;
        while (waited < 1.5f && GetField<bool>(controller, "_dead"))
        {
            waited += Time.deltaTime;
            yield return null;
        }

        Assert.IsFalse(GetField<bool>(controller, "_dead"), "bug should have revived");
        Assert.IsFalse(bugHealth.IsDead, "Health un-dies on revive");
        Assert.AreEqual(bugHealth.MaxHealth, bugHealth.CurrentHealth, "revives at full HP");
        Assert.IsTrue(bug.GetComponent<CharacterController>().enabled, "movement re-enabled on revive");
        Assert.AreEqual(TenLeggedBugController.BugState.Patrol, controller.State, "resumes from Patrol");

        Object.Destroy(player);
        Object.Destroy(bug);
    }

    [UnityTest]
    public IEnumerator SpectatorToggle_SwapsViewOnlyAndRestoresTheOriginalCamera()
    {
        var mainCamGo = new GameObject("MainCam");
        mainCamGo.AddComponent<Camera>();
        var specCamGo = new GameObject("SpecCam");
        specCamGo.AddComponent<Camera>();
        specCamGo.SetActive(false);

        var togGo = new GameObject("Toggle");
        var toggle = togGo.AddComponent<Live2DAction.CameraSystem.SpectatorCameraToggle>();
        SetField(toggle, "spectatorCamera", specCamGo);
        yield return null;

        Assert.IsFalse(toggle.IsSpectating);
        Assert.IsTrue(mainCamGo.activeSelf);
        Assert.IsFalse(specCamGo.activeSelf);

        toggle.Toggle();
        yield return null;
        Assert.IsTrue(toggle.IsSpectating, "now spectating");
        Assert.IsFalse(mainCamGo.activeSelf, "the previous camera is disabled");
        Assert.IsTrue(specCamGo.activeSelf, "the spectator camera is live");

        toggle.Toggle();
        yield return null;
        Assert.IsFalse(toggle.IsSpectating);
        Assert.IsTrue(mainCamGo.activeSelf, "the original camera comes back");
        Assert.IsFalse(specCamGo.activeSelf);

        Object.Destroy(mainCamGo);
        Object.Destroy(specCamGo);
        Object.Destroy(togGo);
    }

    [UnityTest]
    public IEnumerator FarPlayer_StaysPatrolling_ThenChasesWhenClose()
    {
        GameObject player = CreatePlayer(new Vector3(0f, 0f, 30f), out _); // way outside detectionRange
        GameObject bug = CreateBug(Vector3.zero, player.transform,
            out TenLeggedBugController controller, out _, out _);

        yield return null;
        yield return null;
        Assert.AreEqual(TenLeggedBugController.BugState.Patrol, controller.State,
            "an unaware bug patrols");

        player.transform.position = new Vector3(0f, 0f, 4f); // now inside detectionRange
        for (float t = 0f; t < 0.3f; t += Time.deltaTime)
        {
            yield return null;
        }
        Assert.IsTrue(controller.State == TenLeggedBugController.BugState.Chase
                      || controller.State == TenLeggedBugController.BugState.Attack,
            "a bug that spots the player pursues it");

        Object.Destroy(player);
        Object.Destroy(bug);
    }
}
