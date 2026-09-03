using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Live2DAction.Core;

namespace Live2DAction.AI.Boss.Yuanpei
{
    // Runs each of the 6 attack timelines (spec §9). Greybox VFX: primitives + emission colour
    // (spec §21 phase 3 "先使用簡單幾何與純色特效"). Damage is always gated by an explicit Hit
    // Window, never "as long as the FX exists" (spec §9 intro).
    public class YuanpeiAttacks : MonoBehaviour
    {
        public enum Phase { Telegraph, Windup, Active, Recovery }

        [SerializeField] private Transform projectileOrigin;
        [SerializeField] private Transform laserOrigin;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private LayerMask chargeCrashMask;   // ChargeCrashSurface layer (spec §9.6)

        [Header("Greybox colours")]
        [SerializeField] private Color castColor = new Color(1f, 0.9f, 0.4f);
        [SerializeField] private Color warnColor = new Color(1f, 0.55f, 0.15f, 0.5f);
        [SerializeField] private Color burstColor = new Color(1f, 0.35f, 0.15f);
        [SerializeField] private Color dangerColor = new Color(1f, 0.15f, 0.1f);

        private YuanpeiBoss _boss;
        private YuanpeiBossConfig _cfg;
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private bool _majorHazardActive;
        private Transform _core;   // visual core for emission tint

        public bool MajorHazardActive => _majorHazardActive;

        private void Awake()
        {
            _boss = GetComponent<YuanpeiBoss>();
            if (projectileOrigin == null) projectileOrigin = transform;
            if (laserOrigin == null) laserOrigin = transform;
        }

        public void CancelAll()
        {
            for (int i = _spawned.Count - 1; i >= 0; i--)
                if (_spawned[i] != null) Destroy(_spawned[i]);
            _spawned.Clear();
            _majorHazardActive = false;
            StopAllCoroutines();
        }

        public IEnumerator Run(YuanpeiAttackDef def, Transform player, YuanpeiBoss boss, Action<Phase> onPhase)
        {
            _boss = boss;
            _cfg = boss != null ? boss.Config : null;
            if (def == null || player == null) yield break;

            _majorHazardActive = def.isMajorHazard;

            onPhase?.Invoke(Phase.Telegraph);
            yield return new WaitForSeconds(def.telegraphSeconds);
            onPhase?.Invoke(Phase.Windup);
            yield return new WaitForSeconds(def.windupSeconds);

            onPhase?.Invoke(Phase.Active);
            switch (def.attackId)
            {
                case YuanpeiAttackId.ProjectileBurst: yield return ProjectileBurst(def, player); break;
                case YuanpeiAttackId.FocusLaser:      yield return FocusLaser(def, player); break;
                case YuanpeiAttackId.LightningMark:   yield return LightningMark(def, player); break;
                case YuanpeiAttackId.MultiAoE:        yield return MultiAoE(def, player); break;
                case YuanpeiAttackId.Shockwave:       yield return Shockwave(def, player); break;
                case YuanpeiAttackId.BodyCharge:      yield return BodyCharge(def, player); break;
            }

            onPhase?.Invoke(Phase.Recovery);
            yield return new WaitForSeconds(def.recoverySeconds);

            _majorHazardActive = false;
        }

        // ---------------------------------------------------------------- 9.1 光粒子三連射

        private IEnumerator ProjectileBurst(YuanpeiAttackDef def, Transform player)
        {
            float speed = def.number1 > 0 ? def.number1 : 16f;
            float radius = def.number2 > 0 ? def.number2 : 0.35f;
            float homing = def.number3 > 0 ? def.number3 : 3f;
            int shots = Mathf.Max(1, def.count);
            for (int i = 0; i < shots; i++)
            {
                Vector3 origin = projectileOrigin.position;
                Vector3 aim = i == shots - 1
                    ? PredictedPlayerPoint(player, 0.3f)
                    : player.position + Vector3.up * 1.0f;
                Vector3 dir = (aim - origin).normalized;

                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "YuanpeiLightOrb";
                Destroy(go.GetComponent<Collider>());
                var sc = go.AddComponent<SphereCollider>();
                sc.radius = 0.5f; sc.isTrigger = true;      // so player weapon overlap catches it
                go.transform.position = origin;
                go.transform.localScale = Vector3.one * (radius * 2.6f);
                Tint(go, castColor, 2f);
                var proj = go.AddComponent<YuanpeiProjectile>();
                float homeTime = i == 0 ? 0.0f : 0.18f;
                proj.Launch(dir, speed, radius, def.healthDamage, homeTime, homing, player, _boss.gameObject);
                _spawned.Add(go);

                yield return new WaitForSeconds(i == 0 ? 0f : (i == 1 ? 0.45f : 0.2f));
            }
            yield return null;
        }

        // ---------------------------------------------------------------- 9.2 聚焦雷射

