using System;
using System.Collections;
using UnityEngine;
using Live2DAction.Combat;

namespace Live2DAction.AI.Boss.Yuanpei
{
    public enum YuanpeiIntroBeat { SkyWipe, PushToBoss, BossRise, PlayerLeap, Clash, Settle, Done }

    // 續183d - two length presets. Full ≈ 22s wall-clock (the tuned version the user signed off on);
    // Short ≈ 15s for a milestone/demo cut. SAME choreography - every beat is just trimmed. Short is
    // NOT a uniform speed-up: the sky wipe + the slow-mo depth take the biggest cuts, the content
    // beats (boss rise / clash / knockdown / get-up) are kept.
    public enum YuanpeiIntroLength { Full, Short }

    // ---------------------------------------------------------------------------------------------
    // Pure beat-timeline maths for YuanpeiIntroCinematic - which beat is live at time t, and the
    // 0..1 progress within it. No MonoBehaviour, unit-testable (same idea as YuanpeiPhaseLogic /
    // BossDomainEnvelope).
    // ---------------------------------------------------------------------------------------------
    [Serializable]
    public struct YuanpeiIntroTimeline
    {
        public float SkyWipe;
        public float PushToBoss;
        public float BossRise;
        public float PlayerLeap;
        public float Clash;
        public float Settle;

        public static YuanpeiIntroTimeline Default => new YuanpeiIntroTimeline
        {
            SkyWipe = 5.0f, PushToBoss = 1.8f, BossRise = 2.6f,
            // 續182: Clash is measured in SCALED seconds - it plays at clashTimeScale (~0.4x), so
            // 2.4 scaled ≈ 6s of wall-clock slow-motion.
            PlayerLeap = 2.6f, Clash = 2.4f, Settle = 1.6f
        };

        // 續183d - ~15s milestone cut of Default. PlayerLeap/Clash are SCALED seconds (they play in
        // slow-mo) so their wall-clock is longer than the number here; Short also raises the slow-mo
        // floor via shortClashTimeScale so the clash doesn't eat wall-clock. Rough wall budget:
        // 3.2 + 1.1 + 2.0 + ~2.6 + ~2.5 + (0.9 downed) + (0.7 getup) + 1.0 ≈ 14-15s.
        public static YuanpeiIntroTimeline Short => new YuanpeiIntroTimeline
        {
            SkyWipe = 3.2f, PushToBoss = 1.1f, BossRise = 2.0f,
            PlayerLeap = 1.9f, Clash = 1.7f, Settle = 1.0f
        };

        public float Total => SkyWipe + PushToBoss + BossRise + PlayerLeap + Clash + Settle;

        public YuanpeiIntroBeat BeatAt(float t, out float local01)
        {
            float d;
            d = Mathf.Max(0.01f, SkyWipe);    if (t < d) { local01 = t / d; return YuanpeiIntroBeat.SkyWipe; }    t -= d;
            d = Mathf.Max(0.01f, PushToBoss); if (t < d) { local01 = t / d; return YuanpeiIntroBeat.PushToBoss; } t -= d;
            d = Mathf.Max(0.01f, BossRise);   if (t < d) { local01 = t / d; return YuanpeiIntroBeat.BossRise; }   t -= d;
            d = Mathf.Max(0.01f, PlayerLeap); if (t < d) { local01 = t / d; return YuanpeiIntroBeat.PlayerLeap; } t -= d;
            d = Mathf.Max(0.01f, Clash);      if (t < d) { local01 = t / d; return YuanpeiIntroBeat.Clash; }      t -= d;
            d = Mathf.Max(0.01f, Settle);     if (t < d) { local01 = t / d; return YuanpeiIntroBeat.Settle; }
            local01 = 1f; return YuanpeiIntroBeat.Done;
        }
    }

    // ---------------------------------------------------------------------------------------------
    // 2026-09-06 (續 180), explicit user request - a 6-beat intro cutscene when the yuanpei_LogoSky
    // fight arms:
    //   1. the 360 night panorama wipes in from the horizon UP - a clear day sky turning to night
    //   2. camera pushes in on the boss
    //   3. the boss rises out of the ground, spinning, growing to combat size
    //   4. (續183) the player leaps up to one katana-length in front of the boss; playback eases
    //      DOWN into slow-motion (no speed-up) and a normal katana slash swings on arrival
    //   5. (續183) the CLASH: held slow-mo while the blade arcs in - the boss reads it, rears BACK,
    //      rams FORWARD to meet the blade, they connect, and the player is flung back + down. Boss
    //      arc: back -> forward -> meet -> recoil -> ease home. The player stays FACING the boss.
    //   6. (續183) the player lies downed FACING the boss (Mixamo "Dying" clip = fall onto back);
    //      hold a beat, then that clip is scrubbed in REVERSE so they push up + stand, camera eases
    //      to the behind-player combat framing -> real fight
    //
    // Coroutine-driven, like YuanpeiEncounter.DeathDissolve() (this boss deliberately does NOT use
    // the samurai fight's Timeline/Cinemachine rig - see BossIntroManager). Player control + boss AI
    // + HUD are handed off for the duration and handed back in a finally block, plus a failsafe
    // deadline, so a mid-cutscene exception can't leave the player frozen.
    //
    // YuanpeiEncounter.StartEncounter() yields Play() before calling boss.BeginEncounter(playIntro:
    // false) - the cinematic already did the descend, so the boss's own 2.6s IntroRoutine is skipped.
    // ---------------------------------------------------------------------------------------------
    [DisallowMultipleComponent]
    public class YuanpeiIntroCinematic : MonoBehaviour
    {
        [SerializeField] private YuanpeiBoss boss;
        [SerializeField] private BossDomainScreenVFX domainVfx;

