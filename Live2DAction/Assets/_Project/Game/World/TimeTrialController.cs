using UnityEngine;
using UnityEngine.UI;

namespace Live2DAction.World
{
    // 2026-08-19, explicit user request ("3D動作遊戲不是常常有那種 障礙物跨越 或是跳躍爬高的比賽嗎
    // 怎麼設計") - "單純計時衝刺" direction the user picked from a clarifying question (no energy/
    // resource gating, just raw Jump/Dodge/Flight execution skill against a stopwatch). Owns the
    // whole run: which CheckpointGate is next, the running clock, and the persisted best time.
    //
    // Deliberately allows re-attempting immediately (no "start line" gate separate from
    // checkpoint 0) - flying back through checkpoint 0 both resets and restarts the clock in one
    // action, so a player who wants to try again just loops back to the start rather than needing
    // a separate reset control.
    public class TimeTrialController : MonoBehaviour
    {
        [SerializeField] private CheckpointGate[] checkpointsInOrder;
        [SerializeField] private Text statusText;
        [SerializeField] private string bestTimeKey = "SkyIslandTimeTrial_BestTimeSeconds";

        // 2026-08-19, explicit user request ("達成目標後 顯示 完成任務!") - finishing a run used to
        // silently reset straight back to the normal per-checkpoint HUD line with no acknowledgment
        // at all. Held as a timed HUD override rather than a one-shot log/popup, matching this
        // project's existing "everything routes through the one persistent statusText" convention
        // (no separate UI element/animation system introduced just for this).
        [SerializeField] private float finishMessageSeconds = 3f;

        // 2026-08-19, explicit user request ("飛行任務完成後 光圈等待10秒後再復原出現") - finishing
        // used to reset _nextIndex back to 0 immediately, which (combined with CheckpointGate's own
        // touch-vanish effect) meant gate 0 popped back to fully visible/re-enterable the very same
        // frame the run ended. Now the reset itself is deferred - see Update()'s own countdown -
        // giving the vanished rings a real cooldown beat before the course opens back up.
        [SerializeField] private float gateResetDelaySeconds = 10f;

        private int _nextIndex;
        private float _startTime;
        private bool _running;
        private float _bestTimeSeconds = -1f;
        private float _finishMessageTimer;
        private float _lastFinishTimeSeconds;
        private bool _lastFinishWasNewBest;
        private float _resetPendingTimer;

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

            RefreshGateVisuals();
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

        private void OnGateEntered(CheckpointGate gate)
        {
            // Course is on its post-finish cooldown (see gateResetDelaySeconds) - every gate is
            // still vanished/invisible during this window, so no touch should progress a "new"
            // run yet. The gate's own dash-on-touch effect is unaffected by this - that's wired
            // directly in CheckpointGate.OnTriggerEnter, independent of this event entirely.
            if (_resetPendingTimer > 0f)
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

            if (_nextIndex == 0)
            {
                _running = true;
                _startTime = Time.time;
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

            // _nextIndex deliberately NOT reset here anymore - every gate is currently vanished
            // (each one played its own touch effect on the way through), and resetting the index
            // immediately would make RefreshGateVisuals bring gate 0 straight back to "Next"
            // (bright, re-enterable) the same instant the run ends. The actual reset now happens
            // after gateResetDelaySeconds - see Update()'s own countdown.
            _resetPendingTimer = gateResetDelaySeconds;
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
            if (checkpointsInOrder != null && _resetPendingTimer > 0f)
            {
                _resetPendingTimer -= Time.deltaTime;
                if (_resetPendingTimer <= 0f)
                {
                    _resetPendingTimer = 0f;
                    _nextIndex = 0;
                    foreach (CheckpointGate gate in checkpointsInOrder)
                    {
                        if (gate != null)
                        {
                            gate.ResetVisual();
                        }
                    }
                    RefreshGateVisuals();
                }
            }

            if (statusText == null || checkpointsInOrder == null)
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

            float elapsed = _running ? Time.time - _startTime : 0f;
            statusText.text = $"空島競速  檢查點 {_nextIndex}/{checkpointsInOrder.Length}\n目前時間  {FormatTime(elapsed)}\n最佳時間  {bestLine}";
        }

        private static string FormatTime(float seconds)
        {
            int minutes = Mathf.FloorToInt(seconds / 60f);
            float remainder = seconds - minutes * 60f;
            return $"{minutes:00}:{remainder:00.00}";
        }
    }
}
