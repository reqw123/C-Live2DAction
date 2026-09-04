using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Live2DAction.AI.Boss.Yuanpei;

// spec yuanpei_LogoSky_Boss_工程說明文件 §6, §10, §22 - the pure decision logic
// (phase gating + attack scheduler) verified without a live boss/physics.
public class YuanpeiBossLogicTests
{
    // ---------------- phase (spec §6) ----------------

    [Test]
    public void Phase_FullHealth_IsPhase1()
        => Assert.AreEqual(1, YuanpeiPhaseLogic.PhaseForHealth(1.0f, 0.70f, 0.35f));

    [Test]
    public void Phase_At70Percent_StillPhase1_JustAbove()
        => Assert.AreEqual(1, YuanpeiPhaseLogic.PhaseForHealth(0.701f, 0.70f, 0.35f));

    [Test]
    public void Phase_Below70_IsPhase2()
        => Assert.AreEqual(2, YuanpeiPhaseLogic.PhaseForHealth(0.60f, 0.70f, 0.35f));

    [Test]
    public void Phase_Below35_IsPhase3()
        => Assert.AreEqual(3, YuanpeiPhaseLogic.PhaseForHealth(0.20f, 0.70f, 0.35f));

    [Test]
    public void Phase_AtZero_IsPhase3()
        => Assert.AreEqual(3, YuanpeiPhaseLogic.PhaseForHealth(0f, 0.70f, 0.35f));

    // ---------------- scheduler (spec §10) ----------------

    static YuanpeiAttackDef Def(YuanpeiAttackId id, int phase, float cost, float minR, float maxR,
        bool major = true, float weight = 1f)
    {
        var d = ScriptableObject.CreateInstance<YuanpeiAttackDef>();
        d.attackId = id; d.requiredPhase = phase; d.energyCost = cost;
        d.minRange = minR; d.maxRange = maxR; d.isMajorHazard = major;
        d.baseWeight = weight; d.situationalWeightBonus = 1f; d.cooldownSeconds = 3f;
        return d;
    }

    static YuanpeiScheduler.Situation BaseSituation() => new YuanpeiScheduler.Situation
    {
        phase = 1, energy = 100f, playerDistance = 10f, hasLineOfSight = true,
        bossOnScreen = true, onScreenSeconds = 2f, onScreenGrace = 0.5f,
        majorHazardActive = false, hasLastAttack = false, arenaHasGoodFloor = true, now = 100f,
    };

    static readonly Dictionary<YuanpeiAttackId, float> Empty = new Dictionary<YuanpeiAttackId, float>();

    [Test]
    public void Scheduler_PicksTheOnlyValidAttack()
    {
        var pool = new List<YuanpeiAttackDef> { Def(YuanpeiAttackId.ProjectileBurst, 1, 15f, 6f, 15f) };
        var s = BaseSituation();
        var pick = YuanpeiScheduler.Select(pool, in s, Empty, Empty, 0f, 1f, new System.Random(1));
        Assert.NotNull(pick);
        Assert.AreEqual(YuanpeiAttackId.ProjectileBurst, pick.attackId);
    }

    [Test]
    public void Scheduler_SkipsAttackAboveCurrentPhase()
    {
        var pool = new List<YuanpeiAttackDef> { Def(YuanpeiAttackId.FocusLaser, 2, 35f, 9f, 18f) };
        var s = BaseSituation(); s.phase = 1;
        Assert.IsNull(YuanpeiScheduler.Select(pool, in s, Empty, Empty, 0f, 1f, new System.Random(1)));
    }

    [Test]
    public void Scheduler_SkipsWhenNotEnoughEnergy()
    {
        var pool = new List<YuanpeiAttackDef> { Def(YuanpeiAttackId.FocusLaser, 2, 35f, 9f, 18f) };
        var s = BaseSituation(); s.phase = 2; s.energy = 20f;
        Assert.IsNull(YuanpeiScheduler.Select(pool, in s, Empty, Empty, 0f, 1f, new System.Random(1)));
    }