        [Header("Player control handed off for the cutscene")]
        [Tooltip("Left EMPTY on purpose - the player lives in a different (persistent) scene, so it " +
                 "can't be referenced here. These are resolved by type on the passed-in Player at " +
                 "runtime. Fill only to add extra scripts beyond the default set.")]
        [SerializeField] private Behaviour[] playerControlScripts = new Behaviour[0];
        [SerializeField] private GameObject[] playerUiRoots = new GameObject[0];

        // default player/camera control types stood down for the cutscene, resolved at runtime
        static readonly string[] k_PlayerControlTypes =
        {
            "Live2DAction.Input.PlayerInputProvider",
            "Live2DAction.Characters.CharacterMovement",
            "Live2DAction.Characters.CharacterAnimatorLink",   // 續182 - stop it fighting Speed / animator.speed during the cutscene
            "Live2DAction.Combat.PlayerCombat",
            "Live2DAction.Targeting.TargetLockController",
            "Live2DAction.Combat.UltimateAbility",
            "Live2DAction.Combat.PlayerGuard",
            "Live2DAction.Combat.ExecutionAbility",
        };
        static readonly string[] k_CameraControlTypes =
        {
            "Live2DAction.CameraSystem.CameraPossessionSwitcher",
            "Live2DAction.CameraSystem.ViewFocusDirector",
        };
        private System.Collections.Generic.List<Behaviour> _resolvedControls;

        [Header("Beat timing (seconds)")]
        [SerializeField] private YuanpeiIntroTimeline timing = YuanpeiIntroTimeline.Default;

        [Header("Beat 1 - sky (續181: slower, camera pulled back to show most of the sky)")]
        [Tooltip("Establishing camera distance back from the arena centre.")]
        [SerializeField] private float skyCamBack = 34f;
        [SerializeField] private float skyCamHeight = 15f;
        [Tooltip("How high above the arena the establishing shot aims (so the frame is mostly sky).")]
        [SerializeField] private float skyCamAimHeight = 16f;

        [Header("Beat 3 - boss rise (spins UP at its own spot to high altitude)")]
        [SerializeField] private float bossStartDepthBelowArena = 3.0f;
        [SerializeField] private float bossStartScaleFraction = 0.05f;    // of the authored sky-logo scale
        [SerializeField] private float bossSpinDegPerSec = 260f;
        [Tooltip("How high above the arena floor the boss ends the rise - the clash happens up here.")]
        [SerializeField] private float bossAirAltitude = 13f;

        [Header("Beat 4/5 - player leap up + air clash + knockback")]
        [Tooltip("續183: player ends the leap this far (one katana-length) IN FRONT of the boss, same air line - this is where the slow-mo slash happens.")]
        [SerializeField] private float slashStandoff = 1.8f;
        [Tooltip("續183: how far the boss pulls BACK (away from the player) to wind up the ram.")]
        [SerializeField] private float bossChargeBack = 2.6f;
        [Tooltip("續183b: how far the boss LUNGES toward the player from its spot - should be >= slashStandoff so it visibly rams THROUGH the player at the contact frame.")]
        [SerializeField] private float bossRamClose = 2.8f;
        [SerializeField] private float launchBackDistance = 16f;  // horizontal fling from the air
        [SerializeField] private float launchArcHeight = 3.5f;    // extra rise before the long fall to ground

        [Header("Camera")]
        [SerializeField] private float baseFov = 60f;
        [SerializeField] private float closeFov = 32f;

        [Header("Cinematic time-scale (續182/183)")]
        [Tooltip("SLOW-MOTION target for the approach + clash / knockback exchange - the 'formal clash' movie beat. 續183: the approach now eases DOWN into this (no speed-up).")]
        [SerializeField] private float clashTimeScale = 0.4f;

