using System.Collections.Generic;
using UnityEngine;
using Live2DAction.Core;
using Live2DAction.Combat;
using Live2DAction.Combat.Boss;
using Live2DAction.Targeting;
using Live2DAction.Characters;

namespace Live2DAction.AI.Boss
{
    // The boss's own program-level combat FSM - see BossState for the 14 states and this class's
    // own priority cascade (Resolve()) for how they arbitrate. The Animator only ever plays
    // whatever clip the CURRENT state maps to (BossAnimatorBridge-equivalent logic lives inline
    // here rather than a separate class, since every transition needs direct access to this same
    // state) - it never makes a combat decision on its own (no AnyState-driven combo logic the
    // way this project's Player/Enemy Animator Controllers use for Attack1-4).
    //
    // Movement is CharacterController-driven code, same approach as EnemyAI (this project has no
    // NavMeshAgent anywhere - reusing that existing pattern rather than introducing a second
    // movement system). NavMesh itself (baked, but agent-less) IS used for one narrow purpose -
    // validating vanish/dive landing points via NavMesh.SamplePosition, per spec.
    [RequireComponent(typeof(CharacterController))]
    public class BossStateMachine : MonoBehaviour, ICharacterSpeedSource
    {
        // 2026-08-24 - this was originally its own top-level file (BossAnimatorParams.cs) in this
        // same folder/namespace, matching the spec's own "centralize parameter names" request as
        // a standalone class. Folded in here as a nested type after that separate file
        // reproducibly failed to compile into this assembly for reasons a full clean rebuild
        // (CompilationPipeline.RequestScriptCompilation with CleanBuildCache) couldn't resolve,
        // even though a sibling file in the identical folder/namespace (BossState.cs) compiled
        // fine - never root-caused, worth another look if it recurs elsewhere. Nothing outside
        // BossStateMachine ever referenced the old standalone type, so this is a pure relocation,
        // not a design change - still one centralized place for every Animator parameter name.
        public static class BossAnimatorParams
        {
            public static readonly int CombatActive = Animator.StringToHash("CombatActive");
            public static readonly int MovementSpeed = Animator.StringToHash("MovementSpeed");
            public static readonly int Phase = Animator.StringToHash("Phase");
            public static readonly int AttackID = Animator.StringToHash("AttackID");
            public static readonly int AttackTrigger = Animator.StringToHash("AttackTrigger");
            public static readonly int DodgeCounterTrigger = Animator.StringToHash("DodgeCounterTrigger");
            public static readonly int BreakdanceTrigger = Animator.StringToHash("BreakdanceTrigger");
            public static readonly int UltimateTrigger = Animator.StringToHash("UltimateTrigger");
            public static readonly int VanishTrigger = Animator.StringToHash("VanishTrigger");
            public static readonly int DiveTrigger = Animator.StringToHash("DiveTrigger");
            public static readonly int HitFlyUpTrigger = Animator.StringToHash("HitFlyUpTrigger");
            public static readonly int PostureBreakTrigger = Animator.StringToHash("PostureBreakTrigger");
            public static readonly int VictoryTrigger = Animator.StringToHash("VictoryTrigger");
            public static readonly int Dead = Animator.StringToHash("Dead");
            public static readonly int Grounded = Animator.StringToHash("Grounded");
        }

        [Header("Systems reused from the existing project (not duplicated)")]
        [SerializeField] private Health health;
        [SerializeField] private StancePoise stance;
        [SerializeField] private UltimateEnergy ultimateEnergy;
        [Tooltip("2026-08-28, explicit user request (\"飛向天空那招 能量滿格才會觸發 100能量:20秒\") - " +
                 "a SEPARATE UltimateEnergy instance that gates LeapSlam: it fires only when this is " +
                 "full, and Consume()s it at the leap commit. Configure it for the requested rate " +
                 "(regenAmount 5 / regenIntervalSeconds 1 => 100 in 20s). If left unwired, LeapSlam " +
                 "falls back to the old BossTuning.LeapSlamTriggerSeconds combat-time timer.")]
        [SerializeField] private UltimateEnergy leapSlamEnergy;
        [SerializeField] private Animator animator;
        [SerializeField] private Transform target; // Player
        [SerializeField] private LockOnTarget lockOnTarget;
        [Tooltip("Player's own PlayerCombat - polled read-only for CurrentPhase to detect a " +
                 "punishable windup for Dodge_and_Counter (see DetectDodgeTrigger). Nothing here " +
                 "modifies the player's combat state.")]
        [SerializeField] private PlayerCombat playerCombat;

        [Header("Tuning / attack data (all editable assets, no hardcoded numbers)")]
        [SerializeField] private BossTuning tuning;
        [SerializeField] private BossAttackDefinition[] normalAttackPool;
        [SerializeField] private BossAttackDefinition dodgeCounterAttack;
        [SerializeField] private BossAttackDefinition ultimateAttack;
        [Tooltip("2026-08-26, explicit user request - Breakdance_1990, a periodic combat flourish " +
                 "that also lands a real hit (see BossState.Breakdance / BossTuning.BreakdanceTriggerSeconds). " +
                 "Never part of normalAttackPool - it's queued purely by accumulated combat time, not " +
                 "picked by PickAttack()'s distance/angle/weight roll.")]
        [SerializeField] private BossAttackDefinition breakdanceAttack;
        [Tooltip("A minimal BossAttackDefinition used only for its healthDamage/poiseDamage/knockback " +
                 "when landingAoeHitbox resolves - no clip/hit-window fields on it matter.")]
        [SerializeField] private BossAttackDefinition diveLandingAttack;
        [Tooltip("2026-08-26, explicit user request (\"利用他在偵測到玩家過近時就觸發\") - reuses an " +
                 "existing normalAttackPool entry (its own knockback resets distance afterward) as a " +
                 "forced punish once tuning.TooCloseDurationSeconds of continuous point-blank range " +
                 "elapses - see TryEnterTooCloseKick. Wire this to the SAME asset already in the pool " +
                 "(e.g. Wushi_Attack_SpartanKick), not a separate copy.")]
        [SerializeField] private BossAttackDefinition tooCloseAttack;
        [Tooltip("2026-08-27, explicit user request (\"定時小技能，戰鬥每經過20秒就觸發，先飛升到空中，然後" +
                 "落地劈砍\") - a real BossAttackDefinition (clip + tracking + hit windows), unlike " +
                 "diveLandingAttack above which is only ever used for its damage numbers. Never part of " +
                 "normalAttackPool, queued purely by accumulated combat time like breakdanceAttack - see " +
                 "BossState.LeapSlam's own comment for why this doesn't reuse Vanishing/DiveAttack.")]
        [SerializeField] private BossAttackDefinition leapSlamAttack;

        [Tooltip("2026-09-01, user request (\"OverheadSlam 改為每30秒觸發一次\") - a normal-pool attack " +
                 "promoted to a fixed periodic special: removed from normalAttackPool and forced on a " +
                 "combat-time timer instead. Runs through the ordinary Attack state (BeginAttack), just " +
                 "not chosen by PickAttack(). Unwired => disabled (屁孩王 leaves it null).")]
        [SerializeField] private BossAttackDefinition periodicSlamAttack;
        [Tooltip("Seconds of combat between forced periodicSlamAttack uses.")]
        [SerializeField] private float periodicSlamIntervalSeconds = 30f;

        [Tooltip("spec item 8 §9.2 - minimum seconds between ANY two periodic specials " +
                 "(Breakdance / LeapSlam / OverheadSlam). Their interval timers only make them " +
                 "eligible; this shared cooldown stops several firing back-to-back when they come " +
                 "due together. TooCloseKick / Ultimate occupy it but aren't blocked by it. " +
                 "0 = disabled = the old per-special behaviour. Spec suggests 6-10.")]
        [SerializeField] private float sharedSpecialCooldownSeconds = 0f;

        [Header("Hitboxes (see BossHitbox - enabled/disabled per HitWindow, never globally)")]
        [SerializeField] private BossHitbox leftHandHitbox;
        [SerializeField] private BossHitbox rightHandHitbox;
        [SerializeField] private BossHitbox leftFootHitbox;
        [SerializeField] private BossHitbox rightFootHitbox;
        [SerializeField] private BossHitbox bodyHitbox;
        [SerializeField] private BossHitbox landingAoeHitbox;
        [Tooltip("BladeHitbox on a socketed weapon (e.g. Katana) - see BossHitboxPart.Weapon's own comment.")]
        [SerializeField] private BossHitbox weaponHitbox;

        [Header("Clip name references (Animator Controller state names must match)")]
        private const string LocomotionStateName = "Locomotion";
        [SerializeField] private string idleClipName = "PW_Idle";
        [SerializeField] private string walkingClipName = "PW_Walking";
        [SerializeField] private string runningClipName = "PW_Running";
        [SerializeField] private string unsteadyWalkClipName = "PW_UnsteadyWalk";
        [SerializeField] private string sprintClipName = "PW_Sprint";
        [SerializeField] private string fall3ClipName = "PW_Fall3";
        [SerializeField] private string behitFlyUpClipName = "PW_BeHitFlyUp";
        // 2026-08-25, explicit user request (combat AI spec, section 八) - previously Dead reused
        // behitFlyUpClipName because no dedicated death take existed in the old asset pack (see
        // BossTuning.ReviveDelaySeconds' own comment on that stopgap). The new "Man in Black"
        // pack ships a real Shot_and_Fall_Forward take, so Dead gets its own field instead of
        // continuing to borrow the hit-reaction clip. Left blank falls back to
        // behitFlyUpClipName automatically (see OnEnterState's Dead case), so older bosses that
        // never set this keep working unchanged.
        [SerializeField] private string deathClipName = "";
        [SerializeField] private string diveLandClipName = "PW_DiveLand";
        [SerializeField] private string walkToSitClipName = "PW_WalkToSit";
        // 2026-08-26, explicit user request ("Kneel_on_One_Knee_and_Stand 架勢條滿格時觸發") - dedicated
        // PostureBroken clip for the "Man in Black" pack's own kneel-and-stand take, replacing the old
        // fall3ClipName placeholder (that pack never actually shipped a matching Fall3 take, so
        // PostureBroken was silently playing nothing on this boss - see OnEnterState's PostureBroken
        // case). Left blank falls back to fall3ClipName automatically, same fallback pattern as
        // deathClipName above, so older bosses that never set this keep working unchanged.
        [SerializeField] private string kneelStandClipName = "PW2_KneelOnOneKneeAndStand";

        // 2026-08-29, user report ("武士感覺警備範圍怪怪的 明明玩家離得很遠") - the boss had no leash:
        // once it engaged (player within AlertRange), Approach/Attack chased the player with no
        // "give up" distance, so it would follow you clear across the map. This is the guard
        // radius, measured from the boss's HOME post (its position at Awake), NOT its live
        // position: if the player leaves it the boss disengages back to Dormant and walks home.
        // 0 disables the leash (chase forever, the old behaviour).
        [Header("Leash / guard range")]
        // The "give up and go home" radius from the post - NOT the engage range (that's the
        // tuning asset's alertRange / "警備距離"). It must be large enough to contain a whole
        // fight: 2026-08-29 a 6m leash made the boss abort every leap / drop combat the instant
        // the player spaced out past 6m from the post, over and over ("飛空技能失效 只會待在原地
        // 做出像是重複動作"). Default 30 covers all of 本地 (post-to-far-corner ~29m); the boss
        // only disengages if the player actually leaves - through the vehicle hole toward the
        // road / 學校. 0 disables the leash entirely.
        //
        // 2026-08-31 - also feeds ChaseGiveUpDistance() (the from-boss give-up in UpdateIdle /
        // UpdateApproach), so this one number now also decides how far the boss will actually
        // chase. 武士's scene instance is 32 (post at 本地's north end z=11, the vehicle doorway
        // ~26m south) so it pursues the player right out through the doorway before heading home -
        // matching the confined 精怪's walk-to-the-gate reach instead of stopping 9m out.
        [SerializeField] private float leashRange = 30f;

        // 2026-08-29, user request. A 精怪 (屁孩王) is confined to 本地 - it can chase the player up
        // to the walls but not through the vehicle doorway. OFF for 武士 (the real boss keeps only
        // the distance leash). 本地 = a 15.5 half-extent square on the origin.
        //
        // 2026-08-29 follow-up ("一旦碰到邊界時不要直接傳回原本位置，而是判斷是否有目標在警備範圍內...
        // 一直在門口觀望著目標") - hitting the wall no longer teleports it home. Once the player is
        // outside, it walks to the boundary and enters GateWatch (hold + face, no attacks) as long
        // as the player stays within gateWatchRange of it; only when the player is gone does it
        // snap back to its post.
        [UnityEngine.Serialization.FormerlySerializedAs("leashOnLeaveArena")]
        [SerializeField] private bool confineToArena = false;
        [SerializeField] private Vector2 arenaCenterXZ = Vector2.zero;
        [SerializeField] private float arenaHalfExtent = 15.5f;
        // Once the boss has walked to the boundary, how close the player must stay (measured from
        // the boss AT the gate - not while it is still walking there) for it to keep watching
        // rather than return to post. 0 = fall back to the tuning asset's AlertRange.
        [SerializeField] private float gateWatchRange = 0f;
        // Grace after the player passes gateWatchRange before the boss actually gives up and
        // snaps home - a lingering look rather than an instant turn-around.
        [SerializeField] private float gateWatchGiveUpSeconds = 1.5f;

        // 2026-08-29, user request ("移動速度太慢了 *1.5倍 腳步要配合") - the Locomotion blend tree's
        // top clip (PW2_Running) is authored for roughly this ground speed; once BossTuning
        // WalkSpeed/RunSpeed is pushed past it the run clip keeps its authored cadence while the
        // body translates faster and the feet slide. While in Approach the Animator's playback
        // rate is scaled by (actual speed / this) so stride cadence tracks the real speed.
        // OnExitState already resets animator.speed to 1, so this never leaks past Approach.
        // 0 = disabled (武士 leaves it off - separate rig / Animator Controller).
        [Header("Locomotion foot-sync")]
        [SerializeField] private float locomotionAuthoredSpeed = 0f;

        [Header("Debug")]
        [SerializeField] private bool logStateChanges;

        public BossState CurrentState { get; private set; } = BossState.Dormant;
        public BossPhase Phase { get; private set; } = BossPhase.Phase1;

        // 2026-08-26, explicit user request ("把具體踢的範圍畫出來讓我排錯") - read-only exposure so
        // an external visualizer (TooCloseRangeIndicator) can draw the REAL threshold/progress
        // instead of a guessed number - same "read the real thing" reasoning as
        // PlayerCombat.MaxAttackReach's own comment.
        //
        // 2026-08-26, explicit user request ("這個極近距離應該要對齊玩家的極限攻擊距離 保證玩家在最遠
        // 能攻擊到武士的情況下 能觸發武士的踢擊並擊退"), re-confirmed 2026-09-02 ("如果玩家攻擊武士的
        // 最遠距離就一定是站在圈內的 這樣武士的踢擊才有意義"). The too-close zone MUST reach at least
        // as far as the player's own farthest melee reach, so a player cannot poke the boss from any
        // range without eventually eating the punish kick. The corollary the user also stated - "武士
        // 的所有攻擊手段一定都是大於圓圈的，不然就會頻繁觸發踢擊" - is enforced on the OTHER side:
        // the boss commits its own attacks from OUTSIDE this zone (see tuning.AttackStandoffMargin
        // and UpdateIdleOrApproach), so walking in to swing never itself trips the timer.
        public float EffectiveTooCloseDistance => _targetCombat != null
            ? Mathf.Max(tuning.TooCloseDistance, _targetCombat.MaxAttackReach)
            : tuning.TooCloseDistance;
        public float TooCloseProgress01 => tuning.TooCloseDurationSeconds > 0f
            ? Mathf.Clamp01(_tooCloseTimer / tuning.TooCloseDurationSeconds)
            : 0f;
        // 2026-09-02, user rule ("武士的所有攻擊手段一定都是大於圓圈的，不然就會頻繁觸發踢擊") - the boss
        // sets up / re-spaces to at least this far before swinging, so a lunge or a forced point-blank
        // attack that ended inside the too-close kick zone doesn't make it attack from in there again
        // (which would trip the player's hug timer and force a kick, cutting off the boss's own move).
        // Sits outside EffectiveTooCloseDistance by ForcedAttackStandoffMargin, and below the normal
        // AttackReadinessDistance (smallest pool MaxDistance, ~3m) so it never fights that gate.
        private float AttackStandoffFloor => EffectiveTooCloseDistance + tuning.ForcedAttackStandoffMargin;

        // 2026-08-26, explicit user request (Boss AI spec, section 四) - "提供OnDeath事件供BossUI、
        // 獎勵及關卡系統使用". Health.Died already exists and fires the instant HP hits zero, but
        // that's the raw HP event, not "the boss has actually entered BossState.Dead" (there can be
        // a frame or two of PostureBroken/HitReaction still resolving in between on some paths) -
        // this fires once, exactly when OnEnterState(Dead) runs, for callers that specifically want
        // "the death state/animation has started" rather than "HP crossed zero".
        public event System.Action OnDeath;

        // 2026-08-26, explicit user request (Boss AI spec, section 三 - "提供AddPostureDamage(float)
        // 等公開整合介面") - thin passthrough onto StancePoise.AddPostureDamage (see that method's
        // own comment) so external callers (parry systems, scripted events, etc.) have one obvious
        // entry point on the boss's own component instead of needing to know this FSM even uses
        // StancePoise internally. TryEnterPostureBroken() (already existing, unchanged) is what
        // actually reacts to stance.IsStaggered becoming true and drives the state transition -
        // this method only ever touches the stance bar, never BossState directly.
        public void AddPostureDamage(float amount) => stance?.AddPostureDamage(amount);

        // ICharacterSpeedSource - lets CharacterAnimatorLink-equivalent drive a Locomotion blend
        // tree the same way it already does for Player/Enemy, if this boss's own Animator
        // Controller wires MovementSpeed through a blend tree instead of discrete clip states.
        public float CurrentHorizontalSpeed => new Vector2(_horizontalVelocity.x, _horizontalVelocity.z).magnitude;
        public bool IsFlying => false;

        // 2026-08-25 - see ICharacterSpeedSource.IsGrounded's own comment. This class already
        // writes its own Grounded bool directly in WriteAnimatorParameters() below rather than
        // going through CharacterAnimatorLink, but still needs this to satisfy the interface.
        public bool IsGrounded => _controller.isGrounded;

        private CharacterController _controller;
        private NavPathFollower _pathFollower; // optional, see Awake / MoveTowardTarget
        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;

        private bool _hasEngaged; // Alert reached at least once
        private Vector3 _homePosition; // guard post - captured at Awake, see leashRange
        private Quaternion _homeRotation;
        private bool _phase2Locked;
        private bool _postureUnsteady;
        private float _decisionTimer;
        private float _stateTimer; // seconds since entering CurrentState
        private BossAttackDefinition _currentAttack;
        private int _lastActiveHitWindowIndex = -1;

        // spec item 5 sub-step 5A - programmatic attack lunge. Captured at BeginAttack, consumed in
        // UpdateAttack, cleared by EndAttack / CancelAttackInProgress. Inert unless the current
        // attack's AttackMotionProfile has a non-zero forwardDistance.
        private Vector3 _attackMotionOrigin;
        private Vector3 _attackMotionDir;
        private float _attackMotionApplied;   // world metres of forwardDistance already travelled
        private bool _attackMotionHalted;     // a Recoil parry froze the remaining lunge
        private float _attackMotionDistanceOverride = -1f; // >=0 = use this instead of AttackMotion.forwardDistance (LungeDistanceFromTargetGap)
        private float _rootMotionScaleRuntime = -1f; // >=0 = per-cast scale from RootMotionAimAtTarget, else use RootMotionScale
        private Transform _hipsBone;          // cancelClipBodyDrift - see Awake
        private Vector3 _clipDriftBaselineXZ; // hip world offset from root, captured on the attack's first frame
        private bool _clipDriftBaselineSet;
        private Vector3 _clipDriftCompensatedXZ; // cumulative XZ drift already cancelled this attack
        private readonly HashSet<BossHitbox> _openHitboxesThisAttack = new HashSet<BossHitbox>();
        private readonly Dictionary<BossAttackDefinition, float> _cooldownUntil = new Dictionary<BossAttackDefinition, float>();
        // 2026-08-29 - Time.time each pool attack last STARTED (see BeginAttack), for
        // PickAttackFiltered's rotation bias (BossTuning.AttackRotationRecoverySeconds). Distinct
        // from _cooldownUntil: that's a hard "can't pick" gate, this only nudges the weighted roll.
        private readonly Dictionary<BossAttackDefinition, float> _lastUsedTime = new Dictionary<BossAttackDefinition, float>();
        private BossAttackDefinition _lastNormalAttack;
        private int _lastNormalAttackConsecutiveCount;
        private bool _attackLandedAnyHit;
        private bool _sweepUsedThisCombo;
        private BossAttackDefinition _pendingDerivedAttack;

        private bool _ultimatePending;
        private float _lastUltimateEndTime = -999f;

        private float _combatTimeAccumulated;
        private float _breakdanceTimeAccumulated;
        private bool _breakdancePending;
        private float _leapSlamTimeAccumulated;
        private bool _leapSlamPending;
        private float _periodicSlamTimeAccumulated;
        private bool _periodicSlamPending;
        private bool _vanishPending;

        // spec item 8 (M4) §9.2 - a shared cooldown across the periodic special pool
        // (Breakdance / LeapSlam / OverheadSlam). Their own interval timers only grant "eligibility"
        // (the *Pending flag); this stops two of them releasing back-to-back on consecutive frames
        // when several come due together. TooCloseKick + Ultimate occupy it (so a kick can't be
        // instantly chained into a LeapSlam) but aren't gated by it. 0 = off = the old behaviour.
        private float _lastSpecialFireTime = -999f;
        // 2026-08-26, explicit user request ("玩家極近距離靠近武士時 容易躲避所有攻擊") - continuous
        // (not accumulated-whenever-close) seconds at/under the effective too-close distance; resets
        // the instant the player steps back out, unlike _breakdanceTimeAccumulated above which never
        // resets - see UpdateTooCloseTimer/TryEnterTooCloseKick.
        private float _tooCloseTimer;
        // Cached at Awake - see UpdateTooCloseTimer / EffectiveTooCloseDistance for why the effective
        // threshold reads the player's real combo reach instead of tuning.TooCloseDistance alone.
        private PlayerCombat _targetCombat;
        // Gates TryEnterTooCloseKick's diagnostic log to once per threshold-crossing instead of
        // spamming every frame a fire attempt stays blocked - see that method's own comment.
        private bool _tooCloseThresholdLogged;
        private float _lastVanishEndTime = -999f;
        private float _vanishTimer;
        private Vector3 _lockedLandingPoint;
        private bool _landingPointLocked;
        private Renderer[] _renderers;

        private bool _dodgeWindowRequested;
        private float _dodgeReactionDeadline;
        private bool _dodgeIframesActive;
        private AttackPhase _lastObservedPlayerPhase = AttackPhase.Idle;

        private float _postureLastDamagedTime = -999f;
        private bool _postureBrokenHandled;
        // 2026-08-26, explicit user request (Boss AI spec, section 三) - kneel/hit-window/stand-up
        // sub-phase tracking for UpdatePostureBroken()'s pause-at-normalized-time technique. See
        // BossTuning.PostureKneelNormalizedTime's own comment for why this exists instead of three
        // separate clips.
        private bool _postureKneelReached;
        private bool _postureHoldElapsed;
        private float _deathElapsed;

