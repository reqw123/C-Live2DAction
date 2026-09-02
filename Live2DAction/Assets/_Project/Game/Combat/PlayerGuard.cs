using UnityEngine;
using Live2DAction.Characters;
using Live2DAction.CameraSystem;
using Live2DAction.Combat.Boss;
using Live2DAction.Core;
using Live2DAction.Input;

namespace Live2DAction.Combat
{
    // 2026-08-31, user request ("現在我要把滑鼠右鍵改成武士刀防禦"). Hold right mouse (GuardPressed)
    // to raise the katana guard.
    //
    // 2026-09-01, user request (Sekiro-style deflect - 「玩家防禦、一般格擋、完美彈反」). Two windows
    // now, both driven by TIME since the guard-button PRESS EDGE (not by any animation frame):
    //   - press edge → GuardStart, _guardStartTime = now, ParryWindow opens for parryWindowDuration.
    //   - within [0, parryWindowDuration]  → CurrentDefense == Parry
    //   - held, past that window            → CurrentDefense == Guard
    //   - release / stagger / death         → CurrentDefense == None, everything closes
    // Holding the button can NOT re-open the parry window; only a fresh press can.
    //
    // Two paths reach this component:
    //   1. A swept BOSS BLADE whose sweep first crosses the player's guard volume calls
    //      IBladeClashReceiver.TryResolveClash - that's where Parry / Guard / (fall-through) is
    //      decided, boss posture + recoil applied on a Parry, sparks / SFX / hit-stop / shake fired.
    //   2. Anything else that lands on the player's Health while guarding (a kick, or a blade that
    //      got PAST the guard volume to the body) runs through ModifyIncoming. Kicks still get the
    //      soft frontal block; a boss BLADE that reaches here is a body-first hit and takes FULL
    //      damage (per spec C - the clash system owns blades).
    //
    // Mitigation runs through IIncomingDamageModifier, so it applies no matter which collider the
    // hit lands on (Player root Health, or the PlayerHurtbox child forwarding via HurtboxLink).
    [RequireComponent(typeof(Health))]
    public class PlayerGuard : MonoBehaviour, IIncomingDamageModifier, IBladeClashReceiver
    {
        public enum DefenseState { None, Guard, Parry }

        [Header("Wiring")]
        [Tooltip("PlayerInputProvider (as IInputCommand). GuardPressed = right mouse held, GuardPressedThisFrame = its press edge.")]
        [SerializeField] private MonoBehaviour inputSource;

        [Tooltip("Optional. Ground speed is scaled by blockedSpeedMultiplier while blocking. Auto-found on this GameObject if unset.")]
        [SerializeField] private CharacterMovement movement;

        [Tooltip("Optional. Guard can't be raised while dead. Auto-found on this GameObject if unset.")]
        [SerializeField] private Health health;

        [Tooltip("Optional. Guard can't be raised while staggered; a blocked hit builds this. Auto-found on this GameObject if unset.")]
        [SerializeField] private StancePoise stance;

        [Tooltip("Optional. Camera shake on a clash. Auto-found on Camera.main if unset.")]
        [SerializeField] private CameraShake cameraShake;

        [Tooltip("The forearm bone rotated for the procedural guard pose (the sword-arm bone). " +
                 "Typically Bip001-R-Forearm. Leave null to skip the pose entirely.")]
        [SerializeField] private Transform swordArmBone;

        [Tooltip("The upper-arm bone, rotated alongside swordArmBone so the whole arm can raise " +
                 "into a cross-body guard. Typically Bip001-R-UpperArm. Optional.")]
        [SerializeField] private Transform upperArmBone;

        [Header("Block geometry")]
        [Tooltip("Full cone (degrees) centred on the player's facing within which an incoming hit " +
                 "is considered blocked. Spec default 120 (Dot(forward, toBoss) >= cos(60deg)).")]
        [SerializeField, Range(0f, 360f)] private float guardArcDegrees = 120f;

        [Header("Deflect windows")]
        [Tooltip("Base seconds after the guard-button PRESS EDGE that count as a perfect parry - a " +
                 "quick TAP within this window deflects (button doesn't have to stay held). Sekiro's " +
                 "is ~0.2 s (12 frames @60). The EFFECTIVE window is this x the anti-mash scale below.")]
        [SerializeField] private float parryWindowDuration = 0.2f;

        [Tooltip("After a press edge, you still count as GUARDING (soft block, not a full hit) for " +
                 "this long even if the button was released - so a slightly mistimed tap still " +
                 "blocks instead of eating a clean hit. Must be >= parryWindowDuration.")]
        [SerializeField] private float tapGuardWindowSeconds = 0.55f;

