using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Live2DAction.Core;
using Live2DAction.Characters;
using Live2DAction.Combat;

namespace Live2DAction.AI.Boss.Yuanpei
{
    // Posture-break -> fall -> 5s F window -> execution -> re-ascend / death (spec §11).
    // Owns: the fall arc, the ground anchor, the F check, the one-time window lock, the
    // 20-25% HP execution damage (applied on the finisher's own hit event, spec §5.1), and the
    // recover/die branch (spec §11.5 / §11.6). This IS the F-execution for this boss - it does
    // not go through the player's shared ExecutionAbility (which is StancePoise-driven).
    public class YuanpeiExecution : MonoBehaviour
    {
        [SerializeField] private YuanpeiBoss boss;
        [SerializeField] private YuanpeiBossVitals vitals;
        [SerializeField] private Transform executionAnchor;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private LayerMask groundMask = ~0;

        public bool InFinisherAnim { get; private set; }
        public bool WindowOpen { get; private set; }
        public float WindowRemaining { get; private set; }
        public bool PromptVisible => WindowOpen && !_windowConsumed && !InFinisherAnim
            && vitals != null && !vitals.IsDead && PlayerInRange();

        private YuanpeiBossConfig _cfg;
        private Transform _player;
        private bool _windowConsumed;
        private bool _running;

        private void Awake()
        {
            if (boss == null) boss = GetComponent<YuanpeiBoss>();
            if (vitals == null) vitals = GetComponent<YuanpeiBossVitals>();
            if (visualRoot == null) visualRoot = boss != null ? boss.VisualRoot : transform;
            if (executionAnchor == null) executionAnchor = transform;
            _cfg = boss != null ? boss.Config : null;
        }

        public void BeginPostureBreak()
        {
            if (_running) return;
            _running = true;
            _windowConsumed = false;
            _player = boss != null ? boss.Player : null;
            StartCoroutine(Sequence());
        }

        private IEnumerator Sequence()
        {
            _cfg = boss != null ? boss.Config : _cfg;

            // --- fall (spec §11.3) ---
            boss?.OnFallStarted();
            Vector3 start = transform.position;
            Vector3 groundPoint = SampleGround(new Vector3(start.x, start.y, start.z));
            // keep the downed core reachable (spec §11.3): sit a little above ground
            Vector3 land = groundPoint + Vector3.up * 1.0f;

            float t = 0f;
            float dur = _cfg != null ? _cfg.fallSeconds : 1.1f;
            float spin = _cfg != null ? _cfg.fallSpinSpeedDeg : 540f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = t / dur;
                transform.position = Vector3.Lerp(start, land, k * k); // ease-in fall
                if (visualRoot != null)
                    visualRoot.Rotate(spin * Time.deltaTime, spin * 0.4f * Time.deltaTime, 0f, Space.Self);
                yield return null;
            }
            transform.position = land;

            // landing feedback (spec §11.3 - 灰塵、小型震波、鏡頭震動; visual only, no player damage)
            Live2DAction.Combat.HitStopController.Request(0.06f, 0.18f);
            SpawnLandingImpact(new Vector3(land.x, groundPoint.y + 0.05f, land.z));

            // --- F window (spec §11.4) ---
            WindowOpen = true;
            boss?.OnExecutionWindowOpen();
            float window = _cfg != null ? _cfg.executionWindowSeconds : 5f;
            WindowRemaining = window;
            while (WindowRemaining > 0f && !_windowConsumed)
            {
                WindowRemaining -= Time.deltaTime;
                if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame
                    && !vitals.IsDead && PlayerInRange())
                {
                    yield return Finisher();
                    yield break;
                }
                yield return null;
            }

