using NUnit.Framework;
using UnityEngine;
using Live2DAction.Combat;

// 2026-09-01, user request ("我不要攻擊的碰撞顯示 改成防禦"). The guard wedge mesh must span exactly
// PlayerGuard.GuardArcDegrees, centred on local +Z, apex at the origin - so what you see is what
// PlayerGuardUtility.IsFrontalBlock actually tests.
public class PlayerGuardVisualizerTests
{
    [Test]
    public void BuildFanVertices_ApexAtOrigin_RimAtRange()
    {
        Vector3[] v = PlayerGuardVisualizer.BuildFanVertices(150f, 2f, 24);

        Assert.AreEqual(26, v.Length); // apex + (seg + 1) rim points
        Assert.That(v[0], Is.EqualTo(Vector3.zero));
        for (int i = 1; i < v.Length; i++)
        {
            Assert.That(new Vector2(v[i].x, v[i].z).magnitude, Is.EqualTo(2f).Within(1e-3f), $"rim {i} radius");
            Assert.That(v[i].y, Is.EqualTo(0f).Within(1e-6f), "flat in XZ");
        }
    }

    [Test]
    public void BuildFanVertices_EdgesSitAtPlusMinusHalfArc()
    {
        Vector3[] v = PlayerGuardVisualizer.BuildFanVertices(150f, 1f, 24);
        Vector3 first = v[1];
        Vector3 last = v[v.Length - 1];

        // first edge is -75 deg from +Z, last is +75 deg
        Assert.That(Vector3.SignedAngle(Vector3.forward, first, Vector3.up), Is.EqualTo(-75f).Within(0.05f));
        Assert.That(Vector3.SignedAngle(Vector3.forward, last, Vector3.up), Is.EqualTo(75f).Within(0.05f));
        // total span == arc
        Assert.That(Vector3.Angle(first, last), Is.EqualTo(150f).Within(0.05f));
    }

    [Test]
    public void BuildFanVertices_ClampsSegmentsAndRange()
    {
        Assert.AreEqual(6 + 2, PlayerGuardVisualizer.BuildFanVertices(90f, 1f, 2).Length);   // seg clamped up to 6
        Assert.AreEqual(96 + 2, PlayerGuardVisualizer.BuildFanVertices(90f, 1f, 999).Length); // clamped down to 96
        Vector3[] negRange = PlayerGuardVisualizer.BuildFanVertices(90f, -5f, 12);
        Assert.That(new Vector2(negRange[1].x, negRange[1].z).magnitude, Is.EqualTo(0f).Within(1e-6f));
    }
}
