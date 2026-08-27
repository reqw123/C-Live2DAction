using UnityEngine;
using UnityEngine.UI;

namespace Live2DAction.World
{
    // 2026-08-19, explicit user request ("3D動作遊戲不是常常有那種 障礙物跨越 或是跳躍爬高的比賽嗎
    // 怎麼設計") - "單純計時衝刺" direction the user picked from a clarifying question (no energy/
    // resource gating, just raw Jump/Dodge/Flight execution skill against a stopwatch). Owns the
    // whole run: which CheckpointGate is next, the running clock, and the persisted best time.
    //
    // 2026-08-20, explicit user request ("我希望改為互動式，與機關對話後，會觸發短時間內的跑酷計時")
    // - replaces the old "fly through gate 0 whenever you like" free-restart design with an
    // interact-gated, time-LIMITED challenge (see TimeTrialStartMechanism, the "機關" the player
    // now has to walk up to and press E on). The course sits Locked (every gate hidden/untouchable)
    // until BeginChallenge() is called; from that instant the player has challengeDurationSeconds
    // to clear every gate before the run auto-fails. Both a finish and a fail return the course to
    // Locked - there is no more "loop back through gate 0 to retry" shortcut, retrying always goes
    // back through the mechanism.
    public class TimeTrialController : MonoBehaviour
    {
        [SerializeField] private CheckpointGate[] checkpointsInOrder;
        [SerializeField] private Text statusText;
        [SerializeField] private string bestTimeKey = "SkyIslandTimeTrial_BestTimeSeconds";

        // 2026-08-20 - starting guess for how long the ground->sky flight course above
        // Updraft_MainArea should reasonably take (7 gates, ~8 unit gaps, dash-assisted flight) -
        // not measured against an actual playtest, adjust in the Inspector once someone has
        // actually run it.
        // 2026-08-23, explicit user request ("變成S形 總共20") - the S-curve redesign's total path
        // length is ~1.33x the old straight line, and now also demands active steering through 20
        // gates instead of flying roughly straight through 7 - scaled up accordingly rather than
        // left at the old straight-line estimate.
        // 2026-08-23 follow-up, real playtested bug ("s形也不夠彎曲 沒有難度") - the curve amplitude
        // was more than doubled (see SkyIslandTimeTrialSetup.LateralSwayAmplitude's own comment),
        // which pushed total path length up again to 113.14 units (was 66.75, originally 50.14) -
        // scaled once more to match. Still just an estimate, same caveat as before - adjust once
        // someone has actually flown it.
        // 2026-08-24, real playtested bug ("金色光環各自的間隔不夠遠") - amplitude raised again,
        // total path length now 154.44 units - scaled once more (95 * 154.44/113.14 = 129.7,
        // rounded to 130). This same turn ALSO halved CheckpointGate.dashSpeed/
        // DashInstantDisplacement ("衝刺距離改為現在的0.5倍"), which makes covering this longer
        // path slower still on top of the length increase alone - this estimate doesn't attempt to
        // quantify that separately, so it's more likely undershooting than overshooting the time
        // actually needed.
        // 2026-08-24 follow-up, explicit user request ("改為遊戲限時20秒") - overrides the whole
        // path-length-scaled-estimate approach above with a deliberate fixed value: 20 seconds is
        // far tighter than 154.44 units of course (at dashSpeed=7/DashInstantDisplacement=1.25's
        // reduced boost) would take to simply fly at a comfortable pace, so this is intentionally
        // a hard, execution-heavy time limit rather than another "how long should this reasonably
        // take" estimate - not a caveat to auto-adjust away, this one's explicit.
        [SerializeField] private float challengeDurationSeconds = 20f;

        // 2026-08-19, explicit user request ("達成目標後 顯示 完成任務!") - finishing a run used to
        // silently reset straight back to the normal per-checkpoint HUD line with no acknowledgment
        // at all. Held as a timed HUD override rather than a one-shot log/popup, matching this
        // project's existing "everything routes through the one persistent statusText" convention
        // (no separate UI element/animation system introduced just for this).
        [SerializeField] private float finishMessageSeconds = 3f;

        // 2026-08-20 - same "hold a message on the shared statusText for a beat" convention as
        // finishMessageSeconds above, for the new fail case (challengeDurationSeconds ran out
        // before the last gate).
        [SerializeField] private float failMessageSeconds = 3f;

        // 2026-08-20, explicit user request ("再按一次e能直接結束機關") - shorter than the other two
        // messages since cancelling is a deliberate, low-stakes player action, not a result worth
        // dwelling on.
        [SerializeField] private float cancelMessageSeconds = 1.5f;

