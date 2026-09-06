using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Live2DAction.AI.Boss.Yuanpei
{
    public enum YuanpeiState
    {
        Inactive, Intro, Hover, Reposition, AttackTelegraph, Attacking, AttackRecovery,
        EnergyRecharge, PostureBreak, Falling, ExecutionWindow, Executing, Recovering, Dead, BattleEnded
    }

    // Top-level FSM + aerial movement + scheduler driver for yuanpei_LogoSky (spec §4, §7, §10, §12).
    // Attack timelines run in YuanpeiAttacks; the fall/F flow runs in YuanpeiExecution. This class
    // owns state authority + interrupt priority (spec §12.2) and the hover controller.
    [RequireComponent(typeof(YuanpeiBossVitals))]
    public class YuanpeiBoss : MonoBehaviour
    {
        [SerializeField] private YuanpeiBossConfig config;
        [SerializeField] private YuanpeiBossVitals vitals;
        [SerializeField] private YuanpeiAttacks attacks;
        [SerializeField] private YuanpeiExecution execution;
        [SerializeField] private Transform visualRoot;         // spun/tilted; separate from the gameplay root
        [SerializeField] private Transform player;
        [SerializeField] private Transform groundRayOrigin;
        [SerializeField] private List<YuanpeiAttackDef> attackPool = new List<YuanpeiAttackDef>();

        [Header("下馬威 opening barrage (續183e)")]
        [Tooltip("One-shot 長矛+雷射+六連彈 三線齊射 fired the instant combat starts (after the intro). " +
                 "NOT in attackPool - scripted here. Leave null to disable.")]
        [SerializeField] private YuanpeiAttackDef openingBarrageDef;
        [SerializeField] private bool playOpeningBarrage = true;

        [SerializeField] private LayerMask losBlockers = ~0;
        [SerializeField] private LayerMask groundMask = ~0;
        [Tooltip("Log FSM state / player / distance / LOS / on-screen / attack pick once per second.")]
        [SerializeField] private bool verboseLog = true;

        public YuanpeiState State { get; private set; } = YuanpeiState.Inactive;
        public YuanpeiBossVitals Vitals => vitals;
        public YuanpeiBossConfig Config => config;
        public Transform Player => player;
        public Transform VisualRoot => visualRoot;
        public Transform GroundRayOrigin => groundRayOrigin != null ? groundRayOrigin : transform;
        public IReadOnlyList<YuanpeiAttackDef> AttackPool => attackPool;

        // Damage/interaction gates read by the hit receiver / execution controller.
        public bool AcceptsDamageNow =>
            State != YuanpeiState.Inactive && State != YuanpeiState.Intro &&
            State != YuanpeiState.Dead && State != YuanpeiState.BattleEnded &&
            !_postExecInvuln && !vitals.IsDead;
        public bool IsDowned => State == YuanpeiState.Falling || State == YuanpeiState.ExecutionWindow || State == YuanpeiState.Executing;
        public bool BattleOver => State == YuanpeiState.Dead || State == YuanpeiState.BattleEnded;

        private Vector3 _arenaCenter;
        private float _hoverPhase;
        private float _globalRestUntil;
        private float _onScreenSince = -999f;
        private bool _wasOnScreen;
        private float _energyRechargeUntil;
        private bool _postExecInvuln;
        private bool _perfectCounterFlag;
        private float _perfectCounterUntil;

        private readonly Dictionary<YuanpeiAttackId, float> _cooldownUntil = new Dictionary<YuanpeiAttackId, float>();
        private readonly Dictionary<YuanpeiAttackId, float> _lastUsedTime = new Dictionary<YuanpeiAttackId, float>();
        private System.Random _rng;
        private Coroutine _attackRoutine;
        private Camera _cam;

        private void Awake()
        {
            if (vitals == null) vitals = GetComponent<YuanpeiBossVitals>();
            if (attacks == null) attacks = GetComponent<YuanpeiAttacks>();
            if (execution == null) execution = GetComponent<YuanpeiExecution>();
            _rng = new System.Random();
            _arenaCenter = config != null ? config.arenaCenter : transform.position;
            _cam = Camera.main;
            _skyStartPos = transform.position;
            _skyVisualScale = visualRoot != null ? visualRoot.localScale : Vector3.one;
            _skyVisualLocalRot = visualRoot != null ? visualRoot.localRotation : Quaternion.identity;   // authored upright disc orientation
        }

        private void OnEnable()
        {
            if (vitals != null)
            {
                vitals.PostureFull += OnPostureFull;
                vitals.Died += OnDied;
            }
        }

        private void OnDisable()
        {
            if (vitals != null)
            {
                vitals.PostureFull -= OnPostureFull;
                vitals.Died -= OnDied;
            }
        }

        // ---------------------------------------------------------------- external control

        public void BeginEncounter(Vector3 combatCenter, Transform triggeringPlayer = null, bool playIntro = true)
        {
            _arenaCenter = combatCenter;
            if (config != null) config.arenaCenter = combatCenter;
            if (triggeringPlayer != null)
            {
                // never target a vehicle: walk up to the "Player" GameObject rather than blindly
                // taking .root (which is the car while the player is seated). 續 124.
                Transform t = triggeringPlayer;
                while (t != null && t.name != "Player") t = t.parent;
                player = t != null ? t : triggeringPlayer.root;
            }
            if (player == null || player.name != "Player") player = ResolvePlayer();

            if (playIntro)
            {
                StartCoroutine(IntroRoutine());
            }
            else
            {
                // 續 180 - YuanpeiIntroCinematic already ran the descend/rise演出; drop straight into
                // combat at the hover pose (no 2.6s IntroRoutine).
                SnapToCombatPose(combatCenter);
                _lastPlayerPos = player != null ? player.position : Vector3.zero;
                _globalRestUntil = Time.time + 0.6f;
                State = YuanpeiState.Hover;
                if (playOpeningBarrage) StartCoroutine(OpeningBarrageRoutine());   // 續183e 下馬威
            }
        }

        // 續 180 - the boss's own rise/spin/scale beat, exposed for YuanpeiIntroCinematic to drive
        // frame-by-frame during the "boss邊轉圈邊升起" shot. Rises straight up at `center` X/Z (never
        // moves sideways) from `startPos` to `center.x, altitudeAboveFloor, center.z`, growing
        // startScale -> combat visual scale, spinning. 續181: altitude is a param now (the fight
        // choreography happens HIGH in the air, well above the hover pose).
        public void DriveRiseAndSpin(Vector3 center, Vector3 startPos, Vector3 startScale, float altitudeAboveFloor, float t01, float spinDegPerSec)
        {
            _arenaCenter = center;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t01));
            float floor = SampleFloorY(center);
            // 續181: cinematic-only - deliberately NOT clamped to config.maxWorldY. The rise beat takes
            // the boss up to a genuine "高空" for the air clash; SettleToHoverPose() (still clamped)
            // drops it back into combat range before the fight starts, and the boss is disabled during
            // the cutscene so ClampWorldY never fights this.
            float endY = floor + altitudeAboveFloor;
            Vector3 endPos = new Vector3(center.x, endY, center.z);
            Vector3 endScale = _skyVisualScale.sqrMagnitude > 0.0001f
                ? _skyVisualScale * combatVisualScaleFraction
                : startScale;

            transform.position = Vector3.Lerp(startPos, endPos, k);
            if (visualRoot != null)
            {
                visualRoot.localScale = Vector3.Lerp(startScale, endScale, k);
                visualRoot.Rotate(0f, spinDegPerSec * Time.deltaTime, 0f, Space.Self);
            }
        }

        /// <summary>Cinematic hand-off: drop the boss straight to its combat hover pose from wherever it is.</summary>
        public void SettleToHoverPose()
        {
            float floor = SampleFloorY(_arenaCenter);
            float y = config != null ? Mathf.Min(floor + config.hoverHeight + 1.5f, config.maxWorldY) : floor + 4f;
            transform.position = new Vector3(_arenaCenter.x, y, _arenaCenter.z);
            // 續182 - undo the accumulated rise-spin yaw + any cutscene tilt so the disc enters the
            // fight at its authored upright orientation.
            if (visualRoot != null) visualRoot.localRotation = _skyVisualLocalRot;
        }

        // 續 143 (YuanpeiAttackDebugMode, user: "稻草人在非常高的高空") - instantly place the boss at a
        // sensible combat pose (position + shrunk visual scale), no animated descend, no FSM/State
        // change. If a real encounter is never triggered first, the boss just sits at its authored
        // idle "giant sky logo" transform (~Y42, scale 1700) - the debug tool needs a way to get it
        // (and anything spawned relative to it) down to ground level without playing IntroRoutine's
        // 2.6s cinematic every time F8 is pressed.
        public void SnapToCombatPose(Vector3 center)
        {
            _arenaCenter = center;
            if (config != null) config.arenaCenter = center;
            float floor = SampleFloorY(center);
            float y = config != null ? Mathf.Min(floor + config.hoverHeight, config.maxWorldY) : floor + 3f;
            transform.position = new Vector3(center.x, y, center.z);
            if (visualRoot != null && config != null) visualRoot.localScale = _skyVisualScale * combatVisualScaleFraction;
        }

        // The player died in this fight and gets kicked out - put the boss fully back to its
        // pre-fight state so walking back in restarts a clean encounter.
        public void ResetForRematch()
        {
            StopAllCoroutines();
            _attackRoutine = null;
            if (attacks != null) attacks.CancelAll();
            State = YuanpeiState.Inactive;
            _postExecInvuln = false;
            _perfectCounterUntil = 0f;
            _globalRestUntil = 0f;
            _yClampSuspendUntil = 0f;
            _cooldownUntil.Clear();
            _lastUsedTime.Clear();
            _hasLastAttack = false;

            if (vitals != null)
            {
                vitals.ResetPosture();
                if (vitals.Health != null) vitals.Health.ResetHealth();
                if (config != null) vitals.SetEnergy(config.maxEnergy);
                vitals.EvaluatePhase();
            }
            if (visualRoot != null)
            {
                visualRoot.localScale = _skyVisualScale;
                visualRoot.localRotation = _skyVisualLocalRot;
            }
            transform.position = _skyStartPos;
            transform.rotation = Quaternion.identity;

            foreach (var lt in GetComponentsInChildren<Live2DAction.Targeting.LockOnTarget>(true)) lt.enabled = true;
            foreach (var col in GetComponentsInChildren<Collider>(true)) col.enabled = true;
        }
        private Vector3 _skyStartPos;

        // Player + Cat both carry PlayerInputProvider - prefer the one named "Player", else the
        // one WITHOUT a cat-only component, else the first.
        private Transform ResolvePlayer()
        {
            var providers = FindObjectsByType<Live2DAction.Input.PlayerInputProvider>(FindObjectsSortMode.None);
            Transform first = null;
            foreach (var p in providers)
            {
                Transform root = p.transform.root;
                if (first == null) first = root;
                if (root.name == "Player") return root;
                if (root.GetComponent("CatProceduralWalk") == null && root.GetComponentInChildren<Live2DAction.Combat.PlayerCombat>() != null)
                    return root;
            }
            return first;
        }

        // ---------------------------------------------------------------- perfect-dodge counter hook

        public void FlagPerfectDodge()
        {
            _perfectCounterUntil = Time.time + (config != null ? config.perfectDodgeCounterWindowSeconds : 0.5f);
        }

        // consumed once by the hit receiver when a hit lands inside the counter window
        public bool ConsumePerfectCounterFlag()
        {
            if (Time.time <= _perfectCounterUntil)
            {
                _perfectCounterUntil = 0f;
                return true;
            }
            return false;
        }

        public void NotifyPlayerHitLanded(bool crossedPosture)
        {
            // spec §12.2 - a normal hit doesn't hard-interrupt casting; only posture-full does
            // (handled by OnPostureFull). Light feedback could hook here later.
        }

        // ---------------------------------------------------------------- FSM tick

        private float _nextDiagLog;

        private void Update()
        {
            if (config == null || vitals == null) return;

            TrackOnScreen();
            ClampWorldY();

            if (verboseLog && Time.time >= _nextDiagLog && State != YuanpeiState.Inactive)
            {
                _nextDiagLog = Time.time + 1f;
                float d = player != null
                    ? Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z),
                                       new Vector3(player.position.x, 0, player.position.z))
                    : -1f;
                Debug.Log($"[YuanpeiBoss] state={State} player={(player != null ? player.name : "NULL")} dist={d:F1} " +
                          $"LOS={HasLineOfSight()} onScreen={_wasOnScreen}({(Time.time - _onScreenSince):F1}s) " +
                          $"hp={vitals.HealthNormalized:P0} energy={vitals.Energy:F0} posture={vitals.Posture:F0}/{ (config != null ? config.maxPosture : 100f):F0} phase={vitals.Phase} " +
                          $"rest={(Mathf.Max(0, _globalRestUntil - Time.time)):F1}s stuck={(Time.time - _stuckSince):F1}s y={transform.position.y:F1}", this);
            }

            // player died (RespawnController deactivates its GameObject) - stop attacking, just hover
            // until YuanpeiEncounter.Defeat() resets us. Don't chase / cast at an inactive target.
            if (player != null && !player.gameObject.activeInHierarchy)
            {
                if (_attackRoutine != null || (attacks != null && attacks.MajorHazardActive))
                {
                    CancelAttack();
                    if (attacks != null) attacks.CancelAll();
                    State = YuanpeiState.Hover;
                }
                HoldHover();
                return;
            }

            switch (State)
            {
                case YuanpeiState.Inactive:
                case YuanpeiState.Intro:
                case YuanpeiState.Dead:
                case YuanpeiState.BattleEnded:
                case YuanpeiState.PostureBreak:
                case YuanpeiState.Falling:
                case YuanpeiState.ExecutionWindow:
                case YuanpeiState.Executing:
                case YuanpeiState.Recovering:
                    // driven by their own coroutines (execution / intro / recovering)
                    break;

                case YuanpeiState.EnergyRecharge:
                    TickRecharge();
                    break;

                case YuanpeiState.Hover:
                case YuanpeiState.Reposition:
                    TickAirCombat();
                    break;

                case YuanpeiState.AttackTelegraph:
                case YuanpeiState.Attacking:
                case YuanpeiState.AttackRecovery:
                    // hover pose held; attack coroutine drives translation for BodyCharge.
                    // A charge move calls SuspendHover() so HoldHover's 8 m/s Y pull can't drag
                    // the boss back up to hover height mid-dash - that made every charge read as
                    // a flat twitch 2.6 m above the player instead of a dive (user: "感覺只有頭跟尾").
                    if (Time.time >= _hoverSuspendUntil) HoldHover();
                    FaceTarget(config.faceTurnSpeedDegPerSec * 0.4f);
                    break;
            }

            // 續 120 (user "架式條可由時間緩慢積累"): posture creeps up on its own while the boss is
            // actively fighting, so a patient player still earns a fall + F-execution window. Only in
            // air-combat / attack states - never while downed / recharging / intro / dead.
            if (config.postureRegenPerSecond > 0f && IsActiveCombatState())
                vitals.AddPosture(config.postureRegenPerSecond * Time.deltaTime);
        }

        // Passive-posture states. Deliberately EXCLUDES Attacking / AttackRecovery: a posture-full
        // there would `CancelAll()` an in-flight attack mid-Active - e.g. interrupting ChargeCrush's
        // VoidPunt before the 秒殺 lands, so the player survives a clean crush. Telegraph is safe
        // to break (nothing lethal in flight yet) and covers most of a fight's non-hover time.
        private bool IsActiveCombatState() =>
            State == YuanpeiState.Hover || State == YuanpeiState.Reposition
            || State == YuanpeiState.AttackTelegraph;

        // ---------------------------------------------------------------- air combat (spec §7)

        private void TickAirCombat()
        {
            if (player == null) return;

            // passive energy regen while hovering / repositioning (spec §5.2 "一般 Hover／Reposition
            // 狀態可緩慢恢復"). NOT during an attack's Active phase - those states don't run this tick.
            vitals.RegenEnergy(Time.deltaTime, vitals.Phase >= 3);

            // forced recharge (spec §5.2 / §10.2)
            if (vitals.Energy < config.lowEnergyThreshold && !HasAnyAffordableInRange())
            {
                EnterRecharge();
                return;
            }

            HoldHover();
            FaceTarget(config.faceTurnSpeedDegPerSec);
            MaintainRange();

            // schedule
            if (Time.time < _globalRestUntil) { _stuckSince = Time.time; return; }
            var chosen = PickAttack();
            if (chosen == null && Time.time - _stuckSince > 0.12f)
                chosen = ForceAnyInRangeAttack();   // watchdog - the scheduler's soft gates (on-screen / LOS / no-repeat) stall a ranged boss into passivity; keep it pressuring the player (user: "攻擊慾望太低" ×2, 續 119 0.3→0.12)
            if (chosen != null) { BeginAttack(chosen); _stuckSince = Time.time; }
        }
        private float _stuckSince;

        // Watchdog fallback. Unlike PickAttack it ignores on-screen / LOS (those made the boss
        // passive), but it still respects phase / energy / cooldown / no-repeat and picks RANDOMLY
        // among what's valid so the boss doesn't just spam pool[0] (user: "手段太少").
        private readonly List<YuanpeiAttackDef> _wdCandidates = new List<YuanpeiAttackDef>();
        private YuanpeiAttackDef ForceAnyInRangeAttack()
        {
            if (player == null) return null;
            float dist = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z),
                                          new Vector3(player.position.x, 0, player.position.z));

            _wdCandidates.Clear();
            YuanpeiAttackDef ignoreRangeFallback = null;
            foreach (var d in attackPool)
            {
                if (d == null || d.requiredPhase > vitals.Phase || !vitals.CanAfford(d.energyCost)) continue;
                if (_cooldownUntil.TryGetValue(d.attackId, out float cd) && Time.time < cd) continue;
                if (d.isMajorHazard && attacks != null && attacks.MajorHazardActive) continue;
                if (_hasLastAttack && d.attackId == _lastAttackId && attackPool.Count > 1) continue; // no repeat
                if (dist >= d.minRange && dist <= d.maxRange) _wdCandidates.Add(d);
                else if (ignoreRangeFallback == null) ignoreRangeFallback = d;
            }
            if (_wdCandidates.Count > 0)
                return _wdCandidates[_rng.Next(_wdCandidates.Count)];

            // nothing in range - fire an off-cd affordable attack anyway rather than hover forever;
            // the attack coroutines re-aim at the player's live position.
            return ignoreRangeFallback;
        }

        private void HoldHover()
        {
            _hoverPhase += Time.deltaTime * config.hoverBobAmplitudeSpeed.y * Mathf.PI * 2f;
            float floor = SampleFloorY(transform.position);
            float targetY = floor + config.hoverHeight + Mathf.Sin(_hoverPhase) * config.hoverBobAmplitudeSpeed.x;
            // 續 139 (user: "調整之後仍然有一部份是陷入地板...請讓他稍微浮在空中") - hand-tuned hoverHeight
            // numbers kept guessing wrong because they never accounted for the visual's ACTUAL rendered
            // size. Read the disc's real current silhouette and never let its lowest point sink below a
            // floor + margin line, on top of whatever hoverHeight is tuned to - self-corrects if the
            // model/scale changes again instead of needing another round of hand-tuned constants.
            float minY = floor + VisualBottomOffset() + config.groundClearanceMargin;
            if (targetY < minY) targetY = minY;
            Vector3 p = transform.position;
            p.y = Mathf.MoveTowards(p.y, targetY, 8f * Time.deltaTime);
            transform.position = p;
        }

        // How far the visual's CURRENT rendered silhouette extends below the boss's pivot, whatever
        // orientation it's in right now (flat hover tilt vs edge-first charge attitude give different
        // answers) - lets ground-hugging logic stay exact instead of a hand-tuned constant that goes
        // stale the moment the model/scale/rotation changes (續 139).
        public float VisualBottomOffset()
        {
            if (visualRoot == null) return 0f;
            var renderers = visualRoot.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return 0f;
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return transform.position.y - b.min.y;
        }

        private void MaintainRange()
        {
            Vector3 flat = transform.position - player.position; flat.y = 0f;
            float dist = flat.magnitude;
            Vector3 dir = dist > 0.001f ? flat / dist : transform.forward;

            float target = Mathf.Clamp(dist,
                config.idealCombatDistanceMin, config.idealCombatDistanceMax);
            // keep inside the arena
            Vector3 want = player.position + dir * target;
            Vector3 fromCenter = want - _arenaCenter; fromCenter.y = 0f;
            if (fromCenter.magnitude > config.arenaRadius)
                want = _arenaCenter + fromCenter.normalized * config.arenaRadius;

            Vector3 step = Vector3.MoveTowards(
                new Vector3(transform.position.x, 0f, transform.position.z),
                new Vector3(want.x, 0f, want.z),
                config.repositionSpeed * Time.deltaTime);
            transform.position = new Vector3(step.x, transform.position.y, step.z);

            State = (Mathf.Abs(dist - target) > 1.5f) ? YuanpeiState.Reposition : YuanpeiState.Hover;
        }

        private void FaceTarget(float degPerSec)
        {
            if (player == null) return;
            Vector3 to = player.position - transform.position;
            // never straight overhead (spec §7.2): clamp the pitch we look at
            Vector3 flat = new Vector3(to.x, 0f, to.z);
            if (flat.sqrMagnitude < 0.0001f) return;
            float horiz = flat.magnitude;
            float pitch = Mathf.Clamp(
                Mathf.Atan2(-to.y, horiz) * Mathf.Rad2Deg,
                -config.maxPitchToPlayerDeg, -config.minPitchToPlayerDeg);
            Quaternion desired = Quaternion.LookRotation(flat.normalized, Vector3.up)
                                 * Quaternion.Euler(pitch, 0f, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, desired, degPerSec * Time.deltaTime);
        }

        private float SampleFloorY(Vector3 at)
        {
            Vector3 o = new Vector3(at.x, at.y + 20f, at.z);
            if (Physics.Raycast(o, Vector3.down, out var hit, 200f, groundMask, QueryTriggerInteraction.Ignore))
                // never let a raycast that clipped a building roof / prop count as "the floor"
                return Mathf.Min(hit.point.y, _arenaCenter.y + 2f);
            return _arenaCenter.y + 0.5f;
        }

        // Absolute Y ceiling (spec §7.2 "Boss 不飛到玩家正上方" + user: "太高了"). Runs every frame
        // after the state tick so nothing - hover, recharge, a bad floor sample - can fling the
        // boss above config.maxWorldY. Falling / execution states are driven by YuanpeiExecution
        // and are left alone (the boss is meant to be on the ground then).
        // An attack that legitimately goes high (ChargeCrush lining up over the player's head)
        // suspends the Y ceiling for a moment so ClampWorldY doesn't fight it.
        private float _yClampSuspendUntil;
        public void SuspendYClamp(float seconds) => _yClampSuspendUntil = Time.time + Mathf.Max(0f, seconds);

        // Charge moves (BodyCharge / OrbitDash / ChargeCrush) take over the boss's Y for the
        // duration of the dash so HoldHover() doesn't fight the descent. Restores itself.
        private float _hoverSuspendUntil;
        public void SuspendHover(float seconds) => _hoverSuspendUntil = Time.time + Mathf.Max(0f, seconds);

        private void ClampWorldY()
        {
            if (config == null) return;
            if (Time.time < _yClampSuspendUntil) return;
            if (State == YuanpeiState.Falling || State == YuanpeiState.ExecutionWindow
                || State == YuanpeiState.Executing || State == YuanpeiState.Intro) return;
            float ceiling = config.maxWorldY;
            if (transform.position.y > ceiling)
            {
                Vector3 p = transform.position;
                p.y = ceiling;
                transform.position = p;
            }
        }

        // ---------------------------------------------------------------- scheduler

        private bool HasLineOfSight()
        {
            if (player == null) return false;
            Vector3 from = transform.position;
            Vector3 to = player.position + Vector3.up * 1.2f;
            Vector3 d = to - from;
            var hits = Physics.RaycastAll(from, d.normalized, d.magnitude, losBlockers, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (h.collider.transform.root == player.root) continue;    // the player
                if (h.collider.transform.root == transform.root) continue; // our own body
                return false;                                             // a real wall in the way
            }
            return true;
        }

        private bool HasAnyAffordableInRange()
        {
            if (player == null) return false;
            float dist = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z),
                                          new Vector3(player.position.x, 0, player.position.z));
            foreach (var d in attackPool)
            {
                if (d == null || d.requiredPhase > vitals.Phase) continue;
                if (!vitals.CanAfford(d.energyCost)) continue;
                if (dist < d.minRange || dist > d.maxRange) continue;
                if (_cooldownUntil.TryGetValue(d.attackId, out float cd) && Time.time < cd) continue;
                return true;
            }
            return false;
        }

        private YuanpeiAttackDef PickAttack()
        {
            if (player == null) return null;
            float dist = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z),
                                          new Vector3(player.position.x, 0, player.position.z));
            var s = new YuanpeiScheduler.Situation
            {
                phase = vitals.Phase,
                energy = vitals.Energy,
                playerDistance = dist,
                hasLineOfSight = HasLineOfSight(),
                bossOnScreen = _wasOnScreen,
                onScreenSeconds = Time.time - _onScreenSince,
                onScreenGrace = config.onScreenGraceBeforeAttack,
                majorHazardActive = attacks != null && attacks.MajorHazardActive,
                lastAttack = _lastAttackId,
                hasLastAttack = _hasLastAttack,
                playerMovingStraightLong = PlayerMovingStraightLong(),
                playerLingeringArea = PlayerLingering(),
                arenaHasGoodFloor = true,
                now = Time.time,
            };
            return YuanpeiScheduler.Select(attackPool, in s, _cooldownUntil, _lastUsedTime,
                config.rotationRecoverySeconds, config.rotationRecentWeightFactor, _rng);
        }

        private Vector3 _lastPlayerPos;
        private float _straightTime;
        private Vector3 _lingerAnchor;
        private float _lingerTime;

        private bool PlayerMovingStraightLong()
        {
            if (player == null) return false;
            Vector3 vel = (player.position - _lastPlayerPos) / Mathf.Max(1e-4f, Time.deltaTime);
            _lastPlayerPos = player.position;
            if (vel.magnitude > 2f) _straightTime += Time.deltaTime; else _straightTime = 0f;
            return _straightTime > 1.2f;
        }

        private bool PlayerLingering()
        {
            if (player == null) return false;
            if ((player.position - _lingerAnchor).sqrMagnitude > 9f) { _lingerAnchor = player.position; _lingerTime = 0f; }
            else _lingerTime += Time.deltaTime;
            return _lingerTime > 1.5f;
        }

        private YuanpeiAttackId _lastAttackId;
        private bool _hasLastAttack;

        private void BeginAttack(YuanpeiAttackDef def)
        {
            if (verboseLog) Debug.Log($"[YuanpeiBoss] ATTACK -> {def.attackId} (energy {vitals.Energy:F0}, phase {vitals.Phase})", this);
            _lastAttackId = def.attackId;
            _hasLastAttack = true;
            _lastUsedTime[def.attackId] = Time.time;
            _cooldownUntil[def.attackId] = Time.time + def.cooldownSeconds;
            vitals.SpendEnergy(def.energyCost);

            float interval = _rng != null
                ? Mathf.Lerp(config.globalAttackIntervalMin, config.globalAttackIntervalMax, (float)_rng.NextDouble())
                : config.globalAttackIntervalMax;
            if (vitals.Phase >= 3) interval *= config.phase3IntervalScale;

            State = YuanpeiState.AttackTelegraph;
            _attackRoutine = StartCoroutine(RunAttack(def, interval));
        }

        private IEnumerator RunAttack(YuanpeiAttackDef def, float restAfter)
        {
            if (attacks != null)
            {
                yield return attacks.Run(def, player, this, phase =>
                {
                    State = phase == YuanpeiAttacks.Phase.Active ? YuanpeiState.Attacking
                          : phase == YuanpeiAttacks.Phase.Recovery ? YuanpeiState.AttackRecovery
                          : YuanpeiState.AttackTelegraph;
                });
            }
            else
            {
                yield return new WaitForSeconds(def.TotalDuration);
            }
            _attackRoutine = null;
            _globalRestUntil = Time.time + restAfter;
            if (State == YuanpeiState.Attacking || State == YuanpeiState.AttackRecovery || State == YuanpeiState.AttackTelegraph)
                State = YuanpeiState.Hover;
        }

        // 續183e/f - 下馬威. Kicked from IntroRoutine's tail / BeginEncounter(playIntro:false).
        // CLAIMS the FSM immediately (State=AttackTelegraph + a huge _globalRestUntil) so TickAirCombat's
        // 0.12s watchdog can't fire a normal attack in the settle beat and run concurrently with it
        // (that was 續183f's "沒命中 / 看起來沒變" - a scheduled attack stomped the barrage's VisualRoot
        // + _majorHazardActive + timing).
        private IEnumerator OpeningBarrageRoutine()
        {
            if (attacks == null || openingBarrageDef == null) { if (verboseLog) Debug.LogWarning("[YuanpeiBoss] 下馬威 skipped - attacks/def not wired"); yield break; }

            if (_attackRoutine != null) { StopCoroutine(_attackRoutine); _attackRoutine = null; }
            attacks.CancelAll();
            State = YuanpeiState.AttackTelegraph;
            _globalRestUntil = Time.time + 9999f;   // nothing else picks an attack until the barrage restores it

            yield return new WaitForSeconds(0.2f);  // let the player settle + camera hand back
            if (BattleOver || player == null || !player.gameObject.activeInHierarchy)
            { State = YuanpeiState.Hover; _globalRestUntil = Time.time + 0.5f; yield break; }

            if (verboseLog) Debug.Log("[YuanpeiBoss] 下馬威 OpeningBarrage FIRING (player=" + player.name + ")", this);
            _attackRoutine = StartCoroutine(RunOpeningBarrage());
        }

        private IEnumerator RunOpeningBarrage()
        {
            State = YuanpeiState.AttackTelegraph;
            yield return attacks.RunOpeningBarrage(openingBarrageDef, player, this);
            _attackRoutine = null;
            _globalRestUntil = Time.time + 1.2f;
            State = YuanpeiState.Hover;
            if (verboseLog) Debug.Log("[YuanpeiBoss] 下馬威 OpeningBarrage DONE", this);
        }

        // ---------------------------------------------------------------- recharge (spec §5.2)

        private void EnterRecharge()
        {
            CancelAttack();
            State = YuanpeiState.EnergyRecharge;
            _energyRechargeUntil = Time.time + config.energyRechargeMaxSeconds;
            if (attacks != null) attacks.CancelAll();
        }

        private void TickRecharge()
        {
            // descend to a hittable height, no casting, still takes damage (spec §5.2)
            float floor = SampleFloorY(transform.position);
            Vector3 p = transform.position;
            p.y = Mathf.MoveTowards(p.y, floor + config.rechargeHeight, 4f * Time.deltaTime);
            transform.position = p;
            FaceTarget(config.faceTurnSpeedDegPerSec * 0.5f);
            vitals.RegenEnergy(Time.deltaTime, vitals.Phase >= 3);

            if (vitals.Energy >= config.energyRechargeExitThreshold || Time.time >= _energyRechargeUntil)
                State = YuanpeiState.Hover;
        }

        // ---------------------------------------------------------------- interrupts (spec §12.2)

        private void OnPostureFull()
        {
            if (BattleOver) return;
            CancelAttack();
            if (attacks != null) attacks.CancelAll();
            State = YuanpeiState.PostureBreak;
            if (execution != null) execution.BeginPostureBreak();
        }

        private void OnDied()
        {
            // final execution animation gets to finish (spec §12.2); execution controller checks.
            if (State == YuanpeiState.Executing && execution != null && execution.InFinisherAnim)
                return;
            EnterDeath();
        }

        public void EnterDeath()
        {
            CancelAttack();
            if (attacks != null) attacks.CancelAll();
            State = YuanpeiState.Dead;
            StopAllCoroutines();
            // stop lock-on chasing a dead object (spec §17/§20.7) + stop taking hits
            foreach (var lt in GetComponentsInChildren<Live2DAction.Targeting.LockOnTarget>(true))
                lt.enabled = false;
            // 續 129: BodyCollider / CoreWeakPoint are triggers now (a solid boss body PhysX-shoved
            // the player off the map during a charge). Disable them all on death so nothing
            // interacts with the corpse; ResetForRematch re-enables.
            foreach (var col in GetComponentsInChildren<Collider>(true))
                col.enabled = false;
            SendMessage("OnYuanpeiBossDefeated", SendMessageOptions.DontRequireReceiver);
        }

        private void CancelAttack()
        {
            if (_attackRoutine != null) { StopCoroutine(_attackRoutine); _attackRoutine = null; }
        }

        // ---------------------------------------------------------------- state callbacks from YuanpeiExecution

        public void OnFallStarted() => State = YuanpeiState.Falling;
        public void OnExecutionWindowOpen() => State = YuanpeiState.ExecutionWindow;
        public void OnFinisherStarted() => State = YuanpeiState.Executing;

        public void OnRecoverToAir(float invulnSeconds)
        {
            _postExecInvuln = invulnSeconds > 0f;
            State = YuanpeiState.Recovering;
            StartCoroutine(RecoverRoutine(invulnSeconds));
        }

        private IEnumerator RecoverRoutine(float invuln)
        {
            vitals.ResetPosture();
            float floor = SampleFloorY(transform.position);
            float t = 0f;
            float dur = config.reAscendSeconds;
            float startY = transform.position.y;
            float endY = floor + config.hoverHeight;
            Quaternion visFrom = visualRoot != null ? visualRoot.localRotation : Quaternion.identity;
            while (t < dur)
            {
                t += Time.deltaTime;
                Vector3 p = transform.position;
                p.y = Mathf.Lerp(startY, endY, t / dur);
                transform.position = p;
                // 續 121 (user "處決後 boss 會變歪斜"): level the disc back to its authored upright
                // orientation while re-ascending, instead of only spinning down the yaw and leaving
                // the fall's X/Z tumble baked in.
                if (visualRoot != null)
                    visualRoot.localRotation = Quaternion.Slerp(visFrom, _skyVisualLocalRot, Mathf.SmoothStep(0f, 1f, t / dur));
                yield return null;
            }
            if (visualRoot != null) visualRoot.localRotation = _skyVisualLocalRot;
            if (invuln > 0f)
            {
                yield return new WaitForSeconds(invuln);
                _postExecInvuln = false;
            }
            _globalRestUntil = Time.time + 0.5f;
            State = YuanpeiState.Hover;
        }

        // ---------------------------------------------------------------- helpers

        private void TrackOnScreen()
        {
            if (_cam == null) _cam = Camera.main;
            bool on = true; // no camera -> assume visible (don't stall the fight)
            if (_cam != null)
            {
                // generous bounds - a big aerial boss reads as "present" well past the frame edge,
                // and the player almost always has it roughly in view during a fight. Too tight a
                // box made the boss go passive whenever the player turned (user: "攻擊慾望極低").
                Vector3 vp = _cam.WorldToViewportPoint(transform.position);
                on = vp.z > 0f && vp.x > -0.9f && vp.x < 1.9f && vp.y > -0.9f && vp.y < 1.9f;
            }
            if (on && !_wasOnScreen) _onScreenSince = Time.time;
            _wasOnScreen = on;
        }

        [SerializeField] private float combatVisualScaleFraction = 0.28f; // sky-landmark scale * this = fight size

        private IEnumerator IntroRoutine()
        {
            State = YuanpeiState.Intro;
            // descend + shrink from the giant sky logo to a fightable size over the arena
            Vector3 startPos = transform.position;
            Vector3 startVScale = visualRoot != null ? visualRoot.localScale : Vector3.one;
            _skyVisualScale = startVScale;
            float floor = SampleFloorY(_arenaCenter);
            float endY = Mathf.Min(floor + config.hoverHeight + 1.5f, config.maxWorldY);
            Vector3 endPos = new Vector3(_arenaCenter.x, endY, _arenaCenter.z);
            Vector3 endVScale = startVScale * combatVisualScaleFraction;
            float t = 0f, dur = 2.6f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / dur);
                transform.position = Vector3.Lerp(startPos, endPos, k);
                if (visualRoot != null)
                {
                    visualRoot.localScale = Vector3.Lerp(startVScale, endVScale, k);
                    visualRoot.Rotate(0f, 220f * Time.deltaTime, 0f, Space.Self);
                }
                yield return null;
            }
            if (visualRoot != null) visualRoot.localScale = endVScale;
            _lastPlayerPos = player != null ? player.position : Vector3.zero;
            _globalRestUntil = Time.time + 0.8f;
            State = YuanpeiState.Hover;
            if (playOpeningBarrage) StartCoroutine(OpeningBarrageRoutine());   // 續183e 下馬威
        }
        private Vector3 _skyVisualScale = Vector3.one;
        private Quaternion _skyVisualLocalRot = Quaternion.identity;

        private void OnDrawGizmosSelected()
        {
            var c = config != null ? config.arenaCenter : transform.position;
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(c, config != null ? config.arenaRadius : 11f);
        }
    }
}
