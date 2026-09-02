using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Live2DAction.Characters;

public class CatProceduralWalkTests
{
    [Test]
    public void CaptureRest_WithNoLegs_DoesNotThrow()
    {
        var go = new GameObject("Cat");
        var walk = go.AddComponent<CatProceduralWalk>();

        Assert.DoesNotThrow(() => walk.CaptureRest());

        Object.DestroyImmediate(go);
    }

    [Test]
    public void LateUpdate_WithNoLegs_IsANoOp()
    {
        var go = new GameObject("Cat");
        var walk = go.AddComponent<CatProceduralWalk>();
        walk.CaptureRest();

        MethodInfo lateUpdate = typeof(CatProceduralWalk)
            .GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.DoesNotThrow(() => lateUpdate.Invoke(walk, null));

        Object.DestroyImmediate(go);
    }

    // 2026-08-29, explicit user request ("讓貓就有飛行和衝刺功能 參考player") - while flying, the
    // gait must ease OFF regardless of how fast the cat is cruising horizontally, so the legs
    // stop pumping in mid-air (see CatProceduralWalk.LateUpdate's own comment).
    [Test]
    public void ComputeGaitTarget_WhileFlying_IsZeroEvenAtFullSpeed()
    {
        Assert.AreEqual(0f, CatProceduralWalk.ComputeGaitTarget(flying: true, speedNorm: 1f));
    }

    [Test]
    public void ComputeGaitTarget_OnGround_TracksWhetherTheCatIsMoving()
    {
        Assert.AreEqual(1f, CatProceduralWalk.ComputeGaitTarget(flying: false, speedNorm: 1f));
        Assert.AreEqual(0f, CatProceduralWalk.ComputeGaitTarget(flying: false, speedNorm: 0f));
    }

    // 2026-08-29, cat combat design (Docs/CAT_COMBAT_DESIGN.md 3.8) - CatAttackPose calls this
    // every frame; it must clamp to 0..1 so a stray value can't invert or over-drive the gait.
    [Test]
    public void SetAttackSuppression_ClampsToZeroOne()
    {
        var go = new GameObject("Cat");
        var walk = go.AddComponent<CatProceduralWalk>();
        FieldInfo target = typeof(CatProceduralWalk)
            .GetField("_attackSuppressionTarget", BindingFlags.Instance | BindingFlags.NonPublic);

        walk.SetAttackSuppression(5f);
        Assert.AreEqual(1f, (float)target.GetValue(walk));
        walk.SetAttackSuppression(-3f);
        Assert.AreEqual(0f, (float)target.GetValue(walk));

        Object.DestroyImmediate(go);
    }

    [Test]
    public void CaptureRest_SnapshotsEachLegBonesLocalRotation()
    {
        var root = new GameObject("Cat");
        var hipParent = new GameObject("HipParent");
        hipParent.transform.SetParent(root.transform);
        var hip = new GameObject("Hip");
        hip.transform.SetParent(hipParent.transform);
        hip.transform.localRotation = Quaternion.Euler(11f, 22f, 33f);
        var knee = new GameObject("Knee");
        knee.transform.SetParent(hip.transform);
        knee.transform.localRotation = Quaternion.Euler(-5f, 0f, 40f);

        var walk = root.AddComponent<CatProceduralWalk>();
        var legs = new CatProceduralWalk.Leg[]
        {
            new CatProceduralWalk.Leg { swingBone = hip.transform, bendBone = knee.transform, phaseOffset = 0f, bendSign = 1f }
        };
        typeof(CatProceduralWalk).GetField("legs", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(walk, legs);

        walk.CaptureRest();

        var swingRest = (Quaternion[])typeof(CatProceduralWalk)
            .GetField("_swingRest", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(walk);
        Assert.AreEqual(1, swingRest.Length);
        Assert.Less(Quaternion.Angle(swingRest[0], Quaternion.Euler(11f, 22f, 33f)), 0.01f);

        Object.DestroyImmediate(root);
    }
}