        private int _nextIndex;
        private float _startTime;
        private float _challengeDeadline;
        private bool _running;
        // Locked = the mechanism hasn't been triggered yet (or the last attempt already resolved,
        // win or lose) - every gate is inactive (see SetGatesActive) and OnGateEntered is a no-op
        // even if something still managed to touch one. Starts true: no challenge is live until
        // TimeTrialStartMechanism calls BeginChallenge().
        private bool _locked = true;
        private float _bestTimeSeconds = -1f;
        private float _finishMessageTimer;
        private float _lastFinishTimeSeconds;
        private bool _lastFinishWasNewBest;
        private float _failMessageTimer;
        private float _cancelMessageTimer;

        // Read by TimeTrialStartMechanism to decide whether E should do anything right now (and
        // whether to show its own "press E" prompt at all) - false while a run is actually live,
        // or while a finish/fail message is still being shown (so a re-trigger can't stomp the
        // message the player hasn't finished reading yet).
        public bool CanBeginChallenge => _locked && _finishMessageTimer <= 0f && _failMessageTimer <= 0f && _cancelMessageTimer <= 0f;

        // Read by TimeTrialStartMechanism to decide whether E should CANCEL instead of begin -
        // true for the whole duration of a live challenge, letting the player back out early by
        // walking back to the mechanism rather than only ever timing out or finishing.
        public bool IsRunning => _running;

        // 2026-08-24, explicit user request ("'還是做不到嗎'...改成遊戲失敗時才顯示") - lets
        // ChallengeStartTaunt edge-detect the fail result specifically, instead of the challenge
        // simply starting (see that class's own comment for the full history of what it used to
        // poll and why).
        public bool IsShowingFailMessage => _failMessageTimer > 0f;

        private void Awake()
        {
            _bestTimeSeconds = PlayerPrefs.GetFloat(bestTimeKey, -1f);
        }

        private void OnEnable()
        {
            if (checkpointsInOrder == null)
            {
                return;
            }

            foreach (CheckpointGate gate in checkpointsInOrder)
            {
                if (gate != null)
                {
                    gate.Entered += OnGateEntered;
                }
            }

            SetGatesActive(false);
        }

        private void OnDisable()
        {
            if (checkpointsInOrder == null)
            {
                return;
            }

            foreach (CheckpointGate gate in checkpointsInOrder)
            {
                if (gate != null)
                {
                    gate.Entered -= OnGateEntered;
                }
            }
        }

        // Called by TimeTrialStartMechanism when the player presses E on it while
        // CanBeginChallenge is true. Unlocks and reveals every gate, resets progress, and starts
        // both the elapsed-time clock and the challengeDurationSeconds fail countdown from this
        // exact moment - unlike the old design, the clock no longer waits for the player to
        // actually reach gate 0 first, since the whole point of the time limit is to include the
        // ground->updraft->gate-0 approach, not just the gate-to-gate flight.
        public void BeginChallenge()
        {
            if (!CanBeginChallenge)
            {
                return;
            }

            _locked = false;
            _running = true;
            _nextIndex = 0;
            _startTime = Time.time;
            _challengeDeadline = Time.time + challengeDurationSeconds;

            SetGatesActive(true);
            foreach (CheckpointGate gate in checkpointsInOrder)
            {
                if (gate != null)
                {
                    gate.ResetVisual();
                }
            }
            RefreshGateVisuals();
        }

        private void OnGateEntered(CheckpointGate gate)
        {
            // Locked = no challenge is currently live (never started, or the last one already
            // finished/failed and is waiting on the mechanism again) - a touch shouldn't be able
            // to advance a run that doesn't exist. SetGatesActive(false) already deactivates every
            // gate's GameObject while locked so this is mostly a belt-and-suspenders check.
            if (_locked)
            {
                return;
            }

            // Only the CURRENTLY-expected gate advances the run - flying back through an already-
            // passed gate, or ahead into a later one out of order, is silently ignored rather than
            // letting the run skip checkpoints or rewind. "全部照順序穿過才算" is the whole point
            // of a checkpoint course, not an incidental detail.
            int enteredIndex = System.Array.IndexOf(checkpointsInOrder, gate);
            if (enteredIndex != _nextIndex)
            {
                return;
            }

            _nextIndex++;

            if (_nextIndex >= checkpointsInOrder.Length)
            {
                FinishRun();
            }

            RefreshGateVisuals();
        }

        private void FinishRun()
        {
            _running = false;
            _locked = true;
            float finishTime = Time.time - _startTime;

            _lastFinishWasNewBest = _bestTimeSeconds < 0f || finishTime < _bestTimeSeconds;
            if (_lastFinishWasNewBest)
            {
                _bestTimeSeconds = finishTime;
                PlayerPrefs.SetFloat(bestTimeKey, _bestTimeSeconds);
                PlayerPrefs.Save();
            }

            _lastFinishTimeSeconds = finishTime;
            _finishMessageTimer = finishMessageSeconds;

            SetGatesActive(false);
        }

