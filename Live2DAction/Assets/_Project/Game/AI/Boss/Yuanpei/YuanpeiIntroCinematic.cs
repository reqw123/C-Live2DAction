using System;
using System.Collections;
using UnityEngine;
using Live2DAction.Combat;

namespace Live2DAction.AI.Boss.Yuanpei
{
    public enum YuanpeiIntroBeat { SkyWipe, PushToBoss, BossRise, PlayerLeap, Clash, Settle, Done }

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
    //   4. camera pulls back wide - the player leaps at the boss to slash
    //   5. side 2-shot of both faces - on the slash the boss rears back to charge and shoves the
    //      player flying
    //   6. settle -> real fight
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
        [Tooltip("Player ends the leap this far IN FRONT of the boss, on the same horizontal line up in the air.")]
        [SerializeField] private float airStandoff = 3.0f;
        [SerializeField] private float bossChargeBack = 2.6f;     // slight rear-back before the forward "頂" thrust
        [SerializeField] private float launchBackDistance = 16f;  // horizontal fling from the air
        [SerializeField] private float launchArcHeight = 3.5f;    // extra rise before the long fall to ground

        [Header("Camera")]
        [SerializeField] private float baseFov = 60f;
        [SerializeField] private float closeFov = 32f;

        [Header("Cinematic time-scale (續182)")]
        [Tooltip("Playback SPED UP while the player dashes in for the slash - the 'quick approach' movie beat.")]
        [SerializeField] private float leapTimeScale = 1.5f;
        [Tooltip("SLOW-MOTION through the clash / knockback exchange - the 'formal clash' movie beat.")]
        [SerializeField] private float clashTimeScale = 0.4f;
        [Tooltip("Extra seconds the camera holds on the grounded player mid-posture-break stagger before control returns. Tune near the player's StancePoise.staggerDurationSeconds.")]
        [SerializeField] private float groundStaggerHoldSeconds = 1f;

        public bool IsRunning { get; private set; }

        // ---- restore caches ----
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

            _cam = Camera.main;
            AutoFillLists(player);
            LockActors(player);