        [Header("Beat 6 - grounded knockdown + get-up (續183)")]
        [Tooltip("Seconds the player lies downed FACING the boss before starting to get up.")]
        [SerializeField] private float downedHoldSeconds = 1.6f;
        [Tooltip("Seconds to scrub the fall clip in REVERSE (push up off the ground -> stand).")]
        [SerializeField] private float getUpSeconds = 0.95f;

        [Header("Length preset (續183d) - Full ≈ 22s, Short ≈ 15s")]
        [Tooltip("Full = the tuned 22s version. Short = a ~15s milestone cut (same beats, trimmed). " +
                 "Flip here, or via Tools/Live2DAction/Yuanpei Intro Length. All the fields above are " +
                 "the FULL values and are never touched by Short.")]
        [SerializeField] private YuanpeiIntroLength length = YuanpeiIntroLength.Full;
        [SerializeField] private YuanpeiIntroTimeline shortTiming = YuanpeiIntroTimeline.Short;
        [Tooltip("Short-mode slow-mo floor - shallower than the Full clashTimeScale so the clash doesn't eat wall-clock.")]
        [SerializeField] private float shortClashTimeScale = 0.55f;
        [SerializeField] private float shortDownedHoldSeconds = 0.9f;
        [SerializeField] private float shortGetUpSeconds = 0.7f;

        public bool IsRunning { get; private set; }
        public YuanpeiIntroLength Length { get => length; set => length = value; }

        // ---- restore caches ----
        // 續183d - effective (length-preset-resolved) timing, set at the top of Play()
        private YuanpeiIntroTimeline _tl;
        private float _clashTS, _downedHold, _getUp;
        private Camera _cam;
        private Behaviour _camController;
        private bool _camControllerWas;
        private CameraClearFlags _camClearWas;
        private float _camFovWas;
        private bool[] _ctrlWas;
        private bool[] _uiWas;
        private CharacterController _playerCC;
        private bool _playerCCWas;
        private Animator _playerAnim;
        private float _playerAnimSpeedWas = 1f;
        private Behaviour _bossWas;
        private float _prevTimeScale = 1f;
        private StancePoise _stance;

        // -----------------------------------------------------------------------------------------

        public IEnumerator Play(Transform player, Vector3 arenaCenter)
        {
            if (IsRunning) yield break;
            IsRunning = true;

            // 續183d - resolve the length preset ONCE. Full uses every serialized field as-is (the
            // signed-off 22s version); Short swaps in the trimmed timeline + shallower slow-mo + the
            // shorter beat-6 holds, and touches nothing else.
            bool shortV = length == YuanpeiIntroLength.Short;
            _tl         = shortV ? shortTiming : timing;
            _clashTS    = shortV ? shortClashTimeScale    : clashTimeScale;
            _downedHold = shortV ? shortDownedHoldSeconds : downedHoldSeconds;
            _getUp      = shortV ? shortGetUpSeconds      : getUpSeconds;

            _cam = Camera.main;
            AutoFillLists(player);
            LockActors(player);

            // 續182 - unscaledTime, not Time.time: the cutscene deliberately drives Time.timeScale
            // (slow-mo clash) so scaled time runs slow, but unscaledTime still advances one fixed
            // step per frame (unlike realtimeSinceStartup, which tracks wall-clock and would fire
            // this early during editor frame-stepping verification).
            float deadline = Time.unscaledTime + _tl.Total + _downedHold + _getUp + 20f;
            bool faulted = false;
            IEnumerator body = RunBeats(player, arenaCenter, deadline);
            while (true)
            {
                object cur = null;
                try
                {
                    if (!body.MoveNext()) break;
                    cur = body.Current;
                }
                catch (Exception e)
                {
                    Debug.LogError("[YuanpeiIntroCinematic] beat error, aborting cutscene: " + e);
                    faulted = true;
                    break;
                }
                yield return cur;
                if (Time.unscaledTime > deadline) { Debug.LogWarning("[YuanpeiIntroCinematic] failsafe deadline hit."); break; }
            }

            UnlockActors(player);
            IsRunning = false;
            if (faulted) yield break;
        }

        // ---------------------------------------------------------------- beats