    [Test]
    public void Scheduler_SkipsOutOfRange()
    {
        var pool = new List<YuanpeiAttackDef> { Def(YuanpeiAttackId.Shockwave, 1, 20f, 0f, 4.5f) };
        var s = BaseSituation(); s.playerDistance = 10f;
        Assert.IsNull(YuanpeiScheduler.Select(pool, in s, Empty, Empty, 0f, 1f, new System.Random(1)));
    }

    [Test]
    public void Scheduler_SkipsWithoutLineOfSight()
    {
        var pool = new List<YuanpeiAttackDef> { Def(YuanpeiAttackId.ProjectileBurst, 1, 15f, 6f, 15f) };
        var s = BaseSituation(); s.hasLineOfSight = false;
        Assert.IsNull(YuanpeiScheduler.Select(pool, in s, Empty, Empty, 0f, 1f, new System.Random(1)));
    }

    [Test]
    public void Scheduler_SkipsOnCooldown()
    {
        var pool = new List<YuanpeiAttackDef> { Def(YuanpeiAttackId.ProjectileBurst, 1, 15f, 6f, 15f) };
        var s = BaseSituation(); s.now = 100f;
        var cd = new Dictionary<YuanpeiAttackId, float> { { YuanpeiAttackId.ProjectileBurst, 105f } };
        Assert.IsNull(YuanpeiScheduler.Select(pool, in s, cd, Empty, 0f, 1f, new System.Random(1)));
    }

    [Test]
    public void Scheduler_NeverRepeatsTheSameAttack()
    {
        var pool = new List<YuanpeiAttackDef> { Def(YuanpeiAttackId.ProjectileBurst, 1, 15f, 6f, 15f) };
        var s = BaseSituation();
        s.hasLastAttack = true; s.lastAttack = YuanpeiAttackId.ProjectileBurst;
        Assert.IsNull(YuanpeiScheduler.Select(pool, in s, Empty, Empty, 0f, 1f, new System.Random(1)));
    }

    [Test]
    public void Scheduler_SkipsMajorHazardWhileOneIsLive()
    {
        var pool = new List<YuanpeiAttackDef>
        {
            Def(YuanpeiAttackId.FocusLaser, 1, 15f, 6f, 18f, major: true),
            Def(YuanpeiAttackId.BodyCharge, 1, 5f, 5f, 12f, major: false),
        };
        var s = BaseSituation(); s.majorHazardActive = true;
        var pick = YuanpeiScheduler.Select(pool, in s, Empty, Empty, 0f, 1f, new System.Random(1));
        Assert.NotNull(pick);
        Assert.AreEqual(YuanpeiAttackId.BodyCharge, pick.attackId, "only the non-major move should be eligible");
    }

    [Test]
    public void Scheduler_HoldsWhileOffScreen()
    {
        var pool = new List<YuanpeiAttackDef> { Def(YuanpeiAttackId.ProjectileBurst, 1, 15f, 6f, 15f) };
        var s = BaseSituation(); s.bossOnScreen = false;
        Assert.IsNull(YuanpeiScheduler.Select(pool, in s, Empty, Empty, 0f, 1f, new System.Random(1)));
    }

    [Test]
    public void Scheduler_HoldsDuringOnScreenGrace()
    {
        var pool = new List<YuanpeiAttackDef> { Def(YuanpeiAttackId.ProjectileBurst, 1, 15f, 6f, 15f) };
        var s = BaseSituation(); s.onScreenSeconds = 0.2f; s.onScreenGrace = 0.5f;
        Assert.IsNull(YuanpeiScheduler.Select(pool, in s, Empty, Empty, 0f, 1f, new System.Random(1)));
    }

    // MultiAoE (spec §9.4) + YuanpeiAoePlacement removed 追加94 續 119 (attack cut from the pool).
}
