// Dev overlay only - compiled into the Editor and Development builds, stripped from release
// builds (its GameObject in GreyboxTest then loads as a harmless missing-script slot).
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Live2DAction.AI.Boss;
using Live2DAction.Combat.Boss;

namespace Live2DAction.DebugTools
{
    // 2026-09-06, user request ("把元培boss地圖 f8模式裡面的機制設計 用同樣形式修改武士boss的f7模式").
    // Reworked from the old BossAnimationDebugMode (Animator-clip preview) into the SAME shape as
    // YuanpeiAttackDebugMode (F8): the player stays in full control + invulnerable, the boss's own
    // BossStateMachine stops deciding/moving (BossStateMachine.DebugDirectControl), and the number
    // keys fire that boss's REAL attack pool (BossStateMachine.DebugFireAttack) at a movable
    // straw-man dummy - bypassing cooldown / range / angle / rest gates, repeatable, so each
    // telegraph / hit-window / root-motion / VFX can be watched over and over.
    //
    // F7 covers BOTH bosses that use BossStateMachine (武士 + 屁孩王); Tab cycles the current one.
    //
    // Setup: menu "Tools/Live2DAction/[Debug] Setup Boss Attack Debug Mode" (GreyboxTest).
    [DefaultExecutionOrder(200)]
    public class BossAttackDebugMode : MonoBehaviour
    {
        [System.Serializable]
        public class Target
        {
            [Tooltip("Shown in the on-screen list, e.g. \"武士\" / \"屁孩王\".")]
            public string label;
            [Tooltip("The boss's BossStateMachine - the debug mode drives it directly.")]
            public BossStateMachine boss;
        }

        [SerializeField] private Key toggleKey = Key.F7;
        [SerializeField] private Key cycleTargetKey = Key.Tab;
        [SerializeField] private Key pauseKey = Key.P;
        [SerializeField] private Key replayKey = Key.Y;          // matches F8 (R is the player Ultimate key)
        [SerializeField] private Key slowerKey = Key.Minus;
        [SerializeField] private Key fasterKey = Key.Equals;
        [Tooltip("Toggles the leash + selected-attack min/max-range + too-close-kick rings.")]
        [SerializeField] private Key rangeRingKey = Key.G;
        [Tooltip("Snaps the target dummy to the real player's current position.")]
        [SerializeField] private Key snapDummyToPlayerKey = Key.J;
        [Tooltip("Puts the dummy back on a known-good ground spot next to the current boss.")]
        [SerializeField] private Key resetDummyKey = Key.Home;
        [Tooltip("Toggles the camera to the dummy's own point of view (mouse-look only).")]
        [SerializeField] private Key dummyViewKey = Key.L;

        [Header("Reposition (arrows = dummy XZ, PageUp/Down = dummy Y, Shift+either = boss)")]
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private Key verticalUpKey = Key.PageUp;
        [SerializeField] private Key verticalDownKey = Key.PageDown;

        [Header("Free-look / fly camera while paused")]
        [SerializeField] private float freeLookSensitivity = 0.12f;
        [SerializeField] private float freeFlySpeed = 8f;

        [SerializeField] private Target[] targets = System.Array.Empty<Target>();

        public bool Active { get; private set; }

        private int _targetIndex;
        private float _animSpeed = 1f;
        private bool _paused;
        private bool _showRings = true;
        private bool _dummyView;

        private Transform _player;
        private Live2DAction.Core.Health _playerHealth;
        private GameObject _dummy;
        private readonly Dictionary<Target, (Vector3 pos, Quaternion rot, Vector3 home, Quaternion homeRot)> _rest
            = new Dictionary<Target, (Vector3, Quaternion, Vector3, Quaternion)>();
        private float _dummyHeightOffset;

        private Behaviour _camController;
        private bool _camControllerWasEnabled;
        private float _freeYaw, _freePitch;

        // camera-hijacking systems suspended for the whole session (mirrors F8's SetWorldInputLocked)
        private Behaviour _lockedPossessionSwitcher, _lockedViewFocusDirector;
        private bool _lockedPossessionSwitcherWas, _lockedViewFocusDirectorWas;

