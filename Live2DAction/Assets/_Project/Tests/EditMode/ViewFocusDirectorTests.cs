using NUnit.Framework;
using UnityEngine;
using Live2DAction.CameraSystem;

public class ViewFocusDirectorTests
{
    [Test]
    public void BlendPose_AtZero_ReturnsFrom()
    {
        var from = new Pose(new Vector3(1f, 2f, 3f), Quaternion.Euler(10f, 20f, 30f));
        var to = new Pose(new Vector3(9f, 8f, 7f), Quaternion.Euler(-40f, 5f, 0f));

        Pose result = ViewFocusDirector.BlendPose(from, to, 0f);

        Assert.AreEqual(from.position, result.position);
        Assert.Less(Quaternion.Angle(from.rotation, result.rotation), 0.001f);
    }

    [Test]
    public void BlendPose_AtOne_ReturnsTo()
    {
        var from = new Pose(new Vector3(1f, 2f, 3f), Quaternion.Euler(10f, 20f, 30f));
        var to = new Pose(new Vector3(9f, 8f, 7f), Quaternion.Euler(-40f, 5f, 0f));

        Pose result = ViewFocusDirector.BlendPose(from, to, 1f);

        Assert.Less(Vector3.Distance(to.position, result.position), 0.001f);
        Assert.Less(Quaternion.Angle(to.rotation, result.rotation), 0.001f);
    }

    [Test]
    public void BlendPose_AtHalf_PositionIsMidpoint()
    {
        var from = new Pose(new Vector3(0f, 0f, 0f), Quaternion.identity);
        var to = new Pose(new Vector3(10f, 4f, -6f), Quaternion.identity);

        Pose result = ViewFocusDirector.BlendPose(from, to, 0.5f);

        Assert.AreEqual(new Vector3(5f, 2f, -3f), result.position);
    }

    [Test]
    public void BlendPose_ClampsParameterOutsideZeroToOne()
    {
        var from = new Pose(Vector3.zero, Quaternion.identity);
        var to = new Pose(new Vector3(10f, 0f, 0f), Quaternion.identity);

        Assert.AreEqual(from.position, ViewFocusDirector.BlendPose(from, to, -3f).position);
        Assert.Less(Vector3.Distance(to.position, ViewFocusDirector.BlendPose(from, to, 5f).position), 0.001f);
    }

    [Test]
    public void BlendPose_RotationEasesTowardTarget()
    {
        var from = new Pose(Vector3.zero, Quaternion.Euler(0f, 0f, 0f));
        var to = new Pose(Vector3.zero, Quaternion.Euler(0f, 90f, 0f));

        float quarter = Quaternion.Angle(from.rotation, ViewFocusDirector.BlendPose(from, to, 0.25f).rotation);
        float threeQuarter = Quaternion.Angle(from.rotation, ViewFocusDirector.BlendPose(from, to, 0.75f).rotation);

        Assert.Greater(threeQuarter, quarter);
        Assert.AreEqual(22.5f, quarter, 1.0f);
        Assert.AreEqual(67.5f, threeQuarter, 1.0f);
    }

    [Test]
    public void NewDirector_DefaultsToPlayerView()
    {
        var go = new GameObject("ViewDirector");
        var director = go.AddComponent<ViewFocusDirector>();

        Assert.AreEqual(ViewFocusDirector.ViewState.Player, director.State);
        Assert.IsFalse(director.IsFocusedOnWatcher);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void Toggle_WithNoWiring_IsANoOp()
    {
        var go = new GameObject("ViewDirector");
        var director = go.AddComponent<ViewFocusDirector>();

        director.Toggle(); // cameras / watcherViewpoint all null - must not throw or change state

        Assert.AreEqual(ViewFocusDirector.ViewState.Player, director.State);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void NewWatcherViewConfig_HasNoSavedView()
    {
        var config = ScriptableObject.CreateInstance<WatcherViewConfig>();

        Assert.IsFalse(config.hasSavedView, "A fresh config must not claim a saved view - the director should fall back to the authored Viewpoint.");

        Object.DestroyImmediate(config);
    }

    [Test]
    public void Toggle_WithConfigButNoCameras_IsStillANoOp()
    {
        var go = new GameObject("ViewDirector");
        var director = go.AddComponent<ViewFocusDirector>();
        var config = ScriptableObject.CreateInstance<WatcherViewConfig>();
        config.hasSavedView = true;
        config.rootPosition = new Vector3(5f, 40f, -2f);
        typeof(ViewFocusDirector)
            .GetField("viewConfig", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .SetValue(director, config);

        director.Toggle(); // no cameras wired - a saved config must not make it try to operate

        Assert.AreEqual(ViewFocusDirector.ViewState.Player, director.State);

        Object.DestroyImmediate(config);
        Object.DestroyImmediate(go);
    }
}
