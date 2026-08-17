using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Live2DAction.Input;

// Added after a real user report ("角色移動到一半畫面卡住" - the screen freezes partway
// through moving the character). Loads the real GreyboxTest scene (Player, TrainingDummy
// chasing, Player2, everything as shipped, not a synthetic fixture) and holds forward movement
// input for several seconds while recording the wall-clock duration of every single Update
// tick, to catch the multi-hundred-millisecond-or-worse stalls a human would perceive as a
// freeze. At the time this was added, the worst observed frame was under 4ms - no stall was
// reproduced this way, which points the freeze towards something outside this per-frame
// gameplay loop (see Docs/KNOWN_ISSUES.md for the full investigation) - but this stays in the
// suite as a permanent regression guard in case a future change does introduce a real stall.
public class MovementFrameTimingTests
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

    [UnityTest]
    public IEnumerator HoldingForwardInputFor5Seconds_NeverStallsAFrameByMoreThan300ms()
    {
        SceneManager.LoadScene("GreyboxTest");
        yield return null;
        yield return null;

        GameObject player = GameObject.Find("Player");
        Assert.IsNotNull(player, "Player not found in GreyboxTest scene");

        StubInputBehaviour stub = player.AddComponent<StubInputBehaviour>();
        var movement = player.GetComponent<Live2DAction.Characters.CharacterMovement>();
        SetField(movement, "inputSource", stub);
        stub.MoveInput = new Vector2(0f, 1f); // hold forward

        float worstFrameSeconds = 0f;
        float worstFrameAtElapsed = 0f;
        float start = Time.realtimeSinceStartup;
        float lastTick = start;

        while (Time.realtimeSinceStartup - start < 5f)
        {
            yield return null;
            float now = Time.realtimeSinceStartup;
            float frameSeconds = now - lastTick;
            if (frameSeconds > worstFrameSeconds)
            {
                worstFrameSeconds = frameSeconds;
                worstFrameAtElapsed = now - start;
            }
            lastTick = now;
        }

        Debug.Log($"Worst single-frame wall-clock duration over 5s of held forward input: {worstFrameSeconds * 1000f:F1}ms (at t={worstFrameAtElapsed:F2}s)");
        Assert.Less(worstFrameSeconds, 0.3f,
            $"A frame took {worstFrameSeconds * 1000f:F1}ms while holding forward movement input - this is the kind of stall that would read as \"the screen freezes while moving\".");
    }
}