        private readonly List<GameObject> _rings = new List<GameObject>();
        private int _selected = -1;   // index into the current boss's DebugAttackPool

        private Target Current =>
            (targets != null && _targetIndex >= 0 && _targetIndex < targets.Length) ? targets[_targetIndex] : null;
        private BossStateMachine CurBoss => Current != null ? Current.boss : null;

        private const string PrefKeyX = "BossAttackDebugDummy.X";
        private const string PrefKeyY = "BossAttackDebugDummy.Y";
        private const string PrefKeyZ = "BossAttackDebugDummy.Z";
        private const string PrefKeyOff = "BossAttackDebugDummy.Off";

        private void OnDisable() { if (Active) Exit(); }

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
                CycleTarget();

            var boss = CurBoss;
            var pool = boss != null ? boss.DebugAttackPool : null;
            Key[] digits = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0 };
            bool shift = kb[Key.LeftShift].isPressed || kb[Key.RightShift].isPressed;
            if (pool != null)
            {
                for (int d = 0; d < digits.Length; d++)
                {
                    if (!kb[digits[d]].wasPressedThisFrame) continue;
                    if (d < pool.Count && pool[d] != null) Fire(d);
                }
            }
            if (replayKey != Key.None && kb[replayKey].wasPressedThisFrame && _selected >= 0) Fire(_selected);

            if (pauseKey != Key.None && kb[pauseKey].wasPressedThisFrame)
            {
                _paused = !_paused;
                Time.timeScale = _paused ? 0f : _animSpeed;
                SetFreeLook(_paused);
            }
            if (slowerKey != Key.None && kb[slowerKey].wasPressedThisFrame)
            {
                _animSpeed = Mathf.Max(0.05f, _animSpeed - 0.15f); _paused = false;
                Time.timeScale = _animSpeed; SetFreeLook(false);
            }
            if (fasterKey != Key.None && kb[fasterKey].wasPressedThisFrame)
            {
                _animSpeed = Mathf.Min(2f, _animSpeed + 0.15f); _paused = false;
                Time.timeScale = _animSpeed; SetFreeLook(false);
            }
            if (rangeRingKey != Key.None && kb[rangeRingKey].wasPressedThisFrame) { _showRings = !_showRings; RefreshRings(); }
            if (dummyViewKey != Key.None && kb[dummyViewKey].wasPressedThisFrame)
            {
                _dummyView = !_dummyView;
                if (_dummyView && _paused) { _paused = false; Time.timeScale = _animSpeed; }
                SetDummyView(_dummyView);
            }
            if (snapDummyToPlayerKey != Key.None && kb[snapDummyToPlayerKey].wasPressedThisFrame && _dummy != null && _player != null)
            {
                _dummyHeightOffset = 0f;
                _dummy.transform.position = _player.position;
                SnapToGround(_dummy.transform);
                SaveDummyPrefs();
            }
            if (resetDummyKey != Key.None && kb[resetDummyKey].wasPressedThisFrame) ResetDummyPosition();

            HandleReposition(kb, shift);
            if (_paused) DriveFreeLook();
            else if (_dummyView) DriveDummyView();
            else RestorePlayerCameraControl();

