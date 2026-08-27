using UnityEngine;

namespace Live2DAction.Combat.Boss
{
    // Every global (not per-attack) tunable number BossStateMachine reads, in one asset - the
    // design doc's own hard requirement ("所有距離、傷害、機率、時間與冷卻參數都必須可在Inspector或
    // ScriptableObject調整,禁止散落硬編碼"). Per-attack numbers live on BossAttackDefinition
    // instead; this asset is everything that isn't specific to one move.
    [CreateAssetMenu(fileName = "BossTuning", menuName = "Live2DAction/Combat/Boss Tuning")]
    public class BossTuning : ScriptableObject
    {
        [Header("Alert / phase")]
        [SerializeField] private float alertRange = 10f;
        [Tooltip("HP fraction (0-1) at or below which the boss locks permanently into Phase2.")]
        [SerializeField, Range(0f, 1f)] private float phaseThreshold = 0.5f;

        // 2026-08-26, explicit user request (Boss AI spec, section 五 - "全域休息時間") - mandatory
        // post-attack recovery window BEFORE the boss may even consider its next attack, distinct
        // from attackReadinessBufferMin/MaxSeconds below (that's a much shorter 0.2-0.35s pre-ATTACK
        // reaction beat, not a post-attack rest - the two co-exist, this one is new). Explicitly
        // meant to guarantee the player a breathing/heal/counter window: "確保玩家有治療、恢復體力和
        // 反擊的空間". BossStateMachine.EndAttack() rolls this into _globalRestUntil; UpdateIdle()
        // skips PickAttack() entirely (but still faces/repositions) until Time.time passes it.
        [Header("Global rest after every attack (spec 五)")]
        [SerializeField] private float globalRestPhase1MinSeconds = 1.6f;
        [SerializeField] private float globalRestPhase1MaxSeconds = 2.4f;
        [SerializeField] private float globalRestPhase2MinSeconds = 1.1f;
        [SerializeField] private float globalRestPhase2MaxSeconds = 1.8f;
        [Tooltip("Extra rest added on top of the above when the finished attack has BossAttackDefinition.IsMajorAttack set (\"360°旋轉斬、重劈等大型攻擊\").")]
        [SerializeField] private float majorAttackExtraRestMinSeconds = 2f;
        [SerializeField] private float majorAttackExtraRestMaxSeconds = 3f;

        [Header("Approach -> attack readiness")]
        [Tooltip("Distance from the player's own attack range at which the boss starts decelerating " +
                 "instead of running straight into melee range at full speed.")]
        // 2026-08-24, bug report ("感覺很常在待機動作...不會逼近玩家") - this was tuned back when
        // normal attacks' MaxDistance averaged ~2.3m (before the "打空氣" measurement fix reduced
        // them to their real physical reach, ~0.85-1.4m). A fixed 1.5m deceleration zone made
        // sense against a 2.3m readiness distance (starts decelerating at 3.8m) but against the
        // new ~1.4m readiness distance it now starts at 2.9m and the boss crawls at as little as
        // 0.11 m/s (7% of WalkSpeed) for the last ~1.5m - confirmed by direct simulation - which
        // reads as "barely moving / standing around" instead of a confident approach. Shrunk
        // proportionally so the boss holds full speed until genuinely close, then tapers sharply.
        [SerializeField] private float approachDecelerationDistance = 0.5f;
        [SerializeField] private float attackReadinessBufferMinSeconds = 0.2f;
        [SerializeField] private float attackReadinessBufferMaxSeconds = 0.35f;
        [SerializeField] private float walkSpeed = 1.6f;
        [SerializeField] private float runSpeed = 3.2f;
        [SerializeField] private float unsteadyWalkSpeed = 1.2f;
        [SerializeField] private float rotationSpeedDegrees = 360f;

