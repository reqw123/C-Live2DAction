using UnityEngine;

namespace Live2DAction.Combat.Boss
{
    // Data-driven definition for one boss attack/animation-state. One asset per move
    // (PunchCombo/PunchCombo3/GuardKick/SweepingKick/DodgeCounterAttack/RisingFlyingKick) -
    // every number the design doc asked to be tunable lives here rather than hardcoded in
    // BossStateMachine, matching this project's existing AttackData precedent (see
    // Live2DAction.Combat.AttackData) but extended with the selection/derivation/root-motion
    // fields a boss moveset needs that a simple player combo didn't.
    [CreateAssetMenu(fileName = "BossAttack", menuName = "Live2DAction/Combat/Boss Attack Definition")]
    public class BossAttackDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string attackId = "BossAttack";
        [Tooltip("Name of the imported AnimationClip this state plays (see PiHaiWang's PW_* clip names).")]
        [SerializeField] private string clipName;

        [Header("Selection - when is this move even a candidate")]
        [SerializeField] private float minDistance = 0f;
        [SerializeField] private float maxDistance = 2f;
        [Tooltip("Half-angle in degrees the player must be within, measured from the boss's forward.")]
        [SerializeField] private float maxAngleDegrees = 60f;
        [SerializeField] private float cooldownSeconds = 0f;
        [SerializeField] private float selectionWeightPhase1 = 1f;
        [SerializeField] private float selectionWeightPhase2 = 1f;
        [Tooltip("If true, this move can never be picked twice in a row by BossStateMachine's own combo-repeat guard.")]
        [SerializeField] private bool disallowImmediateRepeat = true;
        // 2026-08-24, bug report ("被我攻擊後他就開始伸懶腰了") - disallowImmediateRepeat alone
        // treats every attack as "never repeat", but the spec explicitly allows Punch_Combo up
        // to 2 uses in a row (its own 0s cooldown was meant to fill exactly this kind of gap).
        // With PunchCombo3 (4.5s) and GuardKick (5s) as the only other options, a strict
        // never-repeat rule left the boss with NOTHING pickable for several real seconds at a
        // time right after using either of those - BossStateMachine correctly holds still in
        // Idle rather than log-spamming (see AttackReadinessDistance's own fix), but standing
        // still for that long reads as "boss stopped reacting". Lets an attack repeat up to this
        // many times before disallowImmediateRepeat's guard kicks in - default 1 keeps every
        // existing attack's old "never repeat" behavior unless explicitly raised.
        [Tooltip("How many times in a row this move may be picked before disallowImmediateRepeat blocks it. 1 = never repeat (old default behavior).")]
        [SerializeField] private int maxConsecutiveUses = 1;

        [Header("Timing (seconds, against the real clip length)")]
        [SerializeField] private float startupSeconds = 0.35f;
        [SerializeField] private float recoverySeconds = 0.3f;

        [Header("Facing / tracking")]
        [Tooltip("2026-09-01, user report ('先鎖定好目標的方向再進行施展 不然很像在打空氣') - if true, " +
                 "the boss's yaw SNAPS straight to the target the instant this attack starts, before " +
                 "the first frame plays, instead of only rotating toward it at startupTracking speed. " +
                 "For committed directional attacks (a straight thrust) whose early hit frames would " +
                 "otherwise stab past a target that was even slightly off-axis at commit time.")]
        [SerializeField] private bool faceTargetSnapOnStart;

        [Tooltip("2026-09-02, user report (Wushi_CrossSlash: '攻擊比較偏右...讓武士稍微往左轉一點') - " +
                 "degrees added to the boss's facing while this attack aims at the target (both the " +
                 "snap-on-start and the per-frame tracking). Use it when a clip's swing is baked " +
                 "off-centre: negative turns the boss LEFT, positive RIGHT. 0 = aim straight at the target.")]
        [SerializeField, Range(-60f, 60f)] private float facingYawOffsetDegrees = 0f;