        [Header("Anti-mash (Sekiro-style)")]
        [Tooltip("A press edge THIS soon after the previous one counts as mashing and shrinks the " +
                 "parry window. Space your guards further apart than this to keep the full window.")]
        [SerializeField] private float mashResetSeconds = 0.35f;

        [Tooltip("Fraction of the parry-window scale lost per mashed press (0.4 = each spam-tap " +
                 "cuts the effective window by 40%). Worst case the window shrinks to minMashScale.")]
        [SerializeField, Range(0f, 1f)] private float mashShrinkPerTap = 0.4f;

        [Tooltip("How fast the parry-window scale recovers toward 1 when you're not mashing (per second).")]
        [SerializeField] private float mashRecoverPerSecond = 1.2f;

        [Tooltip("Floor the anti-mash shrink can't go below. 0 = a hard masher can lose the parry " +
                 "window entirely (pure Sekiro); 0.15 keeps a sliver.")]
        [SerializeField, Range(0f, 1f)] private float minMashScale = 0f;

        [Tooltip("A successful PARRY snaps the window scale back to full - so reading + landing a " +
                 "deflect resets the mash penalty (Sekiro does this).")]
        [SerializeField] private bool restoreScaleOnParry = true;

        [Tooltip("2026-09-01 (用戶: 連續攻擊下要能連續彈反) - for this long AFTER a landed parry, while " +
                 "the guard is still up, an incoming frontal clash auto-counts as a Parry even " +
                 "without a fresh press edge, and a re-press is exempt from the anti-mash penalty. " +
                 "Lets you ride a deflect into the next hit of a boss combo. 0 = off.")]
        [SerializeField] private float comboParryGraceSeconds = 0.8f;

        [Tooltip("Minimum seconds between two resolved clashes against this guard - stops one scrape " +
                 "spawning a burst of sparks / SFX / posture ticks. Low enough that a multi-hit " +
                 "attack's separate windows each register.")]
        [SerializeField] private float clashCooldownSeconds = 0.06f;

        [Header("Mitigation (non-blade frontal hits only - kicks)")]
        [Tooltip("Fraction of incoming HEALTH damage that still gets through a soft frontal block of " +
                 "a NON-blade hit. Boss blade strikes ignore this (handled by the clash path).")]
        [SerializeField, Range(0f, 1f)] private float blockedDamageMultiplier = 0.15f;

        [Tooltip("Poise a soft-blocked hit still delivers, as a multiple of its raw damage. KEEP " +
                 "EQUAL TO StancePoise.stanceGainMultiplier (0.2).")]
        [SerializeField, Range(0f, 2f)] private float poiseMultiplier = 0.2f;

        [Header("Clash outcome - Guard")]
        [Tooltip("Chip HP damage the player still takes on a plain guard. 0 = none.")]
        [SerializeField] private float guardChipDamage = 0f;
        [Tooltip("2026-09-01 (spec item 6): a plain guard now costs the ATTACK's own poise damage " +
                 "(BladeClashInfo.PoiseDamage), scaled by this. 1 = the full per-attack poise. So " +
                 "guarding SwordJudgment (22) pressures stance ~2x a ChargeCut (12).")]
        [SerializeField] private float guardPoiseMultiplier = 1f;
        [Tooltip("Fallback poise for a plain guard only when the incoming hit carries NO per-attack " +
                 "poise value (a clash with no BossAttackDefinition behind it). Real boss attacks " +
                 "always supply their own, so this is rarely used.")]
        [SerializeField] private float guardPlayerPoiseDamage = 6f;
        [SerializeField] private float guardHitStopSeconds = 0.05f;
        [Tooltip("Time-scale during the guard hit-stop. Near 1 = barely a hitch; low = hard freeze. " +
                 "Spec says 輕微 for a guard, so keep this gentle (a hard freeze every blocked hit reads as jitter).")]
        [SerializeField, Range(0f, 1f)] private float guardHitStopScale = 0.4f;
        [Tooltip("Camera shake on a plain guard. 0 = none (spec only asks for shake on a parry; a " +
                 "guard shaking every hit reads as camera jitter).")]
        [SerializeField] private float guardShakeAmplitude = 0f;
        [SerializeField] private float guardShakeSeconds = 0.08f;

