// Dev overlay only - compiled into the Editor and Development builds, stripped from release
// builds (its GameObject in GreyboxTest then loads as a harmless missing-script slot).
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using Live2DAction.Combat.Boss;
using Live2DAction.Core;
using Live2DAction.AI.Boss;

namespace Live2DAction.Combat
{
    // 2026-09-01, user request - Sekiro deflect, spec section 七 (「可切換的 Debug 顯示」).
    //
    // Scene-view gizmos + a Game-view text line:
    //   - player blade root->tip, GuardVolume capsule, the frontal block fan
    //   - each active boss BossHitbox's last translation-sweep segment
    //   - current defense state (None / Guard / Parry) and the most recent outcome + point
    //     (None / Parry / Guard / Hit)
    // Toggle with `toggleKey` at runtime. Gizmos also need the Game view's Gizmos toggle on to
    // show there (project norm - same as PlayerCombat's own range gizmo).
    //
    // 2026-09-02, spec item 9 §10.2 groundwork - a running session tally so the final numeric
    // tuning pass is data-driven, not vibes: clash outcomes + ratio, boss posture breaks + mean
    // time-between, player guard-breaks, HP swing on both sides. `resetKey` zeroes it.
    public class SekiroDeflectDebug : MonoBehaviour
    {
        [SerializeField] private PlayerGuard guard;
        [SerializeField] private PlayerGuardVolume guardVolume;
        [SerializeField] private Transform boss;
        [SerializeField] private bool show = true;
        [SerializeField] private KeyCode toggleKey = KeyCode.F9;
        [SerializeField] private KeyCode resetKey = KeyCode.F8;

        private string _lastOutcome = "None";
        private Vector3 _lastPoint;
        private float _lastTime = -99f;
        private Health _playerHealth;

        // --- item 9 §10.2 session metrics ---
        private Health _bossHealth;
        private StancePoise _playerStance;
        private StancePoise _bossStance;
        private BossStateMachine _bossSM;
        private int _parries, _guards, _hitsGuarding, _hitsOpen;
        private int _bossPostureBreaks, _playerStaggers;
        private float _firstEventTime = -1f;
        private float _lastPostureBreakTime = -1f;
        private float _postureBreakIntervalSum;
        private float _playerHpLost, _bossHpLost;
        private float _prevPlayerHp = -1f, _prevBossHp = -1f;
        private bool _bossWasPostureBroken, _playerWasStaggered;

        private void Awake()
        {
            if (guard == null) guard = GetComponentInParent<PlayerGuard>();
            if (guardVolume == null) guardVolume = GetComponentInChildren<PlayerGuardVolume>();
            _playerHealth = guard != null ? guard.GetComponent<Health>() : null;
            _playerStance = guard != null ? guard.GetComponent<StancePoise>() : null;
            if (boss != null)
            {
                _bossHealth = boss.GetComponentInChildren<Health>();
                _bossStance = boss.GetComponentInChildren<StancePoise>();
                _bossSM = boss.GetComponentInChildren<BossStateMachine>();
            }
        }

        private void OnEnable()
        {
            if (guard != null)
            {
                guard.Parried += OnParried;
                guard.Guarded += OnGuarded;
            }
            if (_playerHealth != null)
            {
                _playerHealth.Damaged += OnDamaged;
            }
        }

        private void OnDisable()
        {
            if (guard != null)
            {
                guard.Parried -= OnParried;
                guard.Guarded -= OnGuarded;
            }
            if (_playerHealth != null)
            {
                _playerHealth.Damaged -= OnDamaged;
            }
        }

        private void OnParried(Vector3 p) { _parries++; Stamp(); Record("Parry", p); }
        private void OnGuarded(Vector3 p) { _guards++; Stamp(); Record("Guard", p); }

        private void OnDamaged(DamageInfo d)
        {
            bool wasDefending = guard != null && guard.CurrentDefense != PlayerGuard.DefenseState.None;
            if (wasDefending) _hitsGuarding++; else _hitsOpen++;
            Stamp();
            Record("Hit", d.Point);
        }

        private void Stamp()
        {
            if (_firstEventTime < 0f) _firstEventTime = Time.time;
        }

        private void Record(string outcome, Vector3 point)
        {
            _lastOutcome = outcome;
            _lastPoint = point;
            _lastTime = Time.time;
        }

        private void ResetStats()
        {
            _parries = _guards = _hitsGuarding = _hitsOpen = 0;
            _bossPostureBreaks = _playerStaggers = 0;
            _firstEventTime = _lastPostureBreakTime = -1f;
            _postureBreakIntervalSum = 0f;
            _playerHpLost = _bossHpLost = 0f;
            _prevPlayerHp = _prevBossHp = -1f;
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(toggleKey)) show = !show;
            if (UnityEngine.Input.GetKeyDown(resetKey)) ResetStats();

            // Boss posture-break edges.
            if (_bossSM != null)
            {
                bool broken = _bossSM.CurrentState == BossState.PostureBroken;
                if (broken && !_bossWasPostureBroken)
                {
                    _bossPostureBreaks++;
                    if (_lastPostureBreakTime > 0f) _postureBreakIntervalSum += Time.time - _lastPostureBreakTime;
                    _lastPostureBreakTime = Time.time;
                }
                _bossWasPostureBroken = broken;
            }