            // 續182 - unscaledTime, not Time.time: the cutscene deliberately drives Time.timeScale
            // (slow-mo clash) so scaled time runs slow, but unscaledTime still advances one fixed
            // step per frame (unlike realtimeSinceStartup, which tracks wall-clock and would fire
            // this early during editor frame-stepping verification).
            float deadline = Time.unscaledTime + timing.Total + groundStaggerHoldSeconds + 20f;
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
            for (float t = 0f; t < timing.SkyWipe; t += Time.deltaTime)
            {
                float k = t / timing.SkyWipe;
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
            for (float t = 0f; t < timing.PushToBoss; t += Time.deltaTime)
            {
                float k = Ease(t / timing.PushToBoss);
                _cam.transform.position = Vector3.Lerp(c0, diagCam, k);
                _cam.transform.rotation = Quaternion.Slerp(r0, LookAt(diagCam, riseAim), k);
                _cam.fieldOfView = Mathf.Lerp(f0, baseFov, k);
                yield return null;
            }

            // ---- Beat 3: boss rises STRAIGHT UP at its own spot, spinning, growing to high altitude ----
            Vector3 riseFrom = boss != null ? boss.transform.position : bossStartPos;
            Vector3 riseFromScale = boss != null && boss.VisualRoot != null ? boss.VisualRoot.localScale : bossStartScale;
            for (float t = 0f; t < timing.BossRise; t += Time.deltaTime)
            {
                float k = t / timing.BossRise;
                boss?.DriveRiseAndSpin(arenaCenter, riseFrom, riseFromScale, bossAirAltitude, k, bossSpinDegPerSec);
                Vector3 bp = boss != null ? boss.transform.position : arenaCenter + Vector3.up * bossAirAltitude;
                // fixed diagonal shot, easing up to keep the rising boss framed
                Vector3 dcam = arenaCenter + new Vector3(9f, bp.y * 0.55f + 2f, 11f);
                SetCam(dcam, bp, Mathf.Lerp(baseFov, 46f, k));
                yield return null;
            }
            boss?.DriveRiseAndSpin(arenaCenter, riseFrom, riseFromScale, bossAirAltitude, 1f, 0f);
            Vector3 bossPos = boss != null ? boss.transform.position : arenaCenter + Vector3.up * bossAirAltitude;

            // ---- Beat 4 (續182): player LEAPS UP to the boss with a REAL jump + sword-combo swing,
            //      playback SPED UP for the dash-in (電影感) then ramping into slow-mo as they meet. ----
            Vector3 toBoss = bossPos - player.position; toBoss.y = 0f;
            Vector3 flatDir = toBoss.sqrMagnitude > 0.001f ? toBoss.normalized : player.forward;
            Vector3 side = Vector3.Cross(Vector3.up, flatDir).normalized;   // player screen-left, boss screen-right
            Vector3 leapStart = player.position;
            Vector3 leapEnd = new Vector3(bossPos.x, bossPos.y, bossPos.z) - flatDir * airStandoff;   // SAME Y as the boss
            SetAnimFloat("Speed", 0f);
            SetAnimBool("Grounded", false);
            SetAnimBool("Jump", true);
            bool slashFired = false;
            for (float t = 0f; t < timing.PlayerLeap; t += Time.deltaTime)
            {
                float k = t / timing.PlayerLeap;
                // playback speed envelope: quick dash (1 -> leapTimeScale), then ease down toward the clash slow-mo
                Time.timeScale = k < 0.55f
                    ? Mathf.Lerp(1f, leapTimeScale, Ease(k / 0.55f))
                    : Mathf.Lerp(leapTimeScale, clashTimeScale, Ease(Mathf.InverseLerp(0.55f, 1f, k)));
                Vector3 pp = Vector3.Lerp(leapStart, leapEnd, Ease(k));
                pp.y = Mathf.Lerp(leapStart.y, leapEnd.y, Ease(k)) + Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI) * 2.0f;   // overshoot arc
                SetPlayer(player, pp, flatDir);
                if (!slashFired && k >= 0.6f)
                {
                    SetAnimBool("Jump", false);
                    SetAnimTrigger("AttackComboSword");   // real katana swing on arrival
                    slashFired = true;
                }
                Vector3 mid4 = Vector3.Lerp(player.position, bossPos, 0.5f);
                Vector3 leapCam = mid4 + side * 10f + (-flatDir) * 3f + Vector3.up * 3f;
                SetCam(Vector3.Lerp(_cam.transform.position, leapCam, Ease(k) * 0.6f), mid4, Mathf.Lerp(46f, baseFov, k));
                yield return null;
            }
            SetPlayer(player, leapEnd, flatDir);

