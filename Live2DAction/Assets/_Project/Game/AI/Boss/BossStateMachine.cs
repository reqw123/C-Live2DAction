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

        [Header("Debug")]
        [SerializeField] private bool logStateChanges;

        public BossState CurrentState { get; private set; } = BossState.Dormant;
        public BossPhase Phase { get; private set; } = BossPhase.Phase1;

        // 2026-08-26, explicit user request ("把具體踢的範圍畫出來讓我排錯") - read-only exposure so
        // an external visualizer (TooCloseRangeIndicator) can draw the REAL threshold/progress
        // instead of a guessed number - same "read the real thing" reasoning as
        // PlayerCombat.MaxAttackReach's own comment.
        public float EffectiveTooCloseDistance => _targetCombat != null
            ? Mathf.Max(tuning.TooCloseDistance, _targetCombat.MaxAttackReach)
            : tuning.TooCloseDistance;
        public float TooCloseProgress01 => tuning.TooCloseDurationSeconds > 0f
            ? Mathf.Clamp01(_tooCloseTimer / tuning.TooCloseDurationSeconds)
            : 0f;

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
        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;

        private bool _hasEngaged; // Alert reached at least once
        private bool _phase2Locked;
        private bool _postureUnsteady;
        private float _decisionTimer;
        private float _stateTimer; // seconds since entering CurrentState
        private BossAttackDefinition _currentAttack;
        private int _lastActiveHitWindowIndex = -1;
        private readonly HashSet<BossHitbox> _openHitboxesThisAttack = new HashSet<BossHitbox>();
        private readonly Dictionary<BossAttackDefinition, float> _cooldownUntil = new Dictionary<BossAttackDefinition, float>();
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
        private bool _vanishPending;
        // 2026-08-26, explicit user request ("玩家極近距離靠近武士時 容易躲避所有攻擊") - continuous
        // (not accumulated-whenever-close) seconds at/under the effective too-close distance; resets
        // the instant the player steps back out, unlike _breakdanceTimeAccumulated above which never
        // resets - see UpdateTooCloseTimer/TryEnterTooCloseKick.
        private float _tooCloseTimer;
        // Cached at Awake - see UpdateTooCloseTimer for why the effective threshold reads this
        // instead of (or in addition to) tuning.TooCloseDistance alone.
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
        // 2026-08-26, explicit user request (Boss AI spec, section 五 - "全域休息時間") - see
        // BossTuning's own comment on the paired fields this is rolled from.
        private float _globalRestUntil = -999f;

        private System.Random _random = new System.Random();

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _renderers = GetComponentsInChildren<Renderer>(true);

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
            var hitboxes = GetComponentsInChildren<BossHitbox>(true);
            foreach (var hitbox in hitboxes)
            {
                hitbox.Configure(transform, "Boss");
            }

            // 2026-08-26, explicit user request ("這個極近距離應該要對齊玩家的極限攻擊距離 保證玩家
            // 在最遠能攻擊到武士的情況下 能觸發武士的踢擊並擊退") - see UpdateTooCloseTimer and
            // PlayerCombat.MaxAttackReach's own comment for why this reads the player's real combo
            // Range+Radius instead of a separately-guessed tuning number.
            _targetCombat = target != null ? target.GetComponent<PlayerCombat>() : null;
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
            if (CurrentState != BossState.Dead)
            {
                if (health != null && health.IsDead) { ChangeState(BossState.Dead); }
                else if (CurrentState == BossState.Victory) { /* terminal, nothing pre-empts it */ }
                else if (TryEnterPostureBroken()) { }
                else if (TryEnterHitReaction()) { }
                else if (TryContinuePhaseTransitionVisual()) { }
                else if (TryContinueCommittedSpecialAttack()) { }
                else if (TryEnterUltimate()) { }
                else if (TryEnterUltimateReposition()) { }
                else if (TryEnterVanish()) { }
                else if (TryEnterDodgeCounter()) { }
                else if (TryEnterBreakdance()) { }
                else if (TryEnterLeapSlam()) { }
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
            bool eligible = _hasEngaged && bothAlive && CurrentState != BossState.Vanishing
                             && CurrentState != BossState.DiveAttack && CurrentState != BossState.Dead
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
            _leapSlamTimeAccumulated += Time.deltaTime;
            if (!_leapSlamPending && _leapSlamTimeAccumulated >= tuning.LeapSlamTriggerSeconds)
            {
                _leapSlamPending = true;
                Log("leapSlamPending = true");
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
        private bool TryEnterBreakdance()
        {
            if (!_breakdancePending || CurrentState == BossState.Breakdance || breakdanceAttack == null)
            {
                return false;
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
            ChangeState(BossState.Breakdance);
            return true;
        }

        // 2026-08-27, explicit user request ("定時小技能，戰鬥每經過20秒就觸發，他是一個先飛升到空
        // 中，然後落地劈砍的攻擊動作，落地時請直接鎖定玩家，並且落下的期間全程具有攻擊幀 範圍大") -
        // same "queued by combat-time timer" pattern as TryEnterBreakdance above. "落地時直接鎖定玩
        // 家" is handled by teleporting to the player's CURRENT position (XZ only - Wushi's own
        // grounded Y is kept, never snapped to the player's Y) and committing the facing ONCE here,
        // not by continuously homing during the leap - measured via AnimationMode.SampleAnimationClip
        // that leapSlamAttack's own clip already does the entire rise (~11 units up) and fall
        // ENTIRELY through its own Hips-bone animation with the root Transform never moving, so
        // simply starting the clip already positioned on the player is sufficient for the visual
        // leap-and-slam to land right on them - no separate airborne-physics tracking needed (unlike
        // Vanishing/DiveAttack's real gravity fall).
        private bool TryEnterLeapSlam()
        {
            if (!_leapSlamPending || CurrentState == BossState.LeapSlam || leapSlamAttack == null || target == null)
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
            _leapSlamPending = false;
            _leapSlamTimeAccumulated = 0f;
            _leapSlamPrevExtraHeight = 0f;

            Vector3 landingPos = target.position;
            landingPos.y = transform.position.y;
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
            Vector3 lookDir = target.position - transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);

            Log("LeapSlam: locked onto player at " + landingPos);
            ChangeState(BossState.LeapSlam);
            return true;
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
                || CurrentState == BossState.Breakdance)
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
                case BossState.Attack: UpdateAttack(); break;
                case BossState.DodgeCounter: UpdateDodgeCounter(); break;
                case BossState.Breakdance: UpdateBreakdance(); break;
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
            }
        }

        private void UpdateDormant()
        {
            _horizontalVelocity = Vector3.zero;
            if (target == null) return;
            if (HorizontalDistance() <= tuning.AlertRange)
            {
                _hasEngaged = true;
                ChangeState(BossState.Alert);
            }
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
            if (HorizontalDistance() > tuning.AlertRange * 1.5f)
            {
                ChangeState(BossState.Dormant);
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
            bool sprinting = _sprinting;

            float moveSpeed = ResolveMoveSpeed();
            float readinessDistance = AttackReadinessDistance();

            if (distance > readinessDistance + tuning.ApproachDecelerationDistance)
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
            }
        }

        private void UpdateAttack()
        {
            if (_currentAttack == null)
            {
                ChangeState(BossState.Idle);
                return;
            }

            _horizontalVelocity = Vector3.zero; // ground melee plants feet, matches EnemyAI's own convention

            float normalized = AnimatorNormalizedTime();
            float trackAmount = normalized < _currentAttack.TrackingDropNormalizedTime
                ? _currentAttack.StartupTracking
                : _currentAttack.LateTracking;
            FaceTarget(trackAmount);

            UpdateHitWindows(_currentAttack, normalized);
            ApplyRootMotionIfEnabled(_currentAttack, normalized);

            TryRollSweepDerivation(normalized);

            if (IsAttackAnimationFinished(normalized))
            {
                EndAttack();
            }
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
        // 圍大") - deliberately NO FaceTarget() tracking call here (unlike UpdateAttack/UpdateBreakdance
        // above) - the landing position and facing are committed ONCE in TryEnterLeapSlam right before
        // this state is entered; re-aiming mid-leap would fight the clip's own baked-in rise-and-fall
        // motion and read as the character sliding around in the air. UpdateHitWindows against
        // leapSlamAttack's own wide hit window (spans the whole measured fall, not just the landing
        // instant - see that asset's own designNotes) does the rest exactly like any other attack.
        // 2026-08-27, explicit user request ("不能跳很高嗎 至少讓玩家看不到的高度") - drives
        // _verticalVelocity directly from an explicit height curve (computed each frame from the
        // DELTA between this frame's and last frame's target extra-height, divided by deltaTime) so
        // ApplyMotion's own existing _controller.Move() call carries the root Transform through a
        // real arc reaching leapSlamExtraHeight world units above ground, layered on top of the
        // clip's own much smaller baked bone motion (~11 units - see leapSlamAttack's designNotes).
        // Velocity-based rather than directly setting transform.position every frame so it stays
        // Move()-consistent (proper collision resolution) rather than teleporting through geometry.
        private float _leapSlamPrevExtraHeight;

        private void UpdateLeapSlam()
        {
            if (leapSlamAttack == null) { ChangeState(BossState.Idle); return; }

            _horizontalVelocity = Vector3.zero;
            float normalized = AnimatorNormalizedTime();

            float targetExtraHeight = ComputeLeapSlamExtraHeight(normalized);
            float deltaHeight = targetExtraHeight - _leapSlamPrevExtraHeight;
            _verticalVelocity = deltaHeight / Mathf.Max(Time.deltaTime, 0.0001f);
            _leapSlamPrevExtraHeight = targetExtraHeight;

            UpdateHitWindows(leapSlamAttack, normalized);

            if (IsAttackAnimationFinished(normalized))
            {
                CloseAllHitboxes();
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

            // 2026-08-25, user feedback ("必殺技應該距離很遠才對") - the leap previously had zero
            // actual world-space translation: UseRootMotion is off on the RisingFlyingKick asset,
            // and even if it were on, ApplyMotion's root-motion branch only ever reads _currentAttack
            // (set by BeginAttack for normal attacks), which the ultimate path never sets - so no
            // mechanism moved the boss at all during the kick. Lunge forward in the direction locked
            // during UltimatePrepare's windup (transform.forward - NOT re-tracking the player, per
            // spec's "no air-tracking/snap-turn after leap") until the strike's own hit window opens,
            // then plant so it doesn't keep sliding through the target.
            //
            // 2026-08-25 follow-up, user feedback ("踢到的瞬間玩家就要飛出去 你沒做到這點") - tried
            // stopping the lunge early once within UltimateIdealMaxDistance instead of running all
            // the way to the hit window, on the theory that the leg's own ~2.5-2.6m reach (measured
            // via SampleAnimation) was overshooting past a target already at melee range. Reverted -
            // live-tested twice and it caused straight MISSES (0 damage) at the very distance
            // (~2.95m) that measurement said should connect, meaning something about the real
            // in-game reach/alignment doesn't match the edit-mode SampleAnimation measurement
            // closely enough to trust for a tight plant distance. Lunging all the way to melee range
            // is empirically reliable (repeatedly confirmed landing full damage) even though the
            // choreography doesn't visually match a mid-range target - functional connection over
            // unverified visual polish.
            bool beforeStrike = true;
            if (ultimateAttack != null && ultimateAttack.HitWindows != null && ultimateAttack.HitWindows.Length > 0)
            {
                beforeStrike = normalized < ultimateAttack.HitWindows[0].startNormalized;
            }
            if (!beforeStrike && !_ultimateLungeStopLogged)
            {
                _ultimateLungeStopLogged = true;
                Log($"UltimateAttack: lunge planted at normalized={normalized:F2}, dist-to-player={HorizontalDistance():F2}.");
            }
            _horizontalVelocity = beforeStrike ? transform.forward * tuning.UltimateLeapSpeed : Vector3.zero;

            UpdateHitWindows(ultimateAttack, normalized);

            if (IsAttackAnimationFinished(normalized))
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
            if (CurrentState == BossState.Dead || CurrentState == BossState.Vanishing) return;
            _pendingLaunch = true;
            _forcedHitReactionPending = true;
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
                if (stateInfo.normalizedTime >= tuning.PostureKneelNormalizedTime)
                {
                    animator.speed = 0f; // pause the Animator state - Time.timeScale is untouched
                    _postureKneelReached = true;
                    _stateTimer = 0f; // restart the timer so the hold duration below is measured from the moment we actually froze, not from state-entry
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
                if (_stateTimer >= _postureBreakDuration)
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
            stance.EndStagger(); // zeroes stance + grants existing postStaggerGraceSeconds invulnerability
            stance.RestoreStanceFractionAfterRecovery(tuning.PostureRestoreOnRecover); // spec 13.7: back up to ~20%, not a fresh 0%
            ChangeState(BossState.Idle);
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
            if (tuning.PermanentDeath)
            {
                return;
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
            _tooCloseTimer = 0f;
            _tooCloseThresholdLogged = false;
            RestoreRenderers();
            ChangeState(BossState.Alert);
        }

        // ---------------------------------------------------------------- Attack selection / execution

        private BossAttackDefinition PickAttack()
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
                if (distance < attack.MinDistance || distance > attack.MaxDistance) continue;
                if (angle > attack.MaxAngleDegrees) continue;
                if (Time.time < CooldownUntil(attack)) continue;
                if (attack.DisallowImmediateRepeat && attack == _lastNormalAttack && _lastNormalAttackConsecutiveCount >= attack.MaxConsecutiveUses) continue;
                float w = attack.SelectionWeight(Phase);
                if (w <= 0f) continue;
                weighted.Add((attack, w));
                total += w;
            }
            if (weighted.Count == 0 || total <= 0f)
            {
                Log($"PickAttack: no candidate in range (dist={distance:F2}, angle={angle:F1}, phase={Phase}) - holding.");
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
            _lastNormalAttackConsecutiveCount = (attack == _lastNormalAttack) ? _lastNormalAttackConsecutiveCount + 1 : 1;
            _lastNormalAttack = attack;
            _attackLandedAnyHit = false;
            _lastActiveHitWindowIndex = -1;
            SetCooldown(attack, attack.CooldownSeconds);
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
            // "當前不可中斷動作完成後" - an attack becomes safely pre-emptible once its own
            // Interruptible flag is true, OR once its last active hit window has closed.
            if (_currentAttack == null) return true;
            if (_currentAttack.Interruptible) return true;
            return !IsInsideAnyActiveWindow();
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
            Vector3 to = target.position - transform.position;
            to.y = 0f;
            _horizontalVelocity = to.sqrMagnitude > 0.0001f ? to.normalized * speed : Vector3.zero;
        }

        private void FaceTarget(float trackingAmount)
        {
            if (target == null || trackingAmount <= 0f) return;
            Vector3 to = target.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.0001f) return;
            Quaternion desired = Quaternion.LookRotation(to.normalized, Vector3.up);
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
            // 2026-08-27, explicit user request ("不能跳很高嗎") - excluded for the same reason as
            // the gravity accumulation below: LeapSlam computes an exact _verticalVelocity every
            // frame from its own height curve (see UpdateLeapSlam), and this generic "snap to a
            // small downward trickle once grounded" clamp would clobber that precise value the
            // instant the controller reports grounded=true anywhere near the curve's own landing
            // point, fighting the intended timing.
            if (_controller.isGrounded && _verticalVelocity < 0f && CurrentState != BossState.LeapSlam)
            {
                _verticalVelocity = -1f;
            }
            // Dive/vanish handle their own vertical arc separately (teleport-in-from-above then
            // fall) - normal gravity still applies while airborne during DiveAttack so it reads
            // as a real fall onto the landing point rather than an instant snap.
            // 2026-08-27, explicit user request ("不能跳很高嗎 至少讓玩家看不到的高度") - LeapSlam
            // drives _verticalVelocity itself every frame from its own explicit height curve (see
            // UpdateLeapSlam) rather than falling under normal gravity - the clip's own baked
            // Hips-bone motion only reaches ~11 units up (see this move's own designNotes), nowhere
            // near "off-screen", so the extra height is a script-driven arc layered on top of the
            // root Transform, and real gravity would fight that arc if left enabled here.
            if (CurrentState != BossState.Vanishing && CurrentState != BossState.LeapSlam)
            {
                _verticalVelocity += -20f * Time.deltaTime;
            }

            Vector3 rootMotionDelta = Vector3.zero;
            if (_currentAttack != null && _currentAttack.UseRootMotion && animator != null)
            {
                float normalized = AnimatorNormalizedTime();
                if (normalized >= _currentAttack.RootMotionStartNormalized && normalized <= _currentAttack.RootMotionEndNormalized)
                {
                    rootMotionDelta = animator.deltaPosition;
                }
            }

            Vector3 motion = _horizontalVelocity;
            motion.y = _verticalVelocity;
            _controller.Move((motion * Time.deltaTime) + rootMotionDelta);
        }

        // ---------------------------------------------------------------- Animator plumbing

        private void WriteAnimatorParameters()
        {
            if (animator == null || !animator.isActiveAndEnabled) return;

            animator.SetBool(BossAnimatorParams.CombatActive, CurrentState != BossState.Dormant && CurrentState != BossState.Dead && CurrentState != BossState.Victory);
            animator.SetFloat(BossAnimatorParams.MovementSpeed, CurrentHorizontalSpeed);
            animator.SetInteger(BossAnimatorParams.Phase, Phase == BossPhase.Phase1 ? 0 : 1);
            animator.SetBool(BossAnimatorParams.Grounded, _controller.isGrounded);
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
                case BossState.UltimateReposition:
                    if (state == BossState.Approach)
                    {
                        _attackReadinessBuffer = Random(tuning.AttackReadinessBufferMinSeconds, tuning.AttackReadinessBufferMaxSeconds);
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
                case BossState.LeapSlam:
                    animator?.SetTrigger(BossAnimatorParams.AttackTrigger);
                    if (leapSlamAttack != null) PlayState(leapSlamAttack.ClipName);
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
                    PlayState(string.IsNullOrEmpty(deathClipName) ? behitFlyUpClipName : deathClipName);
                    CancelAllPending();
                    CloseAllHitboxes(); // spec: "關閉刀刃、腳部及其他傷害Hitbox" the instant Dead is entered, not next frame
                    _deathElapsed = 0f;
                    OnDeath?.Invoke();
                    break;
                case BossState.Attack:
                    if (_currentAttack != null)
                    {
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