        [Header("AI decision cadence")]
        [Tooltip("How often (seconds) Idle/Approach re-evaluates its decision - NOT every frame. " +
                 "Legacy single-value fallback, used only if the Phase1/Phase2 pair below is left " +
                 "at (0,0) - see DecisionIntervalSeconds(BossPhase)'s own comment.")]
        [SerializeField] private float decisionIntervalSeconds = 0.2f;
        // 2026-08-25, explicit user request (combat AI spec, section 四) - decision cadence
        // itself is part of the phase-2 aggression bump ("決策間隔縮短為約0.6~1.2秒"), not just
        // move/run speed - a single decisionIntervalSeconds couldn't express "slower, more
        // deliberate in phase 1; snappier, more relentless in phase 2" at all. Min/Max pair (not
        // a single value) so each re-evaluation picks a slightly randomized cadence, matching
        // every other "feels less robotic" min/max pair already in this asset
        // (attackReadinessBuffer, postureBreakDuration, etc.) rather than a perfectly metronomic
        // tick.
        [Header("AI decision cadence - per phase (see spec 四)")]
        [SerializeField] private float decisionIntervalPhase1MinSeconds = 1f;
        [SerializeField] private float decisionIntervalPhase1MaxSeconds = 1.8f;
        [SerializeField] private float decisionIntervalPhase2MinSeconds = 0.6f;
        [SerializeField] private float decisionIntervalPhase2MaxSeconds = 1.2f;

        [Header("Posture / stance")]
        [SerializeField, Range(0f, 1f)] private float postureUnsteadyEnterFraction = 0.75f;
        [SerializeField, Range(0f, 1f)] private float postureUnsteadyExitFraction = 0.6f;
        [SerializeField] private float postureBreakDurationMinSeconds = 2.5f;
        [SerializeField] private float postureBreakDurationMaxSeconds = 4f;
        [Tooltip("Fraction of max poise restored the instant the boss finishes getting back up.")]
        [SerializeField, Range(0f, 1f)] private float postureRestoreOnRecover = 0.2f;
        [Tooltip("Seconds after the LAST poise-damaging hit before poise starts regenerating.")]
        [SerializeField] private float postureRegenDelaySeconds = 2f;
        [SerializeField] private float postureRegenPerSecond = 5f;
        // 2026-08-26, explicit user request (Boss AI spec, section 三) - the kneel-and-stand clip
        // actually available for this pack (falling_down, substituted for the missing dedicated
        // Kneel_on_One_Knee_and_Stand take - see BossStateMachine.kneelStandClipName's own
        // comment) is one continuous take, not three separate kneel/hit-window/stand-up clips.
        // Spec's own fallback instruction: "如果不能直接切割動畫,提供可調整的跪地Normalized Time,於
        // 該姿勢暫停Animator狀態,受擊時間結束後繼續站起;不可停止全域Time.timeScale" - this is that
        // configurable pause point. BossStateMachine.UpdatePostureBroken() plays the clip forward
        // to this normalized time, then sets animator.speed=0 (NOT Time.timeScale) to hold the
        // pose for postureBreakDurationMin/MaxSeconds, then resumes animator.speed=1 to play the
        // clip's own tail out as the "stand up" portion.
        [Tooltip("Normalized time (0-1) of kneelStandClipName at which playback pauses to hold the " +
                 "kneeling pose while the boss is hittable. Needs re-tuning per clip - see the " +
                 "field comment.")]
        [SerializeField, Range(0f, 1f)] private float postureKneelNormalizedTime = 0.5f;

        // 2026-08-24, explicit user request ("死亡後應該要是BeHit_FlyUp動作 五秒後復活") - this
        // boss is being used as a repeatable practice/tuning target during this whole iterative
        // session rather than a one-shot "defeat once, done" encounter, so unlike the original
        // spec's permanent Dead/Victory design, death here plays a reaction (reusing BeHit_FlyUp,
        // the only launch/knockdown-reading clip in the pack - no new animation needed) and
        // fully resets after a delay instead of staying down for good.
        [Header("Death / revive (practice-target behavior, not the original permanent-death spec)")]
        [SerializeField] private float reviveDelaySeconds = 5f;
        // 2026-08-26, explicit user request (Boss AI spec, section 四) - a NEW boss (武士/Wushi)
        // now wants the original permanent-death behavior back ("死亡動畫只播放一次...動畫結束後保持
        // 最後倒地姿勢,不回到Idle"), but the auto-revive above was itself an earlier explicit user
        // request for PiHaiWangV2 specifically (see reviveDelaySeconds' own comment) - rather than
        // ripping that out and breaking PW2's existing tuning asset, this is an opt-in per-asset
        // switch. Defaults to false so every BossTuning asset that predates this field (i.e. PW2's)
        // keeps reviving exactly as before; only newly-authored assets (Wushi's) need to flip it on.
        [Tooltip("If true, UpdateDead() never revives - the boss stays in BossState.Dead forever " +
                 "once health reaches zero, matching the original permanent-death spec. If false " +
                 "(default, existing behavior), the boss auto-revives after reviveDelaySeconds.")]
        [SerializeField] private bool permanentDeath;

