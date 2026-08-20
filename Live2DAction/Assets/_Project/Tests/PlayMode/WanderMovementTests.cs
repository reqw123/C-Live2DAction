using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Live2DAction.Characters;

public class WanderMovementTests
{
    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Expected a private field named '{fieldName}' on {target.GetType().Name}");
        field.SetValue(target, value);
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
    public IEnumerator WandersAround_AndNeverLeavesTheBoundary()
    {
        var go = new GameObject("Wanderer");
        WanderMovement wander = go.AddComponent<WanderMovement>();
        SetField(wander, "moveSpeed", 5f); // fast, so a short real-time test still covers ground
        SetField(wander, "boundaryHalfExtent", 3f);
        SetField(wander, "directionChangeIntervalSeconds", 0.3f);

        Vector3 start = go.transform.position;
        float maxDistanceFromOrigin = 0f;

        yield return RunForSeconds(2f);
        for (int i = 0; i < 30; i++)
        {
            yield return null;
            float distanceFromOrigin = new Vector2(go.transform.position.x, go.transform.position.z).magnitude;
            maxDistanceFromOrigin = Mathf.Max(maxDistanceFromOrigin, distanceFromOrigin);
        }

        Assert.Greater(Vector3.Distance(start, go.transform.position), 0.01f, "Wanderer should have actually moved from its starting position.");
        // Some slack past boundaryHalfExtent (3) is expected - it only starts steering back
        // once past the boundary, it doesn't clamp instantly - but it must not run away
        // unbounded.
        Assert.Less(maxDistanceFromOrigin, 6f, "Wanderer drifted far past its boundary instead of turning back.");

        Object.DestroyImmediate(go);
    }

    [UnityTest]
    public IEnumerator MechaInRealScene_HasWanderMovementAndStaysInsideBoundaryWalls()
    {
        SceneManager.LoadScene("GreyboxTest");
        yield return null;
        yield return null;

        GameObject mecha = GameObject.Find("Mecha");
        Assert.IsNotNull(mecha, "Mecha not found in GreyboxTest scene");

        WanderMovement wander = mecha.GetComponent<WanderMovement>();
        Assert.IsNotNull(wander, "Mecha has no WanderMovement component.");

        yield return RunForSeconds(1f);

        Assert.Less(Mathf.Abs(mecha.transform.position.x), 15f, "Mecha wandered past the +/-X boundary wall.");
        Assert.Less(Mathf.Abs(mecha.transform.position.z), 15f, "Mecha wandered past the +/-Z boundary wall.");
    }
}