            HoldBossesAtRest();
            RefreshRings();
        }

        // ---- enter / exit -------------------------------------------------------------------

        public void Enter()
        {
            if (Active || targets == null || targets.Length == 0) return;
            Active = true;
            _animSpeed = 1f;
            _paused = false;
            Time.timeScale = 1f;

            _player = ResolvePlayer();
            _playerHealth = _player != null ? _player.GetComponentInChildren<Live2DAction.Core.Health>() : null;
            if (_playerHealth != null) _playerHealth.SetInvulnerable(this, true);
            SetWorldInputLocked(true);

            _rest.Clear();
            foreach (var t in targets)
            {
                if (t == null || t.boss == null) continue;
                var tr = t.boss.transform;
                _rest[t] = (tr.position, tr.rotation, t.boss.DebugHomePosition, tr.rotation);
                t.boss.DebugDirectControl = true;
                t.boss.DebugForceIdle();
            }

            bool freshDummy = _dummy == null;
            if (freshDummy) BuildDummy();
            _dummy.SetActive(true);
            if (freshDummy && !TryLoadDummyPrefs()) ResetDummyPosition();

            RetargetCurrent();
            RefreshRings();
            Debug.Log("[BossAttackDebug] ON - target " + (Current != null ? Current.label : "?") +
                      ". Digits fire that boss's real attack pool at the dummy (bypass cooldown/range/rest). " +
                      cycleTargetKey + " cycle boss, arrows move dummy, " + verticalUpKey + "/" + verticalDownKey +
                      " lift dummy, Shift+either moves the boss, " + snapDummyToPlayerKey + " snap dummy to you, " +
                      resetDummyKey + " reset dummy, " + dummyViewKey + " dummy view, P pause (free-look), -/= speed, " +
                      replayKey + " replay, " + rangeRingKey + " rings. 貓咪附身(C)/守望者(T) 暫停。");
        }

        public void Exit()
        {
            if (!Active) return;
            Active = false;
            if (_paused) SetFreeLook(false);
            if (_dummyView) { SetDummyView(false); _dummyView = false; }
            _paused = false;
            Time.timeScale = 1f;
            RestorePlayerCameraControl();
            SetWorldInputLocked(false);

            if (_playerHealth != null) _playerHealth.SetInvulnerable(this, false);
            _playerHealth = null;

            foreach (var kv in _rest)
            {
                var t = kv.Key;
                if (t == null || t.boss == null) continue;
                t.boss.DebugDirectControl = false;
                t.boss.DebugTarget = _player;   // hand the real player back
                // put it back exactly where debug started (+ leash home) so it doesn't sprint away
                t.boss.transform.SetPositionAndRotation(kv.Value.pos, kv.Value.rot);
                t.boss.DebugSetHome(kv.Value.home, kv.Value.homeRot);
                t.boss.DebugForceIdle();
            }
            _rest.Clear();

            if (_dummy != null) _dummy.SetActive(false);
            ClearRings();
            Debug.Log("[BossAttackDebug] OFF");
        }

        private void CycleTarget()
        {
            _targetIndex = (_targetIndex + 1) % targets.Length;
            _selected = -1;
            _freeYaw = 0f;
            RetargetCurrent();
        }

        // every boss is frozen; only the CURRENT one aims at the dummy (the others keep their
        // last target - they're just holding Idle anyway).
        private void RetargetCurrent()
        {
            if (CurBoss != null && _dummy != null) CurBoss.DebugTarget = _dummy.transform;
        }

        private void Fire(int index)
        {
            var boss = CurBoss;
            var pool = boss != null ? boss.DebugAttackPool : null;
            if (pool == null || index < 0 || index >= pool.Count || pool[index] == null) return;
            _selected = index;
            RetargetCurrent();
            boss.DebugFireAttack(pool[index]);
            Debug.Log("[BossAttackDebug] " + Current.label + " -> " + pool[index].AttackId + " (clip " + pool[index].ClipName + ")");
            RefreshRings();
        }

        // hold each frozen boss at its captured rest XZ between attacks (its FSM does nothing now)
        private void HoldBossesAtRest()
        {
            foreach (var kv in _rest)
            {
                var boss = kv.Key != null ? kv.Key.boss : null;
                if (boss == null) continue;
                var st = boss.CurrentState;
                bool playing = st != BossState.Idle && st != BossState.Dormant && st != BossState.Alert;
                if (playing) continue;   // let an in-progress attack move it
                var p = boss.transform.position;
                p.x = kv.Value.pos.x; p.z = kv.Value.pos.z;
                boss.transform.position = p;
            }
        }

        // ---- straw-man dummy (稻草人) ------------------------------------------------------

        private void BuildDummy()
        {
            if (_dummy != null) return;
            _dummy = new GameObject("BossDebugTargetDummy");

            MakePart(PrimitiveType.Cube, "Post", new Vector3(0f, 0.4f, 0f), new Vector3(0.12f, 0.8f, 0.12f), new Color(0.35f, 0.22f, 0.12f));
            MakePart(PrimitiveType.Capsule, "Body", new Vector3(0f, 1.1f, 0f), new Vector3(0.55f, 0.7f, 0.55f), new Color(0.85f, 0.72f, 0.35f));
            MakePart(PrimitiveType.Sphere, "Head", new Vector3(0f, 1.85f, 0f), Vector3.one * 0.42f, new Color(0.75f, 0.6f, 0.3f));
            MakePart(PrimitiveType.Cube, "Arms", new Vector3(0f, 1.45f, 0f), new Vector3(1.5f, 0.1f, 0.1f), new Color(0.4f, 0.28f, 0.14f));

            var beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beacon.name = "Beacon";
            beacon.transform.SetParent(_dummy.transform, false);
            Destroy(beacon.GetComponent<Collider>());
            beacon.transform.localPosition = new Vector3(0f, 4.5f, 0f);
            beacon.transform.localScale = new Vector3(0.08f, 3f, 0.08f);
            var beaconR = beacon.GetComponent<Renderer>();
            var beaconMat = new Material(Shader.Find("Live2DAction/VFX/AdditiveUnlit") ?? Shader.Find("Universal Render Pipeline/Unlit"));
            beaconMat.color = new Color(0.3f, 1f, 0.5f);
            beaconR.material = beaconMat;
            beaconR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var cc = _dummy.AddComponent<CapsuleCollider>();
            cc.center = new Vector3(0f, 1.1f, 0f);
            cc.height = 1.8f;
            cc.radius = 0.4f;

            // invulnerable Health so boss hitboxes register a valid IDamageable target here, but it
            // can never die / trip death reactions (mirrors F8's 稻草人).
            var h = _dummy.AddComponent<Live2DAction.Core.Health>();
            h.SetInvulnerable(this, true);
        }

        private void MakePart(PrimitiveType type, string name, Vector3 localPos, Vector3 localScale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(_dummy.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor(Shader.PropertyToID("_BaseColor"), color);
            r.SetPropertyBlock(mpb);
        }

        private void ResetDummyPosition()
        {
            if (_dummy == null) return;
            _dummyHeightOffset = 0f;
            var boss = CurBoss;
            Vector3 anchor = boss != null ? boss.transform.position : Vector3.zero;
            Vector3 fwd = boss != null ? boss.transform.forward : Vector3.forward;
            _dummy.transform.position = anchor + fwd * 3.5f;
            SnapToGround(_dummy.transform);
            SaveDummyPrefs();
            Debug.Log("[BossAttackDebug] dummy reset to the ground in front of " + (Current != null ? Current.label : "the boss") + ".");
        }

        private void SnapToGround(Transform t, float heightOffset = 0f)
        {
            var hits = Physics.RaycastAll(t.position + Vector3.up * 60f, Vector3.down, 300f, groundMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.collider == null || hit.collider.transform.root == t) continue;
                if (CurBoss != null && hit.collider.transform.root == CurBoss.transform.root) continue;
                var p = t.position;
                p.y = hit.point.y + heightOffset;
                t.position = p;
                return;
            }
            // no floor found - fall back to the boss's own Y
            if (CurBoss != null)
            {
                var p = t.position;
                p.y = CurBoss.transform.position.y + heightOffset;
                t.position = p;
            }
        }

        private void SaveDummyPrefs()
        {
            if (_dummy == null) return;
            Vector3 p = _dummy.transform.position;
            PlayerPrefs.SetFloat(PrefKeyX, p.x);
            PlayerPrefs.SetFloat(PrefKeyY, p.y);
            PlayerPrefs.SetFloat(PrefKeyZ, p.z);
            PlayerPrefs.SetFloat(PrefKeyOff, _dummyHeightOffset);
        }

        private bool TryLoadDummyPrefs()
        {
            if (_dummy == null || !PlayerPrefs.HasKey(PrefKeyX)) return false;
            _dummy.transform.position = new Vector3(
                PlayerPrefs.GetFloat(PrefKeyX), PlayerPrefs.GetFloat(PrefKeyY), PlayerPrefs.GetFloat(PrefKeyZ));
            _dummyHeightOffset = PlayerPrefs.GetFloat(PrefKeyOff, 0f);
            return true;
        }

        // ---- reposition (arrows = dummy XZ, PageUp/Down = dummy Y, Shift+either = boss) -------

        private void HandleReposition(Keyboard kb, bool shift)
        {
            float vertical = 0f;
            if (verticalUpKey != Key.None && kb[verticalUpKey].isPressed) vertical += 1f;
            if (verticalDownKey != Key.None && kb[verticalDownKey].isPressed) vertical -= 1f;
            if (vertical != 0f)
            {
                float dy = vertical * moveSpeed * Time.unscaledDeltaTime;
                if (shift && CurBoss != null)
                {
                    var p = CurBoss.transform.position; p.y += dy;
                    CurBoss.transform.position = p;
                    UpdateRestFor(Current);
                }
                else if (_dummy != null)
                {
                    _dummyHeightOffset += dy;
                    var p = _dummy.transform.position; p.y += dy;
                    _dummy.transform.position = p;
                    SaveDummyPrefs();
                }
            }

            Vector3 input = Vector3.zero;
            if (kb[Key.UpArrow].isPressed) input += Vector3.forward;
            if (kb[Key.DownArrow].isPressed) input += Vector3.back;
            if (kb[Key.LeftArrow].isPressed) input += Vector3.left;
            if (kb[Key.RightArrow].isPressed) input += Vector3.right;
            if (input.sqrMagnitude < 0.0001f) return;

            var cam = Camera.main;
            Vector3 camFwd = cam != null ? cam.transform.forward : Vector3.forward;
            Vector3 camRight = cam != null ? cam.transform.right : Vector3.right;
            camFwd.y = 0f; camRight.y = 0f;
            camFwd = camFwd.sqrMagnitude < 0.0001f ? Vector3.forward : camFwd.normalized;
            camRight = camRight.sqrMagnitude < 0.0001f ? Vector3.right : camRight.normalized;
            Vector3 move = (camFwd * input.z + camRight * input.x).normalized * moveSpeed * Time.unscaledDeltaTime;

            if (shift)
            {
                if (CurBoss == null) return;
                CurBoss.transform.position += move;
                UpdateRestFor(Current);
            }
            else
            {
                if (_dummy == null) return;
                _dummy.transform.position += move;
                SnapToGround(_dummy.transform, _dummyHeightOffset);
                SaveDummyPrefs();
            }
        }

        private void UpdateRestFor(Target t)
        {
            if (t == null || t.boss == null || !_rest.TryGetValue(t, out var v)) return;
            var tr = t.boss.transform;
            _rest[t] = (tr.position, tr.rotation, tr.position, tr.rotation);
            t.boss.DebugSetHome(tr.position, tr.rotation);   // leash home follows so Exit won't sprint it back
        }

        // ---- cameras (paused free-look / dummy view) ---------------------------------------

        private void SetFreeLook(bool on) => TakeOrReleaseCamera(on, snapToDummy: false);
        private void SetDummyView(bool on) => TakeOrReleaseCamera(on, snapToDummy: true);

        private void TakeOrReleaseCamera(bool on, bool snapToDummy)
        {
            var cam = Camera.main;
            if (cam == null) return;
            if (on)
            {
                if (_camController == null)
                {
                    _camController = cam.GetComponent(typeof(Live2DAction.CameraSystem.ThirdPersonCameraController)) as Behaviour;
                    if (_camController != null) { _camControllerWasEnabled = _camController.enabled; _camController.enabled = false; }
                }
                if (snapToDummy && _dummy != null)
                {
                    Vector3 eye = _dummy.transform.position + Vector3.up * 1.6f;
                    Vector3 lookAt = CurBoss != null ? CurBoss.transform.position : eye + Vector3.forward;
                    Quaternion rot = Quaternion.LookRotation((lookAt - eye).normalized, Vector3.up);
                    cam.transform.SetPositionAndRotation(eye, rot);
                    Vector3 e = rot.eulerAngles;
                    _freeYaw = e.y; _freePitch = e.x > 180f ? e.x - 360f : e.x;
                }
                else
                {
                    Vector3 e = cam.transform.rotation.eulerAngles;
                    _freeYaw = e.y; _freePitch = e.x > 180f ? e.x - 360f : e.x;
                }
            }
            else
            {
                if (_camController != null) { _camController.enabled = _camControllerWasEnabled; _camController = null; }
            }
        }

        private void DriveFreeLook()
        {
            var cam = Camera.main; if (cam == null) return;
            var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 d = mouse.delta.ReadValue();
                _freeYaw += d.x * freeLookSensitivity;
                _freePitch = Mathf.Clamp(_freePitch - d.y * freeLookSensitivity, -85f, 85f);
                cam.transform.rotation = Quaternion.Euler(_freePitch, _freeYaw, 0f);
            }
            var kb = Keyboard.current; if (kb == null) return;
            Vector3 mv = Vector3.zero;
            if (kb[Key.W].isPressed) mv += cam.transform.forward;
            if (kb[Key.S].isPressed) mv -= cam.transform.forward;
            if (kb[Key.A].isPressed) mv -= cam.transform.right;
            if (kb[Key.D].isPressed) mv += cam.transform.right;
            if (kb[Key.Space].isPressed) mv += Vector3.up;
            if (kb[Key.LeftCtrl].isPressed) mv -= Vector3.up;
            if (mv.sqrMagnitude > 0.0001f)
                cam.transform.position += mv.normalized * freeFlySpeed * Time.unscaledDeltaTime;
        }

        private void DriveDummyView()
        {
            var cam = Camera.main; if (cam == null || _dummy == null) return;
            var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 d = mouse.delta.ReadValue();
                _freeYaw += d.x * freeLookSensitivity;
                _freePitch = Mathf.Clamp(_freePitch - d.y * freeLookSensitivity, -85f, 85f);
            }
            cam.transform.position = _dummy.transform.position + Vector3.up * 1.6f;
            cam.transform.rotation = Quaternion.Euler(_freePitch, _freeYaw, 0f);
        }

        // whenever this tool isn't holding the camera, force the normal controller back on if any
        // boss attack (a wide eject shot etc.) left it off - mirrors F8's self-heal.
        private void RestorePlayerCameraControl()
        {
            var cam = Camera.main; if (cam == null) return;
            var ctrl = cam.GetComponent(typeof(Live2DAction.CameraSystem.ThirdPersonCameraController)) as Behaviour;
            if (ctrl != null && !ctrl.enabled) ctrl.enabled = true;
        }

        private void SetWorldInputLocked(bool locked)
        {
            if (locked)
            {
                _lockedPossessionSwitcher = FindFirstObjectByType(typeof(Live2DAction.CameraSystem.CameraPossessionSwitcher)) as Behaviour;
                _lockedViewFocusDirector = FindFirstObjectByType(typeof(Live2DAction.CameraSystem.ViewFocusDirector)) as Behaviour;
                if (_lockedPossessionSwitcher != null) { _lockedPossessionSwitcherWas = _lockedPossessionSwitcher.enabled; _lockedPossessionSwitcher.enabled = false; }
                if (_lockedViewFocusDirector != null) { _lockedViewFocusDirectorWas = _lockedViewFocusDirector.enabled; _lockedViewFocusDirector.enabled = false; }
            }
            else
            {
                if (_lockedPossessionSwitcher != null) _lockedPossessionSwitcher.enabled = _lockedPossessionSwitcherWas;
                if (_lockedViewFocusDirector != null) _lockedViewFocusDirector.enabled = _lockedViewFocusDirectorWas;
                _lockedPossessionSwitcher = null;
                _lockedViewFocusDirector = null;
            }
        }

        private Transform ResolvePlayer()
        {
            var providers = FindObjectsByType<Live2DAction.Input.PlayerInputProvider>(FindObjectsSortMode.None);
            Transform first = null;
            foreach (var p in providers)
            {
                Transform root = p.transform.root;
                if (first == null) first = root;
                if (root.name == "Player") return root;
            }
            return first;
        }

        // ---- range rings (視覺化範圍) ----------------------------------------------------

        private GameObject RingFor(int i, Color c, float radius, Vector3 center)
        {
            while (_rings.Count <= i)
            {
                var go = new GameObject("BossDebugRing_" + _rings.Count);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.loop = true;
                lr.positionCount = 48;
                lr.widthMultiplier = 0.08f;
                lr.material = new Material(Shader.Find("Live2DAction/VFX/AdditiveUnlit") ?? Shader.Find("Universal Render Pipeline/Unlit"));
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _rings.Add(go);
            }
            var line = _rings[i].GetComponent<LineRenderer>();
            line.startColor = line.endColor = c;
            for (int k = 0; k < line.positionCount; k++)
            {
                float a = (k / (float)line.positionCount) * Mathf.PI * 2f;
                line.SetPosition(k, center + new Vector3(Mathf.Cos(a) * radius, 0.05f, Mathf.Sin(a) * radius));
            }
            return _rings[i];
        }

        private void RefreshRings()
        {
            var boss = CurBoss;
            if (!_showRings || boss == null) { ClearRings(); return; }

            Vector3 c = boss.transform.position;
            RingFor(0, new Color(1f, 1f, 1f, 0.30f), Mathf.Max(0.5f, boss.DebugLeashRange), boss.DebugHomePosition).SetActive(boss.DebugLeashRange > 0.05f);
            RingFor(1, new Color(1f, 0.55f, 0.2f, 0.55f), boss.DebugStandoffFloor, c).SetActive(boss.DebugStandoffFloor > 0.05f);

            var pool = boss.DebugAttackPool;
            if (pool != null && _selected >= 0 && _selected < pool.Count && pool[_selected] != null)
            {
                var d = pool[_selected];
                RingFor(2, new Color(0.3f, 1f, 0.4f, 0.6f), d.MinDistance, c).SetActive(d.MinDistance > 0.05f);
                RingFor(3, new Color(1f, 0.3f, 0.25f, 0.6f), d.MaxDistance, c).SetActive(d.MaxDistance > 0.05f);
            }
            else
            {
                for (int i = 2; i < _rings.Count; i++) if (_rings[i] != null) _rings[i].SetActive(false);
            }
        }

        private void ClearRings()
        {
            foreach (var r in _rings) if (r != null) Destroy(r);
            _rings.Clear();
        }

        // ---- OnGUI ---------------------------------------------------------------------------

        private void OnGUI()
        {
            if (!Active) return;
            var boss = CurBoss;
            var pool = boss != null ? boss.DebugAttackPool : null;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("BOSS ATTACK DEBUG  (" + toggleKey + " exit)");
            sb.AppendLine(cycleTargetKey + " cycle boss  |  speed " + _animSpeed.ToString("0.00")
                + (_paused ? " [PAUSED - mouse+WASD free-look]" : (_dummyView ? " [稻草人視角]" : ""))
                + "  (-/= speed, P pause, " + replayKey + " replay, " + rangeRingKey + " rings " + (_showRings ? "ON" : "off") + ")");
            sb.AppendLine("arrows = move 稻草人 (XZ), " + verticalUpKey + "/" + verticalDownKey + " = lift (Y), Shift+either = move boss, "
                + snapDummyToPlayerKey + " = snap to you, " + resetDummyKey + " = reset, " + dummyViewKey + " = 稻草人視角");
            sb.AppendLine("white ring = leash, orange = too-close kick, green = attack minRange, red = maxRange");
            for (int i = 0; i < targets.Length; i++)
                sb.AppendLine((i == _targetIndex ? "▶ " : "   ") + targets[i].label + (targets[i].boss != null ? "  [" + targets[i].boss.CurrentState + "]" : "  (missing)"));
            sb.AppendLine("");
            int shown = 0;
            if (pool != null)
            {
                shown = Mathf.Min(pool.Count, 10);
                sb.AppendLine("── " + (Current != null ? Current.label : "?") + " attack pool ──");
                for (int i = 0; i < shown; i++)
                {
                    var d = pool[i];
                    if (d == null) continue;
                    sb.AppendLine("  [" + ((i + 1) % 10) + "] " + d.AttackId + (i == _selected ? "   ◀ selected" : "")
                        + "   range " + d.MinDistance.ToString("0.#") + "-" + d.MaxDistance.ToString("0.#") + "m");
                }
                if (pool.Count > 10) sb.AppendLine("  (+" + (pool.Count - 10) + " more - no key)");
            }
            GUI.color = Color.white;
            GUI.Box(new Rect(12, 12, 520, 92 + 20 * (targets.Length + shown + 3)), sb.ToString());
        }
    }
}
#endif
