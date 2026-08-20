using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Regression test for a real report ("每次 Play 都會浮空掉落") whose actual cause turned out
// to be Player's Transform having been accidentally dragged to an ungrounded position
// (Y=-0.5, half-embedded below Ground) in the Editor, not a code bug - but a stray Editor drag
// like that is exactly the kind of thing that's easy to do again by accident and easy to not
// notice (see Docs/KNOWN_ISSUES.md). This guards against it happening unnoticed: loads the
// real scene with real gravity (most other movement tests zero it out to isolate horizontal
// movement) and confirms the player never free-falls with no input.
public class PlayerSpawnGroundingTests
{
    [UnityTest]
    public IEnumerator PlayerStaysNearSpawnHeight_WithNoInputAndRealGravity()
    {
        SceneManager.LoadScene("GreyboxTest");
        yield return null;
        yield return null;

        GameObject player = GameObject.Find("Player");
        Assert.IsNotNull(player, "Player not found in GreyboxTest scene");
        float startY = player.transform.position.y;

        float start = Time.realtimeSinceStartup;
        float minY = startY;
        while (Time.realtimeSinceStartup - start < 1f)
        {
            yield return null;
            minY = Mathf.Min(minY, player.transform.position.y);
        }

        Assert.Greater(minY, startY - 0.5f,
            $"Player fell from Y={startY} to Y={minY} within 1 second of Play starting with zero input - it is not stably grounded at spawn.");
    }
}