        [Header("30s vanish/dive cycle")]
        [SerializeField] private float vanishTriggerSeconds = 30f;
        [SerializeField] private float vanishTrackDurationSeconds = 3.5f;
        [SerializeField] private float vanishWarningMinSeconds = 0.6f;
        [SerializeField] private float vanishWarningMaxSeconds = 1.5f;
        [Tooltip("Total vanish->dive cycle length (track + warning combined should land here, ~5s).")]
        [SerializeField] private float vanishTotalCycleSeconds = 5f;
        [SerializeField] private float vanishLandingBehindDistanceMin = 2.5f;
        [SerializeField] private float vanishLandingBehindDistanceMax = 4f;
        [SerializeField] private float diveLandingRecoverySeconds = 1f;

        [Header("Dodge & Counter")]
        [SerializeField, Range(0f, 1f)] private float dodgeCounterChancePhase1 = 0.15f;
        [SerializeField, Range(0f, 1f)] private float dodgeCounterChancePhase2 = 0.25f;
        [SerializeField] private float dodgeCounterCooldownMinSeconds = 8f;
        [SerializeField] private float dodgeCounterCooldownMaxSeconds = 10f;
        [SerializeField] private float dodgeCounterReactionDelayMinSeconds = 0.15f;
        [SerializeField] private float dodgeCounterReactionDelayMaxSeconds = 0.25f;
        [Tooltip("Normalized time (0-1 of the Dodge_and_Counter clip) the invulnerability window opens.")]
        [SerializeField, Range(0f, 1f)] private float dodgeIframeStartNormalized = 0.15f;
        [SerializeField, Range(0f, 1f)] private float dodgeIframeEndNormalized = 0.35f;

        [Header("Lean Forward Sprint (phase 2 tactical approach)")]
        [SerializeField] private float sprintMinDistance = 4f;
        [SerializeField] private float sprintMaxDistance = 8f;
        [SerializeField] private float sprintCooldownSeconds = 6f;
        [SerializeField] private float sprintBrakeDistance = 2f;
        [SerializeField] private float sprintBrakeMinSeconds = 0.25f;
        [SerializeField] private float sprintBrakeMaxSeconds = 0.35f;