        [Header("Clash outcome - Parry (perfect deflect)")]
        [Tooltip("Posture damage the PLAYER takes on a perfect parry - spec says极低 or 0.")]
        [SerializeField] private float parryPlayerPoiseDamage = 0f;
        [Tooltip("Posture damage the BOSS takes on a perfect parry (via StancePoise.AddPostureDamage). High.")]
        [SerializeField] private float parryBossPoiseDamage = 14f;
        [SerializeField] private float parryHitStopSeconds = 0.10f;
        [Tooltip("Time-scale during the parry hit-stop. A perfect deflect can bite harder than a guard.")]
        [SerializeField, Range(0f, 1f)] private float parryHitStopScale = 0.15f;
        [SerializeField] private float parryShakeAmplitude = 0.06f;
        [SerializeField] private float parryShakeSeconds = 0.16f;

        [Header("While blocking")]
        [Tooltip("Ground move-speed multiplier while the guard is up. 1 = no slowdown.")]
        [SerializeField, Range(0f, 1f)] private float blockedSpeedMultiplier = 0.35f;

        [Header("Procedural pose")]
        [Tooltip("Blend a procedural 2-bone blade-up pose over the animation. Set FALSE once an " +
                 "authored Guard clip drives the Animator (Phase 3) - otherwise the two fight.")]
        [SerializeField] private bool useProceduralPose = true;
        [SerializeField] private Vector3 guardPoseLocalEuler = new Vector3(-55f, 25f, -165f);
        [SerializeField] private Vector3 upperArmGuardLocalEuler = new Vector3(-30f, -40f, -18f);
        [SerializeField] private bool invertPose;
        [SerializeField] private float guardBlendSpeed = 9f;

        // Fired each time a NON-blade hit is soft-blocked (frontal, while guarding), with the
        // ORIGINAL DamageInfo. (PlayerGuardClashSfx listens.)
        public event System.Action<DamageInfo> Blocked;

        // 2026-09-01 - fired at the world contact point of a resolved blade clash.
        public event System.Action<Vector3> Parried;
        public event System.Action<Vector3> Guarded;

        private IInputCommand Input => inputSource as IInputCommand;
        private float _poseBlend;
        private bool _ownsSpeedKnob;
        private float _guardStartTime = float.NegativeInfinity;
        private float _lastClashTime = -999f;
        private float _lastPressTime = float.NegativeInfinity;
        private float _parryScale = 1f;
        // 2026-09-01 (spec item 2): CancelDefenseAction sets this so a defense hard-ends even with
        // the button still held (Execution / Ultimate / death / stagger); it clears on release.
        private bool _defenseSuppressed;

        // 0..1 - the anti-mash shrink on the parry window (1 = full, 0 = no parry window). Read by
        // SekiroDeflectDebug.
        public float ParryWindowScale => _parryScale;
        // The parry window actually in effect this frame, after anti-mash.
        public float EffectiveParryWindow => parryWindowDuration * _parryScale;

        public bool IsBlocking
        {
            get
            {
                if (_defenseSuppressed)
                {
                    return false;
                }
                IInputCommand input = Input;
                if (input == null || !input.GuardPressed)
                {
                    return false;
                }
                if (health != null && health.IsDead)
                {
                    return false;
                }
                if (stance != null && stance.IsStaggered)
                {
                    return false;
                }
                return true;
            }
        }

        // 2026-09-01 (spec item 2): the SINGLE "the player is currently in a defensive action"
        // signal. True while the button is held (IsBlocking) OR still inside the tap-guard window
        // after a quick release. GuardVolume / Animator / movement slowdown / weapon pose / the
        // telegraph / debug all read THIS (or CurrentDefense) - none of them re-combine IsBlocking
        // with the tap window on their own any more.
        public bool DefenseActionActive => CanDefend && !_defenseSuppressed && (IsBlocking || InTapGuardWindow);

        public float GuardArcDegrees => guardArcDegrees;

        private bool CanDefend => (health == null || !health.IsDead) && (stance == null || !stance.IsStaggered);

        // True for comboParryGraceSeconds after a landed parry, while a defensive action is still up
        // - the "ride the deflect into the next combo hit" window (用戶: 連續彈反).
        private bool InComboParryGrace => comboParryGraceSeconds > 0f
            && DefenseActionActive
            && Time.time - LastParryTime < comboParryGraceSeconds;

        // The parry window is open for EffectiveParryWindow after the press EDGE, WHETHER OR NOT the
        // button is still held - Sekiro tap-to-deflect. The window is the base duration x the
        // anti-mash scale (mashing shrinks it, a landed parry / a pause restores it). OR: still
        // inside the combo-parry grace after a landed deflect.
        public bool InParryWindow => CanDefend
            && (InComboParryGrace
                || (EffectiveParryWindow > 0.001f
                    && BladeClashUtility.WithinParryWindow(Time.time, _guardStartTime, EffectiveParryWindow)));

