// Dev overlay only - compiled into the Editor and Development builds, stripped from release
// builds (its GameObject in GreyboxTest then loads as a harmless missing-script slot).
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Live2DAction.DebugTools
{
    // 2026-09-02, user request ("我想做一套獨立的角色除錯機制模式...以守望者為攝影機視角,可以選擇任一
    // 個目標(武士、屁孩王),選定的目標會固定站在原地,然後我可以透過一些方式控制目標做出她有的特定動作").
    //
    // A self-contained boss-animation inspector. Toggle it on with a key -> the view swaps to a
    // dedicated debug camera framing the current target, the target's own AI (BossStateMachine etc.)
    // is switched off and it's pinned in place, and number keys play that target's individual
    // Animator states so you can watch each attack clip in isolation, slow it down, pause, scrub.
    // Toggle off -> everything restored exactly (cameras, disabled components, timescale).
    //
    // Nothing here is wired into gameplay; it's an always-inactive-until-toggled dev overlay, same
    // spirit as SekiroDeflectDebug / SpectatorCameraToggle. Setup: menu
    // "Tools/Live2DAction/[Debug] Setup Boss Animation Debug Mode".
    [DefaultExecutionOrder(200)]
    public class BossAnimationDebugMode : MonoBehaviour
    {
        [System.Serializable]
        public class Target
        {
            [Tooltip("Shown in the on-screen list, e.g. \"武士\" / \"屁孩王\".")]
            public string label;

            [Tooltip("The Animator that plays this target's clips (武士 = the root; 屁孩王 = its Visual child).")]
            public Animator animator;

            [Tooltip("The transform pinned in place while debugging (usually the gameplay root, NOT the Animator's GameObject if they differ).")]
            public Transform pinRoot;

            [Tooltip("Behaviours switched OFF while this target is being debugged and back ON when the mode exits - its BossStateMachine, NavPathFollower, health regen, etc.")]
            public Behaviour[] disableWhileDebugging = System.Array.Empty<Behaviour>();

            [Tooltip("Animator STATE names (Base Layer) that this debug mode can play - one per attack / pose. Filled by the setup tool from the AnimatorController.")]
            public string[] stateNames = System.Array.Empty<string>();

            [Tooltip("States whose combat height comes from an FSM script arc, NOT the clip (e.g. LeapSlam). " +
                     "The debug mode layers the same triangular Y arc on top of the ground while that state plays.")]
            public VerticalArc[] verticalArcs = System.Array.Empty<VerticalArc>();
        }

        [System.Serializable]
        public class VerticalArc
        {
            public string stateName;
            [Tooltip("Peak height above the ground (world units) - LeapSlam's tuning.LeapSlamExtraHeight.")]
            public float peakHeight = 8f;
            [Range(0f, 1f)] public float riseNt = 0.05f;
            [Range(0f, 1f)] public float peakNt = 0.3f;
            [Range(0f, 1f)] public float fallEndNt = 0.53f;

            public float HeightAt(float nt)
            {
                if (nt <= riseNt || nt >= fallEndNt) return 0f;
                return nt < peakNt
                    ? Mathf.Lerp(0f, peakHeight, Mathf.InverseLerp(riseNt, peakNt, nt))
                    : Mathf.Lerp(peakHeight, 0f, Mathf.InverseLerp(peakNt, fallEndNt, nt));
            }
        }

        [SerializeField] private Key toggleKey = Key.F7;
        [SerializeField] private Key cycleTargetKey = Key.Tab;
        [SerializeField] private Key pauseKey = Key.P;
        [SerializeField] private Key replayKey = Key.R;
        [SerializeField] private Key slowerKey = Key.Minus;
        [SerializeField] private Key fasterKey = Key.Equals;

        [Tooltip("Camera GameObject enabled while debugging (all other active cameras are snapshotted, disabled, and restored on exit). Driven each LateUpdate to frame the current target.")]
        [SerializeField] private GameObject debugCamera;

        [Tooltip("Camera framing: local offset from the target's pin root (x = right, y = up, z = back). The camera looks at the target + aimHeight.")]
        [SerializeField] private Vector3 cameraOffset = new Vector3(2.2f, 2.6f, -5.5f);
        [SerializeField] private float aimHeight = 1.6f;
        [Tooltip("Degrees/sec the camera orbits the target while holding the orbit keys (, and .).")]
        [SerializeField] private float orbitSpeed = 60f;
        [SerializeField] private Key orbitLeftKey = Key.Comma;
        [SerializeField] private Key orbitRightKey = Key.Period;
        [Tooltip("Mouse-wheel zoom: how much one scroll notch changes the camera-distance multiplier.")]
        [SerializeField] private float zoomStep = 0.12f;
        [SerializeField] private float zoomMin = 0.3f;
        [SerializeField] private float zoomMax = 3f;

        [SerializeField] private Target[] targets = System.Array.Empty<Target>();

        public bool Active { get; private set; }

        private int _targetIndex;
        private float _animSpeed = 1f;   // persistent user-chosen playback speed (survives clip switches)
        private bool _paused;
        private float _orbitYaw;
        private float _zoom = 1f;        // camera-distance multiplier, mouse wheel
        private string _lastPlayed = "";

        // While a clip is running the played target is NOT pinned - it moves with the clip's own
        // displacement (root motion). It snaps back to its pinned pose the moment the clip finishes.
        private bool _clipRunning;
        private Target _clipTarget;
        private string _clipState = "";

        private readonly List<GameObject> _restoreCameras = new List<GameObject>();
        private readonly HashSet<GameObject> _restoreSet = new HashSet<GameObject>();
        // the debug camera's Camera components can be individually .enabled=false even while the
        // GameObject is active (守望者's Viewpoint is like this) - toggle both, restore both.
        private Camera[] _debugCams = System.Array.Empty<Camera>();
        private bool _debugCamGoWasActive;
        private bool[] _debugCamWasEnabled = System.Array.Empty<bool>();
        // per Target: which of its disableWhileDebugging behaviours we actually turned off
        private readonly Dictionary<Target, List<Behaviour>> _weDisabled = new Dictionary<Target, List<Behaviour>>();
        private readonly Dictionary<Target, (Vector3 pos, Quaternion rot)> _pinned = new Dictionary<Target, (Vector3, Quaternion)>();
        private readonly Dictionary<Target, AnimatorCullingMode> _cullRestore = new Dictionary<Target, AnimatorCullingMode>();

        private Target Current => (targets != null && _targetIndex >= 0 && _targetIndex < targets.Length) ? targets[_targetIndex] : null;

        private void Start()
        {
            // leave the debug camera exactly as authored (disabled). We only touch it while Active.
        }

        private void EnableDebugCamera(bool on)
        {
            if (debugCamera == null) return;
            if (on)
            {
                _debugCams = debugCamera.GetComponentsInChildren<Camera>(true);
                _debugCamGoWasActive = debugCamera.activeSelf;
                _debugCamWasEnabled = new bool[_debugCams.Length];
                for (int i = 0; i < _debugCams.Length; i++) _debugCamWasEnabled[i] = _debugCams[i].enabled;
                debugCamera.SetActive(true);
                foreach (var c in _debugCams) { c.enabled = true; if (c.TryGetComponent(out AudioListener al)) al.enabled = false; }
            }
            else
            {
                for (int i = 0; i < _debugCams.Length; i++) if (_debugCams[i] != null) _debugCams[i].enabled = _debugCamWasEnabled[i];
                if (debugCamera != null) debugCamera.SetActive(_debugCamGoWasActive);
                _debugCams = System.Array.Empty<Camera>();
            }
        }

        private void KeepDebugCameraLive()
        {
            if (debugCamera == null) return;
            if (!debugCamera.activeSelf) debugCamera.SetActive(true);
            foreach (var c in _debugCams) if (c != null && !c.enabled) c.enabled = true;
        }

        private void OnDisable()
        {
            if (Active) Exit();
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (toggleKey != Key.None && kb[toggleKey].wasPressedThisFrame)
            {
                if (Active) Exit(); else Enter();
                return;
            }
            if (!Active) return;

            if (cycleTargetKey != Key.None && kb[cycleTargetKey].wasPressedThisFrame && targets.Length > 1)
            {
                SnapPlayingTargetBack();
                _targetIndex = (_targetIndex + 1) % targets.Length;
                _orbitYaw = 0f;
                RefreshFreeze();
            }

            // digit 1..0 -> states 1..10; Shift + digit 1..0 -> states 11..20
            Key[] digits = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0 };
            bool shift = kb[Key.LeftShift].isPressed || kb[Key.RightShift].isPressed;
            var t = Current;
            if (t != null && t.animator != null && t.stateNames != null)
            {
                for (int d = 0; d < digits.Length; d++)
                {
                    if (!kb[digits[d]].wasPressedThisFrame) continue;
                    int i = shift ? d + 10 : d;
                    if (i < t.stateNames.Length) Play(t, t.stateNames[i]);
                }
                if (replayKey != Key.None && kb[replayKey].wasPressedThisFrame && !string.IsNullOrEmpty(_lastPlayed))
                {
                    Play(t, _lastPlayed);
                }
                if (pauseKey != Key.None && kb[pauseKey].wasPressedThisFrame)
                {
                    _paused = !_paused;
                    ApplySpeed(t);
                }
                if (slowerKey != Key.None && kb[slowerKey].wasPressedThisFrame)
                {
                    _animSpeed = Mathf.Max(0.05f, _animSpeed - 0.15f);
                    _paused = false;
                    ApplySpeed(t);
                }
                if (fasterKey != Key.None && kb[fasterKey].wasPressedThisFrame)
                {
                    _animSpeed = Mathf.Min(2f, _animSpeed + 0.15f);
                    _paused = false;
                    ApplySpeed(t);
                }
            }

            if (orbitLeftKey != Key.None && kb[orbitLeftKey].isPressed) _orbitYaw -= orbitSpeed * Time.unscaledDeltaTime;
            if (orbitRightKey != Key.None && kb[orbitRightKey].isPressed) _orbitYaw += orbitSpeed * Time.unscaledDeltaTime;

            var mouse = Mouse.current;
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    // wheel up (positive) = zoom in = shorter distance
                    _zoom = Mathf.Clamp(_zoom - Mathf.Sign(scroll) * zoomStep, zoomMin, zoomMax);
                }
            }
        }

        private void ApplySpeed(Target t)
        {
            if (t != null && t.animator != null) t.animator.speed = _paused ? 0f : _animSpeed;
        }

        private void Play(Target t, string stateName)
        {
            if (t.animator == null || string.IsNullOrEmpty(stateName)) return;
            SnapPlayingTargetBack(); // whatever was running, return it to rest first
            // keep the user's chosen playback speed across clips - only a fresh pick un-pauses.
            _paused = false;
            ApplySpeed(t);
            t.animator.CrossFadeInFixedTime(stateName, 0.08f, 0, 0f);
            _lastPlayed = stateName;
            _clipRunning = true;
            _clipTarget = t;
            _clipState = stateName;
            Debug.Log("[BossAnimDebug] " + t.label + " -> " + stateName + "  (speed " + _animSpeed.ToString("0.00") + ")");
        }

        // Returns the currently-playing target to its pinned pose and ends the run.
        private void SnapPlayingTargetBack()
        {
            if (!_clipRunning || _clipTarget == null) { _clipRunning = false; return; }
            if (_clipTarget.pinRoot != null && _pinned.TryGetValue(_clipTarget, out var rest))
            {
                _clipTarget.pinRoot.SetPositionAndRotation(rest.pos, rest.rot);
            }
            _clipRunning = false;
            _clipTarget = null;
        }

        private void LateUpdate()
        {
            if (!Active) return;

            ApplySpeed(Current); // re-assert every frame so nothing silently resets it back to 1

            // The running clip's target moves with the clip's own HORIZONTAL displacement; everyone
            // else is pinned. Y is held at the ground - same as combat, where BossStateMachine
            // zeroes rootMotionDelta.y and gravity keeps the boss planted (otherwise a clip with any
            // vertical hip root motion looks like "武士 空中飛劈").
            if (_clipRunning && _clipTarget != null && _clipTarget.animator != null && _clipTarget.pinRoot != null)
            {
                var anim = _clipTarget.animator;
                Vector3 d = anim.deltaPosition; d.y = 0f;
                _clipTarget.pinRoot.position += d;
                _clipTarget.pinRoot.rotation = anim.deltaRotation * _clipTarget.pinRoot.rotation;

                var si = anim.GetCurrentAnimatorStateInfo(0);
                if (_pinned.TryGetValue(_clipTarget, out var rest))
                {
                    // hold ground Y, then layer any FSM-script vertical arc for this state (LeapSlam etc.)
                    float arc = 0f;
                    var arcs = _clipTarget.verticalArcs;
                    if (arcs != null)
                        foreach (var a in arcs)
                            if (a != null && a.stateName == _clipState) { arc = a.HeightAt(Mathf.Clamp01(si.normalizedTime)); break; }
                    var p = _clipTarget.pinRoot.position; p.y = rest.pos.y + arc;
                    _clipTarget.pinRoot.position = p;
                }

                if (!anim.IsInTransition(0) && si.IsName(_clipState) && si.normalizedTime >= 1f)
                {
                    SnapPlayingTargetBack();
                }
            }

            foreach (var kv in _pinned)
            {
                if (kv.Key.pinRoot != null && !(_clipRunning && kv.Key == _clipTarget))
                {
                    kv.Key.pinRoot.SetPositionAndRotation(kv.Value.pos, kv.Value.rot);
                }
            }

            // keep the debug camera the only live one (other owners re-enable their camera each frame)
            if (debugCamera != null)
            {
                foreach (Camera cam in Camera.allCameras)
                {
                    var go = cam.gameObject;
                    if (go == debugCamera || System.Array.IndexOf(_debugCams, cam) >= 0 || !go.activeSelf) continue;
                    if (_restoreSet.Add(go)) _restoreCameras.Add(go);
                    go.SetActive(false);
                }
                KeepDebugCameraLive();

                var t = Current;
                if (t != null && t.pinRoot != null)
                {
                    Vector3 pivot = t.pinRoot.position + Vector3.up * aimHeight;
                    Quaternion yaw = Quaternion.Euler(0f, _orbitYaw, 0f);
                    Vector3 offset = yaw * (new Vector3(cameraOffset.x, cameraOffset.y, cameraOffset.z) * _zoom);
                    debugCamera.transform.position = pivot + offset;
                    debugCamera.transform.rotation = Quaternion.LookRotation(pivot - debugCamera.transform.position, Vector3.up);
                }
            }
        }

        public void Enter()
        {
            if (Active || targets == null || targets.Length == 0) return;
            Active = true;
            _animSpeed = 1f;
            _paused = false;
            _orbitYaw = 0f;
            _zoom = 1f;

            EnableDebugCamera(true);

            _restoreCameras.Clear();
            _restoreSet.Clear();
            foreach (Camera cam in Camera.allCameras)
            {
                var go = cam.gameObject;
                if (go == debugCamera || System.Array.IndexOf(_debugCams, cam) >= 0 || !go.activeSelf) continue;
                _restoreCameras.Add(go);
                _restoreSet.Add(go);
                go.SetActive(false);
            }

            RefreshFreeze();
            Debug.Log("[BossAnimDebug] ON - target " + (Current != null ? Current.label : "?") + ". Digits play states, Tab cycles target, P pause, R replay, -/= speed, ,/. orbit.");
        }

        // Freeze ALL targets (disable their AI + pin transform). Idempotent - safe to call on target switch.
        private void RefreshFreeze()
        {
            foreach (var t in targets)
            {
                if (t == null) continue;
                if (!_weDisabled.TryGetValue(t, out var list))
                {
                    list = new List<Behaviour>();
                    _weDisabled[t] = list;
                    if (t.disableWhileDebugging != null)
                    {
                        foreach (var b in t.disableWhileDebugging)
                        {
                            if (b != null && b.enabled) { b.enabled = false; list.Add(b); }
                        }
                    }
                }
                if (!_pinned.ContainsKey(t) && t.pinRoot != null)
                {
                    _pinned[t] = (t.pinRoot.position, t.pinRoot.rotation);
                }
                if (!_cullRestore.ContainsKey(t) && t.animator != null)
                {
                    _cullRestore[t] = t.animator.cullingMode;
                    t.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; // pose keeps advancing regardless of framing
                }
            }
        }

        public void Exit()
        {
            if (!Active) return;
            Active = false;
            SnapPlayingTargetBack(); // return the boss to its rest pose/position

            foreach (var go in _restoreCameras) if (go != null) go.SetActive(true);
            _restoreCameras.Clear();
            _restoreSet.Clear();
            EnableDebugCamera(false);

            foreach (var kv in _weDisabled)
            {
                foreach (var b in kv.Value) if (b != null) b.enabled = true;
                if (kv.Key != null && kv.Key.animator != null) kv.Key.animator.speed = 1f;
            }
            foreach (var kv in _cullRestore) if (kv.Key != null && kv.Key.animator != null) kv.Key.animator.cullingMode = kv.Value;
            _cullRestore.Clear();
            _weDisabled.Clear();
            _pinned.Clear();
            Debug.Log("[BossAnimDebug] OFF");
        }

        private void OnGUI()
        {
            if (!Active) return;
            var t = Current;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("BOSS ANIM DEBUG  (" + toggleKey + " exit)");
            sb.AppendLine(cycleTargetKey + " cycle target   |   speed " + _animSpeed.ToString("0.00") + (_paused ? " [PAUSED]" : "") + "   zoom " + _zoom.ToString("0.00") + "  (-/= , P pause, R replay, ,/. orbit, wheel zoom)");
            for (int i = 0; i < targets.Length; i++)
            {
                sb.AppendLine((i == _targetIndex ? "▶ " : "   ") + targets[i].label);
            }
            sb.AppendLine("");
            int shown = 0;
            if (t != null && t.stateNames != null)
            {
                sb.AppendLine("── " + t.label + " states  (11+ = Shift+digit) ──");
                shown = Mathf.Min(t.stateNames.Length, 20);
                for (int i = 0; i < shown; i++)
                {
                    string key = i < 10 ? "" + ((i + 1) % 10) : "Sh+" + ((i - 10 + 1) % 10);
                    sb.AppendLine("  [" + key + "] " + t.stateNames[i] + (t.stateNames[i] == _lastPlayed ? "   ◀ playing" : ""));
                }
                if (t.stateNames.Length > 20) sb.AppendLine("  (+" + (t.stateNames.Length - 20) + " more - no key)");
            }
            GUI.color = Color.white;
            GUI.Box(new Rect(12, 12, 450, 44 + 20 * (targets.Length + shown + 3)), sb.ToString());
        }
    }
}
#endif
