using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Live2DAction.CameraSystem;

public class CameraPossessionSwitcherTests
{
    [Test]
    public void Other_FlipsBetweenPlayerAndCat()
    {
        Assert.AreEqual(CameraPossessionSwitcher.Possessed.Cat,
            CameraPossessionSwitcher.Other(CameraPossessionSwitcher.Possessed.Player));
        Assert.AreEqual(CameraPossessionSwitcher.Possessed.Player,
            CameraPossessionSwitcher.Other(CameraPossessionSwitcher.Possessed.Cat));
    }

    [Test]
    public void NewSwitcher_DefaultsToPlayer()
    {
        var go = new GameObject("CameraPossession");
        var switcher = go.AddComponent<CameraPossessionSwitcher>();

        Assert.AreEqual(CameraPossessionSwitcher.Possessed.Player, switcher.Current);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void Toggle_WithNoWiring_FlipsStateWithoutThrowing()
    {
        var go = new GameObject("CameraPossession");
        var switcher = go.AddComponent<CameraPossessionSwitcher>();

        switcher.Toggle(); // cameras / control arrays all null - must not throw
        Assert.AreEqual(CameraPossessionSwitcher.Possessed.Cat, switcher.Current);

        switcher.Toggle();
        Assert.AreEqual(CameraPossessionSwitcher.Possessed.Player, switcher.Current);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void FocusCatThenFocusPlayer_TogglesCameraGameObjectsAndControlComponents()
    {
        var playerCam = new GameObject("PlayerCam") { };
        var catCam = new GameObject("CatCam") { };
        catCam.SetActive(false);

        var playerControlGo = new GameObject("PlayerControl");
        var playerControl = playerControlGo.AddComponent<DummyBehaviour>();
        var catControlGo = new GameObject("CatControl");
        var catControl = catControlGo.AddComponent<DummyBehaviour>();
        catControl.enabled = false;

        var go = new GameObject("CameraPossession");
        var switcher = go.AddComponent<CameraPossessionSwitcher>();
        const BindingFlags priv = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(CameraPossessionSwitcher).GetField("playerCamera", priv).SetValue(switcher, playerCam);
        typeof(CameraPossessionSwitcher).GetField("catCamera", priv).SetValue(switcher, catCam);
        typeof(CameraPossessionSwitcher).GetField("playerControl", priv).SetValue(switcher, new Behaviour[] { playerControl });
        typeof(CameraPossessionSwitcher).GetField("catControl", priv).SetValue(switcher, new Behaviour[] { catControl });

        switcher.FocusCat();
        Assert.IsFalse(playerCam.activeSelf, "player camera off while cat is possessed");
        Assert.IsTrue(catCam.activeSelf, "cat camera on while cat is possessed");
        Assert.IsFalse(playerControl.enabled, "player control frozen while cat is possessed");
        Assert.IsTrue(catControl.enabled, "cat control active while cat is possessed");

        switcher.FocusPlayer();
        Assert.IsTrue(playerCam.activeSelf);
        Assert.IsFalse(catCam.activeSelf);
        Assert.IsTrue(playerControl.enabled);
        Assert.IsFalse(catControl.enabled);

        Object.DestroyImmediate(go);
        Object.DestroyImmediate(playerCam);
        Object.DestroyImmediate(catCam);
        Object.DestroyImmediate(playerControlGo);
        Object.DestroyImmediate(catControlGo);
    }

    private class DummyBehaviour : MonoBehaviour { }
}