        // A slightly mistimed tap (past the parry window but recent) still counts as a soft block
        // rather than a clean hit - forgiveness so a near-miss isn't fully punished.
        public bool InTapGuardWindow => CanDefend
            && BladeClashUtility.WithinParryWindow(Time.time, _guardStartTime,
                Mathf.Max(parryWindowDuration, tapGuardWindowSeconds));

        // Spec's None / Guard / Parry, evaluated live.
        public DefenseState CurrentDefense =>
            (DefenseState)PlayerGuardUtility.DefenseStateCode(InParryWindow, DefenseActionActive);

        // 2026-09-01 (spec item 2): hard-end the current defensive action even if the guard button
        // is still held - for Execution / Ultimate / death / stagger / guard-break entry. Clears
        // itself once the button is released.
        public void CancelDefenseAction()
        {
            _guardStartTime = float.NegativeInfinity;
            _defenseSuppressed = true;
        }

        public float LastBlockTime { get; private set; } = -999f;
        public float LastParryTime { get; private set; } = -999f;

        private void Awake()
        {
            if (health == null) health = GetComponent<Health>();
            if (movement == null) movement = GetComponent<CharacterMovement>();
            if (stance == null) stance = GetComponent<StancePoise>();
            if (cameraShake == null && Camera.main != null) cameraShake = Camera.main.GetComponent<CameraShake>();
        }

        private void OnDisable()
        {
            if (movement != null && _ownsSpeedKnob)
            {
                movement.ExternalSpeedMultiplier = 1f;
            }
            _ownsSpeedKnob = false;
            _poseBlend = 0f;
            _guardStartTime = float.NegativeInfinity;
        }

        private void Update()
        {
            IInputCommand input = Input;

            // Anti-mash: the parry-window scale recovers toward full whenever you're not mid-spam.
            _parryScale = Mathf.MoveTowards(_parryScale, 1f, mashRecoverPerSecond * Time.deltaTime);

            // Press EDGE opens a fresh parry window. Holding never refreshes it; RELEASING does NOT
            // close it (a quick tap still parries - Sekiro deflect). The window closes on its own
            // once EffectiveParryWindow elapses.
            if (input != null && input.GuardPressedThisFrame)
            {
                // A press too soon after the last one is a MASH - shrink the window (Sekiro:
                // "反覆放開再按防禦，彈反窗口會逐步縮短，最差可能變成 0"). EXCEPT right after a landed
                // parry - re-pressing for the next combo hit is rhythm, not mashing.
                bool comboRhythm = comboParryGraceSeconds > 0f && Time.time - LastParryTime < comboParryGraceSeconds;
                if (Time.time - _lastPressTime < mashResetSeconds && !comboRhythm)
                {
                    _parryScale = Mathf.Max(minMashScale, _parryScale - mashShrinkPerTap);
                }
                _lastPressTime = Time.time;
                _guardStartTime = Time.time;
            }
            // Death / stagger cancels a pending parry immediately.
            if (health != null && health.IsDead || stance != null && stance.IsStaggered)
            {
                CancelDefenseAction();
            }
            // A CancelDefenseAction suppression lifts as soon as the button is let go.
            if (input == null || !input.GuardPressed)
            {
                _defenseSuppressed = false;
            }

            bool defending = DefenseActionActive;

            if (movement != null)
            {
                movement.ExternalSpeedMultiplier = defending ? blockedSpeedMultiplier : 1f;
                _ownsSpeedKnob = true;
            }

            // Show the guard pose for the whole defensive action (held OR still inside the tap
            // window), so a quick tap-deflect still flashes the blade up.
            float poseTarget = defending ? 1f : 0f;
            _poseBlend = PlayerGuardUtility.StepBlend(_poseBlend, poseTarget, guardBlendSpeed, Time.deltaTime);
        }

        private void LateUpdate()
        {
            if (!useProceduralPose || _poseBlend <= 0.001f)
            {
                return;
            }
            float sign = invertPose ? -1f : 1f;
            if (upperArmBone != null)
            {
                Quaternion upperFull = Quaternion.Euler(upperArmGuardLocalEuler * sign);
                upperArmBone.localRotation *= Quaternion.Slerp(Quaternion.identity, upperFull, _poseBlend);
            }
            if (swordArmBone != null)
            {
                Quaternion full = Quaternion.Euler(guardPoseLocalEuler * sign);
                swordArmBone.localRotation *= Quaternion.Slerp(Quaternion.identity, full, _poseBlend);
            }
        }