        [Tooltip("2026-09-02 (ContinuousThrust - 前墊步連刺 aimed at a much shorter player's chest). " +
                 "Degrees the boss's whole visual leans FORWARD/DOWN during the attack (eased in over " +
                 "startup, held through the hit windows, eased out at the end). Drops the blade hitbox " +
                 "to a grounded player's height without touching the upright CharacterController. " +
                 "0 = no lean.")]
        [SerializeField, Range(0f, 45f)] private float attackPitchDegrees = 0f;

        [Tooltip("0-1 turn-speed multiplier applied to the boss's normal rotation speed while this " +
                 "attack's startup is running (1 = full tracking, matches EnemyAI's rotationSpeedDegrees).")]
        [SerializeField, Range(0f, 1f)] private float startupTracking = 1f;
        [Tooltip("Same, but for the recovery/active portion after startupTracking's window ends - " +
                 "e.g. Punch_Combo's own spec: full tracking before hit 1, <=40% after, 10-20% on " +
                 "later hits.")]
        [SerializeField, Range(0f, 1f)] private float lateTracking = 0.15f;
        [Tooltip("Normalized time (0-1) at which tracking drops from startupTracking to lateTracking.")]
        [SerializeField, Range(0f, 1f)] private float trackingDropNormalizedTime = 0.3f;

        [Header("Damage")]
        // 2026-08-28, explicit user request ("不要硬編碼血量 而是百分比設定") - when this is on,
        // baseHealthDamage is a PERCENT of the hit target's own max health (e.g. 5 => 5%) resolved
        // at hit time, not a flat HP number. Lets "every non-major attack chips a fixed 5%" be a
        // design knob that survives the player's max-HP being retuned, instead of a magic 25 baked
        // into each asset. Falls back to treating baseHealthDamage as a flat amount if the target
        // exposes no readable max health. poise damage is unaffected (still flat).
        [Tooltip("If true, baseHealthDamage is a PERCENT of the target's max health (5 = 5%), " +
                 "resolved when the hit lands - not a flat HP amount.")]
        [SerializeField] private bool healthDamageIsPercentOfTargetMax;
        [SerializeField] private float baseHealthDamage = 10f;
        [SerializeField] private float basePoiseDamage = 8f;
        [SerializeField] private float knockbackForce = 2f;
        [SerializeField] private bool launchesTarget;

        [Header("Command grab (scripted proximity capture)")]
        [Tooltip("2026-09-02 (屁孩王 ScissorTakedown). For acrobatic clips whose contact point can't " +
                 "be pinned to a bone hitbox - the head-scissor inverts the boss so its scissoring " +
                 "feet end ~2m up while a grounded player's hurtbox tops out lower, and a bone-parented " +
                 "0.35m foot sphere landing <1m away never overlaps. When true, BossStateMachine does " +
                 "ONE horizontal distance test to the target at CommandGrabNormalized instead of the " +
                 "collider hit windows: within CommandGrabRadius => apply this asset's health/poise " +
                 "damage + knockback + launch directly. Resolves at most once per attack. Leave " +
                 "hitWindows empty on a command-grab attack so it can't also double-hit via a collider.")]
        [SerializeField] private bool commandGrab;
        [SerializeField, Range(0f, 1f)] private float commandGrabNormalized = 0.45f;
        [SerializeField, Min(0.1f)] private float commandGrabRadius = 2f;

        [Header("Defensive properties")]
        [Tooltip("If true, HitReaction/interrupt logic can cut this attack short (posture break, " +
                 "player launch, etc). False for attacks a design explicitly wants to always finish.")]
        [SerializeField] private bool interruptible = true;
        [Tooltip("If true, incoming damage during this attack's ACTIVE hit windows doesn't stagger " +
                 "the boss (super armor) - e.g. Punch_Combo_3's final hit, Rising_Flying_Kick after launch.")]
        [SerializeField] private bool superArmorDuringActiveWindows;
        [Tooltip("0-1 multiplier applied to incoming player damage while this move's own defensive " +
                 "pose is active (Boxing_Guard_Right_Straight_Kick's guard stance) - does NOT reduce " +
                 "poise damage, per spec ('仍正常累積架勢傷害,不得完全無敵').")]
        [SerializeField, Range(0f, 1f)] private float incomingDamageMultiplierWhileGuarding = 1f;