        private IEnumerator RunBeats(Transform player, Vector3 arenaCenter, float deadline)
        {
            float groundY = player.position.y;
            Vector3 bossStartPos = arenaCenter + Vector3.down * bossStartDepthBelowArena;
            Vector3 skyScale = boss != null && boss.VisualRoot != null ? boss.VisualRoot.localScale : Vector3.one;
            Vector3 bossStartScale = skyScale * bossStartScaleFraction;
            if (boss != null)
            {
                boss.transform.position = bossStartPos;   // buried at ITS OWN spot (never moves sideways)
                if (boss.VisualRoot != null) boss.VisualRoot.localScale = bossStartScale;
            }

            // ---- Beat 1: sky wipes clear-day -> night from the horizon UP; camera FAR back so most
            //      of the sky is in frame (續181). ----
            if (domainVfx != null)
            {
                domainVfx.BeginDomain();
                domainVfx.SetNightRise(0f);
                domainVfx.SetIntensity(0.1f);
            }
            Vector3 skyCam = arenaCenter + new Vector3(0f, skyCamHeight, skyCamBack);
            Vector3 skyLook = arenaCenter + Vector3.up * skyCamAimHeight;
            for (float t = 0f; t < _tl.SkyWipe; t += Time.deltaTime)
            {
                float k = t / _tl.SkyWipe;
                domainVfx?.SetNightRise(Mathf.SmoothStep(0f, 1f, k));
                domainVfx?.SetIntensity(Mathf.Lerp(0.1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, k))));
                SetCam(skyCam + new Vector3(0f, 0f, -3f) * Ease(k), skyLook + Vector3.down * 3f * Ease(k), baseFov + 6f);
                yield return null;
            }
            domainVfx?.SetNightRise(1f);
            domainVfx?.SetIntensity(1f);

            // ---- Beat 2: bring the camera down/around to a diagonal on the arena centre (where the
            //      boss is about to rise). ----
            Vector3 c0 = _cam.transform.position; Quaternion r0 = _cam.transform.rotation; float f0 = _cam.fieldOfView;
            Vector3 diagCam = arenaCenter + new Vector3(9f, 5f + bossAirAltitude * 0.35f, 11f);
            Vector3 riseAim = arenaCenter + Vector3.up * (bossAirAltitude * 0.4f);
            for (float t = 0f; t < _tl.PushToBoss; t += Time.deltaTime)
            {
                float k = Ease(t / _tl.PushToBoss);
                _cam.transform.position = Vector3.Lerp(c0, diagCam, k);
                _cam.transform.rotation = Quaternion.Slerp(r0, LookAt(diagCam, riseAim), k);
                _cam.fieldOfView = Mathf.Lerp(f0, baseFov, k);
                yield return null;
            }

            // ---- Beat 3: boss rises STRAIGHT UP at its own spot, spinning, growing to high altitude ----
            Vector3 riseFrom = boss != null ? boss.transform.position : bossStartPos;
            Vector3 riseFromScale = boss != null && boss.VisualRoot != null ? boss.VisualRoot.localScale : bossStartScale;
            for (float t = 0f; t < _tl.BossRise; t += Time.deltaTime)
            {
                float k = t / _tl.BossRise;
                boss?.DriveRiseAndSpin(arenaCenter, riseFrom, riseFromScale, bossAirAltitude, k, bossSpinDegPerSec);
                Vector3 bp = boss != null ? boss.transform.position : arenaCenter + Vector3.up * bossAirAltitude;
                // fixed diagonal shot, easing up to keep the rising boss framed
                Vector3 dcam = arenaCenter + new Vector3(9f, bp.y * 0.55f + 2f, 11f);
                SetCam(dcam, bp, Mathf.Lerp(baseFov, 46f, k));
                yield return null;
            }
            boss?.DriveRiseAndSpin(arenaCenter, riseFrom, riseFromScale, bossAirAltitude, 1f, 0f);
            Vector3 bossPos = boss != null ? boss.transform.position : arenaCenter + Vector3.up * bossAirAltitude;

            // ---- Beat 4 (續183c): player CROUCHES then LEAPS up to one katana-length in front of the
            //      boss - anticipation dip, ease-OUT launch (explode off the ground, decelerate near
            //      the top), jump arc peaking above the boss line. Camera does a clean eased blend
            //      from beat 3's framing (not a per-frame chase). Playback eases into slow-mo; the
            //      normal katana slash swings near the top. ----
            Vector3 toBoss = bossPos - player.position; toBoss.y = 0f;
            Vector3 flatDir = toBoss.sqrMagnitude > 0.001f ? toBoss.normalized : player.forward;
            Vector3 side = Vector3.Cross(Vector3.up, flatDir).normalized;   // player screen-left, boss screen-right
            Vector3 leapStart = player.position;
            Vector3 leapEnd = new Vector3(bossPos.x, bossPos.y, bossPos.z) - flatDir * slashStandoff;   // one katana out, SAME Y as the boss
            SetAnimFloat("Speed", 0f);
            SetPlayer(player, leapStart, flatDir);            // square up to the boss before jumping

            Vector3 l4c0 = _cam.transform.position; Quaternion l4r0 = _cam.transform.rotation; float l4f0 = _cam.fieldOfView;
            Vector3 leapMid = Vector3.Lerp(leapStart, leapEnd, 0.5f);
            Vector3 leapCam = leapMid + side * (slashStandoff + 5f) + (-flatDir) * 2f + Vector3.up * 2f;   // side 2-shot: player screen-L / boss screen-R