            // ---- Beat 5 (續182): SLOW-MOTION 'formal clash'. Boss tilts slightly, eases BACK, then
            //      thrusts FORWARD (頂) to shove the player off - no big lunge, just a short punchy
            //      forward jab. The player is flung down and lands in a full posture-break stagger. ----
            Vector3 playerHome = player.position;                 // high in the air
            Vector3 bossHome = boss != null ? boss.transform.position : bossPos;
            Quaternion bossVisRot0 = boss != null && boss.VisualRoot != null ? boss.VisualRoot.localRotation : Quaternion.identity;
            Vector3 clashMid = Vector3.Lerp(playerHome, bossHome, 0.5f);
            Vector3 closeCam = clashMid + side * (airStandoff + 1.6f) + Vector3.up * 0.3f;   // side, player screen-L / boss screen-R
            Vector3 launchTo = playerHome - flatDir * launchBackDistance;
            launchTo.y = groundY;                                  // the fling ends on the GROUND
            bool launched = false, staggerForced = false;
            for (float t = 0f; t < timing.Clash; t += Time.deltaTime)
            {
                float k = t / timing.Clash;

                // boss motion: 0-0.4 slight tilt + ease BACK a touch; 0.4-0.6 snap FORWARD "頂" thrust;
                // 0.6+ ease home. bossChargeBack sizes the whole tilt/back/thrust move.
                if (boss != null && boss.VisualRoot != null)
                {
                    float back = bossChargeBack * 0.3f, thrust = bossChargeBack;
                    float tilt, fwd;
                    if (k < 0.4f)      { float a = Ease(k / 0.4f);                          tilt = -6f * a;                fwd = -back * a; }
                    else if (k < 0.6f) { float a = Ease(Mathf.InverseLerp(0.4f, 0.6f, k));  tilt = Mathf.Lerp(-6f, 4f, a); fwd = Mathf.Lerp(-back, thrust, a); }
                    else               { float a = Ease(Mathf.InverseLerp(0.6f, 1f, k));    tilt = Mathf.Lerp(4f, 0f, a); fwd = Mathf.Lerp(thrust, 0f, a); }
                    boss.transform.position = bossHome + flatDir * fwd;
                    boss.VisualRoot.localRotation = Quaternion.Euler(tilt, 0f, 0f) * bossVisRot0;
                }

                if (k < 0.55f)
                {
                    Time.timeScale = clashTimeScale;              // held slow-mo for the wind-up
                    SetCam(closeCam, clashMid, closeFov);
                }
                else
                {
                    if (!launched)
                    {
                        SetAnimBool("Jump", false);
                        SetAnimFloat("Speed", 0f);
                        launched = true;
                    }
                    float lk = Ease(Mathf.InverseLerp(0.55f, 1f, k));
                    Time.timeScale = Mathf.Lerp(clashTimeScale, 0.9f, lk);   // ease back toward real time as the player falls
                    // player: flung backward + a short rise, then the long fall to the ground
                    Vector3 fp = Vector3.Lerp(playerHome, launchTo, lk);
                    float rise = Mathf.Sin(Mathf.Clamp01(lk * 1.4f) * Mathf.PI) * launchArcHeight;
                    fp.y = Mathf.Lerp(playerHome.y, groundY, lk * lk) + rise;   // ease-in fall + arc
                    fp.y = Mathf.Max(fp.y, groundY);
                    SetPlayer(player, fp, -flatDir);
                    bool grounded = fp.y <= groundY + 0.05f;
                    SetAnimBool("Grounded", grounded);
                    // force the posture-break kneel the instant the player TOUCHES DOWN (not mid-air):
                    // the player's StancePoise.staggerDurationSeconds is short (~1.2s here), so firing
                    // it any earlier means it's mostly spent before the camera settles on them.
                    if (!staggerForced && grounded && lk >= 0.9f && _stance != null)
                    {
                        _stance.AddPostureDamage(_stance.MaxStance * 2f);   // -> IsStaggered -> StaggerAnimationLink holds the kneel
                        staggerForced = true;
                    }
                    // sweep from the close-up out to a ground-level 3/4 shot ON the landing spot,
                    // so the player crumpling to the ground fills the frame (not a distant speck).
                    Vector3 landSpot = new Vector3(launchTo.x, groundY, launchTo.z);
                    Vector3 landCam = landSpot + side * 5f + (-flatDir) * 4.5f + Vector3.up * 2.4f;
                    Vector3 landLook = Vector3.Lerp(clashMid, landSpot + Vector3.up * 0.9f, lk);
                    SetCam(Vector3.Lerp(closeCam, landCam, Ease(lk)), landLook, Mathf.Lerp(closeFov, baseFov, lk));
                }
                yield return null;
            }
            Time.timeScale = 1f;
            SetPlayer(player, new Vector3(launchTo.x, groundY, launchTo.z), -flatDir);
            if (boss != null && boss.VisualRoot != null) boss.VisualRoot.localRotation = bossVisRot0;
            if (_playerCC != null) _playerCC.enabled = _playerCCWas;
            boss?.SettleToHoverPose();
            if (!staggerForced && _stance != null) { _stance.AddPostureDamage(_stance.MaxStance * 2f); staggerForced = true; }

            // ---- Beat 6: ease to a behind-player framing while the player is knelt in the posture-
            //      break stagger; hold a beat so it reads, then hand control back (UnlockActors ends
            //      the stagger cleanly). ----
            Vector3 s0 = _cam.transform.position; Quaternion sr0 = _cam.transform.rotation;
            Vector3 behindPlayer = player.position - player.forward * 5.5f + Vector3.up * 2.2f;
            float settleTotal = timing.Settle + Mathf.Max(0f, groundStaggerHoldSeconds);
            for (float t = 0f; t < settleTotal; t += Time.deltaTime)
            {
                float k = Ease(t / Mathf.Max(0.01f, timing.Settle));
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
            if (_playerAnim != null) { _playerAnimSpeedWas = _playerAnim.speed; _playerAnim.speed = 1f; }
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