        [Header("Root motion")]
        [Tooltip("If true, BossStateMachine reads Animator.deltaPosition/deltaRotation during this " +
                 "clip and feeds it into the CharacterController.Move call itself, instead of the " +
                 "usual code-driven approach movement - for attacks with a real, intentional lunge " +
                 "(see design doc's Punch_Combo_3/GuardKick root-motion note). False (default) means " +
                 "this state moves exactly like every other state: code-only, Animator never touches " +
                 "position.")]
        [SerializeField] private bool useRootMotion;
        [SerializeField, Range(0f, 1f)] private float rootMotionStartNormalized;
        [SerializeField, Range(0f, 1f)] private float rootMotionEndNormalized = 1f;
        [Tooltip("2026-09-02 - multiplier on Animator.deltaPosition while useRootMotion is on. 1 = the " +
                 "clip's full baked travel. <1 shrinks a Meshy clip that walks several metres more than " +
                 "the attack should. Ignored when rootMotionAimAtTarget is on (that computes the scale " +
                 "per cast). Still real root motion - transform, capsule and lock-on move together.")]
        [SerializeField, Range(0f, 2f)] private float rootMotionScale = 1f;
        [Tooltip("2026-09-02 (前刺/扭轉前劈/翻滾撲擊 gap-closers) - when on, rootMotionScale is REPLACED " +
                 "at commit by whatever makes the clip's net forward travel land the boss " +
                 "rootMotionAimGapMeters short of the player's live position (clamped to " +
                 "rootMotionAimMaxMeters). Needs rootMotionClipForwardMeters set to the clip's own " +
                 "measured net forward RootT travel.")]
        [SerializeField] private bool rootMotionAimAtTarget;
        [SerializeField] private float rootMotionAimGapMeters = 1.4f;
        [SerializeField] private float rootMotionAimMaxMeters = 6f;
        [SerializeField] private float rootMotionClipForwardMeters = 1f;

        [Header("Hit windows - one per visible strike, never one covering the whole clip")]
        [SerializeField] private BossHitWindow[] hitWindows = System.Array.Empty<BossHitWindow>();

        // 2026-09-01, spec item 5 sub-step 5A. Programmatic forward lunge so the gameplay root tracks
        // a clip whose forward travel is baked into the hips (ChargeCut / DoubleCombo's second beat).
        // forwardDistance 0 (default) = no displacement = every existing attack unchanged.
        [Header("Attack motion (5A - programmatic lunge; forwardDistance 0 = off)")]
        [SerializeField] private BossAttackMotionProfile attackMotion = new BossAttackMotionProfile();

        [Tooltip("2026-09-02 (Wushi_ScissorTakedown - a jump-in command grab). If true, the attackMotion " +
                 "lunge distance is REPLACED at commit by the live horizontal gap to the target minus " +
                 "lungeTargetGapMeters (clamped 0..lungeMaxMeters), so the leap lands ON the player " +
                 "instead of a fixed baked distance. Uses the same attackMotion window/curve; set " +
                 "attackMotion.forwardDistance to any tiny non-zero value is NOT required - this flag " +
                 "alone engages the lunge. Pair with a clip imported lockRootPositionXZ so the clip's " +
                 "own baked forward travel doesn't stack on top.")]
        [SerializeField] private bool lungeDistanceFromTargetGap;
        [SerializeField] private float lungeTargetGapMeters = 1.2f;
        [SerializeField] private float lungeMaxMeters = 7f;

        [Tooltip("2026-09-02 - many Meshy attack clips bake several metres of forward 'walk' into the " +
                 "Hips muscle animation (NOT the root curve, so lockRootPositionXZ can't remove it). " +
                 "When true, BossStateMachine cancels that Hips XZ drift every frame during the attack " +
                 "so the visible body stays anchored - then attackMotion / lungeDistanceFromTargetGap " +
                 "add whatever controlled forward travel the attack should actually have.")]
        [SerializeField] private bool cancelClipBodyDrift;