            const float crouch = 0.16f;                       // brief anticipation before the launch
            bool jumpAnimFired = false, slashFired = false;
            for (float t = 0f; t < _tl.PlayerLeap; t += Time.deltaTime)
            {
                float k = t / _tl.PlayerLeap;
                Time.timeScale = Mathf.Lerp(0.95f, _clashTS, Ease(k));   // ease real-time -> slow-mo

                if (k < crouch)
                {
                    float a = Mathf.Sin((k / crouch) * Mathf.PI);             // dip down and back up
                    SetPlayer(player, leapStart + Vector3.down * (0.22f * a), flatDir);
                }
                else
                {
                    if (!jumpAnimFired) { SetAnimBool("Grounded", false); SetAnimBool("Jump", true); jumpAnimFired = true; }
                    float jk = Mathf.InverseLerp(crouch, 1f, k);
                    float move = 1f - Mathf.Pow(1f - jk, 1.8f);              // ease-OUT: fast off the ground, decelerate near the top
                    Vector3 pp = Vector3.Lerp(leapStart, leapEnd, move);
                    pp.y = Mathf.Lerp(leapStart.y, leapEnd.y, move) + Mathf.Sin(Mathf.Clamp01(jk) * Mathf.PI) * 2.6f;   // jump arc peaks above the boss line
                    SetPlayer(player, pp, flatDir);
                    if (!slashFired && jk >= 0.68f)
                    {
                        SetAnimBool("Jump", false);
                        SetAnimTrigger("AttackComboSword");                  // the normal katana slash (same as 續182)
                        slashFired = true;
                    }
                }

                // camera: eased slerp from beat-3's framing to the side 2-shot; look target eases
                // from the boss (where beat 3 left it) to the player/boss midpoint.
                float ck = Ease(k);
                Vector3 lookNow = Vector3.Lerp(bossPos, Vector3.Lerp(player.position, bossPos, 0.5f), ck);
                _cam.transform.position = Vector3.Lerp(l4c0, leapCam, ck);
                _cam.transform.rotation = Quaternion.Slerp(l4r0, LookAt(leapCam, lookNow), ck);
                _cam.fieldOfView = Mathf.Lerp(l4f0, closeFov + 6f, ck);
                yield return null;
            }
            SetPlayer(player, leapEnd, flatDir);

