using NUnit.Framework;
using UnityEngine;
using UnityEngine.Playables;
using Live2DAction.Cutscene;
using Object = UnityEngine.Object;

// 2026-09-01, /grill-with-docs exploration (Docs/BOSS_INTRO_EXPLORATION.md). Covers the one piece
// of BossIntroManager that IS unit-testable: the control hand-off bookkeeping. StartIntro disables
// every wired player-control Behaviour + UI root + the boss AI + the boss health bar; the
// timeline-stopped path re-enables them all. The Timeline/Cinemachine playback itself is verified
// by hand in Play (it can't be meaningfully unit-tested).
public class BossIntroManagerTests
{
    private class Dummy : MonoBehaviour { }

    private static GameObject _root;

    [TearDown]
    public void TearDown()
    {
        if (_root != null)
        {
            Object.DestroyImmediate(_root);
            _root = null;
        }
    }

    private static BossIntroManager BuildWired(out Behaviour[] controls, out GameObject[] ui,
        out Behaviour bossAi, out GameObject bossHp)
    {
        _root = new GameObject("IntroManagerTestRoot");
        var mgr = _root.AddComponent<BossIntroManager>();

        var controlHost = new GameObject("Controls");
        controlHost.transform.SetParent(_root.transform);
        controls = new Behaviour[]
        {
            controlHost.AddComponent<Dummy>(),
            controlHost.AddComponent<Dummy>(),
            null, // a hole in the array must not throw
        };

        ui = new GameObject[]
        {
            NewChild("HUD"),
            NewChild("Crosshair"),
            null,
        };

        var bossAiHost = NewChild("BossAI");
        bossAi = bossAiHost.AddComponent<Dummy>();
        bossHp = NewChild("BossHealthBar");

        mgr.EditorConfigure(_root, controls, ui, bossAi, bossHp, (PlayableDirector)null, null);
        return mgr;
    }

    private static GameObject NewChild(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(_root.transform);
        return go;
    }

    [Test]
    public void ForceStart_DisablesEveryWiredControl()
    {
        var mgr = BuildWired(out Behaviour[] controls, out GameObject[] ui, out Behaviour bossAi, out GameObject bossHp);

        mgr.ForceStartForTest();

        foreach (Behaviour b in controls)
        {
            if (b != null) Assert.IsFalse(b.enabled, $"{b.name} should be disabled during the cutscene");
        }
        foreach (GameObject u in ui)
        {
            if (u != null) Assert.IsFalse(u.activeSelf, $"{u.name} UI should be hidden during the cutscene");
        }
        Assert.IsFalse(bossAi.enabled, "boss AI should be disabled during the cutscene");
        Assert.IsFalse(bossHp.activeSelf, "boss health bar should be hidden during the cutscene");
    }

    [Test]
    public void ForceStop_RestoresEveryWiredControl()
    {
        var mgr = BuildWired(out Behaviour[] controls, out GameObject[] ui, out Behaviour bossAi, out GameObject bossHp);

        mgr.ForceStartForTest();
        mgr.ForceStopForTest();

        foreach (Behaviour b in controls)
        {
            if (b != null) Assert.IsTrue(b.enabled, $"{b.name} should be re-enabled after the cutscene");
        }
        foreach (GameObject u in ui)
        {
            if (u != null) Assert.IsTrue(u.activeSelf, $"{u.name} UI should be shown again after the cutscene");
        }
        Assert.IsTrue(bossAi.enabled, "boss AI should be re-enabled after the cutscene");
        Assert.IsTrue(bossHp.activeSelf, "boss health bar should be shown after the cutscene");
    }

    [Test]
    public void OnIntroComplete_FiresExactlyOnce_EvenOnDoubleStop()
    {
        var mgr = BuildWired(out Behaviour[] _, out GameObject[] __, out Behaviour ___, out GameObject ____);
        int calls = 0;
        mgr.EditorAddOnCompleteListener(() => calls++);

        mgr.ForceStartForTest();
        mgr.ForceStopForTest();
        mgr.ForceStopForTest();

        Assert.AreEqual(1, calls, "onIntroComplete (wired to BossStateMachine.ForceEngage) must fire once and only once");
    }

    [Test]
    public void HandOff_IsIdempotentAndNullSafe()
    {
        _root = new GameObject("BareManager");
        var mgr = _root.AddComponent<BossIntroManager>();
        // nothing wired at all
        Assert.DoesNotThrow(() => mgr.ForceStartForTest());
        Assert.DoesNotThrow(() => mgr.ForceStopForTest());
        Assert.DoesNotThrow(() => mgr.ForceStopForTest());
    }
}