        [Header("Derivation (e.g. Sweeping_Kick chained off Punch_Combo/Punch_Combo_3)")]
        [Tooltip("Attack this move can derive into after its own recovery - null if this move never derives.")]
        [SerializeField] private BossAttackDefinition derivedAttack;
        [Tooltip("Normalized time window (within THIS clip) during which the derivation roll/entry may happen.")]
        [SerializeField, Range(0f, 1f)] private float deriveWindowStartNormalized = 0.7f;
        [SerializeField, Range(0f, 1f)] private float deriveWindowEndNormalized = 0.95f;
        [SerializeField, Range(0f, 1f)] private float deriveChancePhase1;
        [SerializeField, Range(0f, 1f)] private float deriveChancePhase2;
        [Tooltip("Derivation chance is halved when this attack's own hit windows all missed - see " +
                 "spec's '本次拳擊完全未命中時,派生機率減半'.")]
        [SerializeField] private bool halveDeriveChanceOnFullMiss = true;
        [SerializeField] private float deriveCooldownSeconds = 5f;

        // 2026-08-26, explicit user request (Boss AI spec, section 五) - "360°旋轉斬、重劈等大型攻擊:
        // 額外休息2~3秒". BossStateMachine.EndAttack() reads this to add
        // BossTuning.MajorAttackExtraRest*Seconds on top of the normal global rest window whenever
        // a flagged attack finishes - a plain bool rather than a numeric override so the actual
        // extra-rest duration stays centralized/tunable in BossTuning like every other global timing
        // number, instead of being duplicated per-attack-asset.
        [Header("Pacing")]
        [Tooltip("360°旋轉斬、重劈等大型攻擊 - adds BossTuning's major-attack extra rest on top of the " +
                 "normal global rest window after this attack finishes.")]
        [SerializeField] private bool isMajorAttack;

        [Header("Notes / QA")]
        [TextArea]
        [SerializeField] private string designNotes;