        // ---- path 2: non-clash incoming damage (kicks, or a blade that reached the body) --------
        public DamageInfo ModifyIncoming(DamageInfo incoming)
        {
            if (!IsBlocking)
            {
                return incoming;
            }
            if (!PlayerGuardUtility.IsFrontalBlock(transform.forward, incoming.Direction, guardArcDegrees))
            {
                return incoming;
            }
            // A boss BLADE that reached ApplyDamage got PAST the guard volume to the body (spec C:
            // "繞過防禦刀刃、先命中身體" -> full-damage normal hit). The clash path (TryResolveClash,
            // called from BossHitbox.SweepCheck) owns blades; only kicks / non-blade frontal hits
            // get the soft block here.
            if (WasBossWeaponStrike(incoming))
            {
                return incoming;
            }

            LastBlockTime = Time.time;
            Blocked?.Invoke(incoming);

            float mitigated = PlayerGuardUtility.MitigatedAmount(incoming.Amount, blockedDamageMultiplier);
            // spec item 6: prefer the attacker's own per-hit poise (BossHitbox passes it as
            // ExplicitPoiseAmount); fall back to the damage-derived amount only when it's absent.
            float attackPoise = incoming.ExplicitPoiseAmount ?? PlayerGuardUtility.FullPoiseAmount(incoming.Amount, poiseMultiplier);
            float poise = PlayerGuardUtility.GuardPoiseGain(attackPoise, guardPoiseMultiplier, guardPlayerPoiseDamage);
            return new DamageInfo(mitigated, incoming.Point, incoming.Direction, incoming.Source, poise);
        }

        // ---- path 1: a swept boss blade first crossed the guard volume -------------------------
        public BladeClashResult TryResolveClash(in BladeClashInfo info)
        {
            bool frontal = PlayerGuardUtility.IsFrontalBlock(transform.forward, info.AttackDirectionFlat, guardArcDegrees);
            BladeClashResult result = BladeClashUtility.Classify(frontal, InParryWindow, IsBlocking || InTapGuardWindow);

            if (result == BladeClashResult.None)
            {
                return result;
            }
            if (!BladeClashUtility.ClashCooldownElapsed(Time.time, _lastClashTime, clashCooldownSeconds))
            {
                // Still "handled" (don't fall through to a body hit) but no new feedback.
                return result;
            }
            _lastClashTime = Time.time;
            LastBlockTime = Time.time;

            if (result == BladeClashResult.Parried)
            {
                LastParryTime = Time.time;
                // Landing a deflect wipes the anti-mash penalty (Sekiro: "成功彈反後則會恢復").
                if (restoreScaleOnParry) _parryScale = 1f;
                if (parryPlayerPoiseDamage > 0f) stance?.AddPostureDamage(parryPlayerPoiseDamage);

                var bossStance = info.Attacker != null ? info.Attacker.GetComponentInParent<StancePoise>() : null;
                bossStance?.AddPostureDamage(parryBossPoiseDamage);
                NotifyBossParried(info.Attacker, info.Reaction);

                HitStopController.Request(parryHitStopSeconds, parryHitStopScale);
                cameraShake?.Shake(parryShakeAmplitude, parryShakeSeconds);
                Parried?.Invoke(info.ContactPoint);
            }
            else // Guarded
            {
                if (guardChipDamage > 0f && health != null)
                {
                    health.ApplyDamage(new DamageInfo(guardChipDamage, info.ContactPoint, info.AttackDirectionFlat, info.Attacker));
                }
                // spec item 6: the guard costs THIS attack's poise, not a flat number.
                float guardPoise = PlayerGuardUtility.GuardPoiseGain(info.PoiseDamage, guardPoiseMultiplier, guardPlayerPoiseDamage);
                if (guardPoise > 0f) stance?.AddPostureDamage(guardPoise);

                HitStopController.Request(guardHitStopSeconds, guardHitStopScale);
                cameraShake?.Shake(guardShakeAmplitude, guardShakeSeconds);
                Guarded?.Invoke(info.ContactPoint);
            }
            return result;
        }

        private static void NotifyBossParried(GameObject attacker, DeflectReaction reaction)
        {
            if (attacker == null) return;
            var boss = attacker.GetComponentInParent<Live2DAction.AI.Boss.BossStateMachine>();
            boss?.NotifyParried(reaction);
        }

        private static bool WasBossWeaponStrike(DamageInfo info)
        {
            if (info.Source == null)
            {
                return false;
            }
            BossHitbox[] hitboxes = info.Source.GetComponentsInChildren<BossHitbox>(true);
            foreach (BossHitbox hb in hitboxes)
            {
                if (hb.IsActive && hb.ActiveWindowPart == BossHitboxPart.Weapon)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