        private IEnumerator FocusLaser(YuanpeiAttackDef def, Transform player)
        {
            float length = def.number1 > 0 ? def.number1 : 30f;
            float radius = def.number2 > 0 ? def.number2 : 0.6f;
            float tick = def.number3 > 0 ? def.number3 : 0.2f;
            float trackSeconds = Mathf.Max(0f, def.activeSeconds - 0.9f);
            float beamSeconds = Mathf.Min(def.activeSeconds, 1.0f);

            var beamGo = new GameObject("YuanpeiLaser");
            _spawned.Add(beamGo);
            var lr = beamGo.AddComponent<LineRenderer>();
            lr.material = SimpleUnlit();
            lr.startWidth = lr.endWidth = 0.05f;
            lr.positionCount = 2;
            lr.startColor = lr.endColor = castColor;

            Vector3 dir = (player.position + Vector3.up - laserOrigin.position).normalized;
            float t = 0f;
            // aim / tracking phase
            while (t < trackSeconds)
            {
                t += Time.deltaTime;
                Vector3 want = (player.position + Vector3.up - laserOrigin.position).normalized;
                dir = Vector3.Slerp(dir, want, 6f * Time.deltaTime).normalized;
                lr.startWidth = lr.endWidth = 0.04f + Mathf.PingPong(Time.time * 4f, 0.03f);
                lr.SetPosition(0, laserOrigin.position);
                lr.SetPosition(1, laserOrigin.position + dir * length);
                yield return null;
            }
            // lock (spec §9.2.3)
            yield return new WaitForSeconds(0.28f);

            // fire
            lr.startWidth = lr.endWidth = radius * 2f;
            lr.startColor = lr.endColor = dangerColor;
            float fired = 0f, tickT = 0f;
            while (fired < beamSeconds)
            {
                fired += Time.deltaTime; tickT += Time.deltaTime;
                lr.SetPosition(0, laserOrigin.position);
                lr.SetPosition(1, laserOrigin.position + dir * length);
                if (tickT >= tick)
                {
                    tickT = 0f;
                    if (RayHitsPlayer(laserOrigin.position, dir, length, radius, player))
                        DamagePlayer(player, def.healthDamage, dir);
                }
                yield return null;
            }
            if (beamGo != null) Destroy(beamGo);
        }

        // ---------------------------------------------------------------- 9.3 雷擊標記

        private IEnumerator LightningMark(YuanpeiAttackDef def, Transform player)
        {
            float radius = def.number1 > 0 ? def.number1 : 1.4f;
            float warn = def.number2 > 0 ? def.number2 : 0.95f;
            float between = def.number3 > 0 ? def.number3 : 0.5f;
            int strikes = Mathf.Max(1, def.count);
            for (int i = 0; i < strikes; i++)
            {
                Vector3 pos = ProjectToGround(player.position);
                SpawnHazard(YuanpeiHazard.Kind.StrikeCircle, pos, radius, warn, 0.25f, def.healthDamage, player);
                yield return new WaitForSeconds(warn + between);
            }
        }

        // ---------------------------------------------------------------- 9.4 多重延遲範圍光爆

        private IEnumerator MultiAoE(YuanpeiAttackDef def, Transform player)
        {
            float radius = def.number1 > 0 ? def.number1 : 1.45f;
            float warn = def.number2 > 0 ? def.number2 : 1.2f;
            int circles = Mathf.Clamp(def.count, 5, 8);

            var picks = new List<Vector3>();
            picks.Add(ProjectToGround(player.position + RandXZ(1.5f)));
            picks.Add(ProjectToGround(player.position + RandXZ(2.5f)));
            Vector3 predict = PredictedPlayerPoint(player, 0.6f);
            picks.Add(ProjectToGround(predict + RandXZ(1.5f)));
            picks.Add(ProjectToGround(predict + RandXZ(2.5f)));
            Vector3 center = _cfg != null ? _cfg.arenaCenter : transform.position;
            float ar = _cfg != null ? _cfg.arenaRadius : 10f;
            for (int i = picks.Count; i < circles; i++)
            {
                float ang = (i / (float)circles) * Mathf.PI * 2f;
                picks.Add(ProjectToGround(center + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * ar * 0.7f));
            }

            foreach (var p in picks)
                SpawnHazard(YuanpeiHazard.Kind.DelayedAoE, p, radius, warn, 0.3f, def.healthDamage, player);

            yield return new WaitForSeconds(warn + 0.5f);
        }

        // ---------------------------------------------------------------- 9.5 近身震退

        private IEnumerator Shockwave(YuanpeiAttackDef def, Transform player)
        {
            float maxR = def.number1 > 0 ? def.number1 : 5f;
            float speed = def.number2 > 0 ? def.number2 : 7f;
            float thick = def.number3 > 0 ? def.number3 : 0.8f;
            var h = SpawnHazard(YuanpeiHazard.Kind.ExpandingRing, transform.position, maxR, 0f, maxR / speed + 0.5f,
                def.healthDamage, player);
            var hz = h.GetComponent<YuanpeiHazard>();
            hz.Configure(YuanpeiHazard.Kind.ExpandingRing, transform.position, maxR, 0f, maxR / speed + 0.5f,
                def.healthDamage, player, _boss.gameObject, warnColor, burstColor, speed, thick);
            // knockback on hit is handled by the hazard's damage; add a shove via KnockbackReceiver if present
            yield return new WaitForSeconds(maxR / speed + 0.3f);
        }