        public string AttackId => attackId;
        public string ClipName => clipName;
        public float MinDistance => minDistance;
        public float MaxDistance => maxDistance;
        public float MaxAngleDegrees => maxAngleDegrees;
        public bool FaceTargetSnapOnStart => faceTargetSnapOnStart;
        public float FacingYawOffsetDegrees => facingYawOffsetDegrees;
        public float AttackPitchDegrees => attackPitchDegrees;
        public float CooldownSeconds => cooldownSeconds;
        public bool DisallowImmediateRepeat => disallowImmediateRepeat;
        public int MaxConsecutiveUses => Mathf.Max(1, maxConsecutiveUses);
        public float SelectionWeight(BossPhase phase) => phase == BossPhase.Phase1 ? selectionWeightPhase1 : selectionWeightPhase2;
        public float StartupSeconds => startupSeconds;
        public float RecoverySeconds => recoverySeconds;
        public float StartupTracking => startupTracking;
        public float LateTracking => lateTracking;
        public float TrackingDropNormalizedTime => trackingDropNormalizedTime;
        public float BaseHealthDamage => baseHealthDamage;
        public bool HealthDamageIsPercentOfTargetMax => healthDamageIsPercentOfTargetMax;
        public float BasePoiseDamage => basePoiseDamage;
        public float KnockbackForce => knockbackForce;
        public bool LaunchesTarget => launchesTarget;
        public bool CommandGrab => commandGrab;
        public float CommandGrabNormalized => Mathf.Clamp01(commandGrabNormalized);
        public float CommandGrabRadius => Mathf.Max(0.1f, commandGrabRadius);
        public bool Interruptible => interruptible;
        public bool SuperArmorDuringActiveWindows => superArmorDuringActiveWindows;
        public float IncomingDamageMultiplierWhileGuarding => incomingDamageMultiplierWhileGuarding;
        public bool UseRootMotion => useRootMotion;
        public float RootMotionStartNormalized => rootMotionStartNormalized;
        public float RootMotionEndNormalized => rootMotionEndNormalized;
        public float RootMotionScale => Mathf.Max(0f, rootMotionScale);
        public bool RootMotionAimAtTarget => rootMotionAimAtTarget;
        public float RootMotionAimGapMeters => rootMotionAimGapMeters;
        public float RootMotionAimMaxMeters => Mathf.Max(0f, rootMotionAimMaxMeters);
        public float RootMotionClipForwardMeters => Mathf.Max(0.01f, rootMotionClipForwardMeters);
        public BossHitWindow[] HitWindows => hitWindows;
        public BossAttackMotionProfile AttackMotion => attackMotion;
        public bool LungeDistanceFromTargetGap => lungeDistanceFromTargetGap;
        public float LungeTargetGapMeters => lungeTargetGapMeters;
        public float LungeMaxMeters => Mathf.Max(0f, lungeMaxMeters);
        public bool CancelClipBodyDrift => cancelClipBodyDrift;
        public BossAttackDefinition DerivedAttack => derivedAttack;
        public float DeriveWindowStartNormalized => deriveWindowStartNormalized;
        public float DeriveWindowEndNormalized => deriveWindowEndNormalized;
        public float DeriveChance(BossPhase phase) => phase == BossPhase.Phase1 ? deriveChancePhase1 : deriveChancePhase2;
        public bool HalveDeriveChanceOnFullMiss => halveDeriveChanceOnFullMiss;
        public float DeriveCooldownSeconds => deriveCooldownSeconds;
        public bool IsMajorAttack => isMajorAttack;

#if UNITY_EDITOR
        // Editor-only bulk setter so setup scripts (PiHaiWangBossSetup) can configure these
        // assets from measured data without hand-editing 20+ SerializedProperty calls per asset -
        // same "build once from code, tune later in Inspector" precedent as this project's other
        // ScriptableObject setup tools (see AttackData usage in MeshyBossSetup).
        public void EditorConfigure(
            string id, string clip, float minDist, float maxDist, float angle, float cooldown,
            float weight1, float weight2, float startup, float recovery,
            float trackStartup, float trackLate, float trackDrop,
            float healthDamage, float poiseDamage, float knockback, bool launches,
            bool canInterrupt, bool superArmor, float guardMultiplier,
            bool rootMotion, float rmStart, float rmEnd,
            BossHitWindow[] windows, string notes)
        {
            attackId = id;
            clipName = clip;
            minDistance = minDist;
            maxDistance = maxDist;
            maxAngleDegrees = angle;
            cooldownSeconds = cooldown;
            selectionWeightPhase1 = weight1;
            selectionWeightPhase2 = weight2;
            startupSeconds = startup;
            recoverySeconds = recovery;
            startupTracking = trackStartup;
            lateTracking = trackLate;
            trackingDropNormalizedTime = trackDrop;
            baseHealthDamage = healthDamage;
            basePoiseDamage = poiseDamage;
            knockbackForce = knockback;
            launchesTarget = launches;
            interruptible = canInterrupt;
            superArmorDuringActiveWindows = superArmor;
            incomingDamageMultiplierWhileGuarding = guardMultiplier;
            useRootMotion = rootMotion;
            rootMotionStartNormalized = rmStart;
            rootMotionEndNormalized = rmEnd;
            hitWindows = windows ?? System.Array.Empty<BossHitWindow>();
            designNotes = notes;
        }

        public void EditorConfigureDerivation(BossAttackDefinition derived, float windowStart, float windowEnd,
            float chance1, float chance2, bool halveOnMiss, float derivedCooldown)
        {
            derivedAttack = derived;
            deriveWindowStartNormalized = windowStart;
            deriveWindowEndNormalized = windowEnd;
            deriveChancePhase1 = chance1;
            deriveChancePhase2 = chance2;
            halveDeriveChanceOnFullMiss = halveOnMiss;
            deriveCooldownSeconds = derivedCooldown;
        }
#endif
    }
}
