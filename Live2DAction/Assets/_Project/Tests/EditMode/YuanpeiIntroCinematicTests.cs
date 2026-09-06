using NUnit.Framework;
using Live2DAction.AI.Boss.Yuanpei;

// 續 180 - the pure beat-timeline maths for the yuanpei_LogoSky intro cutscene.
public class YuanpeiIntroCinematicTests
{
    static YuanpeiIntroTimeline T => new YuanpeiIntroTimeline
    {
        SkyWipe = 2f, PushToBoss = 1f, BossRise = 2f, PlayerLeap = 2f, Clash = 2f, Settle = 1f
    };

    [Test]
    public void Total_SumsAllBeats()
    {
        Assert.AreEqual(10f, T.Total, 0.0001f);
    }

    [Test]
    public void BeatAt_Start_IsSkyWipeAtZero()
    {
        var b = T.BeatAt(0f, out float l);
        Assert.AreEqual(YuanpeiIntroBeat.SkyWipe, b);
        Assert.AreEqual(0f, l, 0.0001f);
    }

    [Test]
    public void BeatAt_MidSkyWipe_HalfProgress()
    {
        var b = T.BeatAt(1f, out float l);
        Assert.AreEqual(YuanpeiIntroBeat.SkyWipe, b);
        Assert.AreEqual(0.5f, l, 0.0001f);
    }

    [Test]
    public void BeatAt_EachBeatBoundary_AdvancesInOrder()
    {
        Assert.AreEqual(YuanpeiIntroBeat.PushToBoss, T.BeatAt(2.5f, out _));
        Assert.AreEqual(YuanpeiIntroBeat.BossRise,   T.BeatAt(3.5f, out _));
        Assert.AreEqual(YuanpeiIntroBeat.PlayerLeap, T.BeatAt(5.5f, out _));
        Assert.AreEqual(YuanpeiIntroBeat.Clash,      T.BeatAt(7.5f, out _));
        Assert.AreEqual(YuanpeiIntroBeat.Settle,     T.BeatAt(9.5f, out _));
    }

    [Test]
    public void BeatAt_LocalProgress_IsRelativeToThatBeat()
    {
        // 3.5s in: SkyWipe(2) + PushToBoss(1) consumed -> 0.5s into BossRise(2) -> 0.25
        var b = T.BeatAt(3.5f, out float l);
        Assert.AreEqual(YuanpeiIntroBeat.BossRise, b);
        Assert.AreEqual(0.25f, l, 0.0001f);
    }

    [Test]
    public void BeatAt_PastEnd_IsDone()
    {
        var b = T.BeatAt(11f, out float l);
        Assert.AreEqual(YuanpeiIntroBeat.Done, b);
        Assert.AreEqual(1f, l, 0.0001f);
    }

    [Test]
    public void BeatAt_ExactlyAtEnd_IsSettleFullOrDone()
    {
        // t == Total: Settle is [9,10), so 10 lands past it -> Done
        Assert.AreEqual(YuanpeiIntroBeat.Done, T.BeatAt(10f, out _));
        // just inside
        Assert.AreEqual(YuanpeiIntroBeat.Settle, T.BeatAt(9.99f, out _));
    }

    [Test]
    public void Default_IsAReasonableCutsceneLength()
    {
        // 續181: slower sky wipe + air choreography -> ~16-17s
        Assert.That(YuanpeiIntroTimeline.Default.Total, Is.InRange(12f, 20f));
    }

    [Test]
    public void Short_IsShorterThanDefaultAndSane()
    {
        // 續183d: ~15s milestone cut - every beat trimmed but the beat ORDER/maths still hold.
        Assert.Less(YuanpeiIntroTimeline.Short.Total, YuanpeiIntroTimeline.Default.Total);
        Assert.That(YuanpeiIntroTimeline.Short.Total, Is.InRange(8f, 13f));
        // sanity: beats still resolve in order on the Short timeline
        var b = YuanpeiIntroTimeline.Short.BeatAt(0f, out _);
        Assert.AreEqual(YuanpeiIntroBeat.SkyWipe, b);
        Assert.AreEqual(YuanpeiIntroBeat.Done,
            YuanpeiIntroTimeline.Short.BeatAt(YuanpeiIntroTimeline.Short.Total + 0.1f, out _));
    }
}