        // 2026-09-01, user report ("五適硬值時仍然是在空中躺平") - see BossTuning.PostureBrokenGroundDropOffset's
        // own comment. Applied once when the collapse pose freezes, undone once PostureBroken ends.
        private bool _postureBrokenDropApplied;

        // spec item 7 (M4) - Deathblow. BeginExecutionHold pins PostureBroken + grants i-frames for
        // the finisher windup; the deathblow then either phase-transitions or permanently kills.
        // _deathblowFinalKill blocks UpdateDead's auto-revive for THIS death only (an ordinary
        // HP-to-zero death still revives per the 2026-08-24 "五秒後復活" request).
        private float _executionHoldUntil = -999f;
        private bool _executionInvuln;
        private bool _deathblowFinalKill;
        // 2026-08-26, explicit user request (Boss AI spec, section 五 - "全域休息時間") - see
        // BossTuning's own comment on the paired fields this is rolled from.
        private float _globalRestUntil = -999f;

        private System.Random _random = new System.Random();

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _renderers = GetComponentsInChildren<Renderer>(true);
            // 2026-08-31, user report ("被地圖物件擋住路線卡住") - optional. When present, MoveTowardTarget
            // routes around NavMesh obstacles instead of pressing straight into them. Null (no
            // component, or no NavMesh baked) = the original straight-line chase, unchanged.
            _pathFollower = GetComponent<NavPathFollower>();

            // Real bug hit during Play Mode verification: BossHitbox.Configure(attackerRoot, team)
            // was previously only ever called ONCE, at edit-time, from PiHaiWangBossSetup.Apply().
            // _attackerRoot/_attackerTeam on BossHitbox are plain (non-[SerializeField]) fields, so
            // that edit-time call never actually persisted into the scene - any later domain reload
            // (e.g. a script recompile) or scene reopen silently reset them to null/"", after which
            // every OnTriggerEnter bailed out on its very first guard clause
            // (`_attackerRoot == null`) and every boss attack whiffed with zero damage and zero
            // console errors. Re-running Configure() here on every Awake makes it self-healing
            // regardless of when/how the scene was (re)loaded, instead of depending on one-time
            // Editor-time state sticking.
            // 2026-08-28, explicit user request ("武士要能對屁孩王造成傷害") - the attacker team was
            // hardcoded "Boss", so every boss's hitbox matched every other boss's BossTeamMember and
            // BossHitbox.TryResolveHit's friendly-fire guard blocked boss-on-boss damage. Read this
            // boss's OWN BossTeamMember.Team instead (default "Boss" if it has none) - two bosses with
            // different team strings can now hurt each other, while a boss still can't hit its own
            // hurtboxes (the transform.root == attackerRoot check fires first regardless).
            var teamMember = GetComponent<BossTeamMember>();
            string attackerTeam = teamMember != null ? teamMember.Team : "Boss";
            var hitboxes = GetComponentsInChildren<BossHitbox>(true);
            foreach (var hitbox in hitboxes)
            {
                hitbox.Configure(transform, attackerTeam);
            }

            // 2026-08-26, explicit user request ("這個極近距離應該要對齊玩家的極限攻擊距離") - see
            // UpdateTooCloseTimer / EffectiveTooCloseDistance and PlayerCombat.MaxAttackReach's own
            // comment for why this reads the player's real combo Range+Radius.
            _targetCombat = target != null ? target.GetComponent<PlayerCombat>() : null;

            // 2026-09-02 - the Hips bone, for BossAttackDefinition.cancelClipBodyDrift (Meshy attack
            // clips that bake several metres of forward "walk" into the Hips muscle, not the root
            // curve - lockRootPositionXZ doesn't touch it). Null on a non-humanoid rig.
            _hipsBone = animator != null && animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.Hips)
                : null;