            // ---- Beat 5 (續183): the CLASH. Held slow-mo while the slash arcs in; the boss reads it,
            //      rears BACK, then RAMS FORWARD to meet the blade - they connect - and the player is
            //      flung back + down. Boss arc: back -> forward -> meet -> recoil -> ease home. The
            //      player stays FACING the boss the whole fling, so they land facing it. ----
            Vector3 playerHome = player.position;                 // high in the air, one katana out
            Vector3 bossHome = boss != null ? boss.transform.position : bossPos;
            Quaternion bossVisRot0 = boss != null && boss.VisualRoot != null ? boss.VisualRoot.localRotation : Quaternion.identity;
            Vector3 clashMid = Vector3.Lerp(playerHome, bossHome, 0.5f);
            Vector3 closeCam = clashMid + side * (slashStandoff + 1.4f) + Vector3.up * 0.35f;   // tight side 2-shot
            Vector3 launchTo = playerHome - flatDir * launchBackDistance;
            launchTo.y = groundY;                                  // the fling ends on the GROUND
            // contactK is BOTH the boss-visual-reaches-player frame AND the instant the player is
            // launched - they must be the same k or the fling looks disconnected from the hit.
            const float contactK = 0.42f;
            bool launched = false;
            for (float t = 0f; t < _tl.Clash; t += Time.deltaTime)
            {
                float k = t / _tl.Clash;

                // boss ram (fwd is signed distance TOWARD the player - position is bossHome - flatDir*fwd,
                // because flatDir points player->boss so the player sits on the -flatDir side):
                //   0..0.30      wind UP - pull back AWAY from the player (fwd negative) + lean back
                //   0.30..contactK  accelerating LUNGE toward + THROUGH the player (fwd -> +bossRamClose)
                //   contactK..0.66  sharp recoil back past home
                //   0.66..1      ease home
                if (boss != null && boss.VisualRoot != null)
                {
                    float back = bossChargeBack, close = bossRamClose;
                    float fwd, tilt, spin;
                    if (k < 0.30f)         { float a = Ease(k / 0.30f);                              fwd = -back * a;                 tilt = -9f * a;                spin = 70f * a; }
                    else if (k < contactK) { float p = Mathf.InverseLerp(0.30f, contactK, k); float a = p * p;  fwd = Mathf.Lerp(-back, close, a); tilt = Mathf.Lerp(-9f, 7f, a); spin = Mathf.Lerp(70f, 560f, a); }
                    else if (k < 0.66f)    { float a = Ease(Mathf.InverseLerp(contactK, 0.66f, k)); fwd = Mathf.Lerp(close, -back * 0.35f, a); tilt = Mathf.Lerp(7f, -2f, a); spin = Mathf.Lerp(560f, 150f, a); }
                    else                   { float a = Ease(Mathf.InverseLerp(0.66f, 1f, k));       fwd = Mathf.Lerp(-back * 0.35f, 0f, a); tilt = Mathf.Lerp(-2f, 0f, a); spin = Mathf.Lerp(150f, 0f, a); }
                    boss.transform.position = bossHome - flatDir * fwd;
                    boss.VisualRoot.localRotation = Quaternion.Euler(tilt, 0f, 0f) * bossVisRot0;
                    boss.VisualRoot.Rotate(0f, spin * Time.deltaTime, 0f, Space.Self);
                }

                if (k < contactK)
                {
                    Time.timeScale = _clashTS;              // held slow-mo through the wind-up + lunge
                    SetCam(Vector3.Lerp(_cam.transform.position, closeCam, 0.2f), clashMid, closeFov);
                }
                else
                {
                    if (!launched)
                    {
                        SetAnimBool("Jump", false);
                        SetAnimFloat("Speed", 0f);
                        if (_playerAnim != null) _playerAnim.ResetTrigger("AttackComboSword");   // don't let the queued swing fire post-cutscene
                        domainVfx?.Pulse(1f);                    // contact flash
                        launched = true;
                    }
                    float p = Mathf.InverseLerp(contactK, 1f, k);
                    // PUNCHY ease-OUT: the player rockets off the boss the instant it connects, then
                    // decelerates - NOT a slow SmoothStep ease-in (that's what made the fling read as
                    // happening seconds after the boss had already recoiled home).
                    float lk = 1f - Mathf.Pow(1f - p, 2.4f);
                    Time.timeScale = Mathf.Lerp(0.7f, 1f, p);     // snap most of the way back to real time AT contact
                    // horizontal: punchy shove (lk). vertical: natural parabola over real time (p).
                    Vector3 fp = Vector3.Lerp(playerHome, launchTo, lk);
                    float rise = Mathf.Sin(Mathf.Clamp01(p * 1.3f) * Mathf.PI) * launchArcHeight;
                    fp.y = Mathf.Lerp(playerHome.y, groundY, p * p) + rise;
                    fp.y = Mathf.Max(fp.y, groundY);
                    SetPlayer(player, fp, flatDir);           // FACE the boss (not -flatDir)
                    SetAnimBool("Grounded", fp.y <= groundY + 0.05f);
                    // ease into the backward-fall clip while still airborne so the landing reads as a
                    // knockdown, not a stand: scrub "Dead" (Mixamo Dying = topple onto the back).
                    ScrubState("Dead", Mathf.Lerp(0.05f, 0.85f, p));
                    // camera sweeps at a natural pace (p, not the punchy lk) from the tight 2-shot down
                    // to a low ground-level 3/4 on the landing spot, boss still looming in frame.
                    Vector3 landSpot = new Vector3(launchTo.x, groundY, launchTo.z);
                    Vector3 landCam = landSpot + side * 4.5f + (-flatDir) * 4.0f + Vector3.up * 1.8f;
                    Vector3 landLook = Vector3.Lerp(clashMid, landSpot + Vector3.up * 0.7f, Ease(p));
                    SetCam(Vector3.Lerp(closeCam, landCam, Ease(p)), landLook, Mathf.Lerp(closeFov, baseFov, Ease(p)));
                }
                yield return null;
            }
            Time.timeScale = 1f;
            Vector3 downSpot = new Vector3(launchTo.x, groundY, launchTo.z);
            SetPlayer(player, downSpot, flatDir);            // planted, facing the boss
            SetAnimBool("Grounded", true);
            SetAnimBool("Jump", false);
            ScrubState("Dead", 0.9f);
            if (boss != null && boss.VisualRoot != null) boss.VisualRoot.localRotation = bossVisRot0;
            if (_playerCC != null) _playerCC.enabled = _playerCCWas;
            boss?.SettleToHoverPose();
            domainVfx?.SetIntensity(1f);