        [Header("Ultimate (Rising_Flying_Kick)")]
        [SerializeField] private float ultimateIdealMinDistance = 2f;
        [SerializeField] private float ultimateIdealMaxDistance = 4f;
        [SerializeField] private float ultimateMaxDistance = 5f;
        [Tooltip("2026-08-25, user feedback (\"以現有衝刺距離本身 施展時先量測與玩家的距離 距離大於五分之四時" +
                 "施展\") - the ultimate is a long-range gap closer, not another close-range finisher, so " +
                 "it now only fires once the player is beyond this fraction of UltimateMaxDistance (the " +
                 "existing dash range itself - no separate distance invented). At the default 0.8 that's " +
                 "a fire window of (0.8x, 1.0x] * UltimateMaxDistance; inside that, TryEnterUltimate keeps " +
                 "returning false every frame (energy stays banked, not lost) until the player is far " +
                 "enough away again.")]
        [SerializeField] [Range(0f, 1f)] private float ultimateMinTriggerDistanceFraction = 0.8f;
        [Tooltip("2026-08-25, user feedback (\"我的本意是讓你把boss釋放必殺技前先保離一段距離 觀看效果較好\") " +
                 "- speed used by the new UltimateReposition state: once energy is pending but the " +
                 "player is inside the trigger threshold above, the boss actively backpedals (facing " +
                 "the player) to open the gap, instead of just sitting in normal combat forever with " +
                 "the ultimate banked and never used (which is what happened before this - in practice " +
                 "the player is almost always inside melee range).")]
        [SerializeField] private float ultimateRepositionSpeed = 4f;
        [Tooltip("Safety valve for UltimateReposition - this boss can spawn near a corner/wall, so " +
                 "backing away may never fully clear the trigger threshold. After this many seconds " +
                 "of retreating, fire the ultimate from whatever distance was actually reached instead " +
                 "of retreating forever (a real soft-lock otherwise).")]
        [SerializeField] private float ultimateRepositionTimeoutSeconds = 3f;
        [Tooltip("UNUSED as of 2026-08-25 (\"原地蓄力、只靠飛踢本身的撲擊來銜接\") - originally the " +
                 "windup-phase closing speed UpdateUltimatePrepare used to dash toward the player " +
                 "before the leap fired. The user asked for the windup to stand still instead and let " +
                 "UltimateLeapSpeed alone close the gap during the kick, so this field is no longer " +
                 "read anywhere. Left in place (not deleted) in case that direction is reverted later - " +
                 "harmless if so, it's just dead data until then.")]
        [SerializeField] private float ultimateApproachSpeed = 6.5f;
        [Tooltip("2026-08-25, user feedback (\"必殺技應該距離很遠才對\", later \"原地蓄力、只靠飛踢本身的" +
                 "撲擊來銜接\") - forward lunge speed applied " +
                 "during UltimateAttack itself, before the strike's hit window opens (RisingFlyingKick " +
                 "has no root motion wired, so without this the leap never actually translated the " +
                 "boss at all). Faster than the windup's own approach speed for a real leap feel.")]
        [SerializeField] private float ultimateLeapSpeed = 9f;
        [SerializeField] private float ultimateStartupMinSeconds = 1.5f;
        [SerializeField] private float ultimateStartupMaxSeconds = 2f;
        [Tooltip("Last N seconds of startup where tracking tapers to 0 and the strike direction locks.")]
        [SerializeField] private float ultimateTrackingLockSeconds = 0.5f;
        [SerializeField] private float ultimateRecoverySeconds = 1.25f;
        [Tooltip("Minimum normal-combat buffer after an ultimate resolves before a queued vanish can start.")]
        [SerializeField] private float postUltimateBufferSeconds = 1.5f;
        [Tooltip("Minimum normal-combat buffer after a vanish/dive resolves before a queued ultimate can fire.")]
        [SerializeField] private float postVanishBufferSeconds = 1f;

        [Header("Breakdance (periodic combat flourish - also a real attack, see PW2_Attack_Breakdance)")]
        [Tooltip("2026-08-26, explicit user request (\"戰鬥每持續15觸發一次長達5秒的此動作銜接\") - seconds " +
                 "of accumulated in-combat time (same eligibility gate as the vanish cycle - see " +
                 "UpdateCombatTimer) before Breakdance_1990 is queued. Duration itself isn't a separate " +
                 "field here - it just runs for as long as the breakdanceAttack clip actually is, same " +
                 "as every other clip-driven attack state (Ultimate/DodgeCounter included).")]
        [SerializeField] private float breakdanceTriggerSeconds = 15f;

        [Header("Leap Slam (periodic scheduled special - spec: 定時小技能)")]
        [Tooltip("2026-08-27, explicit user request (\"戰鬥每經過20秒就觸發\") - same accumulated-" +
                 "combat-time queueing pattern as breakdanceTriggerSeconds above, own independent " +
                 "timer/reset so the two schedules don't interfere with each other.")]
        [SerializeField] private float leapSlamTriggerSeconds = 20f;

        [Tooltip("2026-08-27, explicit user request (\"不能跳很高嗎 至少讓玩家看不到的高度\") - extra " +
                 "WORLD UNITS of height layered on top of the clip's own baked Hips-bone rise (which " +
                 "only reaches ~11 units on its own - see leapSlamAttack's designNotes), driven " +
                 "directly through _verticalVelocity every frame (see UpdateLeapSlam) rather than " +
                 "being part of the animation itself, so it's tunable without re-editing the clip.")]
        [SerializeField] private float leapSlamExtraHeight = 30f;
        [Tooltip("Normalized time (of leapSlamAttack's own clip) the extra-height arc starts rising " +
                 "from 0 - kept slightly after 0 so it doesn't fight the clip's own initial crouch pose.")]
        [SerializeField] private float leapSlamHeightRiseStartNormalized = 0.05f;
        [Tooltip("Normalized time the extra-height arc peaks (leapSlamExtraHeight above ground).")]
        [SerializeField] private float leapSlamHeightPeakNormalized = 0.30f;
        [Tooltip("Normalized time the extra-height arc returns to 0 - matched to when the clip's own " +
                 "Hips bone independently reaches near-ground (measured ~0.53), so the script-driven " +
                 "extra arc and the clip's own baked landing motion settle back to earth together.")]
        [SerializeField] private float leapSlamHeightFallEndNormalized = 0.53f;