            // Guard post for the leash (see leashRange). The boss's placed scene pose.
            _homePosition = transform.position;
            _homeRotation = transform.rotation;
        }

        private void OnEnable()
        {
            if (health != null) health.Died += OnBossDied;
            if (stance != null)
            {
                // StancePoise's own IsStaggered flips true at 100% - this class treats that
                // exactly as "posture broken" (PostureBroken state), reusing the existing
                // component instead of re-implementing a second poise meter.
            }
        }

        private void OnDisable()
        {
            if (health != null) health.Died -= OnBossDied;
        }

        // Called externally by whatever tracks the PLAYER's own health (see
        // BossVictoryHook) - triggers section 15's Victory sequence. Kept as a public method
        // rather than this class polling Player's Health itself every frame, so it stays
        // decoupled from exactly how the project wires player-death notification.
        public void NotifyPlayerDied()
        {
            if (CurrentState == BossState.Dead || CurrentState == BossState.Victory)
            {
                return;
            }
            CancelAllPending();
            CloseAllHitboxes();
            ChangeState(BossState.Victory);
        }

        // 2026-09-01 - lets an intro cutscene (BossIntroManager) skip the Dormant proximity wait
        // and commit the boss to the fight the instant the cutscene ends, so the sword-raise
        //演出 flows straight into combat instead of the player having to walk closer first.
        // No-op once already engaged / in a terminal state - safe to call unconditionally.
        public void ForceEngage()
        {
            if (target == null
                || CurrentState == BossState.Dead || CurrentState == BossState.Victory
                || CurrentState == BossState.GettingUp)
            {
                return;
            }
            if (CurrentState == BossState.Dormant
                || CurrentState == BossState.ReturnHome || CurrentState == BossState.GateWatch)
            {
                _hasEngaged = true;
                ChangeState(BossState.Alert); // same entry UpdateDormant uses; FSM flows Alert -> Approach -> attack on its own
            }
        }

        private void OnBossDied()
        {
            CancelAllPending();
            CloseAllHitboxes();
            ChangeState(BossState.Dead);
        }

        private void Update()
        {
            _stateTimer += Time.deltaTime;

            UpdatePhaseLock();
            UpdatePostureOverride();
            UpdateCombatTimer();
            UpdateTooCloseTimer();
            DetectDodgeTrigger();

            // Priority cascade - see spec section 3. Each Try* returns true if it took over this
            // frame, in which case lower-priority checks/the current state's own Update are
            // skipped for this frame (the new state's own Enter already ran).
            if (CurrentState != BossState.Dead && CurrentState != BossState.GettingUp)
            {
                if (health != null && health.IsDead) { ChangeState(BossState.Dead); }
                else if (CurrentState == BossState.Victory) { /* terminal, nothing pre-empts it */ }
                else if (TryEnterPostureBroken()) { }
                else if (TryEnterHitReaction()) { }
                else if (TryLeashReset()) { }
                else if (TryContinuePhaseTransitionVisual()) { }
                else if (TryContinueCommittedSpecialAttack()) { }
                else if (TryEnterUltimate()) { }
                else if (TryEnterUltimateReposition()) { }
                else if (TryEnterVanish()) { }
                else if (TryEnterDodgeCounter()) { }
                else if (TryEnterBreakdance()) { }
                else if (TryEnterLeapSlam()) { }
                else if (TryEnterPeriodicSlam()) { }
                else if (TryEnterTooCloseKick()) { }
                // Sweeping_Kick derivation and normal attacks are both handled inside
                // UpdateAttackState/UpdateIdleOrApproach below - they're not separately
                // pre-emptible states, they're sub-decisions within Attack/Approach.
            }

            RunCurrentState();
            ApplyMotion();
            WriteAnimatorParameters();
        }

        // ---------------------------------------------------------------- Phase / posture

        private void UpdatePhaseLock()
        {
            if (_phase2Locked || health == null)
            {
                return;
            }
            float fraction = health.MaxHealth > 0f ? health.CurrentHealth / health.MaxHealth : 1f;
            if (fraction <= tuning.PhaseThreshold)
            {
                _phase2Locked = true;
                Phase = BossPhase.Phase2;
                Log("Phase2 locked permanently (HP fraction " + fraction.ToString("F2") + ")");
            }
        }

        private void UpdatePostureOverride()
        {
            if (stance == null || tuning == null)
            {
                return;
            }
            float fraction = stance.MaxStance > 0f ? stance.CurrentStance / stance.MaxStance : 0f;
            if (!_postureUnsteady && fraction >= tuning.PostureUnsteadyEnterFraction)
            {
                _postureUnsteady = true;
            }
            else if (_postureUnsteady && fraction <= tuning.PostureUnsteadyExitFraction)
            {
                _postureUnsteady = false;
            }
            // Hysteresis band between exit/enter thresholds - deliberately does nothing while
            // fraction sits between the two, so it can't flip every frame at a single boundary
            // value (spec: "不要每幀依49%與51%反覆切換").
        }

        private void UpdateCombatTimer()
        {
            bool bothAlive = health != null && !health.IsDead && target != null;
            // 2026-08-29, user report ("我明明沒接近他 突然他就像我衝過來") - the vanish/breakdance/
            // leapSlam countdown timers used to keep accumulating while the boss was Dormant (only
            // _hasEngaged gated them, and _hasEngaged was never reset once set). So a boss the
            // player had walked away from would silently arm a LeapSlam and then teleport-slam
            // them from across the arena. Now the timers only run while genuinely in a fight, and
            // DisengageAndReturnHome resets _hasEngaged + the accumulators + drains the energy
            // meters the moment the boss gives up.
            bool eligible = _hasEngaged && bothAlive
                             && CurrentState != BossState.Dormant && CurrentState != BossState.ReturnHome
                             && CurrentState != BossState.GateWatch && CurrentState != BossState.Alert
                             && CurrentState != BossState.Vanishing
                             && CurrentState != BossState.DiveAttack && CurrentState != BossState.Dead
                             && CurrentState != BossState.GettingUp
                             && CurrentState != BossState.Victory;
            if (!eligible)
            {
                return;
            }

            _combatTimeAccumulated += Time.deltaTime;
            if (!_vanishPending && _combatTimeAccumulated >= tuning.VanishTriggerSeconds
                && Time.time - _lastVanishEndTime >= tuning.PostVanishBufferSeconds)
            {
                _vanishPending = true;
                Log("vanishPending = true");
            }

            // 2026-08-26, explicit user request ("戰鬥每持續15觸發一次") - independent timer from the
            // vanish cycle above (both accumulate under the same eligibility, but on their own
            // separate cadences/resets - conflating them would mean whichever fires first resets
            // the other's countdown too).
            _breakdanceTimeAccumulated += Time.deltaTime;
            if (!_breakdancePending && _breakdanceTimeAccumulated >= tuning.BreakdanceTriggerSeconds)
            {
                _breakdancePending = true;
                Log("breakdancePending = true");
            }

            // 2026-08-27, explicit user request ("定時小技能，戰鬥每經過20秒就觸發") - own independent
            // timer, same reasoning as breakdanceTimeAccumulated's own comment above (don't conflate
            // separate schedules).
            // 2026-08-28, explicit user request ("飛向天空那招 能量滿格才會觸發 100能量:20秒") - when a
            // leapSlamEnergy instance is wired, the trigger is "energy bar full" instead of a raw
            // combat-time timer (the bar itself regens at whatever rate the UltimateEnergy asset is
            // configured for - 5/s over 20s for the requested 100:20). Consumed in TryEnterLeapSlam
            // the instant the windup starts. Unwired => the old timer, unchanged, for any other boss.
            _leapSlamTimeAccumulated += Time.deltaTime;
            bool leapReady = leapSlamEnergy != null
                ? leapSlamEnergy.IsFull
                : _leapSlamTimeAccumulated >= tuning.LeapSlamTriggerSeconds;
            // 2026-08-28, playtested bug ("被連續觸發兩次") - belt-and-braces alongside the earlier
            // Consume(): never re-arm while the leap sequence itself is running, even for the
            // timer-based fallback path.
            bool leapInProgress = CurrentState == BossState.LeapSlamWindup || CurrentState == BossState.LeapSlam;
            if (!_leapSlamPending && leapReady && !leapInProgress)
            {
                _leapSlamPending = true;
                Log("leapSlamPending = true");
            }

            // 2026-09-01, user request ("OverheadSlam 改為每30秒觸發一次") - own independent timer,
            // same pattern as the others. Fires periodicSlamAttack through the ordinary Attack state.
            if (periodicSlamAttack != null)
            {
                _periodicSlamTimeAccumulated += Time.deltaTime;
                if (!_periodicSlamPending && _periodicSlamTimeAccumulated >= periodicSlamIntervalSeconds)
                {
                    _periodicSlamPending = true;
                    Log("periodicSlamPending = true");
                }
            }
        }

        // 2026-08-26, explicit user request ("玩家極近距離靠近武士時 容易躲避所有攻擊 如何解決") - a
        // CONTINUOUS timer (unlike _combatTimeAccumulated/_breakdanceTimeAccumulated above, which
        // never reset mid-combat) - stepping back out of the too-close threshold even briefly resets
        // the count, so this only fires for genuinely sustained point-blank hugging, not someone who
        // just happened to pass through melee range on their way elsewhere. Runs regardless of
        // UpdateCombatTimer's own "eligible" gating (_hasEngaged etc.) since point-blank range
        // already implies engagement.
        //
        // 2026-08-26 follow-up, explicit user request ("這個極近距離應該要對齊玩家的極限攻擊距離 保
        // 證玩家在最遠能攻擊到武士的情況下 能觸發武士的踢擊並擊退") - tuning.TooCloseDistance alone
        // is a guessed number that could silently fall out of sync if the player's own attack Range/
        // Radius is ever retuned later (same class of bug PrimaryAttack's own comment already
        // warns about for EnemyAI). The EFFECTIVE threshold is whichever is larger: the tuning
        // value, or the player's actual measured max melee reach - so even at the player's own
        // farthest attack range, they're still guaranteed to be inside the too-close zone.
        private void UpdateTooCloseTimer()
        {
            if (target == null || (health != null && health.IsDead))
            {
                _tooCloseTimer = 0f;
                _tooCloseThresholdLogged = false;
                return;
            }
            if (HorizontalDistance() <= EffectiveTooCloseDistance)
            {
                _tooCloseTimer += Time.deltaTime;
            }
            else
            {
                _tooCloseTimer = 0f;
                _tooCloseThresholdLogged = false;
            }
        }

        // ---------------------------------------------------------------- Priority checks

        private bool TryEnterPostureBroken()
        {
            if (stance == null || !stance.IsStaggered || CurrentState == BossState.PostureBroken)
            {
                return false;
            }
            if (CurrentState == BossState.UltimateAttack && _committedUltimate)
            {
                return false; // spec: after the real jump commits, posture break defers to landing
            }
            if (CurrentState == BossState.DiveAttack || CurrentState == BossState.Vanishing)
            {
                return false; // never break posture mid-air / while untargetable
            }
            _postureBrokenHandled = false;
            ChangeState(BossState.PostureBroken);
            return true;
        }

        private bool TryEnterHitReaction()
        {
            if (!_forcedHitReactionPending)
            {
                return false;
            }
            _forcedHitReactionPending = false;
            ChangeState(BossState.HitReaction);
            return true;
        }

        private bool TryContinuePhaseTransitionVisual()
        {
            // No dedicated cinematic was specced beyond "永久進入第二階段,使用Running接近玩家" -
            // the phase flip itself already takes effect via UpdatePhaseLock()/movement mode
            // selection, so there's no separate state to force here. Present as an explicit
            // no-op step (rather than omitted) so the priority list still matches section 3's
            // numbering 1:1 for anyone reading the cascade against the spec.
            return false;
        }

        private bool TryContinueCommittedSpecialAttack()
        {
            // Once UltimateAttack has jumped (_committedUltimate) or DiveAttack/Vanishing has
            // locked its landing point, nothing may pre-empt it except Boss death - already
            // guarded by the checks above/below returning false while those states are active.
            return CurrentState == BossState.UltimateAttack || CurrentState == BossState.DiveAttack
                || CurrentState == BossState.Vanishing;
        }

        private bool TryEnterUltimate()
        {
            if (!_ultimatePending || CurrentState == BossState.UltimatePrepare || CurrentState == BossState.UltimateAttack)
            {
                return false;
            }
            if (CurrentState == BossState.PostureBroken || CurrentState == BossState.HitReaction
                || CurrentState == BossState.Vanishing || CurrentState == BossState.DiveAttack)
            {
                return false;
            }
            if (_currentAttack != null && !AttackFinishedCommittableWindow())
            {
                return false; // let the current not-yet-interruptible action finish first
            }
            // 2026-08-25, user feedback ("必殺技應該距離很遠才對") - this used to have zero distance
            // gating at all, so a full-energy boss fired the ultimate from literally any distance;
            // combined with UpdateUltimatePrepare doing no movement, a far-away trigger just kicked
            // into empty air once the leap's own short root-motion reach ran out. Now gated by
            // UltimateMaxDistance (tuned generously large - see PW2_Tuning) so it CAN still start
            // from far away, while UpdateUltimatePrepare below dashes to close the remaining gap
            // during the windup instead of standing still. If the player is even farther than that,
            // this just returns false and normal Approach/Attack behavior keeps closing distance -
            // TryEnterUltimate is polled every frame, so it fires the moment the gate opens.
            float ultimateDistance = HorizontalDistance();
            if (ultimateDistance > tuning.UltimateMaxDistance)
            {
                return false;
            }
            // 2026-08-25, follow-up user feedback ("以現有衝刺距離本身 施展時先量測與玩家的距離 距離大於
            // 五分之四時施展") - the ultimate is meant to be a long-range gap closer, not a redundant
            // close-range finisher alongside the normal attack pool. Reusing the existing dash range
            // (UltimateMaxDistance) rather than inventing a separate distance: only fires once the
            // player is beyond a fraction of it (default 0.8). Below that, this just returns false
            // every frame - energy stays banked (not consumed, not lost) until the player is far
            // enough away again, and normal combat continues uninterrupted in the meantime.
            if (ultimateDistance < tuning.UltimateMaxDistance * tuning.UltimateMinTriggerDistanceFraction)
            {
                return false;
            }
            MarkSpecialFired(); // spec §9.2 - the ultimate delays the periodic-special pool
            ChangeState(BossState.UltimatePrepare);
            return true;
        }

        // 2026-08-25, user feedback ("我的本意是讓你把boss釋放必殺技前先保離一段距離 觀看效果較好") -
        // TryEnterUltimate's distance gate (added for "必殺技應該距離很遠才對") only checked passively:
        // if the player was too close, it just returned false every frame and normal combat carried
        // on as if no ultimate were pending. Since normal combat almost always keeps the player at
        // melee range, that meant the ultimate effectively never fired once banked - confirmed by
        // this exact report. This state makes the boss actively retreat to open the gap instead of
        // waiting for it to open on its own. Checked right after TryEnterUltimate in the cascade so
        // the real ultimate always gets first refusal if the gap already happens to be open.
        private bool TryEnterUltimateReposition()
        {
            if (!_ultimatePending || CurrentState == BossState.UltimateReposition
                || CurrentState == BossState.UltimatePrepare || CurrentState == BossState.UltimateAttack)
            {
                return false;
            }
            if (CurrentState == BossState.PostureBroken || CurrentState == BossState.HitReaction
                || CurrentState == BossState.Vanishing || CurrentState == BossState.DiveAttack)
            {
                return false;
            }
            if (_currentAttack != null && !AttackFinishedCommittableWindow())
            {
                return false; // let the current not-yet-interruptible action finish first
            }
            if (HorizontalDistance() >= tuning.UltimateMaxDistance * tuning.UltimateMinTriggerDistanceFraction)
            {
                return false; // already far enough - TryEnterUltimate (checked first) handles it
            }
            Log($"UltimateReposition: backing away (dist={HorizontalDistance():F2}, need>={tuning.UltimateMaxDistance * tuning.UltimateMinTriggerDistanceFraction:F2}).");
            ChangeState(BossState.UltimateReposition);
            return true;
        }

        private bool TryEnterVanish()
        {
            if (!_vanishPending || CurrentState == BossState.Vanishing || CurrentState == BossState.DiveAttack)
            {
                return false;
            }
            if (CurrentState == BossState.PostureBroken || CurrentState == BossState.HitReaction
                || CurrentState == BossState.UltimateReposition
                || CurrentState == BossState.UltimatePrepare || CurrentState == BossState.UltimateAttack)
            {
                return false; // ultimate (already arbitrated above) always wins if both pending
            }
            if (_currentAttack != null && !AttackFinishedCommittableWindow())
            {
                return false;
            }
            ChangeState(BossState.Vanishing);
            return true;
        }

        // Edge-detects the PLAYER entering AttackPhase.Startup (a real windup, per this project's
        // own frame-data model - see AttackPhase.cs) and, if this boss is otherwise eligible,
        // requests a dodge AFTER a mandatory reaction delay - spec's own fairness requirement
        // ("禁止同幀讀取玩家輸入並閃避"). The delay is honored by TryEnterDodgeCounter checking
        // Time.time < _dodgeReactionDeadline, not by delaying this detection itself.
        private void DetectDodgeTrigger()
        {
            if (playerCombat == null || stance == null) return;

            AttackPhase phase = playerCombat.CurrentPhase;
            bool enteredStartup = phase == AttackPhase.Startup && _lastObservedPlayerPhase != AttackPhase.Startup;
            _lastObservedPlayerPhase = phase;

            if (!enteredStartup || _dodgeWindowRequested || CurrentState == BossState.DodgeCounter)
            {
                return;
            }
            if (stance.IsStaggered || CurrentState == BossState.PostureBroken || CurrentState == BossState.HitReaction)
            {
                return;
            }
            if (Time.time < CooldownUntil(dodgeCounterAttack))
            {
                return;
            }

            float chance = Phase == BossPhase.Phase1 ? tuning.DodgeCounterChancePhase1 : tuning.DodgeCounterChancePhase2;
            if ((float)_random.NextDouble() > chance)
            {
                return;
            }

            _dodgeWindowRequested = true;
            _dodgeReactionDeadline = Time.time + Random(tuning.DodgeCounterReactionDelayMinSeconds, tuning.DodgeCounterReactionDelayMaxSeconds);
        }

        private bool TryEnterDodgeCounter()
        {
            if (!_dodgeWindowRequested || CurrentState == BossState.DodgeCounter)
            {
                return false;
            }
            if (Time.time < _dodgeReactionDeadline)
            {
                return false; // still inside the mandatory 0.15-0.25s reaction delay
            }
            // 2026-08-26 - extended to also cover Breakdance (a real hit-dealing attack state, see
            // TryEnterBreakdance) alongside the normal Attack pool - both are "the boss is mid its
            // own active strike", same "don't dodge-counter through your own swing" rule applies.
            // Breakdance never sets _currentAttack (that field is reserved for normalAttackPool
            // bookkeeping), so this checks the physical hitboxes directly instead.
            if ((CurrentState == BossState.Attack || CurrentState == BossState.Breakdance || CurrentState == BossState.LeapSlam) && IsInsideAnyActiveWindow())
            {
                _dodgeWindowRequested = false;
                return false; // spec: Boss不在自己的攻擊有效幀
            }
            if (Time.time < CooldownUntil(dodgeCounterAttack))
            {
                _dodgeWindowRequested = false;
                return false;
            }
            _dodgeWindowRequested = false;
            SetCooldown(dodgeCounterAttack, Random(tuning.DodgeCounterCooldownMinSeconds, tuning.DodgeCounterCooldownMaxSeconds));
            ChangeState(BossState.DodgeCounter);
            return true;
        }

        // 2026-08-26, explicit user request ("霹靂舞:戰鬥每持續15觸發一次長達5秒的此動作銜接") - queued
        // purely by UpdateCombatTimer's own accumulated-time timer (mirrors how vanishPending works),
        // not by PickAttack()'s distance/angle/weight roll - this always fires exactly on schedule
        // once nothing higher-priority is happening, rather than competing against the normal pool.
        // Checked last in the priority cascade (after DodgeCounter) so it never steals a punish
        // window or pre-empts anything more urgent - it's a scheduled flourish, not a reaction.
        //
        // Known gap (documented, not silently papered over): unlike normal-pool attacks, this
        // doesn't check AttackFinishedCommittableWindow() against ITS OWN active hit window before
        // Ultimate/Vanish/DodgeCounter are allowed to preempt it (those all gate on _currentAttack,
        // which Breakdance never sets) - so in the rare case those become eligible mid-swing, this
        // move can be cut off mid-strike. Not fixed here since it would need touching three other
        // methods for a scenario that's rare in practice (all three are already low-frequency/
        // random) - worth revisiting if playtesting shows it happening often.
        // spec item 8 §9.2 - the shared periodic-special cooldown. Ready when disabled (0) or the
        // last special was long enough ago. The *Pending flag stays armed while this blocks, so an
        // eligible special just waits its turn rather than being lost.
        private bool SharedSpecialReady => SpecialScheduleUtility.SharedCooldownReady(
            _lastSpecialFireTime, Time.time, sharedSpecialCooldownSeconds);

        private void MarkSpecialFired() => _lastSpecialFireTime = Time.time;

        private bool TryEnterBreakdance()
        {
            if (!_breakdancePending || CurrentState == BossState.Breakdance || breakdanceAttack == null)
            {
                return false;
            }
            if (!SharedSpecialReady)
            {
                return false; // eligible, but another special fired too recently - stay pending
            }
            if (CurrentState == BossState.PostureBroken || CurrentState == BossState.HitReaction
                || CurrentState == BossState.Vanishing || CurrentState == BossState.DiveAttack
                || CurrentState == BossState.UltimateReposition || CurrentState == BossState.UltimatePrepare
                || CurrentState == BossState.UltimateAttack || CurrentState == BossState.DodgeCounter)
            {
                return false;
            }
            if (_currentAttack != null && !AttackFinishedCommittableWindow())
            {
                return false; // let the current not-yet-interruptible normal attack finish first
            }
            _breakdancePending = false;
            _breakdanceTimeAccumulated = 0f;
            MarkSpecialFired();
            ChangeState(BossState.Breakdance);
            return true;
        }

        // 2026-09-01, user request ("OverheadSlam 改為每30秒觸發一次") - forces periodicSlamAttack on
        // schedule, through the ordinary Attack state (BeginAttack). Same interrupt courtesy as
        // TryEnterBreakdance: doesn't cut into terminal / special / not-yet-interruptible states.
        private bool TryEnterPeriodicSlam()
        {
            if (!_periodicSlamPending || periodicSlamAttack == null)
            {
                return false;
            }
            if (!SharedSpecialReady)
            {
                return false; // eligible, but another special fired too recently - stay pending
            }
            if (CurrentState == BossState.PostureBroken || CurrentState == BossState.HitReaction
                || CurrentState == BossState.Vanishing || CurrentState == BossState.DiveAttack
                || CurrentState == BossState.UltimateReposition || CurrentState == BossState.UltimatePrepare
                || CurrentState == BossState.UltimateAttack || CurrentState == BossState.DodgeCounter
                || CurrentState == BossState.Breakdance
                || CurrentState == BossState.LeapSlamWindup || CurrentState == BossState.LeapSlam
                || CurrentState == BossState.Dormant || CurrentState == BossState.ReturnHome
                || CurrentState == BossState.GateWatch)
            {
                return false;
            }
            // 2026-09-02, user report ("發動 Wushi_OverheadSlam 武士會突然很接近玩家且處於踢擊範圍內") -
            // periodic slam bypasses Approach's standoff, so if a lunging pool attack (DoubleCombo)
            // just ended point-blank it would slam from inside the too-close kick zone, then get
            // stuck force-kicking. Stay pending until the boss is clear of that zone (the too-close
            // kick, or the player/boss simply repositioning, opens the gap - OverheadSlam's own
            // maxDistance 3.2 easily reaches from there).
            if (target != null && HorizontalDistance() < AttackStandoffFloor)
            {
                return false;
            }
            if (_currentAttack != null && !AttackFinishedCommittableWindow())
            {
                return false; // let the current not-yet-interruptible normal attack finish first
            }
            _periodicSlamPending = false;
            _periodicSlamTimeAccumulated = 0f;
            MarkSpecialFired();
            BeginAttack(periodicSlamAttack);
            return true;
        }

        // 2026-08-27, explicit user request ("定時小技能，戰鬥每經過20秒就觸發，他是一個先飛升到空
        // 中，然後落地劈砍的攻擊動作，落地時請直接鎖定玩家，並且落下的期間全程具有攻擊幀 範圍大") -
        // queued by the same combat-time timer pattern as TryEnterBreakdance. The clip's own baked
        // Hips motion only rises ~11 units, so:
        //   - facing + landing XZ get a coarse first placement here (teleport to a point
        //     tuning.LeapSlamLandingOffset short of the player, so Wushi doesn't land inside the
        //     player's own capsule - see the landingPos block below), THEN UpdateLeapSlam's homing
        //     block re-steers toward the player's live position until tuning.LeapSlamTrackUntilNormalized
        //     (2026-08-28, "落地前追蹤玩家位置 然後落地") before locking for the final drop;
        //   - landing Y is snapped to the real ground (raycast + tuning.LeapSlamLandingGroundedOffset),
        //     re-probed while homing, and pinned for the landing/stand-up (see _leapSlamLandingY);
        //   - the dramatic "off-screen" height is a script arc (tuning.LeapSlamExtraHeight ~30 units)
        //     layered on the root every frame in UpdateLeapSlam, with gravity/grounded clamp off for
        //     the whole state so it doesn't fight the arc.
        // See UpdateLeapSlam / ComputeLeapSlamExtraHeight for the per-frame vertical drive, and the
        // extensive 2026-08-27/28 playtest-bug history there (stale-normalizedTime slam, climbing
        // landing Y, float on landing).
        private bool TryEnterLeapSlam()
        {
            if (!_leapSlamPending || CurrentState == BossState.LeapSlam || CurrentState == BossState.LeapSlamWindup
                || leapSlamAttack == null || target == null)
            {
                return false;
            }
            if (CurrentState == BossState.PostureBroken || CurrentState == BossState.HitReaction
                || CurrentState == BossState.Vanishing || CurrentState == BossState.DiveAttack
                || CurrentState == BossState.UltimateReposition || CurrentState == BossState.UltimatePrepare
                || CurrentState == BossState.UltimateAttack || CurrentState == BossState.DodgeCounter
                || CurrentState == BossState.Breakdance)
            {
                return false;
            }
            if (_currentAttack != null && !AttackFinishedCommittableWindow())
            {
                return false; // let the current not-yet-interruptible normal attack finish first
            }
            if (!SharedSpecialReady)
            {
                return false; // eligible, but another special fired too recently - stay pending
            }
            // 2026-08-29, user report ("我明明沒接近他 突然他就像我衝過來") - CommitLeapSlamLanding
            // teleports the boss right on top of the player, so a LeapSlam that fires while the
            // player is way out of range reads as the boss warping across the arena at them. Only
            // commit to the leap from a range where it's a readable gap-closer (arm stays pending
            // until the player is actually that close).
            // 2026-08-29 follow-up (same report, still felt like a cross-arena warp) - tightened
            // from max(AlertRange*3, 15) ≈ 18m to ≈ 9m: a short lunge, not a teleport from the far
            // side of 本地.
            float leapCap = Mathf.Max(tuning.AlertRange * 1.5f, 9f);
            if (HorizontalDistance() > leapCap)
            {
                return false;
            }
            _leapSlamPending = false;
            _leapSlamTimeAccumulated = 0f;
            MarkSpecialFired();
            _leapSlamPrevExtraHeight = 0f;
            _leapSlamClipConfirmed = false;
            _leapSlamHolding = false;
            _leapSlamHitWindowsDone = false;
            _leapSlamLandingLocked = false;

            // 2026-08-28, playtested bug ("觸發必殺技時沒有及時清空能量導致被連續觸發兩次") - the energy
            // used to be Consume()d only at the leap commit ~1s later (UpdateLeapSlamWindup). During
            // that windup second, UpdateCombatTimer runs every frame, still sees leapSlamEnergy.IsFull,
            // and re-arms _leapSlamPending - so the instant the first LeapSlam ended, a second one
            // fired. Spend it HERE, the moment the move is committed to (enters windup). A windup
            // cancelled by a posture break now costs the energy, which is the right trade for a
            // fully-telegraphed 必殺 (matches how a committed Sekiro-style special works).
            leapSlamEnergy?.Consume();

            // 2026-08-28, explicit user request ("站在原地") - just hold still (LeapSlamWindup plays the
            // idle pose); no crouch. The teleport-to-landing still happens at the END of the hold
            // (CommitLeapSlamLanding, from UpdateLeapSlamWindup) so the player doesn't see a blink to
            // a new spot before the leap.
            Log("LeapSlamWindup: charging (dist=" + HorizontalDistance().ToString("F2") + ")");
            ChangeState(BossState.LeapSlamWindup);
            return true;
        }

        // The commit: teleport to the landing spot + lock facing, called once at the end of
        // LeapSlamWindup right before ChangeState(LeapSlam). Was inline in TryEnterLeapSlam before the
        // 2026-08-28 windup was added.
        private void CommitLeapSlamLanding()
        {
            if (target == null) return;

            // 2026-08-27, playtested bug ("落地位置仍然不對 還是浮空") - teleporting to the player's
            // EXACT xz drops Wushi straight down onto the player's OWN CharacterController capsule;
            // controller-vs-controller collision then reports _controller.isGrounded at roughly
            // player-height above the real floor, and Wushi hangs there through the entire landing/
            // stand-up (looked like "沒踩到地"). Land a short step SHORT of the player instead,
            // along the line back toward wherever Wushi leapt from - the landing AOE (3.0 world-unit
            // radius, see leapSlamAttack.designNotes) still comfortably covers the player, and
            // "鎖定玩家" is about facing/aim, not physically standing inside them. Set
            // LeapSlamLandingOffset to 0 in tuning to restore dead-centre landing.
            Vector3 landingPos = target.position;
            Vector3 backToBoss = transform.position - target.position;
            backToBoss.y = 0f;
            Vector3 offsetDir = backToBoss.sqrMagnitude > 0.0001f
                ? backToBoss.normalized
                : (transform.forward.sqrMagnitude > 0.0001f ? transform.forward.normalized : Vector3.forward);
            landingPos += offsetDir * tuning.LeapSlamLandingOffset;

            // 2026-08-27, playtested bug ("變得比剛剛還上面了 告訴我落地後武士的y座標") - the landing
            // Y was taken from transform.position.y (Wushi's CURRENT height), so a LeapSlam that
            // failed to settle fed its inflated Y into the next one and casts climbed. Snap it to
            // the REAL ground at the landing xz: raycast down, add tuning.LeapSlamLandingGroundedOffset.
            // 2026-08-28, user request ("武士飛空後著地y座標從0.623改為0.5") - that offset used to be
            // auto-computed (~0.123 = capsule-bottom-to-origin + skinWidth, resting the capsule
            // flush); it's now a plain tuning value (default 0 = transform origin on the ground
            // surface). UpdateLeapSlam holds this Y for the rest of the state with gravity/grounded
            // clamp off, so the CharacterController can't push it back to its natural rest height.
            Vector3 probeFrom = new Vector3(landingPos.x, transform.position.y + 50f, landingPos.z);
            if (Physics.Raycast(probeFrom, Vector3.down, out var groundHit, 200f, ~0, QueryTriggerInteraction.Ignore)
                && !groundHit.collider.transform.IsChildOf(transform))
            {
                landingPos.y = groundHit.point.y + tuning.LeapSlamLandingGroundedOffset;
            }
            else
            {
                landingPos.y = transform.position.y; // no ground found (map edge) - keep current height
            }
            _leapSlamLandingY = landingPos.y;

            if (tuning.LeapSlamTeleportToLanding)
            {
                // 2026-08-27, real playtested bug (confirmed by direct isolated test: a plain
                // transform.position assignment sticks for exactly one line, then ApplyMotion's own
                // _controller.Move() call silently snaps it right back to wherever the
                // CharacterController's own internal state still thinks it is) - CharacterController
                // caches its own position separately from Transform and doesn't pick up a direct
                // transform.position write until the component is toggled, which is the standard/only
                // reliable way to teleport one. Same "_controller" field ApplyMotion below already uses.
                _controller.enabled = false;
                transform.position = landingPos;
                _controller.enabled = true;
            }
            // else (2026-08-29, 屁孩王 "飛向天空那朝沒有鎖定玩家方向飛過去攻擊"): no takeoff blink -
            // the boss stays on its takeoff spot and UpdateLeapSlam's airborne homing physically
            // flies it to the player over the arc, so it reads as a real pounce, not a teleport.
            Vector3 lookDir = target.position - transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);

            Log("LeapSlam: " + (tuning.LeapSlamTeleportToLanding ? "teleported to" : "locked onto (flying to)")
                + " player, landing Y " + _leapSlamLandingY.ToString("F2"));
        }

        // 2026-08-28, explicit user request - the windup hold. Stand still (idle pose, set in
        // OnEnterState), face the player, hold tuning.LeapSlamWindupSeconds, then commit to the leap.
        // Same structure as UpdateUltimatePrepare. The energy was already spent in TryEnterLeapSlam.
        private void UpdateLeapSlamWindup()
        {
            _horizontalVelocity = Vector3.zero;
            FaceTarget(1f);

            if (_stateTimer < tuning.LeapSlamWindupSeconds)
            {
                return;
            }

            CommitLeapSlamLanding();
            ChangeState(BossState.LeapSlam);
        }

        // 2026-08-26, explicit user request ("玩家極近距離靠近武士時 容易躲避所有攻擊 如何解決" /
        // "有一招是踢擊 並且有擊退效果 利用他在偵測到玩家過近時就觸發，但必須達到範圍內持續2秒才踢擊
        // 給玩家一點輸出空間") - a real weapon swing's own arc has a minimum reach (see this
        // session's own BladeHitbox hit-window measurement history), so a player standing
        // point-blank could dodge every normal-pool attack forever with nothing to punish it. Fires
        // tooCloseAttack (wired to the SAME asset already in normalAttackPool, e.g. SpartanKick,
        // not a separate copy) through the ordinary BeginAttack path - no new BossState needed,
        // this just becomes a normal Attack that happens to have been forced rather than rolled by
        // PickAttack(). Reusing an attack that already carries knockback is deliberate: landing it
        // physically shoves the player back out of point-blank range, so no separate "create
        // distance" logic is needed - the punish IS the fix.
        private bool TryEnterTooCloseKick()
        {
            if (_tooCloseTimer < tuning.TooCloseDurationSeconds || tooCloseAttack == null)
            {
                return false;
            }
            // 2026-08-26, real playtested bug ("我認為是觸發踢擊條件時被當下其他動作占用導致被忽略")
            // - the timer itself only ever resets on distance leaving the zone or a successful
            // fire (see UpdateTooCloseTimer/the fire path below) - it is NEVER reset just because a
            // fire attempt got blocked here, so a blocked attempt keeps re-trying every subsequent
            // frame rather than being silently dropped. This log (once per threshold-crossing, not
            // spammed every blocked frame) makes that visible either way: if it never appears, the
            // timer itself is what's resetting (a distance/positioning issue); if it appears
            // repeatedly with the same reason, that confirms this exact block is the culprit.
            if (!_tooCloseThresholdLogged)
            {
                _tooCloseThresholdLogged = true;
                Log("TooCloseKick: threshold reached (state=" + CurrentState + ", currentAttack=" + (_currentAttack != null ? _currentAttack.AttackId : "none") + ")");
            }
            if (CurrentState == BossState.PostureBroken || CurrentState == BossState.HitReaction
                || CurrentState == BossState.Vanishing || CurrentState == BossState.DiveAttack
                || CurrentState == BossState.UltimateReposition || CurrentState == BossState.UltimatePrepare
                || CurrentState == BossState.UltimateAttack || CurrentState == BossState.DodgeCounter
                || CurrentState == BossState.Breakdance
                || CurrentState == BossState.LeapSlamWindup || CurrentState == BossState.LeapSlam)
            {
                return false; // blocked: special state - still too disruptive/rare to crudely cut off
            }
            if (_currentAttack == tooCloseAttack)
            {
                return false; // already mid this exact kick (e.g. rolled normally right before the timer expired) - don't restart it
            }
            // 2026-08-26, real playtested bug ("很明顯沒有觸發踢擊 動畫也沒出來" / "試著條件滿足時打斷
            // 當動作直接踢擊") - two separate fixes bundled here, both explicit user requests:
            //
            // 1) The AttackFinishedCommittableWindow() gate that used to sit here (only firing once
            // a normal-pool attack reached its interruptible point) is REMOVED - the punish now cuts
            // off whatever normal attack is mid-swing immediately once the timer expires, exactly as
            // requested ("打斷當動作直接踢擊"), not just during any-longer-tolerable gaps.
            //
            // 2) Real root cause of "animation never even appeared": ChangeState(next) no-ops if
            // CurrentState already equals next (see its own guard) - was a silent no-op when
            // interrupting one normal attack with another, since CurrentState is ALREADY Attack.
            // Now fixed centrally inside BeginAttack() itself (see that method's own comment) rather
            // than bounced through here - every caller gets the fix, not just this one.
            Log("TooCloseKick: forced (distance=" + HorizontalDistance().ToString("F2") + ", interrupted=" + (_currentAttack != null ? _currentAttack.AttackId : "none") + ")");
            _tooCloseTimer = 0f;
            _tooCloseThresholdLogged = false;
            MarkSpecialFired(); // spec §9.2 - a punish kick occupies the shared cooldown so it can't chain straight into a LeapSlam
            // A forced punish is a fresh top-level pick, not a combo continuation - see PickAttack's
            // own call site for why this resets (2026-08-26 combo-chain fix).
            _sweepUsedThisCombo = false;
            BeginAttack(tooCloseAttack);
            return true;
        }

        // ---------------------------------------------------------------- Per-state Update

        private void RunCurrentState()
        {
            switch (CurrentState)
            {
                case BossState.Dormant: UpdateDormant(); break;
                case BossState.Alert: UpdateAlert(); break;
                case BossState.Idle: UpdateIdle(); break;
                case BossState.Approach: UpdateApproach(); break;
                case BossState.GateWatch: UpdateGateWatch(); break;
                case BossState.ReturnHome: UpdateReturnHome(); break;
                case BossState.Attack: UpdateAttack(); break;
                case BossState.DodgeCounter: UpdateDodgeCounter(); break;
                case BossState.Breakdance: UpdateBreakdance(); break;
                case BossState.LeapSlamWindup: UpdateLeapSlamWindup(); break;
                case BossState.LeapSlam: UpdateLeapSlam(); break;
                case BossState.UltimateReposition: UpdateUltimateReposition(); break;
                case BossState.UltimatePrepare: UpdateUltimatePrepare(); break;
                case BossState.UltimateAttack: UpdateUltimateAttack(); break;
                case BossState.Vanishing: UpdateVanishing(); break;
                case BossState.DiveAttack: UpdateDiveAttack(); break;
                case BossState.HitReaction: UpdateHitReaction(); break;
                case BossState.PostureBroken: UpdatePostureBroken(); break;
                case BossState.Victory: UpdateVictory(); break;
                case BossState.Dead: UpdateDead(); break;
                case BossState.GettingUp: UpdateGettingUp(); break;
            }
        }

        private void UpdateDormant()
        {
            // 2026-08-29, user report ("丟失目標後返回時走路姿勢不正常") - the boss no longer WALKS
            // home in Dormant (the Dormant animator state is a static pose, not a locomotion
            // blend, so a moving Dormant boss foot-slid). A leash reset now snaps it straight to
            // its post (TryLeashReset), invisibly - the player is 30m+ away / out of the arena and
            // can't see it - so Dormant is always genuinely stationary again.
            _horizontalVelocity = Vector3.zero;

            if (target == null) return;
            if (HorizontalDistance() <= tuning.AlertRange)
            {
                _hasEngaged = true;
                ChangeState(BossState.Alert);
            }
        }

        // 2026-08-29, user report ("武士感覺警備範圍怪怪的 明明玩家離得很遠") - the boss gives up and
        // returns to its post once the PLAYER leaves the guard radius (measured from the post, not
        // the boss's chased-to position). leashRange 0 disables it. Skipped for terminal states
        // and the phase-transition visual (a scripted, uninterruptible beat).
        //
        // 2026-08-29 follow-up ("警備距離改為5m") - a player hovering right at the edge could flip
        // Dormant<->engaged frame to frame, so the player must be outside for leashGraceSeconds
        // continuous before it actually disengages.
        [SerializeField] private float leashGraceSeconds = 0.35f;
        private float _outsideLeashTimer;
        private float _gateWatchGiveUpTimer;

        // 2026-08-31, user report ("武士只會待在警備範圍 而不是像屁孩王一樣會追逐玩家到門口") - the
        // from-boss give-up in UpdateIdle / UpdateApproach was a flat AlertRange*1.5 (9m for 武士),
        // so the unconfined boss actually pursued LESS far than the confined 精怪, which walks all
        // the way to the doorway via GateWatch. Widen the from-boss give-up to at least leashRange
        // so the from-POST leash (TryLeashReset, with its own grace timer) is the real authority on
        // how far the boss chases - set 武士's leashRange large enough to reach the vehicle doorway
        // (~26m from its post) and it'll follow the player right out of 本地. Still bounded: keep
        // running past leashRange from the post and it heads home. AlertRange*1.5 stays the floor
        // for a boss with a tiny leash (kited-in-circles-near-the-post case).
        private float ChaseGiveUpDistance() => Mathf.Max(tuning.AlertRange * 1.5f, leashRange);

        private bool TryLeashReset()
        {
            if (target == null) return false;
            if (CurrentState == BossState.Dormant || CurrentState == BossState.Dead
                || CurrentState == BossState.Victory)
            {
                _outsideLeashTimer = 0f;
                return false;
            }

            // Already jogging back to the post - let UpdateReturnHome finish (it re-engages on its
            // own if the player re-enters AlertRange). Returning true blocks the rest of the
            // cascade so no LeapSlam/Breakdance/Ultimate fires mid-retreat.
            if (CurrentState == BossState.ReturnHome)
            {
                _outsideLeashTimer = 0f;
                return true;
            }

            // 2026-08-29 ("武士的飛空技能失效 只會待在原地做出像是重複動作") - never abort a committed
            // special / flight / ultimate mid-move: a leap or dive arcs AWAY from the post, so a
            // too-tight leash was cancelling it a frame after launch and dropping the boss back to
            // Dormant, over and over. Same special-state set TryEnterTooCloseKick refuses to
            // interrupt. The leash still applies from every ordinary state (Alert/Idle/Approach/
            // Attack/GateWatch) - it just lets an airborne attack finish first.
            if (CurrentState == BossState.Vanishing || CurrentState == BossState.DiveAttack
                || CurrentState == BossState.LeapSlamWindup || CurrentState == BossState.LeapSlam
                || CurrentState == BossState.UltimateReposition || CurrentState == BossState.UltimatePrepare
                || CurrentState == BossState.UltimateAttack || CurrentState == BossState.DodgeCounter
                || CurrentState == BossState.Breakdance || CurrentState == BossState.PostureBroken
                || CurrentState == BossState.HitReaction)
            {
                return false;
            }

            // 2026-08-29 ("一旦碰到邊界時不要直接傳回原本位置，而是判斷是否有目標在警備範圍內...一直在
            // 門口觀望著目標") - a 精怪 confined to 本地 (confineToArena) can't chase the player out the
            // doorway. The instant the player crosses out it switches to GateWatch and WALKS to the
            // boundary (it usually starts the chase far from the doorway - measuring "is the player
            // still close" from here would wrongly read as "gone" and snap it home immediately,
            // which was the 2026-08-29 "還是一碰到邊界就瞬間回到初始位置" report). The actual
            // give-up decision is deferred to UpdateGateWatch, which only fires it once the boss
            // has genuinely reached the wall AND the player has left watch range from there.
            if (confineToArena)
            {
                bool playerOutside = ArenaBounds.IsOutside(target.position, arenaCenterXZ, arenaHalfExtent);
                if (playerOutside)
                {
                    _outsideLeashTimer = 0f;
                    if (CurrentState != BossState.GateWatch)
                    {
                        Log("Arena: player left 本地 - walking to the gate to watch.");
                        ChangeState(BossState.GateWatch);
                    }
                    return true;
                }
                if (CurrentState == BossState.GateWatch)
                {
                    // player is back inside the arena - re-engage through the normal cascade
                    ChangeState(BossState.Idle);
                    return true;
                }
            }

            if (leashRange <= 0f)
            {
                return false;
            }

            // Ordinary distance leash - the player has walked the boss far from its guard post
            // (measured from the post, not the boss's chased-to position). leashGraceSeconds of
            // continuous overshoot before it actually disengages, so a player hovering right at
            // the edge can't flip Dormant<->engaged frame to frame.
            Vector3 fromPost = target.position - _homePosition;
            fromPost.y = 0f;
            if (fromPost.magnitude <= leashRange)
            {
                _outsideLeashTimer = 0f;
                return false;
            }
            _outsideLeashTimer += Time.deltaTime;
            if (_outsideLeashTimer < leashGraceSeconds)
            {
                return false;
            }

            _outsideLeashTimer = 0f;
            DisengageAndReturnHome($"player {fromPost.magnitude:F1}m from post (> {leashRange:F1})");
            return true;
        }

        // 2026-08-29 ("一直在門口觀望著目標") - a confined 精怪 whose target has slipped out the doorway.
        // Walk to the boundary along the line to the player (ApplyMotion's arena clamp stops it at
        // the wall), then hold and stare. Never attacks - TryLeashReset returns true every frame
        // this state is active, so the whole lower cascade (Ultimate / Vanish / LeapSlam /
        // TooCloseKick / DodgeCounter) is skipped. Re-engagement (player back inside) is
        // TryLeashReset's job; the give-up (player gone) is decided here, only after the boss has
        // actually reached the wall.
        private void UpdateGateWatch()
        {
            if (target == null)
            {
                DisengageAndReturnHome("target lost");
                return;
            }

            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;

            Vector3 probeAhead = transform.position
                + (toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized * 0.5f : Vector3.zero);
            bool atWall = ArenaBounds.IsOutside(probeAhead, arenaCenterXZ, arenaHalfExtent);

            if (!atWall && distance > 1.5f)
            {
                // still closing on the doorway - never give up while there's ground to cover
                MoveTowardTarget(tuning.WalkSpeed);
                FaceTarget(0.9f);
                _gateWatchGiveUpTimer = 0f;
                return;
            }

            // reached the boundary (or the player is right at the gap) - hold and stare
            _horizontalVelocity = Vector3.zero;
            FaceTarget(1f);

            float watchRange = gateWatchRange > 0.01f ? gateWatchRange : tuning.AlertRange;
            if (distance > watchRange)
            {
                _gateWatchGiveUpTimer += Time.deltaTime;
                if (_gateWatchGiveUpTimer >= gateWatchGiveUpSeconds)
                {
                    _gateWatchGiveUpTimer = 0f;
                    DisengageAndReturnHome($"player {distance:F1}m from the gate (> {watchRange:F1})");
                }
            }
            else
            {
                _gateWatchGiveUpTimer = 0f;
            }
        }

        // Instant place + rotate onto the guard post. Used for the final arrival snap of a
        // ReturnHome jog (exact pose, no residual drift), and by LeapSlam's own landing pin.
        private void SnapToHome()
        {
            bool wasEnabled = _controller != null && _controller.enabled;
            if (_controller != null) _controller.enabled = false;
            transform.SetPositionAndRotation(_homePosition, _homeRotation);
            if (_controller != null) _controller.enabled = wasEnabled;
        }

        // 2026-08-29, user report ("脫離戰鬥後他也沒跑回原位" + "突然他就像我衝過來") - the single
        // "the fight is over, go home" entry point. Clears the engagement latch and every queued
        // special so a boss the player has walked away from stops silently counting down to a
        // teleport-slam, then routes to ReturnHome (jog back, see UpdateReturnHome). Every
        // disengage path (Idle's range check, the distance leash, GateWatch giving up) goes
        // through here now instead of ChangeState(Dormant)/SnapToHome directly.
        private void DisengageAndReturnHome(string reason)
        {
            Log("Disengage (" + reason + ") - returning to post at a run.");
            _hasEngaged = false;
            _combatTimeAccumulated = 0f;
            _breakdanceTimeAccumulated = 0f;
            _leapSlamTimeAccumulated = 0f;
            _periodicSlamTimeAccumulated = 0f;
            _periodicSlamPending = false;
            _globalRestUntil = -999f;
            CancelAllPending();
            leapSlamEnergy?.Consume();
            ultimateEnergy?.Consume();
            _horizontalVelocity = Vector3.zero;
            _verticalVelocity = 0f;
            ChangeState(BossState.ReturnHome);
        }

        // 2026-08-29, user request ("脫離追擊範圍後 要使用跑步歸位"). Runs the guard back to its post
        // playing the Running blend tree (MovementSpeed = CurrentHorizontalSpeed, driven below),
        // instead of the old instant teleport. Re-engages immediately if the player wanders back
        // into AlertRange during the jog; on arrival snaps the exact pose and drops to Dormant.
        private void UpdateReturnHome()
        {
            // Player came back while we were retreating - straight back into the fight.
            if (target != null && HorizontalDistance() <= tuning.AlertRange)
            {
                _horizontalVelocity = Vector3.zero;
                _hasEngaged = true;
                ChangeState(BossState.Alert);
                return;
            }

            Vector3 toHome = _homePosition - transform.position;
            toHome.y = 0f;
            float distance = toHome.magnitude;

            if (distance <= 0.4f)
            {
                _horizontalVelocity = Vector3.zero;
                SnapToHome();
                ChangeState(BossState.Dormant);
                return;
            }

            // 2026-08-31 - path around obstacles on the jog home too (the boss may have chased the
            // player out through a doorway and now has geometry between it and its post). Facing
            // follows the actual move direction here, not a target, so both use pathDir.
            Vector3 straightDir = toHome / distance;
            Vector3 pathDir = _pathFollower != null ? _pathFollower.SteeringDirection(_homePosition) : straightDir;
            if (pathDir.sqrMagnitude < 0.0001f) pathDir = straightDir;
            _horizontalVelocity = pathDir * tuning.RunSpeed;

            Quaternion desired = Quaternion.LookRotation(pathDir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, desired,
                tuning.RotationSpeedDegrees * Time.deltaTime);
        }

        private void UpdateAlert()
        {
            _horizontalVelocity = Vector3.zero;
            FaceTarget(1f);
            if (_stateTimer >= 0.3f)
            {
                ChangeState(BossState.Idle);
            }
        }

        private void UpdateIdle()
        {
            _horizontalVelocity = Vector3.zero;
            FaceTarget(0.5f);

            _decisionTimer -= Time.deltaTime;
            if (_decisionTimer > 0f)
            {
                return;
            }
            _decisionTimer = tuning.RollDecisionInterval(Phase, Random);

            if (target == null) return;
            if (HorizontalDistance() > ChaseGiveUpDistance())
            {
                DisengageAndReturnHome($"player {HorizontalDistance():F1}m away, left combat range");
                return;
            }

            // 2026-09-02 - a lunge left the boss point-blank; re-open the gap (Approach's own
            // too-close branch backs it out to AttackStandoffFloor) before picking the next attack,
            // so it stops cycling attacks from inside its own kick zone.
            if (HorizontalDistance() < AttackStandoffFloor)
            {
                ChangeState(BossState.Approach);
                return;
            }

            // 2026-08-26, explicit user request (Boss AI spec, section 五 - "全域休息時間") - during
            // the rest window the boss must not even consider a new attack ("休息期間...禁止再次攻
            // 擊"), but everything else in this method (FaceTarget above, the Approach re-entry
            // below) still runs normally - "保持架勢、面向玩家或緩慢調整位置".
            if (Time.time < _globalRestUntil)
            {
                if (HorizontalDistance() > AttackReadinessDistance())
                {
                    ChangeState(BossState.Approach);
                }
                return;
            }

            BossAttackDefinition candidate = PickAttack();
            if (candidate != null)
            {
                // 2026-08-26, explicit user request ("希望變成連砍") - _sweepUsedThisCombo used to
                // never reset anywhere at all once set, so TryRollSweepDerivation's chain-into-a-
                // follow-up-attack mechanic could only ever fire ONCE per boss in its entire
                // lifetime, not once per combo. This is the actual "fresh top-level attack, not a
                // combo continuation" entry point (as opposed to EndAttack's own BeginAttack(derived)
                // call, which deliberately does NOT reset this - a derived attack chaining into a
                // FURTHER derived attack would need its own opt-in, not happen automatically), so
                // resetting here makes each new PickAttack() roll eligible for its own chain again.
                _sweepUsedThisCombo = false;
                BeginAttack(candidate);
                return;
            }

            // Defensive backstop for the same bug class the AttackReadinessDistance fix above
            // targets (see its own comment) - if every pool attack is unavailable for a reason
            // that has nothing to do with distance (e.g. all three happen to be on cooldown at
            // once), re-entering Approach would immediately satisfy its own "close enough" check
            // again (nothing to actually walk toward) and bounce straight back to Idle, forever
            // - console-spamming every single state-change log along the way. Only re-enter
            // Approach if there's real distance left to close; otherwise just hold here and
            // keep facing the player (FaceTarget above already does that) until the next
            // decision tick, when a cooldown will likely have ticked down.
            if (HorizontalDistance() > AttackReadinessDistance())
            {
                ChangeState(BossState.Approach);
            }
        }

        private void UpdateApproach()
        {
            _decisionTimer -= Time.deltaTime;

            float distance = HorizontalDistance();

            // 2026-08-29, user report ("脫離戰鬥後他也沒跑回原位") - Approach had no give-up of its
            // own, so a player who just kept moving kept the boss in Approach indefinitely: it
            // never caught up to reach Idle, and Idle's own "> AlertRange*1.5 => disengage" check
            // was the only distance-based bail-out. Mirror that exact check here - Idle already
            // only routes the boss into Approach for gaps under AlertRange*1.5, so anything past
            // that means the player has broken off and the boss should head home (the from-post
            // leashRange is the coarser backstop; this is the "player kited me in circles near my
            // own post" case the leash can't see).
            if (target != null && distance > ChaseGiveUpDistance())
            {
                DisengageAndReturnHome($"player {distance:F1}m away while approaching, left combat range");
                return;
            }

            bool sprinting = _sprinting;

            float moveSpeed = ResolveMoveSpeed();
            float readinessDistance = AttackReadinessDistance();

            if (distance < AttackStandoffFloor)
            {
                // 2026-09-02 (re-added, less aggressive than 續 39's version) - a lunging attack
                // (ContinuousThrust's 前墊步, TwinStrike's root motion) or a player hugging in has
                // left the boss inside its own too-close kick zone, where it just cycles point-blank
                // attacks and the forced kick keeps cutting them off ("正式模式看不到動作 9"). Back
                // off to the standoff floor (~2.2m, still well outside the 1.6m kick circle and
                // inside every in-place attack's blade reach) before setting up again.
                MoveAwayFromTarget(moveSpeed);
                FaceTarget(0.8f);
            }
            else if (distance > readinessDistance + tuning.ApproachDecelerationDistance)
            {
                MoveTowardTarget(moveSpeed);
                FaceTarget(0.6f);
            }
            else if (distance > readinessDistance)
            {
                // 2026-08-24, bug report ("老是在伸懶腰沒有追擊玩家") - Lerp'ing speed all the way
                // to 0 as distance approaches readinessDistance is a real asymptote, not just a
                // slow finish: each frame's movement is speed*deltaTime, and speed itself shrinks
                // with the remaining gap, so the gap shrinks geometrically and mathematically
                // never actually reaches readinessDistance in finite time. Confirmed by direct
                // simulation - distance converged to ~1.52m (readinessDistance was 1.4m) and
                // speed decayed to 0.001 m/s, functionally frozen, for 4+ real seconds before
                // floating-point drift finally tipped it over. A minimum speed floor guarantees
                // real, bounded-time progress instead of an infinite crawl.
                float t = Mathf.InverseLerp(readinessDistance + tuning.ApproachDecelerationDistance, readinessDistance, distance);
                float minSpeed = moveSpeed * 0.35f;
                MoveTowardTarget(Mathf.Lerp(moveSpeed, minSpeed, t));
                FaceTarget(0.8f);
            }
            else
            {
                // Spec: stop, face target, hold a readable 0.2-0.35s buffer BEFORE attacking -
                // never zero-startup straight out of a run.
                _horizontalVelocity = Vector3.zero;
                FaceTarget(1f);
                if (_stateTimer >= _attackReadinessBuffer)
                {
                    ChangeState(BossState.Idle); // Idle's own decision timer picks the actual attack
                    return;
                }
            }

            if (sprinting && distance <= tuning.SprintBrakeDistance)
            {
                _sprinting = false; // handled fully inside UpdateApproachSprintBrake below via flag
            }

            if (_decisionTimer <= 0f)
            {
                _decisionTimer = tuning.RollDecisionInterval(Phase, Random);

                // 2026-08-29, user request ("有連續位移的可以不用綁死近戰攻擊距離") - a useRootMotion
                // attack drives its own lunge, so let the boss commit to one straight from the
                // approach instead of first walking into melee range (where its MinDistance may not
                // even be satisfied any more). Only while still closing (distance > readiness) and
                // not inside the post-attack rest window; PickAttackFiltered still enforces the
                // move's own MaxDistance / angle / cooldown.
                // 2026-09-02, user request ("這幾招由於都有位移,改為玩家距離較遠時才觸發,作為快速接近
                // 玩家手段") - 前刺/扭轉前劈/翻滾撲擊 are useRootMotion gap-closers with a high MinDistance,
                // so PickAttack() at the close standoff never rolls them - only this approach path does.
                if (target != null && distance > readinessDistance && Time.time >= _globalRestUntil)
                {
                    // 2026-09-02 - a code-driven lunge (LungeDistanceFromTargetGap, e.g.
                    // ScissorTakedown after its spinning clip broke useRootMotion) closes its own gap
                    // too, so it's just as valid an approach-time pick as a useRootMotion charge.
                    BossAttackDefinition gapCloser = PickAttackFiltered(a => a.UseRootMotion || a.LungeDistanceFromTargetGap);
                    if (gapCloser != null)
                    {
                        Log($"Approach gap-closer: {gapCloser.AttackId} (dist={distance:F2}).");
                        _sweepUsedThisCombo = false;
                        BeginAttack(gapCloser);
                        return;
                    }
                }
            }
        }

        private void UpdateAttack()
        {
            if (_currentAttack == null)
            {
                ChangeState(BossState.Idle);
                return;
            }

            float normalized = AnimatorNormalizedTime();

            // 2026-08-29, user report ("有些攻擊打不倒玩家") - the boss used to plant its feet the
            // instant an attack started (matching EnemyAI's convention), so any small player
            // backstep during the 0.3-0.7s wind-up pushed them past the ~1m reach and the swing
            // whiffed with the boss standing still. Allow a capped creep toward the target while
            // the attack is still in its tracking phase - enough to reel a step-back back in, not
            // a full chase. useRootMotion attacks drive their own translation, leave those alone.
            BossAttackMotionProfile motion = _currentAttack.AttackMotion;
            if (motion != null && (motion.HasDisplacement || _currentAttack.LungeDistanceFromTargetGap) && !_currentAttack.UseRootMotion)
            {
                // spec 5A - drive the gameplay root along the locked commit direction on the curve,
                // so it tracks a lunge whose forward travel is baked into the clip's hips. A Recoil
                // parry (item 1) freezes _attackMotionApplied so the rest of the slide is dropped.
                // 2026-09-02 - LungeDistanceFromTargetGap swaps forwardDistance for the runtime gap
                // computed at commit (see BeginAttack).
                float lungeDistance = _attackMotionDistanceOverride >= 0f
                    ? _attackMotionDistanceOverride
                    : motion.forwardDistance;
                float targetApplied = _attackMotionHalted
                    ? _attackMotionApplied
                    : motion.TravelFraction01(normalized) * lungeDistance;
                float step = targetApplied - _attackMotionApplied;
                _attackMotionApplied = targetApplied;
                _horizontalVelocity = (step > 0f && Time.deltaTime > 1e-5f)
                    ? _attackMotionDir * (step / Time.deltaTime)
                    : Vector3.zero;
            }
            else if (target != null
                && !_currentAttack.UseRootMotion
                && normalized < _currentAttack.TrackingDropNormalizedTime
                && HorizontalDistance() > _currentAttack.MaxDistance * 0.85f)
            {
                Vector3 toTarget = target.position - transform.position;
                toTarget.y = 0f;
                _horizontalVelocity = toTarget.sqrMagnitude > 0.0001f
                    ? toTarget.normalized * (tuning.WalkSpeed * 0.5f)
                    : Vector3.zero;
            }
            else
            {
                _horizontalVelocity = Vector3.zero; // ground melee plants feet
            }

            // 2026-09-02 - cancel the Hips XZ drift a Meshy clip bakes into its body pose, so the
            // visible body stays anchored and only the code-driven lunge above actually moves the
            // boss. Delta-based (per-frame increment), so a slightly-off baseline can't accumulate.
            if (_currentAttack.CancelClipBodyDrift && _hipsBone != null)
            {
                Vector3 nowOffset = Vector3.ProjectOnPlane(_hipsBone.position - transform.position, Vector3.up);
                if (!_clipDriftBaselineSet)
                {
                    _clipDriftBaselineXZ = nowOffset;
                    _clipDriftBaselineSet = true;
                }
                Vector3 drift = nowOffset - _clipDriftBaselineXZ;
                Vector3 driftStep = drift - _clipDriftCompensatedXZ;
                _clipDriftCompensatedXZ = drift;
                if (Time.deltaTime > 1e-5f)
                {
                    _horizontalVelocity -= driftStep / Time.deltaTime;
                }
            }

            float trackAmount = normalized < _currentAttack.TrackingDropNormalizedTime
                ? _currentAttack.StartupTracking
                : _currentAttack.LateTracking;
            FaceTarget(trackAmount, _currentAttack.FacingYawOffsetDegrees);

            // 2026-09-02 (ContinuousThrust) - lean the whole visual forward/down so a tall boss's
            // blade hitbox drops to a grounded player's chest. Applied AFTER FaceTarget (pure yaw)
            // as a local pitch; eased 0 -> full by nt 0.15, held, eased back to 0 from nt 0.78.
            // The upright CharacterController is untouched.
            if (_currentAttack.AttackPitchDegrees > 0.01f)
            {
                float w = normalized < 0.15f ? Mathf.SmoothStep(0f, 1f, normalized / 0.15f)
                        : normalized > 0.78f ? Mathf.SmoothStep(1f, 0f, (normalized - 0.78f) / 0.22f)
                        : 1f;
                transform.rotation *= Quaternion.Euler(_currentAttack.AttackPitchDegrees * w, 0f, 0f);
            }

            UpdateHitWindows(_currentAttack, normalized);
            if (_currentAttack.CommandGrab) TryResolveCommandGrab(_currentAttack, normalized);
            ApplyRootMotionIfEnabled(_currentAttack, normalized);

            TryRollSweepDerivation(normalized);

            // 2026-08-30, user report (屁孩王: "攻擊後搖太長 / 技能銜接不夠快") - end the attack early
            // once its last hit window has closed, cutting the clip's return-to-stance recovery
            // tail. Only when tuning opts in (AttackRecoveryTailCutNormalized < ~1) and there IS a
            // pending derived attack OR the cut point is genuinely past the strike. Never before
            // an active window (that's still the swing).
            float lastEnd = LastHitWindowEndNormalized(_currentAttack);
            if (lastEnd > 0f
                && normalized >= lastEnd + tuning.AttackRecoveryTailCutNormalized
                && normalized < 0.97f
                && !IsInsideAnyActiveWindow())
            {
                EndAttack();
                return;
            }

            if (IsAttackAnimationFinished(normalized))
            {
                EndAttack();
            }
        }

        private static float LastHitWindowEndNormalized(BossAttackDefinition attack)
        {
            float last = 0f;
            if (attack?.HitWindows != null)
            {
                foreach (var w in attack.HitWindows)
                {
                    if (w != null) last = Mathf.Max(last, w.endNormalized);
                }
            }
            return last;
        }

        // 2026-09-02, user request (屁孩王 ScissorTakedown: "倒立用雙腳內扣的中間動作...朝向玩家的頭部
        // 去鎖定,如果有接觸到就把玩家跩起來然後反向甩出擊飛"). The clip inverts the boss so its scissoring
        // feet end up ~2m up while a grounded player's hurtbox tops out lower - a bone-parented foot
        // hitbox (0.35m sphere) that lands <1m away simply never overlaps, so the collider path can't
        // land this. Command-grab attacks opt out of the hit-window model: ONE horizontal proximity
        // test at the clinch moment, and if the player is within reach, apply the asset's own
        // damage / poise / knockback / launch numbers directly. Fires at most once per attack.
        private bool _commandGrabResolved;
        private void TryResolveCommandGrab(BossAttackDefinition attack, float normalized)
        {
            if (_commandGrabResolved || normalized < attack.CommandGrabNormalized) return;
            _commandGrabResolved = true;
            if (target == null) return;

            Vector3 flat = target.position - transform.position;
            flat.y = 0f;
            float gap = flat.magnitude;
            if (gap > attack.CommandGrabRadius)
            {
                Log($"CommandGrab {attack.AttackId}: whiffed (gap={gap:F2} > {attack.CommandGrabRadius:F2}).");
                return;
            }

            Vector3 dir = flat.sqrMagnitude > 1e-4f ? flat.normalized : transform.forward;

            float healthDamage = attack.BaseHealthDamage;
            if (attack.HealthDamageIsPercentOfTargetMax)
            {
                var targetHealth = target.GetComponentInParent<Health>();
                if (targetHealth != null) healthDamage = targetHealth.MaxHealth * (attack.BaseHealthDamage / 100f);
            }

            var damageable = target.GetComponentInParent<IDamageable>();
            damageable?.ApplyDamage(new DamageInfo(healthDamage, target.position, dir, gameObject, attack.BasePoiseDamage));

            var knockback = target.GetComponentInParent<IKnockbackReceiver>();
            knockback?.ApplyKnockback(dir, attack.KnockbackForce, attack.LaunchesTarget);

            _attackLandedAnyHit = true;
            Log($"CommandGrab {attack.AttackId}: caught target (gap={gap:F2}) dmg={healthDamage:F0} poise={attack.BasePoiseDamage:F0} knock={attack.KnockbackForce:F1} launch={attack.LaunchesTarget}.");
        }

        private void UpdateDodgeCounter()
        {
            _horizontalVelocity = Vector3.zero;
            float normalized = AnimatorNormalizedTime();

            _dodgeIframesActive = normalized >= tuning.DodgeIframeStartNormalized
                                   && normalized <= tuning.DodgeIframeEndNormalized;
            if (health != null)
            {
                health.SetInvulnerable(this, _dodgeIframesActive);
            }

            if (dodgeCounterAttack != null)
            {
                UpdateHitWindows(dodgeCounterAttack, normalized);
            }

            if (IsAttackAnimationFinished(normalized))
            {
                if (health != null) health.SetInvulnerable(this, false);
                CloseAllHitboxes();
                ChangeState(BossState.Idle);
            }
        }

        // Runs exactly like a normal attack (UpdateAttack above) - real hit windows against
        // breakdanceAttack, plants feet, exits once the clip itself finishes. The only difference
        // from a pool attack is how it got here (timer, not PickAttack) and that it doesn't touch
        // _currentAttack/cooldown/repeat bookkeeping (those are normalAttackPool-only concerns).
        private void UpdateBreakdance()
        {
            if (breakdanceAttack == null) { ChangeState(BossState.Idle); return; }

            _horizontalVelocity = Vector3.zero;
            float normalized = AnimatorNormalizedTime();
            // Same startup/late tracking split UpdateAttack uses for normal-pool attacks - reads
            // straight off breakdanceAttack's own fields rather than a hardcoded number, so it's
            // tunable in the Inspector exactly like every other attack's tracking.
            float trackAmount = normalized < breakdanceAttack.TrackingDropNormalizedTime
                ? breakdanceAttack.StartupTracking
                : breakdanceAttack.LateTracking;
            FaceTarget(trackAmount);

            UpdateHitWindows(breakdanceAttack, normalized);

            if (IsAttackAnimationFinished(normalized))
            {
                CloseAllHitboxes();
                ChangeState(BossState.Idle);
            }
        }

        // 2026-08-27, explicit user request ("落地時請直接鎖定玩家，並且落下的期間全程具有攻擊幀 範
        // 圍大") - UpdateHitWindows against leapSlamAttack's own wide hit window (spans the whole
        // measured fall, not just the landing instant - see that asset's own designNotes) does the
        // "全程攻擊幀" part exactly like any other attack.
        // 2026-08-28, explicit user request ("Wushi_LeapSlam 落地前我想要讓他能追蹤玩家位置 然後落地")
        // - REVERSES the original "landing xz + facing committed ONCE, never re-aimed mid-leap"
        // rule this comment used to describe. The homing block near the top of UpdateLeapSlam now
        // steers the horizontal toward the player's live position (and re-faces them) until
        // tuning.LeapSlamTrackUntilNormalized, then locks. CommitLeapSlamLanding's takeoff teleport
        // stays as the coarse first placement; the homing only corrects for the player moving
        // during the airborne arc. Kept below LeapSlamHeightFallEndNormalized so it never fights
        // the pinned-landing tail.
        // 2026-08-27, explicit user request ("不能跳很高嗎 至少讓玩家看不到的高度") - drives
        // _verticalVelocity directly from an explicit height curve (computed each frame from the
        // DELTA between this frame's and last frame's target extra-height, divided by deltaTime) so
        // ApplyMotion's own existing _controller.Move() call carries the root Transform through a
        // real arc reaching leapSlamExtraHeight world units above ground, layered on top of the
        // clip's own much smaller baked bone motion (~11 units - see leapSlamAttack's designNotes).
        // Velocity-based rather than directly setting transform.position every frame so it stays
        // Move()-consistent (proper collision resolution) rather than teleporting through geometry.
        // Telescoping tracker for the script height arc - guaranteeing the root returns to exactly
        // the locked landing Y regardless of frame rate (the guard on this in UpdateLeapSlam makes
        // sure the arc's final descent delta is applied, not stranded, on a frame skip).
        private float _leapSlamPrevExtraHeight;
        // 2026-08-27, playtested bug (per-frame probe caught it: a LeapSlam that entered with the
        // Animator still reporting the OUTGOING clip's stale normalizedTime ~0.30 computed a full
        // ~30-unit targetExtraHeight and slammed the root 30 units up in ONE frame). The height arc
        // must not run until we've positively seen normalizedTime come back down near 0 at least
        // once - i.e. confirmed the LeapSlam clip itself is genuinely playing from the start, not
        // the crossfade showing us leftover data from Locomotion/Attack. Same stale-data hazard
        // MinStateTimeBeforeFinishCheck guards for IsAttackAnimationFinished.
        private bool _leapSlamClipConfirmed;
        // 2026-08-28, user request ("武士飛空後著地y座標從0.623改為0.5") - the exact world Y the
        // landing/stand-up tail is pinned to, locked at teleport time (ground-hit +
        // tuning.LeapSlamLandingGroundedOffset). Once the arc is done UpdateLeapSlam pins the
        // transform here directly (CharacterController toggled off for the set, since a landing Y
        // at/below the natural flush-capsule rest can't be reached through _controller.Move -
        // ground collision stops it short) and ApplyMotion skips its Move for the rest of the state.
        private float _leapSlamLandingY;
        private bool _leapSlamHolding;
        // 2026-08-28, playtested bug ("飛空前到飛空後這一整段不該有攻擊幀") - latched true the moment
        // normalizedTime clears the LandingAOE window's end, so a "% 1f" wrap during the stand-up
        // tail can never re-open it. Combined with the _leapSlamClipConfirmed gate on the OTHER end
        // (crossfade-in stale data), the hitbox is live ONLY during the genuine descent.
        private bool _leapSlamHitWindowsDone;
        // 2026-08-28, explicit user request ("落地前追蹤玩家位置 然後落地") - latched true once
        // normalizedTime passes tuning.LeapSlamTrackUntilNormalized, from which point the landing xz
        // is frozen and the rest of the descent is a committed straight drop. Before that, UpdateLeapSlam
        // steers _horizontalVelocity toward the player's live position every frame.
        private bool _leapSlamLandingLocked;

        private void UpdateLeapSlam()
        {
            if (leapSlamAttack == null) { ChangeState(BossState.Idle); return; }

            _horizontalVelocity = Vector3.zero;
            float normalized = AnimatorNormalizedTime();

            // 2026-08-29, 屁孩王 (leapSlamTeleportToLanding=0, user: "沒有鎖定玩家位置飛過去") - the
            // shared path below steers the horizontal from a normalizedTime-derived speed estimate
            // that a crossfade blend makes wildly wrong for the first frames, stalling the travel.
            // The no-teleport pounce is its own self-contained, wall-time-driven path instead.
            if (!tuning.LeapSlamTeleportToLanding)
            {
                UpdateLeapSlamPounce();
                return;
            }

            if (!_leapSlamClipConfirmed
                && normalized < tuning.LeapSlamHeightPeakNormalized
                && (normalized <= tuning.LeapSlamHeightRiseStartNormalized || _stateTimer >= 0.2f))
            {
                // Confirmed: normalizedTime is now genuinely low (either right at the clip's start,
                // or at least sane-and-pre-peak after enough real frames that it can't still be
                // stale outgoing-clip data). Safe to let the height arc drive from here.
                _leapSlamClipConfirmed = true;
            }

            // 2026-08-28, explicit user request ("Wushi_LeapSlam 落地前我想要讓他能追蹤玩家位置 然後
            // 落地") - reverses this method's earlier "landing xz + facing committed ONCE, no
            // mid-leap homing" rule (CommitLeapSlamLanding still teleports Wushi roughly over the
            // player at leap start; this corrects for the player dodging DURING the ~0.5s arc).
            // Only while the clip is confirmed, still airborne, and before the lock time: steer
            // _horizontalVelocity at the player's live xz (short by tuning.LeapSlamLandingOffset,
            // same as CommitLeapSlamLanding) so the slam lands where the player IS. Move only as
            // fast as needed to close the gap by the lock time, capped by LeapSlamMaxTrackSpeed.
            // The height arc pins the transform after LeapSlamHeightFallEndNormalized and
            // ApplyMotion stops Move()ing, so this must finish before then (the tuning tooltip
            // enforces LeapSlamTrackUntilNormalized < that).
            bool canHomeThisFrame = _leapSlamClipConfirmed && !_leapSlamLandingLocked && target != null
                && normalized < tuning.LeapSlamTrackUntilNormalized;
            if (canHomeThisFrame)
            {
                Vector3 backToBoss = transform.position - target.position;
                backToBoss.y = 0f;
                Vector3 offsetDir = backToBoss.sqrMagnitude > 0.0001f
                    ? backToBoss.normalized
                    : (transform.forward.sqrMagnitude > 0.0001f ? transform.forward.normalized : Vector3.forward);
                Vector3 desired = target.position + offsetDir * tuning.LeapSlamLandingOffset;

                Vector3 toDesired = desired - transform.position;
                toDesired.y = 0f;
                float gap = toDesired.magnitude;
                if (gap > 0.02f)
                {
                    float totalSeconds = normalized > 0.02f ? _stateTimer / normalized : 1.5f;
                    float secondsToLock = Mathf.Max(0.05f,
                        (tuning.LeapSlamTrackUntilNormalized - normalized) * totalSeconds);
                    float speed = Mathf.Min(gap / secondsToLock, tuning.LeapSlamMaxTrackSpeed);
                    _horizontalVelocity = toDesired.normalized * speed;
                }

                // Keep the pinned landing Y honest for wherever the homing is now heading.
                Vector3 probeFrom = new Vector3(desired.x, transform.position.y + 50f, desired.z);
                if (Physics.Raycast(probeFrom, Vector3.down, out var trackHit, 200f, ~0, QueryTriggerInteraction.Ignore)
                    && !trackHit.collider.transform.IsChildOf(transform))
                {
                    _leapSlamLandingY = trackHit.point.y + tuning.LeapSlamLandingGroundedOffset;
                }

                FaceTarget(1f);
            }
            else if (_leapSlamClipConfirmed && !_leapSlamLandingLocked
                     && normalized >= tuning.LeapSlamTrackUntilNormalized)
            {
                // Past the tracking window - freeze the landing spot; the rest of the descent is a
                // committed straight drop (the player's last-instant dodge window). Before the clip
                // is confirmed neither branch runs, so an early stale-normalizedTime frame can't
                // latch this prematurely.
                _leapSlamLandingLocked = true;
                Log("LeapSlam: landing locked at nt " + normalized.ToString("F2")
                    + " (dist to player " + HorizontalDistance().ToString("F2") + ")");
            }
            float targetExtraHeight = _leapSlamClipConfirmed ? ComputeLeapSlamExtraHeight(normalized) : 0f;
            if (targetExtraHeight > 0f || _leapSlamPrevExtraHeight > 0f)
            {
                // Arc is lifting the root, or still has residual height to bleed off - drive
                // _verticalVelocity from the exact frame-to-frame height delta. The telescoping
                // sum is exact regardless of frame rate; the guard on _leapSlamPrevExtraHeight
                // guarantees the last (possibly large, on a frame skip) descent delta is applied.
                _leapSlamHolding = false;
                float deltaHeight = targetExtraHeight - _leapSlamPrevExtraHeight;
                _verticalVelocity = deltaHeight / Mathf.Max(Time.deltaTime, 0.0001f);
                _leapSlamPrevExtraHeight = targetExtraHeight;
            }
            else
            {
                // Arc done (or not started yet) - pin the transform to the locked landing Y and let
                // ApplyMotion skip its Move for the rest of the state. Toggling the CharacterController
                // for the set because that Y can sit at/below the natural flush-capsule rest, which
                // _controller.Move (ground collision) can't reach and would creep back up from.
                _leapSlamHolding = true;
                _leapSlamPrevExtraHeight = 0f;
                _verticalVelocity = 0f;
                if (Mathf.Abs(transform.position.y - _leapSlamLandingY) > 0.0005f)
                {
                    _controller.enabled = false;
                    Vector3 p = transform.position;
                    p.y = _leapSlamLandingY;
                    transform.position = p;
                    _controller.enabled = true;
                }
            }

            // 2026-08-28, playtested bug ("飛空前到飛空後這一整段不該有攻擊幀") - the LandingAOE hitbox
            // was opening OUTSIDE its own nt 0.32-0.56 window: (a) during the crossfade-in,
            // AnimatorNormalizedTime() briefly reports the OUTGOING Locomotion clip's stale
            // normalizedTime, which can land inside the window while the boss is still grounded/rising;
            // (b) after the slam, the non-looping clip's normalizedTime passes 1 and "% 1f" wraps back
            // through the window during the held-landing / stand-up tail. Fix both ends: only run hit
            // windows while _leapSlamClipConfirmed (kills (a)), and latch them permanently shut for
            // this leap the instant normalized clears the last window (kills (b)).
            if (_leapSlamClipConfirmed && !_leapSlamHitWindowsDone)
            {
                UpdateHitWindows(leapSlamAttack, normalized);

                float lastWindowEnd = 0f;
                if (leapSlamAttack.HitWindows != null)
                {
                    foreach (var hw in leapSlamAttack.HitWindows)
                    {
                        lastWindowEnd = Mathf.Max(lastWindowEnd, hw.endNormalized);
                    }
                }
                if (normalized >= lastWindowEnd && normalized < 0.98f)
                {
                    _leapSlamHitWindowsDone = true;
                    CloseAllHitboxes();
                }
            }

            if (IsAttackAnimationFinished(normalized))
            {
                CloseAllHitboxes();
                ChangeState(BossState.Idle);
            }
        }

        // 2026-08-29, 屁孩王's flying pounce (leapSlamTeleportToLanding=0). Self-contained and
        // driven off _stateTimer (wall time), NOT the clip's normalizedTime - a crossfade reports
        // stale/blended nt for the first frames, which made the shared path's nt-derived homing
        // speed near-zero early and the boss "沒有鎖定玩家位置飛過去". Horizontal: re-aim at the
        // player's live xz, closing the gap to arrive by leapSlamFlightSeconds, then a committed
        // straight drop. Vertical: a simple parabola to leapSlamExtraHeight peaking at the
        // half-way point, back to the ground by leapSlamFlightSeconds. Hit window + exit are
        // _stateTimer-relative too (a crossfade's stale normalizedTime was closing the window /
        // aborting the whole state before the slam ever landed - "沒有攻擊到玩家").
        private void UpdateLeapSlamPounce()
        {
            float flight = Mathf.Max(0.2f, tuning.LeapSlamFlightSeconds);
            float t = _stateTimer;

            // --- horizontal: fly to the player, or (past the flight window) hold for the drop
            if (!_leapSlamLandingLocked && t < flight && target != null)
            {
                Vector3 backToBoss = transform.position - target.position;
                backToBoss.y = 0f;
                Vector3 offsetDir = backToBoss.sqrMagnitude > 0.0001f
                    ? backToBoss.normalized
                    : (transform.forward.sqrMagnitude > 0.0001f ? transform.forward.normalized : Vector3.forward);
                Vector3 desired = target.position + offsetDir * tuning.LeapSlamLandingOffset;

                Vector3 toDesired = desired - transform.position;
                toDesired.y = 0f;
                float gap = toDesired.magnitude;
                float secondsLeft = Mathf.Max(0.05f, flight - t);
                if (gap > 0.05f)
                {
                    float speed = Mathf.Min(gap / secondsLeft, tuning.LeapSlamMaxTrackSpeed);
                    _horizontalVelocity = toDesired.normalized * speed;
                }
                FaceTarget(1f);

                // Landing Y: raycast down at the aim point, taking the CLOSEST hit that isn't one
                // of our own colliders OR the player's. A plain Raycast at an aim point this close
                // to the player would otherwise hit the top of their CharacterController capsule
                // and pin the boss at head height (the old "浮空" bug that leapSlamLandingOffset
                // used to dodge by staying 2m away - which is exactly why the slam never landed on
                // the player: "落點永遠沒有在玩家身上").
                Vector3 probeFrom = new Vector3(desired.x, transform.position.y + 50f, desired.z);
                var probeHits = Physics.RaycastAll(probeFrom, Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore);
                float bestDist = float.MaxValue;
                foreach (var ph in probeHits)
                {
                    if (ph.distance >= bestDist) continue;
                    if (ph.collider.transform.IsChildOf(transform)) continue;
                    if (target != null && (ph.collider.transform == target || ph.collider.transform.IsChildOf(target))) continue;
                    bestDist = ph.distance;
                    _leapSlamLandingY = ph.point.y + tuning.LeapSlamLandingGroundedOffset;
                }
            }
            else if (!_leapSlamLandingLocked)
            {
                _leapSlamLandingLocked = true;
                Log($"LeapSlam pounce: flight done at t={t:F2}s, dist to player {HorizontalDistance():F2}.");
            }

            // --- vertical: parabola up to leapSlamExtraHeight, driven through _verticalVelocity by
            // the frame-to-frame height delta so ApplyMotion's Move carries a real arc (same
            // technique as the shared ComputeLeapSlamExtraHeight path).
            float apex = flight * 0.5f;
            float targetHeight;
            if (t >= flight) targetHeight = 0f;
            else if (t < apex) targetHeight = Mathf.Lerp(0f, tuning.LeapSlamExtraHeight, t / Mathf.Max(0.01f, apex));
            else targetHeight = Mathf.Lerp(tuning.LeapSlamExtraHeight, 0f, (t - apex) / Mathf.Max(0.01f, flight - apex));

            if (t < flight)
            {
                _leapSlamHolding = false;
                float deltaHeight = targetHeight - _leapSlamPrevExtraHeight;
                _verticalVelocity = deltaHeight / Mathf.Max(Time.deltaTime, 0.0001f);
                _leapSlamPrevExtraHeight = targetHeight;
            }
            else
            {
                // Landed - pin to the locked landing Y and let ApplyMotion skip its Move for the
                // rest of the clip's stand-up tail (same as the shared path's landed branch).
                _leapSlamHolding = true;
                _leapSlamPrevExtraHeight = 0f;
                _verticalVelocity = 0f;
                if (Mathf.Abs(transform.position.y - _leapSlamLandingY) > 0.0005f)
                {
                    _controller.enabled = false;
                    Vector3 p = transform.position;
                    p.y = _leapSlamLandingY;
                    transform.position = p;
                    _controller.enabled = true;
                }
            }

            // --- hit window: the slam connects at touchdown, so drive it off _stateTimer like the
            // rest of this pounce. The shared path reads the clip's normalized windows, but a
            // crossfade's stale OUTGOING normalizedTime was latching _leapSlamHitWindowsDone shut
            // (normalized briefly >= the window end) before the slam ever landed - "沒有攻擊到玩家".
            BossHitWindow slamWindow = (leapSlamAttack.HitWindows != null && leapSlamAttack.HitWindows.Length > 0)
                ? leapSlamAttack.HitWindows[0]
                : null;
            BossHitbox slamHitbox = slamWindow != null ? ResolveHitbox(slamWindow.part) : null;
            if (slamHitbox != null && !_leapSlamHitWindowsDone)
            {
                bool active = t >= flight - 0.15f && t <= flight + 0.45f;
                if (active && !slamHitbox.IsActive)
                {
                    slamHitbox.Activate(leapSlamAttack, slamWindow);
                    _openHitboxesThisAttack.Add(slamHitbox);
                }
                else if (!active && slamHitbox.IsActive && _openHitboxesThisAttack.Contains(slamHitbox))
                {
                    slamHitbox.Deactivate();
                    _openHitboxesThisAttack.Remove(slamHitbox);
                    if (t > flight) _leapSlamHitWindowsDone = true;
                }
            }

            // --- exit: also _stateTimer-based. IsAttackAnimationFinished off a stale crossfade
            // normalizedTime could otherwise abort the whole state ~0.1s in (before the slam).
            // Hold the landed pose for a short recovery, then Idle.
            float totalStateSeconds = flight + Mathf.Max(0.3f, leapSlamAttack.RecoverySeconds);
            if (t >= totalStateSeconds || (t > flight + 0.5f && AnimatorHasFinished()))
            {
                CloseAllHitboxes();
                // A committed 50%-HP nuke gets a real breather afterwards - the normal Attack path's
                // EndAttack does this via _globalRestUntil, but LeapSlam exits straight to Idle, so
                // set it here too (normal rest + the major-attack extra, same as any isMajorAttack).
                _globalRestUntil = Time.time
                    + Random(tuning.GlobalRestMinSeconds(Phase), tuning.GlobalRestMaxSeconds(Phase))
                    + Random(tuning.MajorAttackExtraRestMinSeconds, tuning.MajorAttackExtraRestMaxSeconds);
                ChangeState(BossState.Idle);
            }
        }

        private float ComputeLeapSlamExtraHeight(float normalized)
        {
            float riseStart = tuning.LeapSlamHeightRiseStartNormalized;
            float peak = tuning.LeapSlamHeightPeakNormalized;
            float fallEnd = tuning.LeapSlamHeightFallEndNormalized;

            if (normalized <= riseStart || normalized >= fallEnd) return 0f;
            if (normalized < peak)
            {
                float t = Mathf.InverseLerp(riseStart, peak, normalized);
                return Mathf.Lerp(0f, tuning.LeapSlamExtraHeight, t);
            }
            else
            {
                float t = Mathf.InverseLerp(peak, fallEnd, normalized);
                return Mathf.Lerp(tuning.LeapSlamExtraHeight, 0f, t);
            }
        }

        private bool _ultimateTrackingLocked;
        private bool _committedUltimate;
        private bool _ultimateLungeStopLogged;
        // 2026-08-29 - _stateTimer at which the lunge planted (reached the player / hit the cap),
        // so the strike hitbox opens synced to that contact instead of a clip-normalized window
        // that the crossfade could shift - same fix pattern as UpdateLeapSlamPounce.
        private float _ultimateContactTime;
        private bool _ultimateHitDone;
        private const float UltimateLungeTimeCap = 0.75f;

        private void UpdateUltimateReposition()
        {
            if (target == null) { ChangeState(BossState.Idle); return; }

            FaceTarget(1f); // keep watching the player throughout, whether backing away or committing

            // 2026-08-25, user feedback ("退後到適當距離時馬上在該位置停下來 施展必殺技飛踢") - check the
            // distance BEFORE applying this frame's movement and, once it's open, stop dead right
            // there and commit straight to UltimatePrepare - not another frame of backpedal, and not
            // handing off to Idle first (that indirection let the player close the gap again before
            // the normal cascade got around to re-checking it next frame - the reported "退後兩次"
            // symptom).
            if (HorizontalDistance() >= tuning.UltimateMaxDistance * tuning.UltimateMinTriggerDistanceFraction)
            {
                _horizontalVelocity = Vector3.zero;
                ChangeState(BossState.UltimatePrepare);
                return;
            }

            Vector3 away = transform.position - target.position;
            away.y = 0f;
            _horizontalVelocity = away.sqrMagnitude > 0.0001f ? away.normalized * tuning.UltimateRepositionSpeed : Vector3.zero;

            // Safety valve - this boss can spawn near a corner/wall, so retreating may never fully
            // clear the trigger threshold (otherwise TryEnterUltimateReposition would just re-trigger
            // every frame forever - a real soft-lock). Fire from whatever distance was actually
            // reached instead; UpdateUltimatePrepare's own windup-dash still closes any remaining
            // gap toward the player, so this degrades gracefully rather than freezing.
            if (_stateTimer >= tuning.UltimateRepositionTimeoutSeconds)
            {
                Log($"UltimateReposition: timed out (likely cornered) at dist={HorizontalDistance():F2} - firing anyway.");
                ChangeState(BossState.UltimatePrepare);
            }
        }

        private void UpdateUltimatePrepare()
        {
            float remaining = _ultimateStartupDuration - _stateTimer;
            bool lockPhase = remaining <= tuning.UltimateTrackingLockSeconds;

            // 2026-08-25, user feedback ("原地蓄力、只靠飛踢本身的撲擊來銜接") - windup used to dash
            // toward the player (see the removed MoveTowardTarget call this replaced) so a far-away
            // trigger wouldn't just fire in place; the user has now asked for the opposite - stand
            // completely still and face the player through the whole windup, relying only on
            // UpdateUltimateAttack's own leap lunge (UltimateLeapSpeed) to close the gap during the
            // kick itself. UltimateLeapSpeed was bumped (9 -> see BossTuning) to reliably cover the
            // full trigger band (UltimateMaxDistance * MinTriggerDistanceFraction .. UltimateMaxDistance)
            // given the kick's own measured ~2.3m foot reach + hitbox/player radii (~3m combined) -
            // verified via SampleAnimation, not guessed.
            _horizontalVelocity = Vector3.zero;
            if (!lockPhase)
            {
                FaceTarget(1f);
            }
            else if (!_ultimateTrackingLocked)
            {
                _ultimateTrackingLocked = true; // direction frozen from here on - stop calling FaceTarget
            }

            // Posture break can still cancel the PREPARE phase (not-yet-committed) - handled by
            // TryEnterPostureBroken's own guard already allowing PostureBroken to win here.

            if (_stateTimer >= _ultimateStartupDuration)
            {
                _committedUltimate = true;
                if (ultimateEnergy != null)
                {
                    // Spec: consumes ALL energy at the real jump commit point, hit or miss.
                    // UltimateEnergy.Consume() (not a Drain(amount) API - checked its actual
                    // members first) always zeroes it fully, which is exactly what's needed here.
                    ultimateEnergy.Consume();
                }
                ChangeState(BossState.UltimateAttack);
            }
        }

        private void UpdateUltimateAttack()
        {
            float normalized = AnimatorNormalizedTime();
            float t = _stateTimer;

            // 2026-08-29, user ("飛踢碰到玩家時 動畫上玩家沒有馬上被擊退出去 能仿照 LeapSmash 的作法嗎")
            // - the old lunge ran transform.forward * UltimateLeapSpeed for the WHOLE clip-normalized
            // pre-strike window (~0.9s of the 1.5s clip = ~9m) from a ~5m trigger distance, blowing
            // ~3-4m PAST the player; the foot hitbox then opened AFTER the overshoot, so the hit only
            // ever registered on the foot's RETRACTION sweep back through the player - late and
            // disconnected from the visual contact ("延遲 不銜接"). Now, like UpdateLeapSlamPounce:
            // lunge only until we're ON the player (or a short time cap), then plant and open the
            // strike hitbox timed to THAT contact (via _stateTimer, immune to the crossfade's stale
            // normalizedTime). transform.forward, not re-tracking - the direction was locked during
            // UltimatePrepare per spec ("no air-tracking after leap"); a sidestep dodges it.
            bool reached = target == null
                || HorizontalDistance() <= 1.3f
                || t >= UltimateLungeTimeCap;
            if (!reached)
            {
                _horizontalVelocity = transform.forward * tuning.UltimateLeapSpeed;
            }
            else
            {
                _horizontalVelocity = Vector3.zero;
                if (!_ultimateLungeStopLogged)
                {
                    _ultimateLungeStopLogged = true;
                    _ultimateContactTime = t;
                    Log($"UltimateAttack: planted at t={t:F2}s, dist-to-player={HorizontalDistance():F2}.");
                }
            }

            // Strike hitbox: opens right as the lunge plants (contact), _stateTimer-relative. Reads
            // part/damageMultiplier from the asset's own HitWindows[0]; the normalized start/end on
            // it are no longer used for timing.
            float contact = _ultimateLungeStopLogged ? _ultimateContactTime : UltimateLungeTimeCap;
            BossHitWindow kickWindow = (ultimateAttack != null && ultimateAttack.HitWindows != null && ultimateAttack.HitWindows.Length > 0)
                ? ultimateAttack.HitWindows[0]
                : null;
            BossHitbox kickHitbox = kickWindow != null ? ResolveHitbox(kickWindow.part) : null;
            if (kickHitbox != null && !_ultimateHitDone)
            {
                bool activeNow = t >= contact - 0.06f && t <= contact + 0.35f;
                if (activeNow && !kickHitbox.IsActive)
                {
                    kickHitbox.Activate(ultimateAttack, kickWindow);
                    _openHitboxesThisAttack.Add(kickHitbox);
                }
                else if (!activeNow && kickHitbox.IsActive && _openHitboxesThisAttack.Contains(kickHitbox))
                {
                    kickHitbox.Deactivate();
                    _openHitboxesThisAttack.Remove(kickHitbox);
                    if (t > contact) _ultimateHitDone = true;
                }
            }

            // Exit off the real clip length (Rising_Flying_Kick = 45f @ 30fps = 1.5s), with a late
            // AnimatorHasFinished fallback - not IsAttackAnimationFinished, whose stale-normalizedTime
            // path could abort the whole state ~0.1s in.
            if (t >= 1.5f || (t > 1.0f && AnimatorHasFinished()))
            {
                _committedUltimate = false;
                _ultimateTrackingLocked = false;
                _ultimatePending = false;
                _lastUltimateEndTime = Time.time;
                CloseAllHitboxes();
                ChangeState(BossState.Idle);
            }
        }

        // ---- Vanish / Dive ----
        private const float VanishLockPointTime = 3.5f;

        private void UpdateVanishing()
        {
            _horizontalVelocity = Vector3.zero;
            _vanishTimer += Time.deltaTime;

            if (_vanishTimer < VanishLockPointTime)
            {
                // "消失後前3.5秒可以持續追蹤玩家位置" - re-resolve the intended landing spot each
                // frame (cheap - only used for the eventual lock, not applied to transform yet).
                _lockedLandingPoint = ComputeLandingPoint();
            }
            else if (!_landingPointLocked)
            {
                _lockedLandingPoint = ComputeLandingPoint();
                _landingPointLocked = true;
                if (lockOnTarget != null) lockOnTarget.enabled = false;
                Log("Vanish landing point locked at " + _lockedLandingPoint);
            }

            if (_vanishTimer >= tuning.VanishTotalCycleSeconds)
            {
                transform.position = _lockedLandingPoint + Vector3.up * 6f; // arrive from above
                if (target != null)
                {
                    Vector3 lookDir = target.position - transform.position;
                    lookDir.y = 0f;
                    if (lookDir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
                }
                ChangeState(BossState.DiveAttack);
            }
        }

        private void UpdateDiveAttack()
        {
            _horizontalVelocity = Vector3.zero;
            float normalized = AnimatorNormalizedTime();

            if (!_diveLanded)
            {
                // Falls under normal gravity via _verticalVelocity (set in ApplyMotion) until the
                // CharacterController actually reports grounded - matches "空中期間停止普通AI",
                // landing-frame-triggered LandingAOE below.
                if (_controller.isGrounded && _stateTimer > 0.2f)
                {
                    _diveLanded = true;
                    // Real bug hit during Play Mode verification: _diveLandedAtTime was left at
                    // its OnEnterState sentinel (float.MaxValue, meant only to guarantee the
                    // "not landed yet" branch above runs first) and never actually stamped with
                    // the real landing time here - so the recovery check below
                    // (_stateTimer >= _diveLandedAtTime + recovery) could never pass and the boss
                    // stayed stuck in DiveAttack forever after touching down. Stamping the real
                    // _stateTimer at the moment of landing fixes it.
                    _diveLandedAtTime = _stateTimer;
                    if (landingAoeHitbox != null && _currentAttack == null)
                    {
                        var window = new BossHitWindow { part = BossHitboxPart.LandingAOE, startNormalized = 0f, endNormalized = 1f, damageMultiplier = 1f, measured = false };
                        landingAoeHitbox.Activate(diveLandingAttack, window);
                    }
                }
            }
            else
            {
                if (landingAoeHitbox != null && landingAoeHitbox.IsActive)
                {
                    landingAoeHitbox.Deactivate(); // single-frame resolve, spec: only once
                }

                if (_stateTimer >= (_diveLandedAtTime + tuning.DiveLandingRecoverySeconds))
                {
                    ResetVanishCycle();
                    if (lockOnTarget != null) lockOnTarget.enabled = true;
                    RestoreRenderers();
                    ChangeState(BossState.Idle);
                }
            }
        }

        // ---- Hit reaction / posture ----
        private bool _forcedHitReactionPending;
        private bool _pendingLaunch;

        // Called externally when an incoming hit qualifies as a launch per spec section 14
        // ("玩家攻擊具有Launch屬性" etc.) - kept as a public entry point so whatever resolves the
        // player's own attack properties can request this without BossStateMachine polling.
        public void RequestBeHitFlyUp()
        {
            if (CurrentState == BossState.Dead || CurrentState == BossState.GettingUp
                || CurrentState == BossState.Vanishing) return;
            _pendingLaunch = true;
            _forcedHitReactionPending = true;
        }

        // 2026-09-01, Sekiro deflect - the player perfect-parried the boss's blade. Interrupt
        // whatever it's doing with the SAME HitReaction recoil RequestBeHitFlyUp uses, minus the
        // launch (it's a stagger, not a knock-up). The posture damage itself is applied by
        // PlayerGuard via StancePoise.AddPostureDamage; if that crosses the boss's stance max,
        // UpdateHitReaction already routes straight into PostureBroken when the recoil finishes.
        public void NotifyParried() => NotifyParried(DeflectReaction.Recoil);

        // 2026-09-01, spec item 1. The parry's posture damage is already applied by PlayerGuard
        // (StancePoise.AddPostureDamage) BEFORE this is called - so whatever branch runs here, a
        // parry that crossed the boss's stance max will still be caught by TryEnterPostureBroken()
        // (highest priority in the cascade) on the very next Update, overriding all of these.
        public void NotifyParried(DeflectReaction reaction)
        {
            if (CurrentState == BossState.Dead || CurrentState == BossState.GettingUp
                || CurrentState == BossState.Vanishing || CurrentState == BossState.Victory) return;

            switch (reaction)
            {
                case DeflectReaction.ContinueCombo:
                    // Leave the FSM alone - this attack's remaining hit windows keep playing out.
                    // A combo shouldn't die just because its first hit got parried.
                    break;
                case DeflectReaction.CancelAttack:
                    CancelAttackInProgress();
                    if (CurrentState == BossState.Attack) ChangeState(BossState.Idle);
                    break;
                default: // Recoil - the pre-2026-09-01 behaviour, and the default for every
                         // un-migrated BossHitWindow.
                    _forcedHitReactionPending = true;
                    // spec 5A - a parried lunge shouldn't keep sliding through the player.
                    if (_currentAttack?.AttackMotion != null && _currentAttack.AttackMotion.stopOnDeflectRecoil)
                    {
                        _attackMotionHalted = true;
                    }
                    break;
            }
        }

        private void UpdateHitReaction()
        {
            _horizontalVelocity = Vector3.zero;
            CloseAllHitboxes();
            float normalized = AnimatorNormalizedTime();
            if (IsAttackAnimationFinished(normalized))
            {
                _pendingLaunch = false;
                if (stance != null && stance.IsStaggered)
                {
                    ChangeState(BossState.PostureBroken);
                }
                else
                {
                    ChangeState(BossState.Idle);
                }
            }
        }

        // 2026-08-26, rewritten per explicit user request (Boss AI spec, section 三) - three
        // sub-phases played out of ONE clip (kneelStandClipName) instead of the old single
        // "play once, wait a fixed duration, switch to Idle" version:
        //   1. Entering: clip plays forward normally (CrossFaded in from OnEnterState) until it
        //      reaches tuning.PostureKneelNormalizedTime ("跪地" pose).
        //   2. Held: animator.speed=0 (NOT Time.timeScale - spec explicitly forbids that) freezes
        //      the pose for a random postureBreakDurationMin/MaxSeconds window. Boss is hittable
        //      (nothing here touches Health/invulnerability) but CloseAllHitboxes()+
        //      CancelAttackInProgress() already ran on entry so it can't move, turn, or attack.
        //   3. Standing: animator.speed=1 resumes, clip plays its own tail out as "stand up". Once
        //      it reports finished (or a safety timeout elapses, in case a clip's Loop Time is on
        //      or normalizedTime behaves unexpectedly - never gets stuck in PostureBroken forever)
        //      stance resets and control returns to Idle.
        private void UpdatePostureBroken()
        {
            _horizontalVelocity = Vector3.zero;
            if (!_postureBrokenHandled)
            {
                _postureBrokenHandled = true;
                _postureKneelReached = false;
                _postureHoldElapsed = false;
                CloseAllHitboxes();
                CancelAttackInProgress();
                _postureBreakDuration = Random(tuning.PostureBreakDurationMinSeconds, tuning.PostureBreakDurationMaxSeconds);
            }

            if (animator == null)
            {
                // No Animator to sample normalizedTime from - fall back to the old fixed-duration
                // behavior rather than getting stuck here forever.
                if (_stateTimer >= _postureBreakDuration) EndPostureBroken();
                return;
            }

            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);

            if (!_postureKneelReached)
            {
                // 2026-09-01, user report ("武士架式條滿了後 沒有做出蹲下的動作") - PostureBroken is
                // almost always entered mid-attack (you break posture by hitting the boss during
                // its swings). CrossFadeInFixedTime(kneelStand) doesn't take effect until the
                // Animator's next internal pass, so for the first frame(s) GetCurrentAnimatorStateInfo(0)
                // still reports the OUTGOING attack clip - whose normalizedTime is usually already
                // well past PostureKneelNormalizedTime. The old code then set animator.speed = 0
                // immediately, freezing the Animator on the attack pose before the kneel clip ever
                // started: the boss just locked up mid-swing with no kneel. Wait until the Animator
                // is genuinely IN the kneel state (transition done) before sampling its time. Same
                // stale-CrossFade hazard AnimatorHasFinished()/the attack path's _stateTimer floor
                // already guard against elsewhere in this class.
                string kneelName = string.IsNullOrEmpty(kneelStandClipName) ? fall3ClipName : kneelStandClipName;
                bool inKneelState = !animator.IsInTransition(0) && stateInfo.IsName(kneelName);
                if (!inKneelState)
                {
                    // Don't hang forever if the CrossFade never lands (bad state name / missing clip).
                    if (_stateTimer >= 1.5f)
                    {
                        _postureKneelReached = true;
                        _stateTimer = 0f;
                        ApplyPostureBrokenGroundDrop();
                    }
                    return;
                }

                if (stateInfo.normalizedTime >= tuning.PostureKneelNormalizedTime)
                {
                    animator.speed = 0f; // pause the Animator state - Time.timeScale is untouched
                    _postureKneelReached = true;
                    _stateTimer = 0f; // restart the timer so the hold duration below is measured from the moment we actually froze, not from state-entry
                    ApplyPostureBrokenGroundDrop();
                }
                // Safety: a clip shorter than expected, or a bad kneel-normalized-time value, could
                // finish (normalizedTime>=1, non-looping) before ever reaching the configured pause
                // point - don't let PostureBroken run forever unhit-window-able in that case.
                else if (stateInfo.normalizedTime >= 1f)
                {
                    animator.speed = 0f;
                    _postureKneelReached = true;
                    _stateTimer = 0f;
                }
                return;
            }

            if (!_postureHoldElapsed)
            {
                // spec item 7 - a finisher in progress pins the kneel until the deathblow resolves
                // (or the safety buffer expires); the boss must not stand back up mid-execution.
                if (_stateTimer >= _postureBreakDuration && !ExecutionHoldActive)
                {
                    _postureHoldElapsed = true;
                    animator.speed = 1f; // resume playback - "受擊時間結束後繼續站起"
                }
                return;
            }

            // Standing back up - wait for the clip's own tail to finish, with a generous timeout
            // safety net (kneelStandClipName's stand-up portion should be well under this).
            bool clipFinished = stateInfo.normalizedTime >= 1f && !stateInfo.loop;
            if (clipFinished || _stateTimer >= _postureBreakDuration + 5f)
            {
                EndPostureBroken();
            }
        }

        private void EndPostureBroken()
        {
            RestorePostureBrokenGroundDrop();
            stance.EndStagger(); // zeroes stance + grants existing postStaggerGraceSeconds invulnerability
            stance.RestoreStanceFractionAfterRecovery(tuning.PostureRestoreOnRecover); // spec 13.7: back up to ~20%, not a fresh 0%
            ChangeState(BossState.Idle);
        }

        // spec: "五適硬值時仍然是在空中躺平" - see BossTuning.PostureBrokenGroundDropOffset. Dropping the
        // ROOT (not the mesh alone) means ApplyMotion must stop re-grounding the CharacterController
        // for the duration - see its own PostureBroken guard.
        private void ApplyPostureBrokenGroundDrop()
        {
            if (_postureBrokenDropApplied) return;
            _postureBrokenDropApplied = true;
            transform.position += Vector3.down * tuning.PostureBrokenGroundDropOffset;
        }

        private void RestorePostureBrokenGroundDrop()
        {
            if (!_postureBrokenDropApplied) return;
            _postureBrokenDropApplied = false;
            transform.position += Vector3.up * tuning.PostureBrokenGroundDropOffset;
        }

        // ---------------------------------------------------------------- Deathblow (spec item 7, M4)

        // Called by BossLifeNodeController when a finisher animation STARTS. Freezes the kneeling
        // PostureBroken pose (UpdatePostureBroken won't stand back up while the hold is live) and
        // makes the boss invulnerable for the windup. Auto-releases if the deathblow never resolves.
        public void BeginExecutionHold(float seconds)
        {
            _executionHoldUntil = Time.time + Mathf.Max(0.1f, seconds) + 1.5f; // +buffer; EndExecutionHold is the real release
            if (!_executionInvuln && health != null)
            {
                health.SetInvulnerable(this, true);
                _executionInvuln = true;
            }
        }

        public void EndExecutionHold()
        {
            _executionHoldUntil = -999f;
            if (_executionInvuln && health != null)
            {
                health.SetInvulnerable(this, false);
            }
            _executionInvuln = false;
        }

        private bool ExecutionHoldActive => Time.time < _executionHoldUntil;

        // First (non-final) Deathblow: spend a node, restore the boss, lock phase 2, rise and fight on.
        public void DeathblowPhaseTransition(bool restoreHealth)
        {
            EndExecutionHold();
            CancelAllPending();                 // spec §8.3 - clear pending specials / hit windows / attack motion
            CloseAllHitboxes();
            if (animator != null) animator.speed = 1f;

            if (restoreHealth && health != null)
            {
                health.ResetHealth();
            }
            _phase2Locked = true;
            Phase = BossPhase.Phase2;
            _postureBrokenHandled = false;

            if (stance != null)
            {
                stance.EndStagger();
                stance.RestoreStanceFractionAfterRecovery(0f); // fresh posture bar for the new phase
            }

            // GettingUp already grants get-up i-frames (see OnEnterState) and flows back to Alert.
            ChangeState(BossState.GettingUp);
            Log("Deathblow -> phase transition (nodes now driving phase 2).");
        }

        // Final Deathblow: permanent death, no revive.
        public void DeathblowFinalKill(GameObject executor)
        {
            EndExecutionHold();
            _deathblowFinalKill = true;
            CancelAllPending();
            CloseAllHitboxes();
            if (animator != null) animator.speed = 1f;

            if (health != null && !health.IsDead)
            {
                // Route through the damage pipeline so Health.Died fires -> OnBossDied -> Dead,
                // same as any other death; UpdateDead then sees _deathblowFinalKill and never revives.
                health.ApplyDamage(new DamageInfo(health.MaxHealth * 2f + 1f, transform.position, Vector3.zero,
                    executor != null ? executor : gameObject));
            }
            else
            {
                ChangeState(BossState.Dead);
            }
            Log("Deathblow -> permanent death.");
        }

        private void UpdateVictory()
        {
            _horizontalVelocity = Vector3.zero;
            // Walk_to_Sit's own root motion (if any) is intentionally left to the Animator via
            // useRootMotion-equivalent handling below - see ApplyRootMotionIfEnabled's call site
            // in UpdateVictory being absent: Victory is terminal and one-shot, so this project's
            // safer default is to let it play in place (no forward creep) unless a VictorySeatPoint
            // is configured - see PiHaiWangBossSetup's own comment on this being flagged, not solved.
        }

        private void UpdateDead()
        {
            _horizontalVelocity = Vector3.zero;
            CloseAllHitboxes();

            // 2026-08-24, explicit user request ("五秒後復活") - see this state's own
            // OnEnterState comment for why this boss revives at all instead of staying
            // permanently dead like the original spec. Mirrors RespawnController's own
            // ResetHealth()+EndStagger() pair (the project's existing revive precedent for
            // Player/Mecha) rather than reinventing what "fully okay again" means, but handled
            // directly here instead of via that generic component - RespawnController expects to
            // un-hide a GameObject Health.ApplyDamage deactivated itself, but this boss's Health
            // has deferDeactivationToDeathAnimation=true (stays active so BeHit_FlyUp can
            // actually play), and RespawnController has no concept of an FSM state to exit
            // afterward - Dead is deliberately terminal everywhere else in this class (see
            // TryEnterPostureBroken etc. all refusing to fire once CurrentState==Dead), so
            // something has to explicitly walk it back out again.
            // 2026-08-26, explicit user request (Boss AI spec, section 四) - "死亡動畫只播放一次...
            // 動畫結束後保持最後倒地姿勢,不回到Idle". See BossTuning.PermanentDeath's own comment -
            // opt-in per tuning asset so PW2's existing auto-revive tuning is untouched.
            if (tuning.PermanentDeath || _deathblowFinalKill)
            {
                return; // _deathblowFinalKill (spec item 7): a final Deathblow never revives
            }

            _deathElapsed += Time.deltaTime;
            if (_deathElapsed < tuning.ReviveDelaySeconds)
            {
                return;
            }

            if (health != null) health.ResetHealth();
            if (stance != null)
            {
                stance.EndStagger(); // already zeroes CurrentStance - "fully okay", not staggered
            }
            _postureBrokenHandled = false;
            _phase2Locked = false;
            Phase = BossPhase.Phase1;
            ResetVanishCycle();
            _breakdanceTimeAccumulated = 0f;
            _breakdancePending = false;
            _leapSlamTimeAccumulated = 0f;
            _leapSlamPending = false;
            _periodicSlamTimeAccumulated = 0f;
            _periodicSlamPending = false;
            _tooCloseTimer = 0f;
            _tooCloseThresholdLogged = false;
            RestoreRenderers();
            // 2026-08-31, explicit user request ("復活時間到慢慢站起來") - rise off the ground over
            // tuning.StandUpSeconds (GettingUp plays the death take in reverse) instead of snapping
            // straight to a standing Alert pose. Health/stance/flags are already reset above so the
            // priority cascade sees a live boss the instant GettingUp begins.
            ChangeState(BossState.GettingUp);
        }

        // The death take played in reverse = "climb back onto your feet" - no boss pack ships a
        // dedicated stand-up clip. Manually scrubbed frame-by-frame (animator.speed held at 0 in
        // OnEnterState) from normalizedTime 1 (face-down) back to 0 (standing) so it lands exactly
        // on the standing pose, the same "don't trust normalizedTime to auto-advance cleanly"
        // caution the LeapSlam code arrived at. Nothing pre-empts this state (see the cascade guard
        // in Tick and UpdateCombatTimer's eligibility list); it always ends by dropping to Alert.
        private void UpdateGettingUp()
        {
            _horizontalVelocity = Vector3.zero;
            CloseAllHitboxes();

            float duration = Mathf.Max(0.1f, tuning.StandUpSeconds);
            float t = Mathf.Clamp01(_stateTimer / duration);
            if (animator != null)
            {
                animator.Play(DeathStateName(), 0, 1f - t);
            }

            if (_stateTimer >= duration)
            {
                if (animator != null) animator.speed = 1f;
                if (health != null) health.SetInvulnerable(this, false);
                ChangeState(BossState.Alert);
            }
        }

        // The Animator state Dead / GettingUp drive - deathClipName when the tuning asset set one
        // (Wushi / PW2 both do), else the shared BeHit_FlyUp fallback for older bosses. Mirrors the
        // expression in OnEnterState(Dead).
        private string DeathStateName() =>
            string.IsNullOrEmpty(deathClipName) ? behitFlyUpClipName : deathClipName;

        // ---------------------------------------------------------------- Attack selection / execution

        private BossAttackDefinition PickAttack() => PickAttackFiltered(null);

        // 2026-08-29 - shared body for the normal Idle roll (extraFilter null) and the Approach
        // gap-closer roll (extraFilter = "useRootMotion only", see UpdateApproach: user request
        // "有連續位移的可以不用綁死近戰攻擊距離" - a charging move closes its own gap, so the boss
        // shouldn't have to walk all the way into punching range before it can choose one). Same
        // distance / angle / cooldown / repeat / weight gates; only the candidate set differs.
        private BossAttackDefinition PickAttackFiltered(System.Func<BossAttackDefinition, bool> extraFilter)
        {
            if (normalAttackPool == null || normalAttackPool.Length == 0 || target == null)
            {
                return null;
            }

            float distance = HorizontalDistance();
            float angle = AngleToTarget();
            var weighted = new List<(BossAttackDefinition attack, float weight)>();
            float total = 0f;
            foreach (var attack in normalAttackPool)
            {
                if (attack == null) continue;
                if (extraFilter != null && !extraFilter(attack)) continue;
                if (distance < attack.MinDistance || distance > attack.MaxDistance) continue;
                if (angle > attack.MaxAngleDegrees) continue;
                if (Time.time < CooldownUntil(attack)) continue;
                if (attack.DisallowImmediateRepeat && attack == _lastNormalAttack && _lastNormalAttackConsecutiveCount >= attack.MaxConsecutiveUses) continue;
                float w = attack.SelectionWeight(Phase);
                if (w <= 0f) continue;

                // 2026-08-29, user request ("每招 盡量做到輪流施放 不要有技能被孤立") - soft
                // least-recently-used bias: a move used within AttackRotationRecoverySeconds has
                // its weight scaled toward AttackRotationRecentFactor, recovering linearly to full
                // by the end of that window. Keeps a high-weight staple from crowding the pool out
                // over a long fight, without a hard rotation lock.
                if (tuning.AttackRotationRecoverySeconds > 0.01f)
                {
                    float sinceUsed = _lastUsedTime.TryGetValue(attack, out float lastUsed)
                        ? Time.time - lastUsed
                        : 999f;
                    w *= Mathf.Lerp(tuning.AttackRotationRecentFactor, 1f,
                        Mathf.Clamp01(sinceUsed / tuning.AttackRotationRecoverySeconds));
                }

                weighted.Add((attack, w));
                total += w;
            }
            if (weighted.Count == 0 || total <= 0f)
            {
                if (extraFilter == null)
                {
                    Log($"PickAttack: no candidate in range (dist={distance:F2}, angle={angle:F1}, phase={Phase}) - holding.");
                }
                return null;
            }

            float roll = (float)_random.NextDouble() * total;
            float acc = 0f;
            foreach (var (attack, weight) in weighted)
            {
                acc += weight;
                if (roll <= acc)
                {
                    Log($"PickAttack: chose {attack.AttackId} (dist={distance:F2}, angle={angle:F1}, weight={weight:F1}/{total:F1}, phase={Phase}).");
                    return attack;
                }
            }
            return weighted[weighted.Count - 1].attack;
        }

        private void BeginAttack(BossAttackDefinition attack)
        {
            _currentAttack = attack;
            // 2026-09-01, user request ("確定出招前列印出的動作是正確名稱") - ONE consistent line for
            // EVERY attack path (normal pool roll, TryRollSweepDerivation continuation, forced
            // TooCloseKick, periodic OverheadSlam). PickAttack only logs the pool roll; the others
            // previously showed just the clip name from PlayState. attackId is the move's own name;
            // ClipName is the Animator state it will CrossFade to next.
            Log($"BeginAttack: {(attack != null ? attack.AttackId : "null")} (clip {(attack != null ? attack.ClipName : "-")})");
            _lastNormalAttackConsecutiveCount = (attack == _lastNormalAttack) ? _lastNormalAttackConsecutiveCount + 1 : 1;
            _lastNormalAttack = attack;
            _lastUsedTime[attack] = Time.time; // rotation bias - see PickAttackFiltered
            _attackLandedAnyHit = false;
            _lastActiveHitWindowIndex = -1;
            _commandGrabResolved = false;
            SetCooldown(attack, attack.CooldownSeconds);

            // spec 5A - lock this lunge's origin + a fixed commit direction now (no infinite
            // tracking toward the player once the swing is out). Flat toward the target, or the
            // boss's own forward if there's no target.
            _attackMotionApplied = 0f;
            _attackMotionHalted = false;
            _attackMotionDistanceOverride = -1f;
            _clipDriftBaselineSet = false;
            _clipDriftCompensatedXZ = Vector3.zero;
            // 2026-09-02 - a useRootMotion gap-closer that aims at the target: scale the clip's baked
            // net forward travel so it lands RootMotionAimGapMeters short of the player's live position.
            _rootMotionScaleRuntime = -1f;
            if (attack.UseRootMotion && attack.RootMotionAimAtTarget && target != null)
            {
                float need = Mathf.Clamp(HorizontalDistance() - attack.RootMotionAimGapMeters, 0f, attack.RootMotionAimMaxMeters);
                _rootMotionScaleRuntime = Mathf.Clamp(need / attack.RootMotionClipForwardMeters, 0f, 2f);
            }
            if (attack.AttackMotion != null && (attack.AttackMotion.HasDisplacement || attack.LungeDistanceFromTargetGap))
            {
                _attackMotionOrigin = transform.position;
                Vector3 commit = target != null ? target.position - transform.position : transform.forward;
                commit.y = 0f;
                _attackMotionDir = commit.sqrMagnitude > 0.0001f ? commit.normalized : transform.forward;
                // 2026-09-02 (Wushi_ScissorTakedown) - a jump-in grab: replace the baked lunge distance
                // with the live gap to the player so the leap lands ON them. Land a bit short
                // (LungeTargetGapMeters) so the boss ends up right in front, not overlapping.
                if (attack.LungeDistanceFromTargetGap && target != null)
                {
                    float gap = HorizontalDistance() - attack.LungeTargetGapMeters;
                    _attackMotionDistanceOverride = Mathf.Clamp(gap, 0f, attack.LungeMaxMeters);
                }
            }
            // 2026-08-26, real playtested bug ("希望變成連砍" - chaining SwordJudgment into
            // OverheadSlam via TryRollSweepDerivation, and separately "很明顯沒有觸發踢擊 動畫也沒出
            // 來" for the too-close punish kick) - ChangeState(next) no-ops if CurrentState already
            // equals next (see its own guard). BeginAttack is called from THREE places
            // (PickAttack's normal roll, EndAttack's own derived-attack continuation, and
            // TryEnterTooCloseKick's forced punish) and ALL THREE can legitimately be called while
            // CurrentState is ALREADY Attack (attack-into-attack, the exact case that guard breaks) -
            // OnEnterState (which actually calls PlayState/CrossFades to the new clip) would silently
            // never re-run, leaving _currentAttack pointing at the new attack while the Animator kept
            // playing whatever the PREVIOUS attack's clip already was. Fixed once here instead of at
            // each call site: bounce through Idle first so ChangeState sees a real transition both
            // ways (also runs OnExitState's CloseAllHitboxes() cleanup for whatever was interrupted).
            if (CurrentState == BossState.Attack)
            {
                ChangeState(BossState.Idle);
            }
            ChangeState(BossState.Attack);
        }

        private void EndAttack()
        {
            CloseAllHitboxes();
            BossAttackDefinition finished = _currentAttack;
            _currentAttack = null;
            _attackMotionApplied = 0f;
            _attackMotionHalted = false;

            if (_pendingDerivedAttack != null)
            {
                var derived = _pendingDerivedAttack;
                _pendingDerivedAttack = null;
                BeginAttack(derived);
                return;
            }

            // 2026-08-26, explicit user request (Boss AI spec, section 五) - only a real
            // normal-pool attack finishing (not a derived-into chain, handled above) starts the
            // mandatory global rest window - see BossTuning.GlobalRestMinSeconds' own comment.
            float rest = Random(tuning.GlobalRestMinSeconds(Phase), tuning.GlobalRestMaxSeconds(Phase));
            if (finished != null && finished.IsMajorAttack)
            {
                rest += Random(tuning.MajorAttackExtraRestMinSeconds, tuning.MajorAttackExtraRestMaxSeconds);
            }
            _globalRestUntil = Time.time + rest;

            ChangeState(BossState.Idle);
        }

        private void CancelAttackInProgress()
        {
            _currentAttack = null;
            _pendingDerivedAttack = null;
            _attackMotionApplied = 0f;
            _attackMotionHalted = false;
        }

        private void TryRollSweepDerivation(float normalized)
        {
            if (_currentAttack == null || _currentAttack.DerivedAttack == null || _sweepUsedThisCombo)
            {
                return;
            }
            if (normalized < _currentAttack.DeriveWindowStartNormalized || normalized > _currentAttack.DeriveWindowEndNormalized)
            {
                return;
            }
            if (_pendingDerivedAttack != null)
            {
                return; // already rolled for this attack
            }
            if (target == null || (health != null && health.IsDead)) return;

            var derived = _currentAttack.DerivedAttack;
            float distance = HorizontalDistance();
            float angle = AngleToTarget();
            if (distance < derived.MinDistance || distance > derived.MaxDistance) { _pendingDerivedAttack = FailedDerivationMarker(); return; }
            if (angle > derived.MaxAngleDegrees) { _pendingDerivedAttack = FailedDerivationMarker(); return; }
            if (Time.time < CooldownUntil(derived)) { _pendingDerivedAttack = FailedDerivationMarker(); return; }
            if (stance != null && stance.IsStaggered) { _pendingDerivedAttack = FailedDerivationMarker(); return; }

            float chance = _currentAttack.DeriveChance(Phase);
            if (_currentAttack.HalveDeriveChanceOnFullMiss && !_attackLandedAnyHit)
            {
                chance *= 0.5f;
            }

            if ((float)_random.NextDouble() <= chance)
            {
                _sweepUsedThisCombo = true;
                SetCooldown(derived, _currentAttack.DeriveCooldownSeconds);
                _pendingDerivedAttack = derived;
            }
            else
            {
                _pendingDerivedAttack = FailedDerivationMarker();
            }
        }

        // A non-null sentinel distinct from any real attack, so TryRollSweepDerivation's own
        // "already rolled" guard works even on a failed roll (don't re-roll every frame inside
        // the derive window) - EndAttack treats this the same as "no derivation" since it's
        // never a real BossAttackDefinition instance from normalAttackPool/derivedAttack.
        private static readonly BossAttackDefinition FailedRollSentinel = null;
        private BossAttackDefinition FailedDerivationMarker() => FailedRollSentinel;

        private bool IsInsideAnyActiveWindow()
        {
            return (leftHandHitbox != null && leftHandHitbox.IsActive)
                || (rightHandHitbox != null && rightHandHitbox.IsActive)
                || (leftFootHitbox != null && leftFootHitbox.IsActive)
                || (rightFootHitbox != null && rightFootHitbox.IsActive)
                || (bodyHitbox != null && bodyHitbox.IsActive)
                || (weaponHitbox != null && weaponHitbox.IsActive)
                || (landingAoeHitbox != null && landingAoeHitbox.IsActive);
        }

        private bool AttackFinishedCommittableWindow()
        {
            // "當前不可中斷動作完成後" - a NON-interruptible attack stays committed until its last
            // active hit window has closed.
            if (_currentAttack == null) return true;
            if (!_currentAttack.Interruptible) return !IsInsideAnyActiveWindow();

            // 2026-08-29, user report ("直接打斷當前動作 只看到起手姿勢") - an interruptible attack
            // used to be pre-emptible from frame 0, so a scheduled flourish (Breakdance every 15s,
            // LeapSlam every 20s, Ultimate on a full energy bar ~every 15s) routinely cut a normal
            // attack during its pure wind-up: the boss visibly cocked back and never swung. Now an
            // interruptible attack still can't be pre-empted by one of those SCHEDULED moves until
            // it has at least reached its first hit window (i.e. actually thrown the strike).
            // Player-inflicted stagger is a separate path (TryEnterPostureBroken / the vehicle's
            // RequestBeHitFlyUp) and is unaffected - it still cuts in immediately.
            return AnimatorNormalizedTime() >= FirstStrikeNormalized(_currentAttack);
        }

        // Earliest hit-window start of an attack - the normalized time by which it has thrown its
        // first real strike. Falls back to a mid-clip default for an attack with no windows.
        private static float FirstStrikeNormalized(BossAttackDefinition attack)
        {
            float earliest = 1f;
            if (attack?.HitWindows != null)
            {
                foreach (var w in attack.HitWindows)
                {
                    if (w != null) earliest = Mathf.Min(earliest, w.startNormalized);
                }
            }
            return earliest < 1f ? earliest : 0.5f;
        }

        // Scratch buffers reused every call instead of allocating fresh collections each frame -
        // UpdateHitWindows runs every Update() while an attack is active.
        private readonly Dictionary<BossHitbox, BossHitWindow> _activeWindowByHitboxScratch = new Dictionary<BossHitbox, BossHitWindow>();
        private readonly HashSet<BossHitbox> _possibleHitboxesScratch = new HashSet<BossHitbox>();

        private void UpdateHitWindows(BossAttackDefinition attack, float normalized)
        {
            if (attack?.HitWindows == null) return;

            // 2026-08-24, bug found while wiring Dodge_and_Counter's two-handed double-strike
            // data (two separate windows both targeting LeftHand) - this used to loop through
            // windows one at a time and activate/deactivate the SHARED BossHitbox per-window
            // independently. When two windows on the same attack reference the same physical
            // hitbox (e.g. a LeftHand jab window AND a later LeftHand finisher window), the
            // second window's own "normalized not in my range" check would see the hitbox left
            // open by the FIRST window and immediately close it in the same loop/frame -
            // clobbering a hit window that should have stayed open. This silently broke
            // Punch_Combo_3 too (its own two hit windows both target LeftHand). Fixed by first
            // resolving, per PHYSICAL hitbox, whether ANY of this attack's windows currently
            // claim it active, then acting once on that aggregate instead of per-window.
            _activeWindowByHitboxScratch.Clear();
            _possibleHitboxesScratch.Clear();
            for (int i = 0; i < attack.HitWindows.Length; i++)
            {
                BossHitWindow window = attack.HitWindows[i];
                BossHitbox hitbox = ResolveHitbox(window.part);
                if (hitbox == null) continue;
                _possibleHitboxesScratch.Add(hitbox);

                bool shouldBeActive = normalized >= window.startNormalized && normalized <= window.endNormalized;
                if (shouldBeActive && !_activeWindowByHitboxScratch.ContainsKey(hitbox))
                {
                    _activeWindowByHitboxScratch[hitbox] = window;
                }
            }

            foreach (var hitbox in _possibleHitboxesScratch)
            {
                if (_activeWindowByHitboxScratch.TryGetValue(hitbox, out var activeWindow))
                {
                    if (!hitbox.IsActive)
                    {
                        hitbox.Activate(attack, activeWindow);
                        _openHitboxesThisAttack.Add(hitbox);
                    }
                }
                else if (hitbox.IsActive && _openHitboxesThisAttack.Contains(hitbox))
                {
                    hitbox.Deactivate();
                    _openHitboxesThisAttack.Remove(hitbox);
                }
            }
        }

        private BossHitbox ResolveHitbox(BossHitboxPart part)
        {
            switch (part)
            {
                case BossHitboxPart.LeftHand: return leftHandHitbox;
                case BossHitboxPart.RightHand: return rightHandHitbox;
                case BossHitboxPart.LeftFoot: return leftFootHitbox;
                case BossHitboxPart.RightFoot: return rightFootHitbox;
                case BossHitboxPart.Body: return bodyHitbox;
                case BossHitboxPart.LandingAOE: return landingAoeHitbox;
                case BossHitboxPart.Weapon: return weaponHitbox;
                default: return null;
            }
        }

        private void CloseAllHitboxes()
        {
            leftHandHitbox?.Deactivate();
            rightHandHitbox?.Deactivate();
            leftFootHitbox?.Deactivate();
            rightFootHitbox?.Deactivate();
            bodyHitbox?.Deactivate();
            landingAoeHitbox?.Deactivate();
            weaponHitbox?.Deactivate();
            _openHitboxesThisAttack.Clear();
        }

        // ---------------------------------------------------------------- Movement helpers

        private bool _sprinting;
        private float _attackReadinessBuffer;

        private float ResolveMoveSpeed()
        {
            if (_postureUnsteady)
            {
                return tuning.UnsteadyWalkSpeed;
            }
            return Phase == BossPhase.Phase1 ? tuning.WalkSpeed : tuning.RunSpeed;
        }

        private float AttackReadinessDistance()
        {
            // 2026-08-24, bug report ("console一直在報訊息" + "boss重複動作") - this used to be the
            // WIDEST MaxDistance in the pool (e.g. GuardKick's 1.4m), so Approach would stop as
            // soon as it was close enough for the widest-range move alone. If the boss then
            // settled somewhere between the widest and the NEXT-widest attack's own MaxDistance
            // (e.g. 1.05-1.4m, where only GuardKick's own distance gate passes) and that one
            // attack happened to be on cooldown/mis-angled, PickAttack() returned null for every
            // single attack in the pool - Idle sent it back to Approach, but Approach's own stop
            // check (distance <= readiness) was ALREADY satisfied since the boss hadn't actually
            // moved, so it immediately re-entered Idle and repeated forever. Confirmed directly
            // in the console log: 15+ consecutive "Idle -> Approach"/"Approach -> Idle" pairs.
            // Using the SMALLEST MaxDistance instead guarantees every attack's own MinDistance/
            // MaxDistance gate is satisfied at once the boss stops (every pool MinDistance here
            // is <= the smallest MaxDistance), so at least one move is always geometrically
            // eligible - only cooldown/angle/weight can still say no, which resolves on its own
            // as cooldowns tick down, instead of being a permanent geometric dead end.
            float min = float.MaxValue;
            if (normalAttackPool != null)
            {
                foreach (var a in normalAttackPool)
                {
                    if (a != null) min = Mathf.Min(min, a.MaxDistance);
                }
            }
            return min < float.MaxValue ? min : 1.5f;
        }

        private void MoveTowardTarget(float speed)
        {
            if (target == null) { _horizontalVelocity = Vector3.zero; return; }

            // 2026-08-31 ("被地圖物件擋住路線卡住") - route around NavMesh obstacles when a
            // NavPathFollower is wired; it fails open to the straight-line direction (identical to
            // the old code below) when there's no baked mesh / an unreachable target.
            Vector3 dir;
            if (_pathFollower != null)
            {
                dir = _pathFollower.SteeringDirection(target.position);
            }
            else
            {
                Vector3 to = target.position - transform.position;
                to.y = 0f;
                dir = to.sqrMagnitude > 0.0001f ? to.normalized : Vector3.zero;
            }
            _horizontalVelocity = dir * speed;
        }

        // 2026-09-02 (re-added) - straight-line retreat, the mirror of MoveTowardTarget. Used by
        // UpdateApproach to re-open the gap when a lunge left the boss inside AttackStandoffFloor.
        // No NavPathFollower routing (short, away from the player); a wall behind just stops it.
        private void MoveAwayFromTarget(float speed)
        {
            if (target == null) { _horizontalVelocity = Vector3.zero; return; }
            Vector3 away = transform.position - target.position;
            away.y = 0f;
            _horizontalVelocity = away.sqrMagnitude > 0.0001f ? away.normalized * speed : Vector3.zero;
        }

        private void FaceTarget(float trackingAmount, float extraYawDegrees = 0f)
        {
            if (target == null || trackingAmount <= 0f) return;
            Vector3 to = target.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.0001f) return;
            Quaternion desired = Quaternion.LookRotation(to.normalized, Vector3.up)
                                 * Quaternion.Euler(0f, extraYawDegrees, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, desired,
                tuning.RotationSpeedDegrees * trackingAmount * Time.deltaTime);
        }

        private float HorizontalDistance()
        {
            if (target == null) return float.MaxValue;
            Vector3 d = target.position - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        private float AngleToTarget()
        {
            if (target == null) return 180f;
            Vector3 to = target.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.0001f) return 0f;
            return Vector3.Angle(transform.forward, to.normalized);
        }

        private void ApplyMotion()
        {
            // LeapSlam owns its own vertical for the WHOLE state - UpdateLeapSlam drives
            // _verticalVelocity every frame, from the height arc while airborne (2026-08-27,
            // "不能跳很高嗎 至少讓玩家看不到的高度" - a ~30-unit script arc real gravity would fight)
            // and from the gap-to-locked-landing-Y once landed (2026-08-28, "著地y座標從0.623改為
            // 0.5" - the grounded clamp / gravity would otherwise push it up off that Y whenever it
            // sits at/below the natural flush-capsule rest height). Vanishing/DiveAttack handle
            // their own arc too; everything else keeps normal gravity + the grounded clamp.
            if (_controller.isGrounded && _verticalVelocity < 0f && CurrentState != BossState.LeapSlam)
            {
                _verticalVelocity = -1f;
            }
            if (CurrentState != BossState.Vanishing && CurrentState != BossState.LeapSlam)
            {
                _verticalVelocity += -20f * Time.deltaTime;
            }

            // 2026-08-28, "著地y座標從0.623改為0.5" - while LeapSlam is pinning the transform to its
            // locked landing Y (see UpdateLeapSlam), don't Move the CharacterController at all: it
            // holds nothing but a stationary pose, and any Move would depenetrate the capsule back
            // up off a landing Y that sits at/below the natural flush rest.
            if (CurrentState == BossState.LeapSlam && _leapSlamHolding)
            {
                return;
            }

            // spec ("五適硬值時仍然是在空中躺平") - same reasoning as the LeapSlam guard above: the
            // collapse pose's root was manually dropped by ApplyPostureBrokenGroundDrop() to compensate
            // the clip's floaty baked Hips height. A grounded CharacterController.Move() would
            // immediately depenetrate the capsule back up and undo the drop every frame.
            if (CurrentState == BossState.PostureBroken && _postureBrokenDropApplied)
            {
                return;
            }

            // 2026-08-29 - only ever applies within the Attack state itself. Previously guarded on
            // _currentAttack alone, which stays non-null for a frame or two after LeapSlam/
            // Breakdance pre-empt an interruptible attack (CHANGELOG's flagged "root-motion attack
            // trap") - harmless while every attack was useRootMotion=0, a real drift once
            // PW2_LeapSmash / PW2_ChargeSlam turned it on. Needs BossAnimatorRootMotionRelay on the
            // Animator's GameObject for animator.deltaPosition to be non-zero.
            Vector3 rootMotionDelta = Vector3.zero;
            if (CurrentState == BossState.Attack
                && _currentAttack != null && _currentAttack.UseRootMotion && animator != null)
            {
                float normalized = AnimatorNormalizedTime();
                if (normalized >= _currentAttack.RootMotionStartNormalized && normalized <= _currentAttack.RootMotionEndNormalized)
                {
                    // 2026-09-02 - scale the Meshy clip's baked RootT travel down to what the attack
                    // should actually cover (前刺/扭劈/翻滾). Stays REAL root motion (transform +
                    // capsule + lock-on all move together, no desync). RootMotionAimAtTarget makes
                    // the scale per-cast so the gap-closer actually reaches the player (see BeginAttack).
                    float rmScale = _rootMotionScaleRuntime >= 0f ? _rootMotionScaleRuntime : _currentAttack.RootMotionScale;
                    rootMotionDelta = animator.deltaPosition * rmScale;
                    rootMotionDelta.y = 0f; // vertical stays gravity/grounding-driven (clips import keepOriginalPositionY)
                }
            }

            Vector3 motion = _horizontalVelocity;
            motion.y = _verticalVelocity;
            _controller.Move((motion * Time.deltaTime) + rootMotionDelta);

            // 2026-08-29 ("他們出不了牆") - a confined 精怪 can chase up to the walls but not through
            // the doorway. Soft clamp back onto the square after the Move; the CharacterController
            // re-syncs on its next Move. See TryLeashReset / UpdateGateWatch for the watch-at-the-
            // gate behaviour once the player is outside.
            if (confineToArena)
            {
                Vector3 clamped = ArenaBounds.ClampInside(transform.position, arenaCenterXZ, arenaHalfExtent);
                if ((clamped - transform.position).sqrMagnitude > 1e-6f)
                {
                    transform.position = clamped;
                }
            }
        }

        // ---------------------------------------------------------------- Animator plumbing

        private void WriteAnimatorParameters()
        {
            if (animator == null || !animator.isActiveAndEnabled) return;

            // ReturnHome is a disengage - the fight is over for now, so drop the combat stance
            // (and the boss HUD, see WushiBossHudVisibility) even though the boss is still moving.
            animator.SetBool(BossAnimatorParams.CombatActive,
                CurrentState != BossState.Dormant && CurrentState != BossState.Dead
                && CurrentState != BossState.GettingUp
                && CurrentState != BossState.Victory && CurrentState != BossState.ReturnHome);
            animator.SetFloat(BossAnimatorParams.MovementSpeed, CurrentHorizontalSpeed);
            animator.SetInteger(BossAnimatorParams.Phase, Phase == BossPhase.Phase1 ? 0 : 1);
            animator.SetBool(BossAnimatorParams.Grounded, _controller.isGrounded);

            // Foot-sync while moving on the Locomotion blend tree - see locomotionAuthoredSpeed.
            // Approach / GateWatch / ReturnHome are the only states that drive it toward a real
            // translation speed; every other state is a one-shot clip whose pace must not be
            // touched, and OnExitState resets animator.speed to 1 the instant they end (and
            // re-asserts it for PostureBroken's own speed=0 hold).
            if (locomotionAuthoredSpeed > 0.01f
                && (CurrentState == BossState.Approach || CurrentState == BossState.GateWatch
                    || CurrentState == BossState.ReturnHome))
            {
                animator.speed = ComputeStrideRate(CurrentHorizontalSpeed, locomotionAuthoredSpeed);
            }
        }

        // 1 (normal playback) unless the boss is closing faster than the Locomotion blend tree's
        // top clip was authored for - then the ratio, clamped so the deceleration tail near the
        // player doesn't drop into visible slow-motion and a mistuned speed can't blur the run.
        public static float ComputeStrideRate(float currentSpeed, float authoredSpeed, float minRate = 0.6f, float maxRate = 2.5f)
        {
            if (authoredSpeed <= 0.01f)
            {
                return 1f;
            }

            return Mathf.Clamp(currentSpeed / authoredSpeed, minRate, maxRate);
        }

        private float AnimatorNormalizedTime()
        {
            if (animator == null) return 0f;
            var info = animator.GetCurrentAnimatorStateInfo(0);
            return info.normalizedTime % 1f;
        }

        private bool AnimatorHasFinished()
        {
            if (animator == null) return true;
            var info = animator.GetCurrentAnimatorStateInfo(0);
            return !animator.IsInTransition(0) && info.normalizedTime >= 1f;
        }

        // 2026-08-26, real playtested bug ("希望變成連砍" - SwordJudgment deriving into
        // OverheadSlam via TryRollSweepDerivation while CurrentState is already Attack, same class
        // of case as the too-close punish kick interrupting a swing) - CrossFadeInFixedTime doesn't
        // take effect inside the Animator until its own next internal Update pass, so for at least
        // one frame after BeginAttack() starts a brand new clip, GetCurrentAnimatorStateInfo(0) can
        // still report the OUTGOING clip's own stale normalizedTime (often already >=0.98 for a
        // combo that derives right at the tail of the attack it's chaining FROM) while
        // IsInTransition(0) also still reads false - both halves of "normalized >= 0.98f ||
        // AnimatorHasFinished()" can trip on that one-frame-stale data and immediately re-end an
        // attack before its own animation ever visibly played (confirmed: currentAttack correctly
        // became the derived attack, then flipped straight back to null one frame later, with the
        // Animator never actually leaving the previous clip). ChangeState always zeroes _stateTimer
        // on entry, so gating on a small minimum guarantees every fresh attack gets at least a few
        // real frames before this can fire, giving the Animator time to genuinely start the new clip.
        private const float MinStateTimeBeforeFinishCheck = 0.1f;

        private bool IsAttackAnimationFinished(float normalized)
        {
            if (_stateTimer < MinStateTimeBeforeFinishCheck) return false;
            return normalized >= 0.98f || AnimatorHasFinished();
        }

        private void ApplyRootMotionIfEnabled(BossAttackDefinition attack, float normalized)
        {
            // Actual application happens centrally in ApplyMotion() (single Move() call per
            // frame) - this method is a documented no-op hook kept separate so the intent
            // ("root motion only for states that opt in, only within their own configured
            // window") reads clearly at the call site inside UpdateAttack.
        }

        // ---------------------------------------------------------------- Vanish/dive support

        private bool _diveLanded;
        private float _diveLandedAtTime;
        private float _postureBreakDuration;
        private float _ultimateStartupDuration;

        private void ResetVanishCycle()
        {
            _combatTimeAccumulated = 0f;
            _vanishPending = false;
            _vanishTimer = 0f;
            _landingPointLocked = false;
            _diveLanded = false;
            _lastVanishEndTime = Time.time;
        }

        private void RestoreRenderers()
        {
            foreach (var r in _renderers) if (r != null) r.enabled = true;
        }

        private void HideRenderers()
        {
            foreach (var r in _renderers) if (r != null) r.enabled = false;
        }

        private Vector3 ComputeLandingPoint()
        {
            if (target == null) return transform.position;
            Vector3[] candidateOffsets =
            {
                -target.forward, // directly behind
                Quaternion.Euler(0f, -35f, 0f) * -target.forward, // behind-left
                Quaternion.Euler(0f, 35f, 0f) * -target.forward,  // behind-right
            };
            float distance = Random(tuning.VanishLandingBehindDistanceMin, tuning.VanishLandingBehindDistanceMax);

            foreach (var offset in candidateOffsets)
            {
                Vector3 desired = target.position + offset.normalized * distance;
                if (TrySampleNavMesh(desired, out Vector3 valid))
                {
                    return valid;
                }
            }
            // Nearest valid position to the player as a last resort, per spec ("依序嘗試...及附近
            // 最近的合法位置").
            if (TrySampleNavMesh(target.position, out Vector3 nearest))
            {
                return nearest;
            }
            return target.position; // NavMesh missing entirely - see PiHaiWangBossSetup's own bake step
        }

        private bool TrySampleNavMesh(Vector3 desired, out Vector3 result)
        {
            if (UnityEngine.AI.NavMesh.SamplePosition(desired, out var hit, 3f, UnityEngine.AI.NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
            result = desired;
            return false;
        }

        // ---------------------------------------------------------------- Ultimate energy hook

        private void CheckUltimateEnergy()
        {
            if (ultimateEnergy == null || _ultimatePending) return;
            if (ultimateEnergy.IsFull)
            {
                _ultimatePending = true;
                Log("ultimatePending = true (energy full)");
            }
        }

        // ---------------------------------------------------------------- Utility

        private void ChangeState(BossState next)
        {
            if (CurrentState == next) return;
            OnExitState(CurrentState);
            Log(CurrentState + " -> " + next);
            CurrentState = next;
            _stateTimer = 0f;
            OnEnterState(next);
        }

        private void OnExitState(BossState state)
        {
            if (state == BossState.Attack || state == BossState.DodgeCounter || state == BossState.UltimateAttack
                || state == BossState.Breakdance || state == BossState.LeapSlam)
            {
                CloseAllHitboxes(); // spec: "離開攻擊狀態時必須保證所有Hitbox關閉"
            }

            // Safety net for the get-up i-frames (see OnEnterState(GettingUp)) - UpdateGettingUp
            // already clears this on its normal exit, but never leave the boss invulnerable if it
            // somehow leaves GettingUp another way.
            if (state == BossState.GettingUp && health != null)
            {
                health.SetInvulnerable(this, false);
            }

            // spec item 7 - if PostureBroken ends any way other than a deathblow resolving
            // (DeathblowPhaseTransition / DeathblowFinalKill both call EndExecutionHold themselves),
            // don't leave the boss frozen-invulnerable from a finisher that never completed.
            if (state == BossState.PostureBroken && _executionInvuln)
            {
                EndExecutionHold();
            }

            // Safety net for ApplyPostureBrokenGroundDrop - if PostureBroken is pre-empted (Dead is
            // the one case that can actually happen, e.g. HP hits 0 mid-collapse) rather than ending
            // through EndPostureBroken normally, never leave the root sunk below its real ground height.
            if (state == BossState.PostureBroken && _postureBrokenDropApplied)
            {
                RestorePostureBrokenGroundDrop();
            }

            // 2026-08-26, explicit user request (Boss AI spec, section 三) - UpdatePostureBroken()
            // pauses the Animator (animator.speed=0) to hold the kneeling pose. If PostureBroken is
            // pre-empted by a higher-priority state (Dead is the one case that can actually happen -
            // "如果HP在跪地期間歸零,立即切換死亡" - Priority cascade in Update() already checks Dead
            // before PostureBroken every frame) the speed=0 would otherwise leak into whatever state
            // comes next and silently freeze its animation too. Unconditional reset here, on every
            // single state exit regardless of which state, is cheap and guarantees this can never
            // leak - cheaper to always reset than to enumerate every path that could leave
            // PostureBroken.
            if (animator != null) animator.speed = 1f;
        }

        // 2026-08-24 design simplification, given the sheer number of states this spec asks for -
        // rather than hand-wiring a full AnyState/Trigger transition graph for every one of the 14
        // states (error-prone at this scale, and the spec itself only wants AnyState reserved for
        // "真正高優先級轉場" anyway), every discrete action state is entered by CrossFading straight
        // to its own clip's Animator state by name from C# - the priority arbitration already
        // happened in Resolve()/TryEnter* above, so the Animator doesn't need its own transition
        // logic to re-decide anything. Trigger PARAMETERS are still set alongside (satisfies the
        // spec's own required-parameter list / gives an Inspector-visible record of what just
        // fired) even though they don't drive the actual transition. Locomotion is the one
        // exception - it stays a live blend tree driven by MovementSpeed every frame, since that's
        // continuous motion, not a one-shot clip.
        private void OnEnterState(BossState state)
        {
            switch (state)
            {
                case BossState.Idle:
                case BossState.Approach:
                case BossState.GateWatch:
                case BossState.ReturnHome:
                case BossState.UltimateReposition:
                    if (state == BossState.Approach)
                    {
                        _attackReadinessBuffer = Random(tuning.AttackReadinessBufferMinSeconds, tuning.AttackReadinessBufferMaxSeconds);
                    }
                    if (state == BossState.GateWatch)
                    {
                        _gateWatchGiveUpTimer = 0f;
                    }
                    // "Locomotion" is the blend-tree STATE name (see PiHaiWangBossSetup), not a
                    // single clip - Idle is just that blend tree at MovementSpeed=0, which
                    // WriteAnimatorParameters already drives every frame from actual velocity.
                    // UltimateReposition reuses it too (drives off _horizontalVelocity's magnitude
                    // regardless of direction) - known limitation, not hidden: backing away plays
                    // the forward Walking/Running clip, so it reads as a "moonwalk" rather than a
                    // real backpedal animation. This pack has no dedicated backward-step take.
                    PlayState(LocomotionStateName);
                    break;
                case BossState.Vanishing:
                    HideRenderers();
                    if (health != null) health.SetInvulnerable(this, true);
                    _vanishTimer = 0f;
                    _landingPointLocked = false;
                    animator?.SetTrigger(BossAnimatorParams.VanishTrigger);
                    break;
                case BossState.DiveAttack:
                    RestoreRenderers();
                    if (health != null) health.SetInvulnerable(this, false);
                    _diveLanded = false;
                    _diveLandedAtTime = float.MaxValue;
                    animator?.SetTrigger(BossAnimatorParams.DiveTrigger);
                    PlayState(diveLandClipName);
                    break;
                case BossState.UltimatePrepare:
                    _ultimateStartupDuration = Random(tuning.UltimateStartupMinSeconds, tuning.UltimateStartupMaxSeconds);
                    _ultimateTrackingLocked = false;
                    break;
                case BossState.UltimateAttack:
                    animator?.SetTrigger(BossAnimatorParams.UltimateTrigger);
                    if (ultimateAttack != null) PlayState(ultimateAttack.ClipName);
                    _ultimateLungeStopLogged = false;
                    _ultimateContactTime = 0f;
                    _ultimateHitDone = false;
                    // 2026-08-29, user ("為甚麼飛踢步行呢") - pop into the air on the first lunge frame
                    // so the forward leap is a real flying kick, not a ground slide. ApplyMotion's
                    // gravity arcs it back down through the strike. 0 = the old flat slide.
                    if (_controller.isGrounded && tuning.UltimateLeapJumpSpeed > 0f)
                    {
                        _verticalVelocity = tuning.UltimateLeapJumpSpeed;
                    }
                    break;
                case BossState.DodgeCounter:
                    animator?.SetTrigger(BossAnimatorParams.DodgeCounterTrigger);
                    if (dodgeCounterAttack != null) PlayState(dodgeCounterAttack.ClipName);
                    break;
                case BossState.PostureBroken:
                    animator?.SetTrigger(BossAnimatorParams.PostureBreakTrigger);
                    PlayState(string.IsNullOrEmpty(kneelStandClipName) ? fall3ClipName : kneelStandClipName);
                    break;
                case BossState.Breakdance:
                    animator?.SetTrigger(BossAnimatorParams.BreakdanceTrigger);
                    if (breakdanceAttack != null) PlayState(breakdanceAttack.ClipName);
                    break;
                case BossState.LeapSlamWindup:
                    // 2026-08-28, user feedback ("大招起飛前搖不要蹲下 改站在原地") - just hold the idle/
                    // locomotion pose for the windup. The tell is that the boss stops moving and
                    // attacking for tuning.LeapSlamWindupSeconds; no crouch. UpdateLeapSlamWindup
                    // keeps facing the player. When it ends, OnEnterState(LeapSlam) CrossFades the
                    // leap clip normally from 0 (its own opening frames carry the takeoff).
                    PlayState(LocomotionStateName);
                    break;
                case BossState.LeapSlam:
                    animator?.SetTrigger(BossAnimatorParams.AttackTrigger);
                    if (leapSlamAttack != null)
                    {
                        PlayState(leapSlamAttack.ClipName);
                    }
                    break;
                case BossState.HitReaction:
                    // Only BeHit_FlyUp exists in this pack - no generic light-hit-flinch clip -
                    // so HitReaction always plays the launch variant regardless of _pendingLaunch.
                    // Flagged in the final report as a real gap, not silently papered over.
                    animator?.SetTrigger(BossAnimatorParams.HitFlyUpTrigger);
                    PlayState(behitFlyUpClipName);
                    break;
                case BossState.Victory:
                    animator?.SetTrigger(BossAnimatorParams.VictoryTrigger);
                    PlayState(walkToSitClipName);
                    break;
                case BossState.Dead:
                    // 2026-08-24, explicit user request ("死亡後應該要是BeHit_FlyUp動作 五秒後
                    // 復活") - originally reused BeHit_FlyUp since no dedicated death take
                    // existed in the old asset pack. 2026-08-25 (combat AI spec, section 八): the
                    // new pack ships a real Shot_and_Fall_Forward take - deathClipName now plays
                    // that when set, falling back to behitFlyUpClipName only for older bosses
                    // that never configured it (see deathClipName's own field comment).
                    animator?.SetTrigger(BossAnimatorParams.Dead);
                    PlayState(DeathStateName());
                    CancelAllPending();
                    CloseAllHitboxes(); // spec: "關閉刀刃、腳部及其他傷害Hitbox" the instant Dead is entered, not next frame
                    _deathElapsed = 0f;
                    OnDeath?.Invoke();
                    break;
                case BossState.GettingUp:
                    // Hard-cut to the LAST frame of the death take (same face-down pose the boss is
                    // already holding, so no visible pop), then freeze the Animator - UpdateGettingUp
                    // scrubs it backwards to standing over tuning.StandUpSeconds. i-frames for the
                    // rise so a stray hit can't launch the boss out of its own get-up animation;
                    // cleared in UpdateGettingUp / OnExitState.
                    if (animator != null)
                    {
                        animator.Play(DeathStateName(), 0, 1f);
                        animator.speed = 0f;
                    }
                    if (health != null) health.SetInvulnerable(this, true);
                    break;
                case BossState.Attack:
                    if (_currentAttack != null)
                    {
                        // 2026-09-01, user report ("先鎖定好目標的方向再進行施展") - a committed
                        // directional attack (ContinuousThrust) snaps its yaw to the target here,
                        // before frame 0, so its early hit frames don't stab past a slightly-off
                        // player while FaceTarget is still rotating toward them at capped speed.
                        if (_currentAttack.FaceTargetSnapOnStart && target != null)
                        {
                            Vector3 toTarget = target.position - transform.position;
                            toTarget.y = 0f;
                            if (toTarget.sqrMagnitude > 0.0001f)
                            {
                                transform.rotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up)
                                                     * Quaternion.Euler(0f, _currentAttack.FacingYawOffsetDegrees, 0f);
                            }
                        }

                        int poolIndex = normalAttackPool != null ? System.Array.IndexOf(normalAttackPool, _currentAttack) : -1;
                        animator?.SetInteger(BossAnimatorParams.AttackID, poolIndex); // -1 for a derived move (e.g. Sweeping_Kick) - informational only, doesn't drive the transition
                        animator?.SetTrigger(BossAnimatorParams.AttackTrigger);
                        PlayState(_currentAttack.ClipName);
                    }
                    break;
            }
        }

        private void PlayState(string clipStateName)
        {
            if (animator == null || string.IsNullOrEmpty(clipStateName)) return;
            // 2026-08-26, explicit user request ("有辦法讓武士做出的每個動作名稱都列在Console嗎") -
            // this is the single chokepoint every Animator state transition already goes through
            // (Locomotion, every attack's ClipName, kneel/death states - see OnEnterState's own
            // comment on why it CrossFades by name instead of driving transitions), so logging
            // here catches every animation name with no risk of missing/duplicating a case.
            // Gated by the existing logStateChanges toggle (already on for 武士) rather than a new
            // field - same on/off switch as every other [Boss FSM] log line.
            Log($"PlayState: {clipStateName}");
            animator.CrossFadeInFixedTime(clipStateName, 0.08f, 0, 0f);
        }

        private void CancelAllPending()
        {
            _ultimatePending = false;
            _vanishPending = false;
            _breakdancePending = false;
            _leapSlamPending = false;
            _periodicSlamPending = false;
            _dodgeWindowRequested = false;
            _pendingDerivedAttack = null;
            _forcedHitReactionPending = false;
            _tooCloseTimer = 0f;
            _tooCloseThresholdLogged = false;
        }

        private float CooldownUntil(BossAttackDefinition attack)
        {
            return attack != null && _cooldownUntil.TryGetValue(attack, out float t) ? t : 0f;
        }

        private void SetCooldown(BossAttackDefinition attack, float seconds)
        {
            if (attack == null) return;
            _cooldownUntil[attack] = Time.time + seconds;
        }

        private float Random(float min, float max)
        {
            return min + (float)_random.NextDouble() * (max - min);
        }

        private void Log(string message)
        {
            if (logStateChanges)
            {
                Debug.Log("[Boss FSM] " + message, this);
            }
        }

        // 2026-08-25, explicit user request (combat AI spec, section 十) - Scene-view-only
        // visualization of every distance/angle number PickAttack() actually gates on, so
        // "why didn't it attack" is answerable by looking at the Scene view instead of guessing.
        // Gizmos.DrawWireSphere/DrawWireArc-equivalent calls only run when the GameObject is
        // selected (OnDrawGizmosSelected) - drawing them unconditionally for every boss in the
        // scene at once would be visual noise, same reasoning EnemyAI's own gizmo range circle
        // uses elsewhere in this project.
        [Header("Debug Gizmos")]
        [SerializeField] private bool showGizmos = true;

        private void OnDrawGizmosSelected()
        {
            if (!showGizmos || tuning == null)
            {
                return;
            }

            Vector3 origin = transform.position;

            // Alert range - first-engagement radius (see UpdateDormant).
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(origin, tuning.AlertRange);

            // Facing direction / current attack angle cone reference line.
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(origin, origin + transform.forward * 2f);

            // Each normal attack's own min/max distance ring, colored by whether it's currently
            // a valid candidate (distance+angle+cooldown+repeat-guard all satisfied right now) -
            // green if PickAttack() could choose it THIS instant, gray if geometrically out of
            // range/angle, red if in range but still on cooldown.
            if (normalAttackPool != null)
            {
                float distanceNow = Application.isPlaying ? HorizontalDistance() : -1f;
                float angleNow = Application.isPlaying ? AngleToTarget() : -1f;
                foreach (var attack in normalAttackPool)
                {
                    if (attack == null) continue;

                    bool inRange = Application.isPlaying && distanceNow >= attack.MinDistance && distanceNow <= attack.MaxDistance && angleNow <= attack.MaxAngleDegrees;
                    bool onCooldown = Application.isPlaying && Time.time < CooldownUntil(attack);
                    Gizmos.color = !Application.isPlaying ? new Color(1f, 1f, 1f, 0.35f)
                        : (inRange && !onCooldown) ? new Color(0.2f, 1f, 0.2f, 0.7f)
                        : onCooldown ? new Color(1f, 0.3f, 0.2f, 0.6f)
                        : new Color(0.6f, 0.6f, 0.6f, 0.35f);
                    DrawHorizontalRing(origin, attack.MaxDistance);
                    if (attack.MinDistance > 0.01f)
                    {
                        DrawHorizontalRing(origin, attack.MinDistance);
                    }
                }
            }

            // Ultimate ideal/max distance rings.
            Gizmos.color = new Color(0.7f, 0.2f, 1f, 0.7f);
            DrawHorizontalRing(origin, tuning.UltimateIdealMinDistance);
            DrawHorizontalRing(origin, tuning.UltimateIdealMaxDistance);
            Gizmos.color = new Color(0.7f, 0.2f, 1f, 0.35f);
            DrawHorizontalRing(origin, tuning.UltimateMaxDistance);
            // Trigger threshold - inside this ring the ultimate won't fire (UltimateReposition
            // backs away first instead); only fires between this ring and the outer max-distance one.
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.6f);
            DrawHorizontalRing(origin, tuning.UltimateMaxDistance * tuning.UltimateMinTriggerDistanceFraction);
        }

        private static void DrawHorizontalRing(Vector3 center, float radius)
        {
            const int segments = 32;
            Vector3 prev = center + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }

        private void LateUpdate()
        {
            // Ultimate-energy polling lives in LateUpdate so it observes this frame's own
            // Update()-driven drain (e.g. a landed dodge-counter finisher) before deciding
            // ultimatePending for the NEXT frame - avoids a same-frame read-after-write race.
            CheckUltimateEnergy();
        }
    }
}