            // ---- Beat 6 (續183): player lies downed FACING the boss; hold a beat, then the fall clip
            //      is scrubbed in REVERSE so they push up + stand, camera eases to the behind-player
            //      combat framing, control hands back -> real fight. ----
            // (a) downed hold - low camera looking past the fallen player up at the boss
            Vector3 downCam = downSpot + side * 3.2f + (-flatDir) * 3.2f + Vector3.up * 1.2f;
            for (float t = 0f; t < Mathf.Max(0.01f, _downedHold); t += Time.deltaTime)
            {
                float k = t / Mathf.Max(0.01f, _downedHold);
                ScrubState("Dead", 0.9f + 0.09f * k);      // settle the last of the fall
                SetPlayer(player, downSpot, flatDir);
                SetCam(downCam + (-flatDir) * (0.6f * Ease(k)),
                       downSpot + Vector3.up * (0.6f + bossAirAltitude * 0.15f * k), baseFov);
                yield return null;
            }
            // (b) get up - the fall clip played backward (0.9 -> 0.12), then blend to locomotion
            Vector3 g0 = _cam.transform.position; Quaternion gr0 = _cam.transform.rotation; float gf0 = _cam.fieldOfView;
            Vector3 behindGetUp = downSpot - flatDir * 5.5f + Vector3.up * 2.2f;
            for (float t = 0f; t < Mathf.Max(0.01f, _getUp); t += Time.deltaTime)
            {
                float k = Ease(t / Mathf.Max(0.01f, _getUp));
                ScrubState("Dead", Mathf.Lerp(0.9f, 0.12f, k));
                SetPlayer(player, downSpot, flatDir);
                _cam.transform.position = Vector3.Lerp(g0, behindGetUp, k * 0.6f);
                _cam.transform.rotation = Quaternion.Slerp(gr0, LookAt(behindGetUp, downSpot + Vector3.up * 1.2f), k * 0.6f);
                _cam.fieldOfView = Mathf.Lerp(gf0, baseFov, k);
                yield return null;
            }
            SetAnimFloat("Speed", 0f);
            SetAnimBool("Grounded", true);
            if (_playerAnim != null) _playerAnim.CrossFade("Locomotion", 0.15f, 0);   // MUST leave "Dead" before control returns