        // 2026-08-26, explicit user request ("玩家極近距離靠近武士時 容易躲避所有攻擊 如何解決") - a
        // real weapon swing's own arc has a minimum reach (you can't cut something touching the
        // hilt with the blade's edge - see BladeHitbox/hit-window measurement history), so a
        // player standing point-blank against the boss can dodge every normal-pool attack forever.
        // Rather than inventing a new close-range move (no spare animation exists - see this
        // session's own survey of the Wushi clip folder), this reuses the boss's own existing kick
        // (which already carries knockback) as a dedicated "too close" punish: continuous
        // point-blank distance for tooCloseDurationSeconds forces it, same "queued timer -> Try*
        // consumes it" pattern as breakdanceTriggerSeconds above, and the kick's own knockback
        // naturally resets distance afterward instead of needing separate reposition logic.
        [Header("Too-close punish (spec: 極近距離強制踢擊)")]
        [Tooltip("World-unit distance (boss root to player root) at or under which the close-range timer accumulates.")]
        [SerializeField] private float tooCloseDistance = 1.6f;
        [Tooltip("Continuous seconds at/under tooCloseDistance before the punish kick is forced - " +
                 "explicit user request (\"必須達到範圍內持續2秒才踢擊 給玩家一點輸出空間\"): deliberately " +
                 "not instant, so hugging briefly to land a hit isn't punished immediately.")]
        [SerializeField] private float tooCloseDurationSeconds = 2f;

