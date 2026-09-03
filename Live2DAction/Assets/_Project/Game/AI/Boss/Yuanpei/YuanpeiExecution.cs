using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Live2DAction.Core;

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

            // landing shake (visual only - spec §11.3)
            Live2DAction.Combat.HitStopController.Request(0.04f, 0.2f);

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
            if (_player != null)
            {
                Vector3 stand = anchorPos - faceDir.normalized * 1.6f;
                stand.y = _player.position.y;
                if (cc != null) cc.enabled = false;
                _player.SetPositionAndRotation(stand, Quaternion.LookRotation(faceDir.normalized, Vector3.up));
                if (cc != null) cc.enabled = true;
            }

            // try to play the player's own execute animation
            TryPlayPlayerExecuteAnim();

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
                boss?.EnterDeath();
                _running = false;
                yield break;
            }

            // survived (spec §11.5.9) - reset posture, energy ~50, brief i-frames, re-ascend
            RecoverToAir(_cfg != null ? _cfg.energyAfterExecution : 50f,
                         _cfg != null ? _cfg.postExecutionInvulnSeconds : 1f);
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

        private Vector3 SampleGround(Vector3 from)
        {
            Vector3 o = new Vector3(from.x, from.y + 40f, from.z);
            if (Physics.Raycast(o, Vector3.down, out var hit, 300f, groundMask, QueryTriggerInteraction.Ignore))
                return hit.point;
            return new Vector3(from.x, (_cfg != null ? _cfg.arenaCenter.y : 0f) + 0.5f, from.z);
        }
    }
}