            // --- missed (spec §11.6) ---
            WindowOpen = false;
            RecoverToAir(_cfg != null ? _cfg.energyAfterMissedExecution : 40f, 0f);
        }

        private IEnumerator Finisher()
        {
            _windowConsumed = true;
            WindowOpen = false;
            InFinisherAnim = true;
            boss?.OnFinisherStarted();

            // align player to the anchor (spec §11.5.4)
            var cc = _player != null ? _player.GetComponent<CharacterController>() : null;
            Vector3 anchorPos = executionAnchor.position;
            Vector3 faceDir = (transform.position - anchorPos); faceDir.y = 0f;
            if (faceDir.sqrMagnitude < 0.01f) faceDir = _player != null ? _player.forward : Vector3.forward;
            faceDir.Normalize();
            if (_player != null)
            {
                Vector3 stand = anchorPos - faceDir * 2.8f;   // 續 130: was 1.6 - player model clipped into the disc ("穿模")
                float footToRoot = cc != null ? Mathf.Max(0f, cc.height * 0.5f - cc.center.y) : 0f;
                stand.y = SampleGround(new Vector3(stand.x, _player.position.y + 0.5f, stand.z)).y + footToRoot + 0.05f;
                if (cc != null) cc.enabled = false;
                _player.SetPositionAndRotation(stand, Quaternion.LookRotation(faceDir, Vector3.up));
                if (cc != null) cc.enabled = true;
            }

            TryPlayPlayerExecuteAnim();

            // --- 續 125 (user): capture positions, cinematic camera, lock controls ---
            LockPlayer(true);
            Camera cam = Camera.main;
            _execCamCtrl = cam != null
                ? cam.GetComponent(typeof(Live2DAction.CameraSystem.ThirdPersonCameraController)) as Behaviour
                : null;
            _execCamCtrlWas = _execCamCtrl != null && _execCamCtrl.enabled;
            if (_execCamCtrl != null) _execCamCtrl.enabled = false;

            // pick the shoulder once, from where the camera currently is
            Vector3 pboss = transform.position, pplayer = _player != null ? _player.position : pboss;
            Vector3 lineAxis = pboss - pplayer; lineAxis.y = 0f;
            if (lineAxis.sqrMagnitude < 0.01f) lineAxis = faceDir;
            lineAxis.Normalize();
            _execCamSide = Vector3.Cross(Vector3.up, lineAxis);
            if (cam != null && Vector3.Dot(cam.transform.position - pplayer, _execCamSide) < 0f) _execCamSide = -_execCamSide;
            _execCamMode = ExecCamMode.FrameBoth;
            _execCamHandBack = true;
            Coroutine camRoutine = cam != null ? StartCoroutine(DriveExecutionCam(cam)) : null;

            float anim = _cfg != null ? _cfg.executionAnimationSeconds : 1.6f;
            float half = anim * 0.6f;
            yield return new WaitForSeconds(half);

            // --- hit event: apply execution damage (spec §5.1 - at the hit, not on press) ---
            bool lethal = vitals.ApplyExecutionDamage(boss != null ? boss.gameObject : gameObject);
            Live2DAction.Combat.HitStopController.Request(0.08f, 0.12f);

            yield return new WaitForSeconds(anim - half);
            InFinisherAnim = false;

            if (lethal || vitals.IsDead)
            {
                // hand off to YuanpeiEncounter.DeathDissolve (it takes Camera.main itself and
                // re-enables the controller at the end) - stop our cam without re-enabling.
                _execCamHandBack = false;
                _execCamMode = ExecCamMode.Done;
                if (camRoutine != null) StopCoroutine(camRoutine);
                LockPlayer(false);
                boss?.EnterDeath();
                _running = false;
                yield break;
            }

            // --- survived: the boss body-BUMPS the player away, then re-ascends; camera follows ---
            Vector3 away = _player != null ? (_player.position - transform.position) : -faceDir;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = -faceDir;
            away.Normalize();

            _execCamMode = ExecCamMode.FollowPlayer;
            StartCoroutine(BossBump(away, 0.32f));
            if (_player != null) yield return ShoveBack(_player, away, 5f, 0.5f);
            else yield return new WaitForSeconds(0.5f);

            // force the player back onto the ground (user: "f處決完後 要強制讓玩家降落到地面") -
            // the shove / a mid-air F press could leave them floating for the rest of the cinematic.
            SnapPlayerToGround(true);

            _execCamMode = ExecCamMode.FollowBossUp;
            RecoverToAir(_cfg != null ? _cfg.energyAfterExecution : 50f,
                         _cfg != null ? _cfg.postExecutionInvulnSeconds : 1f);
            float ascend = _cfg != null ? _cfg.reAscendSeconds : 1.4f;
            yield return new WaitForSeconds(ascend * 0.85f);

            _execCamMode = ExecCamMode.ReturnToPlayer;
            yield return new WaitForSeconds(0.55f);

            LockPlayer(false);                 // control back only now (user: "接著攝影機才回到玩家身上")
            _execCamMode = ExecCamMode.Done;   // DriveExecutionCam re-enables the controller + snaps yaw
        }

        // Boss lunges a short distance toward the player (into them), then springs back - the "肉身
        // 彈開" contact beat. Owns `transform` (boss root) - Update() doesn't touch it in Executing.
        private IEnumerator BossBump(Vector3 awayFromBoss, float seconds)
        {
            Vector3 home = transform.position;
            Vector3 lunge = home + awayFromBoss * 0.7f;   // toward the player - short so it doesn't clip them (續 130)
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Sin(Mathf.Clamp01(t / seconds) * Mathf.PI);   // out and back
                transform.position = Vector3.Lerp(home, lunge, k);
                yield return null;
            }
            transform.position = home;
        }

        // Skid the player back along `dir`, ease-out, with a little hop. CharacterController off so
        // it can't snag on geometry mid-cinematic; restored after.
        private IEnumerator ShoveBack(Transform who, Vector3 dir, float dist, float seconds)
        {
            var cc = who.GetComponent<CharacterController>();
            bool ccWas = cc != null && cc.enabled;
            if (cc != null) cc.enabled = false;
            Vector3 from = who.position;
            Vector3 to = from + dir * dist;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float lin = Mathf.Clamp01(t / seconds);
                float k = 1f - (1f - lin) * (1f - lin);   // ease-out
                who.position = Vector3.Lerp(from, to, k) + Vector3.up * (Mathf.Sin(lin * Mathf.PI) * 0.35f);
                yield return null;
            }
            if (cc != null) cc.enabled = ccWas;
        }

        // --- player control lock for the execution cinematic (續 123, user: "鎖住玩家操控") ---
        private Behaviour _lockedMove, _lockedCombat;
        private bool _lockedMoveWas, _lockedCombatWas, _isLocked;

        private void LockPlayer(bool locked)
        {
            if (locked == _isLocked) return;
            if (locked)
            {
                Transform p = _player;
                _lockedMove = p != null ? p.GetComponentInChildren<CharacterMovement>() as Behaviour : null;
                _lockedCombat = p != null ? p.GetComponentInChildren<PlayerCombat>() as Behaviour : null;
                _lockedMoveWas = _lockedMove != null && _lockedMove.enabled;
                _lockedCombatWas = _lockedCombat != null && _lockedCombat.enabled;
                if (_lockedMove != null) _lockedMove.enabled = false;
                if (_lockedCombat != null) _lockedCombat.enabled = false;
                _isLocked = true;
            }
            else
            {
                if (_lockedMove != null) _lockedMove.enabled = _lockedMoveWas;
                if (_lockedCombat != null) _lockedCombat.enabled = _lockedCombatWas;
                _lockedMove = _lockedCombat = null;
                _isLocked = false;
            }
        }

        private void OnDisable()
        {
            LockPlayer(false);   // never leave the player frozen
            if (_execCamCtrl != null) { _execCamCtrl.enabled = true; _execCamCtrl = null; }   // or the camera
        }

        // --- execution cinematic camera (續 125) ---
        // FrameBoth: side, close, both player + boss in shot for the execution animation.
        // FollowPlayer: track the player being body-bumped away.
        // FollowBossUp: hold on the ground while the boss re-ascends, tilting up.
        // ReturnToPlayer: ease to a normal over-the-shoulder of the player.
        // Done: re-enable ThirdPersonCameraController (+ snap yaw) unless _execCamHandBack is false.
        private enum ExecCamMode { FrameBoth, FollowPlayer, FollowBossUp, ReturnToPlayer, Done }
        private ExecCamMode _execCamMode;
        private Vector3 _execCamSide;
        private Behaviour _execCamCtrl;
        private bool _execCamCtrlWas, _execCamHandBack;

        private IEnumerator DriveExecutionCam(Camera cam)
        {
            while (_execCamMode != ExecCamMode.Done)
            {
                Vector3 bp = transform.position;
                Vector3 pp = _player != null ? _player.position : bp;
                Vector3 focus, want;
                switch (_execCamMode)
                {
                    case ExecCamMode.FrameBoth:
                        focus = (pp + bp) * 0.5f + Vector3.up * 1.15f;
                        float spread = Vector3.Distance(pp, bp);
                        want = focus + _execCamSide * Mathf.Clamp(spread * 0.9f + 2.9f, 3.7f, 6.8f) + Vector3.up * 1.25f;
                        break;
                    case ExecCamMode.FollowPlayer:
                        focus = pp + Vector3.up * 1.0f;
                        want = focus + _execCamSide * 4.6f + Vector3.up * 1.7f;
                        break;
                    case ExecCamMode.FollowBossUp:
                        // keep the player low in frame, the rising boss high in it
                        focus = Vector3.Lerp(pp + Vector3.up * 0.9f, bp, 0.55f);
                        want = pp + _execCamSide * 7.5f + Vector3.up * 2.6f;
                        break;
                    default: // ReturnToPlayer
                        focus = pp + Vector3.up * 1.25f;
                        Vector3 behind = _player != null ? _player.forward : Vector3.forward; behind.y = 0f;
                        if (behind.sqrMagnitude < 0.01f) behind = Vector3.forward;
                        want = focus - behind.normalized * 4.6f + Vector3.up * 1.9f;
                        break;
                }
                float rate = 7f * Time.deltaTime;
                cam.transform.position = Vector3.Lerp(cam.transform.position, want, rate);
                Vector3 look = focus - cam.transform.position;
                if (look.sqrMagnitude > 1e-4f)
                    cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation,
                        Quaternion.LookRotation(look.normalized, Vector3.up), rate);
                yield return null;
            }

            if (_execCamHandBack && _execCamCtrl != null)
            {
                _execCamCtrl.enabled = true;
                (_execCamCtrl as Live2DAction.CameraSystem.ThirdPersonCameraController)?.SnapYawToTarget();
            }
            _execCamCtrl = null;
        }

        private void RecoverToAir(float energy, float invuln)
        {
            _running = false;
            WindowOpen = false;
            if (vitals != null)
            {
                vitals.ResetPosture();
                vitals.SetEnergy(energy);
            }
            boss?.OnRecoverToAir(invuln);
        }

        private void TryPlayPlayerExecuteAnim()
        {
            if (_player == null) return;
            var ea = _player.GetComponentInChildren<Live2DAction.Combat.ExecutionAbility>();
            if (ea == null) return;
            // ExecutionAbility.executeTriggerName is private; play the common triggers on the animator
            var anim = _player.GetComponentInChildren<Animator>();
            if (anim == null) return;
            foreach (var trig in new[] { "ExecuteThrust", "Execute" })
            {
                foreach (var p in anim.parameters)
                    if (p.type == AnimatorControllerParameterType.Trigger && p.name == trig)
                    { anim.SetTrigger(trig); return; }
            }
        }

        private bool PlayerInRange()
        {
            if (_player == null || executionAnchor == null) return false;
            float r = _cfg != null ? _cfg.executionInteractDistance : 2.4f;
            Vector3 a = executionAnchor.position; Vector3 b = _player.position;
            a.y = 0f; b.y = 0f;
            return (a - b).sqrMagnitude <= r * r;
        }

        // Force the player onto the floor at their current XZ (user: "f處決完後 要強制讓玩家降落到
        // 地面"). Also used to seat them at the execution anchor. CC toggled so the teleport doesn't
        // fight collision; a small landing puff sells it.
        private void SnapPlayerToGround(bool puff)
        {
            if (_player == null) return;
            Vector3 g = SampleGround(_player.position + Vector3.up * 0.5f);
            var cc = _player.GetComponent<CharacterController>();
            float footToRoot = cc != null ? Mathf.Max(0f, cc.height * 0.5f - cc.center.y) : 0f;
            Vector3 pos = new Vector3(_player.position.x, g.y + footToRoot + 0.02f, _player.position.z);
            bool ccWas = cc != null && cc.enabled;
            if (cc != null) cc.enabled = false;
            _player.position = pos;
            if (cc != null) cc.enabled = ccWas;
            if (puff) SpawnLandingImpact(new Vector3(g.x, g.y + 0.05f, g.z));
        }

        private Vector3 SampleGround(Vector3 from)
        {
            Vector3 o = new Vector3(from.x, from.y + 40f, from.z);
            var hits = Physics.RaycastAll(o, Vector3.down, 320f, groundMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                var col = hits[i].collider;
                if (col == null) continue;
                if (col.GetComponentInParent<Live2DAction.Input.PlayerInputProvider>() != null) continue; // not the player
                if (col.GetComponentInParent<CharacterController>() != null) continue;
                if (boss != null && col.transform.root == boss.transform.root) continue;                  // not our own body
                return hits[i].point;
            }
            return new Vector3(from.x, (_cfg != null ? _cfg.arenaCenter.y : 0f) + 0.5f, from.z);
        }

        // spec §11.3 landing 灰塵 + 小型震波 - all cosmetic, no hit volume. Self-cleaning.
        private void SpawnLandingImpact(Vector3 ground)
        {
            var root = new GameObject("YuanpeiLandingImpact");
            root.transform.position = ground;
            root.AddComponent<LandingImpactFx>();
        }

        private sealed class LandingImpactFx : MonoBehaviour
        {
            private Transform _ring;
            private Renderer _ringR;
            private readonly System.Collections.Generic.List<Transform> _dust = new System.Collections.Generic.List<Transform>();
            private readonly System.Collections.Generic.List<Vector3> _dustVel = new System.Collections.Generic.List<Vector3>();
            private MaterialPropertyBlock _mpb;
            private float _t;

            private void Start()
            {
                _mpb = new MaterialPropertyBlock();

                _ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
                _ring.SetParent(transform, false);
                Destroy(_ring.GetComponent<Collider>());
                _ring.localScale = new Vector3(0.4f, 0.02f, 0.4f);
                _ringR = _ring.GetComponent<Renderer>();
                _ringR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                var rng = new System.Random();
                for (int i = 0; i < 8; i++)
                {
                    var d = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
                    d.SetParent(transform, false);
                    Destroy(d.GetComponent<Collider>());
                    d.localScale = Vector3.one * (0.25f + (float)rng.NextDouble() * 0.35f);
                    float a = (float)(rng.NextDouble() * System.Math.PI * 2.0);
                    var v = new Vector3(Mathf.Cos(a), 0.7f + (float)rng.NextDouble() * 0.6f, Mathf.Sin(a));
                    _dust.Add(d);
                    _dustVel.Add(v * (2.5f + (float)rng.NextDouble() * 2f));
                    var dr = d.GetComponent<Renderer>();
                    dr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }

            private void Update()
            {
                _t += Time.deltaTime;
                float life = 0.55f;
                float k = Mathf.Clamp01(_t / life);

                if (_ring != null)
                {
                    float r = Mathf.Lerp(0.4f, 5.5f, k);
                    _ring.localScale = new Vector3(r, 0.02f, r);
                    var c = new Color(0.75f, 0.68f, 0.55f, 1f - k);
                    _ringR.GetPropertyBlock(_mpb);
                    _mpb.SetColor("_BaseColor", c);
                    _mpb.SetColor("_EmissionColor", c * 0.6f);
                    _ringR.SetPropertyBlock(_mpb);
                }

                for (int i = 0; i < _dust.Count; i++)
                {
                    if (_dust[i] == null) continue;
                    var v = _dustVel[i];
                    v.y -= 9f * Time.deltaTime;
                    _dustVel[i] = v;
                    _dust[i].position += v * Time.deltaTime;
                    float s = Mathf.Max(0f, (0.35f) * (1f - k));
                    _dust[i].localScale = Vector3.one * s;
                }

                if (_t >= life) Destroy(gameObject);
            }
        }
    }
}