        // 2026-08-20, explicit user request ("與機關對話後，會觸發短時間內的跑酷計時") - mirrors
        // FinishRun but for running out of challengeDurationSeconds before the last gate. Re-locks
        // the course the same way a finish does - retrying always goes back through the mechanism,
        // there's no partial-credit resume.
        private void FailRun()
        {
            _running = false;
            _locked = true;
            _failMessageTimer = failMessageSeconds;

            SetGatesActive(false);
        }

        // 2026-08-20, explicit user request ("再按一次e能直接結束機關") - lets the player back out
        // of a live challenge early by walking back to the mechanism and pressing E again (see
        // TimeTrialStartMechanism.Update, which is what actually calls this). Re-locks the course
        // exactly like FailRun, just without the "超過時限" framing - this was a deliberate stop,
        // not a timeout.
        public void CancelChallenge()
        {
            if (!_running)
            {
                return;
            }

            _running = false;
            _locked = true;
            _cancelMessageTimer = cancelMessageSeconds;

            SetGatesActive(false);
        }

        // Locked gates are fully deactivated (not just visually dimmed) - this also disables their
        // trigger Collider, so a locked gate can neither advance a (nonexistent) run nor fire its
        // own dash-on-touch boost (CheckpointGate.OnTriggerEnter) while the challenge hasn't
        // started.
        private void SetGatesActive(bool active)
        {
            if (checkpointsInOrder == null)
            {
                return;
            }

            foreach (CheckpointGate gate in checkpointsInOrder)
            {
                if (gate != null)
                {
                    gate.gameObject.SetActive(active);
                }
            }
        }

        private void RefreshGateVisuals()
        {
            if (checkpointsInOrder == null)
            {
                return;
            }

            for (int i = 0; i < checkpointsInOrder.Length; i++)
            {
                if (checkpointsInOrder[i] == null)
                {
                    continue;
                }

                CheckpointGate.GateState state = i < _nextIndex
                    ? CheckpointGate.GateState.Passed
                    : i == _nextIndex
                        ? CheckpointGate.GateState.Next
                        : CheckpointGate.GateState.Upcoming;
                checkpointsInOrder[i].SetState(state);
            }
        }

        private void Update()
        {
            // Fail check runs even if statusText is unassigned - the countdown has to actually
            // expire the challenge regardless of whether there's a HUD around to show it.
            if (_running && Time.time >= _challengeDeadline)
            {
                FailRun();
            }

            if (statusText == null || checkpointsInOrder == null)
            {
                return;
            }

            // 2026-08-23, explicit user request ("左上角"空島競速"等等ui文字請你在 機關啟動期間才
            // 顯示 其餘時間隱藏") - the idle/locked line ("前往上升氣流入口，與機關互動開始挑戰") used
            // to sit permanently in the top-left corner even while just wandering around with no
            // challenge active at all, which is exactly the clutter being complained about. Hidden
            // now specifically during THAT idle state; still shown while a run is actually live
            // (_running) AND during the brief finish/fail/cancel result messages right after one
            // ends - those are direct feedback about a run that WAS just active (a separate
            // explicit request: "達成目標後 顯示 完成任務!"), not the same thing as the standing
            // "go start a challenge" idle prompt this hides.
            bool showStatusText = _running || _finishMessageTimer > 0f || _failMessageTimer > 0f || _cancelMessageTimer > 0f;
            if (statusText.gameObject.activeSelf != showStatusText)
            {
                statusText.gameObject.SetActive(showStatusText);
            }
            if (!showStatusText)
            {
                return;
            }

            string bestLine = _bestTimeSeconds >= 0f ? FormatTime(_bestTimeSeconds) : "--:--.--";

            if (_finishMessageTimer > 0f)
            {
                _finishMessageTimer -= Time.deltaTime;
                string newBestLine = _lastFinishWasNewBest ? "\n新紀錄！" : "";
                statusText.text = $"完成任務！\n本次時間  {FormatTime(_lastFinishTimeSeconds)}{newBestLine}\n最佳時間  {bestLine}";
                return;
            }

            if (_failMessageTimer > 0f)
            {
                _failMessageTimer -= Time.deltaTime;
                statusText.text = $"挑戰失敗！超過時限\n最佳時間  {bestLine}";
                return;
            }

            if (_cancelMessageTimer > 0f)
            {
                _cancelMessageTimer -= Time.deltaTime;
                statusText.text = $"挑戰已中止\n最佳時間  {bestLine}";
                return;
            }

            float elapsed = Time.time - _startTime;
            float remaining = Mathf.Max(0f, _challengeDeadline - Time.time);
            statusText.text = $"空島競速  檢查點 {_nextIndex}/{checkpointsInOrder.Length}\n目前時間  {FormatTime(elapsed)}  剩餘 {FormatTime(remaining)}\n最佳時間  {bestLine}";
        }

        private static string FormatTime(float seconds)
        {
            int minutes = Mathf.FloorToInt(seconds / 60f);
            float remainder = seconds - minutes * 60f;
            return $"{minutes:00}:{remainder:00.00}";
        }
    }
}