            // Player stagger edges.
            if (_playerStance != null)
            {
                bool st = _playerStance.IsStaggered;
                if (st && !_playerWasStaggered) _playerStaggers++;
                _playerWasStaggered = st;
            }

            // HP swing (deltas, so revives / phase-heals don't count as "lost").
            if (_playerHealth != null)
            {
                float hp = _playerHealth.CurrentHealth;
                if (_prevPlayerHp >= 0f && hp < _prevPlayerHp) _playerHpLost += _prevPlayerHp - hp;
                _prevPlayerHp = hp;
            }
            if (_bossHealth != null)
            {
                float hp = _bossHealth.CurrentHealth;
                if (_prevBossHp >= 0f && hp < _prevBossHp) _bossHpLost += _prevBossHp - hp;
                _prevBossHp = hp;
            }
        }

        private void OnDrawGizmos()
        {
            if (!show)
            {
                return;
            }

            bool parrying = guard != null && guard.CurrentDefense == PlayerGuard.DefenseState.Parry;

            if (guardVolume != null && Application.isPlaying)
            {
                Gizmos.color = parrying ? Color.white : new Color(0.2f, 0.6f, 1f);
                Vector3 a = guardVolume.BladeRoot;
                Vector3 b = guardVolume.BladeTip;
                Gizmos.DrawLine(a, b);
                Gizmos.DrawWireSphere(a, guardVolume.Radius);
                Gizmos.DrawWireSphere(b, guardVolume.Radius);
            }

            if (guard != null)
            {
                Vector3 o = guard.transform.position + Vector3.up;
                float half = guard.GuardArcDegrees * 0.5f;
                Vector3 fwd = guard.transform.forward;
                Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.7f);
                Vector3 l = Quaternion.Euler(0f, -half, 0f) * fwd * 2f;
                Vector3 r = Quaternion.Euler(0f, half, 0f) * fwd * 2f;
                Gizmos.DrawLine(o, o + l);
                Gizmos.DrawLine(o, o + r);
                Gizmos.DrawLine(o + l, o + r);
            }

            if (boss != null)
            {
                Gizmos.color = Color.red;
                foreach (var hb in boss.GetComponentsInChildren<BossHitbox>())
                {
                    if (hb.HasSwept)
                    {
                        Gizmos.DrawLine(hb.LastSweepFrom, hb.LastSweepTo);
                        Gizmos.DrawWireSphere(hb.LastSweepTo, 0.08f);
                    }
                }
            }

            if (Time.time - _lastTime < 1.0f)
            {
                Gizmos.color = _lastOutcome == "Parry" ? Color.white
                    : _lastOutcome == "Guard" ? Color.cyan
                    : Color.red;
                Gizmos.DrawSphere(_lastPoint, 0.09f);
            }
        }

        private void OnGUI()
        {
            if (!show)
            {
                return;
            }
            string state = guard != null ? guard.CurrentDefense.ToString() : "?";
            float age = Time.time - _lastTime;
            float scale = guard != null ? guard.ParryWindowScale : 1f;
            float eff = guard != null ? guard.EffectiveParryWindow : 0f;
            GUI.color = scale < 0.5f ? Color.red : scale < 0.95f ? Color.yellow : Color.white;
            GUI.Label(new Rect(10f, 64f, 760f, 22f),
                $"[Sekiro]  Defense: {state}   ParryWin: {eff * 1000f:F0}ms (x{scale:F2})   " +
                $"Last: {_lastOutcome} ({age:F1}s ago)   (F9 hide / F8 reset)");

            // --- item 9 §10.2 session tally ---
            GUI.color = Color.white;
            int clashes = _parries + _guards + _hitsGuarding;
            float parryRate = clashes > 0 ? 100f * _parries / clashes : 0f;
            float meanBreakGap = _bossPostureBreaks > 1 ? _postureBreakIntervalSum / (_bossPostureBreaks - 1) : 0f;
            float dur = _firstEventTime > 0f ? Time.time - _firstEventTime : 0f;
            GUI.Label(new Rect(10f, 86f, 760f, 22f),
                $"[Stats {dur:F0}s]  Parry {_parries} / Guard {_guards} / HitBlocking {_hitsGuarding} / HitOpen {_hitsOpen}   ParryRate {parryRate:F0}%");
            GUI.Label(new Rect(10f, 108f, 760f, 22f),
                $"           BossPostureBreak x{_bossPostureBreaks}" +
                (meanBreakGap > 0f ? $" (mean {meanBreakGap:F1}s apart)" : "") +
                $"   PlayerStagger x{_playerStaggers}   HP lost - player {_playerHpLost:F0} / boss {_bossHpLost:F0}");
        }

        public void EditorConfigure(PlayerGuard g, PlayerGuardVolume vol, Transform bossRoot)
        {
            guard = g;
            guardVolume = vol;
            boss = bossRoot;
        }
    }
}
#endif
