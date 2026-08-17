using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Live2DAction.Input;

// Regression test for a real user report ("希望柱子、Player1/2 都做碰撞阻擋，不會穿透") -
// loads the real GreyboxTest scene and drives the player straight into TrainingDummy and into
// Player2, confirming their colliders (CharacterController vs CharacterController, and
// CharacterController vs Player2's CapsuleCollider) actually keep them from fully overlapping.
// Player2 previously had no Collider at all and was walked straight through.
public class CharacterCollisionBlockingTests
{
    private class StubInputBehaviour : MonoBehaviour, IInputCommand
    {
        public Vector2 MoveInput { get; set; }
        public bool AttackPressed { get; set; }
        public bool DodgePressed { get; set; }
        public bool LockOnPressed { get; set; }
        public bool JumpPressed { get; set; }
        public bool UltimatePressed { get; set; }
        public bool FlyPressed { get; set; }
        public bool FlyDescendPressed { get; set; }
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Expected a private field named '{fieldName}' on {target.GetType().Name}");
        field.SetValue(target, value);
    }

    // Same rationale as CharacterMovementTests.MoveForSeconds: headless batchmode's per-frame
    // Time.deltaTime can be tiny, so a fixed frame count can't be trusted to cover a target
    // amount of simulated time.
    private static IEnumerator RunForSeconds(float seconds)
    {
        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < seconds)
        {
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator WalkingIntoTrainingDummy_DoesNotFullyOverlap()
    {
        SceneManager.LoadScene("GreyboxTest");
        yield return null;
        yield return null;

        GameObject player = GameObject.Find("Player");
        GameObject dummy = GameObject.Find("TrainingDummy");
        Assert.IsNotNull(player, "Player not found in GreyboxTest scene");
        if (dummy == null)
        {
            // 2026-08-12: the user deliberately deleted TrainingDummy from GreyboxTest (see
            // Docs/KNOWN_ISSUES.md's "GreyboxTest 現況備忘") - not missing/broken, an
            // intentional scene state. Skip rather than hard-fail so this doesn't read as a
            // regression; re-add TrainingDummy (Tools/Live2DAction/Replace Enemy Visual With
            // Humanoid Placeholder, after rebuilding it) to get this test exercised again.
            Assert.Ignore("TrainingDummy is not currently in GreyboxTest (deliberately removed by the user, see KNOWN_ISSUES.md) - skipping.");
        }

        // Start right in front of the dummy, already close, and push forward into it for a
        // while - if the two CharacterControllers didn't block each other, the player would
        // walk straight through to (roughly) the dummy's own position.
        player.transform.position = dummy.transform.position - new Vector3(0f, 0f, 1.5f);

        StubInputBehaviour stub = player.AddComponent<StubInputBehaviour>();
        var movement = player.GetComponent<Live2DAction.Characters.CharacterMovement>();
        SetField(movement, "inputSource", stub);
        stub.MoveInput = new Vector2(0f, 1f); // hold forward, towards the dummy

        yield return RunForSeconds(1f);

        float distance = Vector3.Distance(player.transform.position, dummy.transform.position);
        Assert.Greater(distance, 0.7f,
            $"Player ended up only {distance} units from TrainingDummy after walking straight at it for 1s - the CharacterControllers aren't blocking each other.");
    }

    [UnityTest]
    public IEnumerator WalkingIntoPlayer2_DoesNotPassThrough()
    {
        SceneManager.LoadScene("GreyboxTest");
        yield return null;
        yield return null;

        GameObject player = GameObject.Find("Player");
        GameObject player2 = GameObject.Find("Player2");
        Assert.IsNotNull(player, "Player not found in GreyboxTest scene");
        Assert.IsNotNull(player2, "Player2 not found in GreyboxTest scene");
        Assert.IsNotNull(player2.GetComponent<Collider>(), "Player2 has no Collider - nothing would stop the player walking through it.");

        Vector3 towardPlayer2 = (player2.transform.position - player.transform.position);
        towardPlayer2.y = 0f;
        towardPlayer2.Normalize();

        // Start close to Player2, already facing roughly its direction, then push straight at
        // it - if there were no collider, the player would walk straight through to (roughly)
        // Player2's own position.
        player.transform.position = player2.transform.position - towardPlayer2 * 1.5f + Vector3.up * 0.5f;

        StubInputBehaviour stub = player.AddComponent<StubInputBehaviour>();
        var movement = player.GetComponent<Live2DAction.Characters.CharacterMovement>();
        SetField(movement, "inputSource", stub);
        // World-space input matching the direction towards Player2, bypassing camera-relative
        // conversion (CameraRelativeDirection at yaw 0 maps input.y to +Z / input.x to +X,
        // which towardPlayer2's components already are in this fixed-world-axis camera setup).
        stub.MoveInput = new Vector2(towardPlayer2.x, towardPlayer2.z);

        yield return RunForSeconds(1f);

        float distanceXZ = Vector2.Distance(
            new Vector2(player.transform.position.x, player.transform.position.z),
            new Vector2(player2.transform.position.x, player2.transform.position.z));
        // 0.7, not the two colliders' exact combined radius (~0.8): pushback settles right at
        // that boundary, and frame-timing variance (see CharacterMovementTests on headless
        // batchmode's variable per-frame integration) can land a hair under 0.8 even though
        // the collider is genuinely blocking (observed 0.798 - collision failing outright
        // looks like ~0.1-0.3, not a few mm short of the exact radius).
        Assert.Greater(distanceXZ, 0.7f,
            $"Player ended up only {distanceXZ} units (XZ) from Player2 after walking straight at it for 1s - Player2's collider isn't blocking the player.");
    }

    // Regression test for a real user report (2026-08-12, "一旦我很靠近敵人時，角色1就突然消失
    // 了 畫面定格") - root cause was CharacterController.stepOffset's default (0.3) letting
    // Player climb up the rounded top of Player4's own CharacterController when pushed
    // directly into it (confirmed by a throwaway diagnostic test before this one was written:
    // Y drifted from 0.58 to 1.66 within about a second of continued contact, then got stuck
    // oscillating at the top - reading as "disappeared" once the collision-avoidance-free
    // camera likely ended up clipped into Player4's head geometry from up there). Fixed by
    // zeroing stepOffset on every character's CharacterController (see
    // GreyboxSceneBuilder.CreatePlayer's stepOffset comment).
    [UnityTest]
    public IEnumerator WalkingIntoPlayer4_DoesNotClimbOnTop()
    {
        SceneManager.LoadScene("GreyboxTest");
        yield return null;
        yield return null;

        GameObject player = GameObject.Find("Player");
        GameObject player4 = GameObject.Find("Player4");
        Assert.IsNotNull(player, "Player not found in GreyboxTest scene");
        Assert.IsNotNull(player4, "Player4 not found in GreyboxTest scene");

        Vector3 towardPlayer4 = player4.transform.position - player.transform.position;
        towardPlayer4.y = 0f;
        towardPlayer4.Normalize();

        // Same Y as Player4 (both share the same CharacterController height/center
        // convention - see Player4EnemyAISetup.cs) rather than WalkingIntoPlayer2's own
        // "+ Vector3.up * 0.5f" offset (tuned for Player2's different collider setup) - an
        // extra vertical offset here would just measure gravity pulling back down to the
        // correct resting height, not the climbing bug this test targets.
        player.transform.position = player4.transform.position - towardPlayer4 * 1.5f;
        float startingY = player.transform.position.y;

        StubInputBehaviour stub = player.AddComponent<StubInputBehaviour>();
        var movement = player.GetComponent<Live2DAction.Characters.CharacterMovement>();
        SetField(movement, "inputSource", stub);
        stub.MoveInput = new Vector2(towardPlayer4.x, towardPlayer4.z);

        yield return RunForSeconds(2f);

        float yDrift = Mathf.Abs(player.transform.position.y - startingY);
        Assert.Less(yDrift, 0.2f,
            $"Player's Y drifted {yDrift} units (from {startingY} to {player.transform.position.y}) after walking straight into Player4 for 2s - it's climbing up onto Player4 instead of being blocked horizontally.");
    }

    // Regression test for a real user report (2026-08-16, "跳躍有機會卡在敵人頭上，需要自行下來") -
    // a different way onto Player4's head than the walk-in case above (which stepOffset=0
    // already fixed): a jump's ballistic arc can land the player directly on top instead of
    // climbing there by walking. See GroundSlopeUtility's own comment for the root cause
    // (CharacterController.isGrounded reads true there regardless of how steep/round the
    // surface actually is - slopeLimit only blocks walking UP onto a steep slope, it does
    // nothing once already resting on one) and CharacterMovement's TryGetGroundNormal/slide
    // fix.
    [UnityTest]
    public IEnumerator LandingOnTopOfPlayer4_SlidesOffWithoutAnyInput()
    {
        SceneManager.LoadScene("GreyboxTest");
        yield return null;
        yield return null;

        GameObject player = GameObject.Find("Player");
        GameObject player4 = GameObject.Find("Player4");
        Assert.IsNotNull(player, "Player not found in GreyboxTest scene");
        Assert.IsNotNull(player4, "Player4 not found in GreyboxTest scene");

        var playerController = player.GetComponent<CharacterController>();
        var player4Controller = player4.GetComponent<CharacterController>();
        Assert.IsNotNull(playerController, "Player has no CharacterController");
        Assert.IsNotNull(player4Controller, "Player4 has no CharacterController");

        // Positions Player resting right on top of Player4's capsule, matching what a jump
        // landing there would look like the instant it settles - deliberately offset slightly
        // off-center (not dead on the apex), both because that's how a real jump would
        // actually land and because GroundSlopeUtility.ComputeSlideDirection's own comment
        // notes the exact apex is a genuinely undefined/unstable case, not this fix's target.
        float player4TopWorldY = player4.transform.position.y + player4Controller.center.y + player4Controller.height / 2f;
        float playerHalfHeight = playerController.center.y + playerController.height / 2f;
        float startingY = player4TopWorldY + playerHalfHeight + 0.05f;
        player.transform.position = new Vector3(
            player4.transform.position.x + 0.15f,
            startingY,
            player4.transform.position.z + 0.1f);

        // Deliberately no input at all, ever - the bug report is specifically that the player
        // has to manually walk off; this test only passes if it resolves entirely on its own.
        player.AddComponent<StubInputBehaviour>();
        var movement = player.GetComponent<Live2DAction.Characters.CharacterMovement>();
        SetField(movement, "inputSource", player.GetComponent<StubInputBehaviour>());

        yield return RunForSeconds(3f);

        // Checks Player's own Y height, not distance to Player4 - Player4 has its own EnemyAI
        // that actively chases Player (confirmed live: detectionRange 8, moveSpeed 2), so
        // Player4 closing the gap on its own would make an XZ-distance assertion pass/fail for
        // the wrong reason regardless of whether the slide fix actually works. A player that's
        // still resting elevated on top of Player4 stays near startingY; one that's slid/fallen
        // off drops back down toward normal ground-level height.
        float finalY = player.transform.position.y;
        Assert.Less(finalY, startingY - 0.3f,
            $"Player's Y only dropped from {startingY} to {finalY} after 3s with no input at all - it's still resting elevated on top of Player4 instead of sliding/falling off.");
    }
}