            // (c) final settle to the behind-player combat framing, then hand control back
            Vector3 s0 = _cam.transform.position; Quaternion sr0 = _cam.transform.rotation;
            Vector3 behindPlayer = player.position - player.forward * 5.5f + Vector3.up * 2.2f;
            for (float t = 0f; t < _tl.Settle; t += Time.deltaTime)
            {
                float k = Ease(t / Mathf.Max(0.01f, _tl.Settle));
                _cam.transform.position = Vector3.Lerp(s0, behindPlayer, k);
                _cam.transform.rotation = Quaternion.Slerp(sr0, LookAt(behindPlayer, player.position + Vector3.up * 1.2f), k);
                _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, baseFov, k);
                yield return null;
            }
        }

        // ---------------------------------------------------------------- helpers

        private static float Ease(float x) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(x));
        private static Quaternion LookAt(Vector3 from, Vector3 target)
        {
            Vector3 d = target - from;
            return d.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(d.normalized, Vector3.up) : Quaternion.identity;
        }

        private void SetCam(Vector3 pos, Vector3 lookAt, float fov)
        {
            if (_cam == null) return;
            _cam.transform.position = pos;
            _cam.transform.rotation = LookAt(pos, lookAt);
            _cam.fieldOfView = fov;
        }

        private void SetPlayer(Transform player, Vector3 pos, Vector3 faceDir)
        {
            player.position = pos;
            faceDir.y = 0f;
            if (faceDir.sqrMagnitude > 1e-5f) player.rotation = Quaternion.LookRotation(faceDir.normalized, Vector3.up);
        }

        private void SetAnimFloat(string p, float v) { if (_playerAnim != null) _playerAnim.SetFloat(p, v); }
        private void SetAnimBool(string p, bool v) { if (_playerAnim != null) _playerAnim.SetBool(p, v); }
        private void SetAnimTrigger(string p)
        {
            if (_playerAnim == null) return;
            int hash = Animator.StringToHash(p);
            if (_playerAnim.HasState(0, hash) || System.Array.Exists(_playerAnim.parameters, x => x.name == p))
                _playerAnim.SetTrigger(p);
        }
        // 續183 - manually scrub a state's playhead each frame (fwd for the knockdown fall, reverse
        // for the get-up). Play() re-anchors every frame, overriding any AnyState transition.
        private void ScrubState(string state, float normalizedTime)
        {
            if (_playerAnim == null) return;
            if (_playerAnim.HasState(0, Animator.StringToHash(state)))
                _playerAnim.Play(state, 0, Mathf.Clamp01(normalizedTime));
        }

        // ---------------------------------------------------------------- lock / unlock

        private void AutoFillLists(Transform player)
        {
            if (_cam != null)
                _camController = _cam.GetComponent(typeof(Live2DAction.CameraSystem.ThirdPersonCameraController)) as Behaviour;

            Transform root = player;
            while (root != null && root.name != "Player") root = root.parent;
            if (root == null) root = player;
            _playerCC = root.GetComponent<CharacterController>();
            _playerAnim = root.GetComponentInChildren<Animator>();
            _stance = root.GetComponentInChildren<StancePoise>(true);   // 續182 - force the posture-break kneel on landing
            if (boss == null) boss = FindFirstObjectByType<YuanpeiBoss>();
            if (domainVfx == null) domainVfx = FindFirstObjectByType<BossDomainScreenVFX>();

            // resolve the control scripts by type at runtime (they're in a different scene, so they
            // can't be serialized here). Inspector `playerControlScripts` is merged on top as extras.
            _resolvedControls = new System.Collections.Generic.List<Behaviour>();
            foreach (var tn in k_PlayerControlTypes)
            {
                var t = ResolveType(tn);
                if (t == null) continue;
                if (root.GetComponentInChildren(t, true) is Behaviour b && !_resolvedControls.Contains(b))
                    _resolvedControls.Add(b);
            }
            foreach (var tn in k_CameraControlTypes)
            {
                var t = ResolveType(tn);
                if (t == null) continue;
                var b = (FindFirstObjectByType(t) as Behaviour);
                if (b != null && !_resolvedControls.Contains(b)) _resolvedControls.Add(b);
            }
            if (playerControlScripts != null)
                foreach (var b in playerControlScripts)
                    if (b != null && !_resolvedControls.Contains(b)) _resolvedControls.Add(b);
        }

        static System.Type ResolveType(string full)
        {
            var t = System.Type.GetType(full);
            if (t != null) return t;
            foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                t = a.GetType(full);
                if (t != null) return t;
            }
            return null;
        }

        private void LockActors(Transform player)
        {
            var ctrls = _resolvedControls ?? new System.Collections.Generic.List<Behaviour>();
            _ctrlWas = new bool[ctrls.Count];
            for (int i = 0; i < ctrls.Count; i++)
            {
                if (ctrls[i] == null) continue;
                _ctrlWas[i] = ctrls[i].enabled;
                ctrls[i].enabled = false;
            }
            _uiWas = new bool[playerUiRoots.Length];
            for (int i = 0; i < playerUiRoots.Length; i++)
            {
                if (playerUiRoots[i] == null) continue;
                _uiWas[i] = playerUiRoots[i].activeSelf;
                playerUiRoots[i].SetActive(false);
            }
            if (_playerCC != null) { _playerCCWas = _playerCC.enabled; _playerCC.enabled = false; }
            if (boss != null) { _bossWas = boss; boss.enabled = false; }

            if (_camController != null) { _camControllerWas = _camController.enabled; _camController.enabled = false; }
            if (_cam != null) { _camFovWas = _cam.fieldOfView; }

            // 續182 - the cutscene owns Time.timeScale (leap speed-up / clash slow-mo) and the
            // player Animator's playback rate for its duration.
            _prevTimeScale = Time.timeScale;
            if (_playerAnim != null)
            {
                _playerAnimSpeedWas = _playerAnim.speed;
                _playerAnim.speed = 1f;
                // 續183c - kill the run cycle the instant the cutscene arms: CharacterAnimatorLink
                // is disabled above, so nothing else drives Speed for the ~9s sky-wipe/rise beats
                // and the player would otherwise be stuck running on the spot on camera.
                _playerAnim.SetFloat("Speed", 0f);
                _playerAnim.SetBool("Grounded", true);
                _playerAnim.SetBool("Jump", false);
                if (_playerAnim.HasState(0, Animator.StringToHash("Locomotion")))
                    _playerAnim.CrossFade("Locomotion", 0.12f, 0);
            }
        }

        private void UnlockActors(Transform player)
        {
            // 續182 - always restore first, before anything else can early-out.
            Time.timeScale = _prevTimeScale;
            if (_playerAnim != null) _playerAnim.speed = _playerAnimSpeedWas;
            if (_stance != null && _stance.IsStaggered) _stance.EndStagger();   // clean stand-up as control returns (grants the usual post-stagger i-frames)

            var ctrls = _resolvedControls;
            if (ctrls != null && _ctrlWas != null)
                for (int i = 0; i < ctrls.Count && i < _ctrlWas.Length; i++)
                    if (ctrls[i] != null) ctrls[i].enabled = _ctrlWas[i];
            if (_uiWas != null)
                for (int i = 0; i < playerUiRoots.Length && i < _uiWas.Length; i++)
                    if (playerUiRoots[i] != null) playerUiRoots[i].SetActive(_uiWas[i]);

            if (_playerCC != null) _playerCC.enabled = _playerCCWas;
            if (_bossWas != null) _bossWas.enabled = true;   // YuanpeiEncounter re-drives it via BeginEncounter next

            if (_camController != null)
            {
                _camController.enabled = _camControllerWas;
                (_camController as Live2DAction.CameraSystem.ThirdPersonCameraController)?.SnapYawToTarget();
            }
            if (_cam != null) _cam.fieldOfView = _camFovWas;
        }
    }
}