        // ---------------------------------------------------------------- 9.6 肉身衝撞

        private IEnumerator BodyCharge(YuanpeiAttackDef def, Transform player)
        {
            float speed = def.number1 > 0 ? def.number1 : 26f;
            float maxDist = def.number2 > 0 ? def.number2 : 15f;
            float hitR = def.number3 > 0 ? def.number3 : 1.8f;

            // slight back-off + tilt (spec §9.6.1-2). Then lock (spec §9.6.4).
            yield return new WaitForSeconds(0.2f);
            Vector3 start = transform.position;
            Vector3 dir = (player.position + Vector3.up * 0.5f - start); dir.y *= 0.3f; dir.Normalize();
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

            bool hitPlayer = false, hitWall = false;
            float travelled = 0f;
            while (travelled < maxDist && !hitPlayer && !hitWall)
            {
                float step = speed * Time.deltaTime;
                // wall check (spec §9.6 - only ChargeCrashSurface stuns)
                if (Physics.SphereCast(transform.position, hitR * 0.8f, dir, out var wall, step + hitR, chargeCrashMask, QueryTriggerInteraction.Ignore))
                {
                    hitWall = true;
                    transform.position = wall.point - dir * hitR;
                    break;
                }
                transform.position += dir * step;
                travelled += step;

                if (player != null && (transform.position - (player.position + Vector3.up)).sqrMagnitude <= hitR * hitR)
                {
                    hitPlayer = true;
                    DamagePlayer(player, def.healthDamage, dir);
                    var kb = player.GetComponent<Live2DAction.Combat.Boss.IKnockbackReceiver>();
                    kb?.ApplyKnockback(dir, 10f, false);
                }
            }

            if (hitWall)
            {
                // spec §9.6 - terminate, stun ~2.5s, big self-posture
                if (_boss != null && _boss.Vitals != null && _cfg != null)
                    _boss.Vitals.AddPosture(_cfg.maxPosture * _cfg.chargeCrashPostureFraction);
                float stun = 2.5f;
                float e = 0f;
                while (e < stun)
                {
                    e += Time.deltaTime;
                    if (_boss != null && _boss.VisualRoot != null)
                        _boss.VisualRoot.Rotate(0f, 0f, Mathf.Sin(e * 20f) * 6f * Time.deltaTime, Space.Self);
                    yield return null;
                }
            }
            else if (!hitPlayer)
            {
                yield return new WaitForSeconds(1.2f);
            }
        }

        // ---------------------------------------------------------------- helpers

        private GameObject SpawnHazard(YuanpeiHazard.Kind kind, Vector3 pos, float radius, float warn,
            float active, float dmg, Transform player)
        {
            var go = new GameObject("YuanpeiHazard");
            var hz = go.AddComponent<YuanpeiHazard>();
            hz.Configure(kind, pos, radius, warn, active, dmg, player, _boss.gameObject, warnColor, burstColor);
            _spawned.Add(go);
            return go;
        }

        private Vector3 PredictedPlayerPoint(Transform player, float lead)
        {
            var cm = player.GetComponent<Rigidbody>();
            Vector3 vel = cm != null ? cm.linearVelocity : (player.position - _lastPP) / Mathf.Max(1e-4f, Time.deltaTime);
            _lastPP = player.position;
            return player.position + Vector3.up * 1.0f + new Vector3(vel.x, 0, vel.z) * lead;
        }
        private Vector3 _lastPP;

        private Vector3 ProjectToGround(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 30f, Vector3.down, out var hit, 200f, groundMask, QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * 0.02f;
            return new Vector3(p.x, (_cfg != null ? _cfg.arenaCenter.y : 0f) + 0.52f, p.z);
        }

        private Vector3 RandXZ(float r)
        {
            var c = UnityEngine.Random.insideUnitCircle * r;
            return new Vector3(c.x, 0f, c.y);
        }

        private bool RayHitsPlayer(Vector3 origin, Vector3 dir, float length, float radius, Transform player)
        {
            if (player == null) return false;
            Vector3 toP = (player.position + Vector3.up) - origin;
            float along = Vector3.Dot(toP, dir);
            if (along < 0f || along > length) return false;
            Vector3 closest = origin + dir * along;
            return (closest - (player.position + Vector3.up)).sqrMagnitude <= (radius + 0.4f) * (radius + 0.4f);
        }

        private void DamagePlayer(Transform player, float dmg, Vector3 dir)
        {
            var d = player.GetComponentInChildren<IDamageable>() ?? player.GetComponent<IDamageable>();
            d?.ApplyDamage(new DamageInfo(dmg, player.position, dir, _boss != null ? _boss.gameObject : gameObject));
        }

        private static Material _unlit;
        private static Material SimpleUnlit()
        {
            if (_unlit == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                _unlit = new Material(sh);
            }
            return _unlit;
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private void Tint(GameObject go, Color c, float emi)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorId, c);
            mpb.SetColor(EmissionId, c * emi);
            r.SetPropertyBlock(mpb);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