        public float AlertRange => alertRange;
        public float PhaseThreshold => phaseThreshold;
        public float ApproachDecelerationDistance => approachDecelerationDistance;
        public float AttackReadinessBufferMinSeconds => attackReadinessBufferMinSeconds;
        public float AttackReadinessBufferMaxSeconds => attackReadinessBufferMaxSeconds;
        public float WalkSpeed => walkSpeed;
        public float RunSpeed => runSpeed;
        public float UnsteadyWalkSpeed => unsteadyWalkSpeed;
        public float RotationSpeedDegrees => rotationSpeedDegrees;
        public float DecisionIntervalSeconds => decisionIntervalSeconds;
        // Returns a fresh randomized interval for the given phase. Falls back to the legacy
        // single decisionIntervalSeconds if the Phase1/Phase2 fields are all left at 0 (an asset
        // saved before this pair existed) so nothing silently breaks into a 0-second busy-loop.
        public float RollDecisionInterval(BossPhase phase, System.Func<float, float, float> random)
        {
            float min = phase == BossPhase.Phase1 ? decisionIntervalPhase1MinSeconds : decisionIntervalPhase2MinSeconds;
            float max = phase == BossPhase.Phase1 ? decisionIntervalPhase1MaxSeconds : decisionIntervalPhase2MaxSeconds;
            if (min <= 0f && max <= 0f)
            {
                return decisionIntervalSeconds;
            }
            return random(min, max);
        }
        public float PostureUnsteadyEnterFraction => postureUnsteadyEnterFraction;
        public float PostureUnsteadyExitFraction => postureUnsteadyExitFraction;
        public float PostureBreakDurationMinSeconds => postureBreakDurationMinSeconds;
        public float PostureBreakDurationMaxSeconds => postureBreakDurationMaxSeconds;
        public float PostureRestoreOnRecover => postureRestoreOnRecover;
        public float PostureRegenDelaySeconds => postureRegenDelaySeconds;
        public float PostureRegenPerSecond => postureRegenPerSecond;
        public float PostureKneelNormalizedTime => postureKneelNormalizedTime;
        public bool PermanentDeath => permanentDeath;
        public float GlobalRestMinSeconds(BossPhase phase) => phase == BossPhase.Phase1 ? globalRestPhase1MinSeconds : globalRestPhase2MinSeconds;
        public float GlobalRestMaxSeconds(BossPhase phase) => phase == BossPhase.Phase1 ? globalRestPhase1MaxSeconds : globalRestPhase2MaxSeconds;
        public float MajorAttackExtraRestMinSeconds => majorAttackExtraRestMinSeconds;
        public float MajorAttackExtraRestMaxSeconds => majorAttackExtraRestMaxSeconds;
        public float ReviveDelaySeconds => reviveDelaySeconds;
        public float VanishTriggerSeconds => vanishTriggerSeconds;
        public float VanishTrackDurationSeconds => vanishTrackDurationSeconds;
        public float VanishWarningMinSeconds => vanishWarningMinSeconds;
        public float VanishWarningMaxSeconds => vanishWarningMaxSeconds;
        public float VanishTotalCycleSeconds => vanishTotalCycleSeconds;
        public float VanishLandingBehindDistanceMin => vanishLandingBehindDistanceMin;
        public float VanishLandingBehindDistanceMax => vanishLandingBehindDistanceMax;
        public float DiveLandingRecoverySeconds => diveLandingRecoverySeconds;
        public float DodgeCounterChancePhase1 => dodgeCounterChancePhase1;
        public float DodgeCounterChancePhase2 => dodgeCounterChancePhase2;
        public float DodgeCounterCooldownMinSeconds => dodgeCounterCooldownMinSeconds;
        public float DodgeCounterCooldownMaxSeconds => dodgeCounterCooldownMaxSeconds;
        public float DodgeCounterReactionDelayMinSeconds => dodgeCounterReactionDelayMinSeconds;
        public float DodgeCounterReactionDelayMaxSeconds => dodgeCounterReactionDelayMaxSeconds;
        public float DodgeIframeStartNormalized => dodgeIframeStartNormalized;
        public float DodgeIframeEndNormalized => dodgeIframeEndNormalized;
        public float SprintMinDistance => sprintMinDistance;
        public float SprintMaxDistance => sprintMaxDistance;
        public float SprintCooldownSeconds => sprintCooldownSeconds;
        public float SprintBrakeDistance => sprintBrakeDistance;
        public float SprintBrakeMinSeconds => sprintBrakeMinSeconds;
        public float SprintBrakeMaxSeconds => sprintBrakeMaxSeconds;
        public float UltimateIdealMinDistance => ultimateIdealMinDistance;
        public float UltimateIdealMaxDistance => ultimateIdealMaxDistance;
        public float UltimateMaxDistance => ultimateMaxDistance;
        public float TooCloseDistance => tooCloseDistance;
        public float TooCloseDurationSeconds => tooCloseDurationSeconds;
        public float LeapSlamTriggerSeconds => leapSlamTriggerSeconds;
        public float LeapSlamExtraHeight => leapSlamExtraHeight;
        public float LeapSlamHeightRiseStartNormalized => leapSlamHeightRiseStartNormalized;
        public float LeapSlamHeightPeakNormalized => leapSlamHeightPeakNormalized;
        public float LeapSlamHeightFallEndNormalized => leapSlamHeightFallEndNormalized;
        public float UltimateMinTriggerDistanceFraction => ultimateMinTriggerDistanceFraction;
        public float UltimateRepositionSpeed => ultimateRepositionSpeed;
        public float UltimateRepositionTimeoutSeconds => ultimateRepositionTimeoutSeconds;
        public float UltimateApproachSpeed => ultimateApproachSpeed;
        public float UltimateLeapSpeed => ultimateLeapSpeed;
        public float UltimateStartupMinSeconds => ultimateStartupMinSeconds;
        public float UltimateStartupMaxSeconds => ultimateStartupMaxSeconds;
        public float UltimateTrackingLockSeconds => ultimateTrackingLockSeconds;
        public float UltimateRecoverySeconds => ultimateRecoverySeconds;
        public float PostUltimateBufferSeconds => postUltimateBufferSeconds;
        public float PostVanishBufferSeconds => postVanishBufferSeconds;
        public float BreakdanceTriggerSeconds => breakdanceTriggerSeconds;
    }
}
