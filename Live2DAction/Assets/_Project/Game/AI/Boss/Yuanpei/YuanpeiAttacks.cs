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

        [Header("雷擊標記 flipbook telegraph (紅圈攻擊特效.mp4 → RedCircleStrike_Flip.png)")]
        [SerializeField] private Material strikeFlipbookMaterial;   // Live2DAction/GroundStrikeURP (atlas assigned on the material)
        [SerializeField] private int strikeFlipbookCols = 6;
        [SerializeField] private int strikeFlipbookRows = 6;
        [SerializeField] private int strikeFlipbookFrames = 36;
        [Tooltip("Fraction of the frame count reached when the fire pillar lands - synced to the hazard's warn window.")]
        [SerializeField] private float strikeFlipbookImpactFraction = 0.55f;

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

            // spec §3.2 - no skeleton, so "蓄力" is a scale pulse + emission ramp on the visual root.
            Transform vis = boss != null ? boss.VisualRoot : null;
            Vector3 visBase = vis != null ? vis.localScale : Vector3.one;
            Quaternion visRotBase = vis != null ? vis.rotation : Quaternion.identity;   // restored after (charges tilt the disc)

            onPhase?.Invoke(Phase.Telegraph);
            yield return TelegraphPulse(vis, visBase, def.telegraphSeconds, 0.04f, 5f);
            onPhase?.Invoke(Phase.Windup);
            yield return TelegraphPulse(vis, visBase, def.windupSeconds, 0.09f, 9f);
            if (vis != null) vis.localScale = visBase;

            onPhase?.Invoke(Phase.Active);
            switch (def.attackId)
            {
                case YuanpeiAttackId.ProjectileBurst: yield return ProjectileBurst(def, player); break;
                case YuanpeiAttackId.FocusLaser:      yield return FocusLaser(def, player); break;
                case YuanpeiAttackId.LightningMark:   yield return LightningMark(def, player); break;
                case YuanpeiAttackId.MultiAoE:        yield return MultiAoE(def, player); break;
                case YuanpeiAttackId.Shockwave:       yield return Shockwave(def, player); break;
                case YuanpeiAttackId.BodyCharge:      yield return BodyCharge(def, player); break;
                case YuanpeiAttackId.ChargeLine:      yield return BodyCharge(def, player); break;   // same logic, longer/faster via the def's numbers
                case YuanpeiAttackId.ChargeCrush:     yield return ChargeCrush(def, player); break;
                case YuanpeiAttackId.OrbitDash:       yield return OrbitDash(def, player); break;
            }

            onPhase?.Invoke(Phase.Recovery);
            // ease the disc back to its combat orientation (charges + FaceDiscAlong tilt it) while
            // the recovery window runs, rather than adding time on top of it.
            {
                float rt = 0f, rdur = Mathf.Max(0.05f, def.recoverySeconds);
                while (rt < rdur)
                {
                    rt += Time.deltaTime;
                    if (vis != null) vis.rotation = Quaternion.Slerp(vis.rotation, visRotBase, 8f * Time.deltaTime);
                    yield return null;
                }
                if (vis != null) vis.rotation = visRotBase;
            }

            _majorHazardActive = false;
        }

        private IEnumerator TelegraphPulse(Transform vis, Vector3 baseScale, float seconds, float amp, float speed)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                if (vis != null)
                {
                    float k = 1f + Mathf.Sin(t * speed) * amp * Mathf.Clamp01(t / Mathf.Max(0.01f, seconds) + 0.3f);
                    vis.localScale = baseScale * k;
                }
                yield return null;
            }
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
                Tint(go, castColor, 3.5f);
                var trail = go.AddComponent<TrailRenderer>();
                trail.material = SimpleUnlit();
                trail.time = 0.22f;
                trail.startWidth = radius * 2.2f;
                trail.endWidth = 0f;
                trail.startColor = new Color(castColor.r, castColor.g, castColor.b, 0.9f);
                trail.endColor = new Color(burstColor.r, burstColor.g, burstColor.b, 0f);
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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
            lr.numCapVertices = 4;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // charging bead at the origin so the wind-up reads clearly (spec §17 "持續升高的鎖定音"
            // has no visual twin otherwise)
            var bead = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bead.name = "YuanpeiLaserBead";
            Destroy(bead.GetComponent<Collider>());
            bead.transform.SetParent(laserOrigin, false);
            bead.transform.localPosition = Vector3.zero;
            _spawned.Add(bead);

            Vector3 dir = (player.position + Vector3.up - laserOrigin.position).normalized;
            float t = 0f;
            // aim / tracking phase
            while (t < trackSeconds)
            {
                t += Time.deltaTime;
                Vector3 want = (player.position + Vector3.up - laserOrigin.position).normalized;
                dir = Vector3.Slerp(dir, want, 6f * Time.deltaTime).normalized;
                lr.startWidth = lr.endWidth = 0.05f + Mathf.PingPong(Time.time * 4f, 0.04f);
                lr.SetPosition(0, laserOrigin.position);
                lr.SetPosition(1, laserOrigin.position + dir * length);
                float grow = 0.2f + 0.9f * Mathf.Clamp01(t / Mathf.Max(0.01f, trackSeconds));
                bead.transform.localScale = Vector3.one * grow;
                Tint(bead, Color.Lerp(castColor, dangerColor, grow), 2f + grow * 4f);
                yield return null;
            }
            // lock (spec §9.2.3)
            yield return new WaitForSeconds(0.28f);
            if (bead != null) Destroy(bead);

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

        // RPG-style telegraphed barrage: a run of marks (default 6), each RE-LOCKED on the player's
        // position at the moment it spawns, then a warn window with the 紅圈攻擊特效 video decal on
        // the floor, then the burst. Marks are STAGGERED (a new one every `between`s) instead of
        // strictly serial, so they overlap into a "keep moving" pressure pattern.
        private IEnumerator LightningMark(YuanpeiAttackDef def, Transform player)
        {
            float radius = def.number1 > 0 ? def.number1 : 2.0f;
            float warn = def.number2 > 0 ? def.number2 : 1.4f;    // rune-circle wind-up = video warn phase
            float between = def.number3 > 0 ? def.number3 : 0.55f;
            int strikes = Mathf.Max(1, def.count);
            for (int i = 0; i < strikes; i++)
            {
                if (player == null) yield break;
                Vector3 pos = ProjectToGround(player.position);
                var go = SpawnHazard(YuanpeiHazard.Kind.StrikeCircle, pos, radius, warn, 0.25f, def.healthDamage, player);
                if (strikeFlipbookMaterial != null)
                {
                    var hz = go.GetComponent<YuanpeiHazard>();
                    if (hz != null) hz.SetFlipbook(strikeFlipbookMaterial, strikeFlipbookCols, strikeFlipbookRows,
                        strikeFlipbookFrames, strikeFlipbookImpactFraction);
                }
                yield return new WaitForSeconds(between);
            }
            yield return new WaitForSeconds(warn + 0.35f);   // let the final mark resolve before Recovery
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

            // spec §9.4 - never cover every escape route. Drop circles until at least one "walk or
            // one-dodge" spot around the player stays clear.
            var candidates = new List<YuanpeiAoePlacement.Circle>();
            foreach (var p in picks)
                candidates.Add(new YuanpeiAoePlacement.Circle { center = new Vector2(p.x, p.z), radius = radius });
            var safe = YuanpeiAoePlacement.EnsureSafeRoute(
                candidates,
                new Vector2(player.position.x, player.position.z),
                new Vector2(center.x, center.z), ar);

            foreach (var c in safe)
            {
                Vector3 gp = ProjectToGround(new Vector3(c.center.x, player.position.y, c.center.y));
                SpawnHazard(YuanpeiHazard.Kind.DelayedAoE, gp, c.radius, warn, 0.3f, def.healthDamage, player);
            }

            yield return new WaitForSeconds(warn + 0.5f);
        }

        // ---------------------------------------------------------------- 9.5 近身震退

        private IEnumerator Shockwave(YuanpeiAttackDef def, Transform player)
        {
            float maxR = def.number1 > 0 ? def.number1 : 5f;
            float speed = def.number2 > 0 ? def.number2 : 7f;
            float thick = def.number3 > 0 ? def.number3 : 0.8f;
            // the ring lives on the floor under the boss, not at hover height
            Vector3 ringPos = ProjectToGround(transform.position);
            var h = SpawnHazard(YuanpeiHazard.Kind.ExpandingRing, ringPos, maxR, 0f, maxR / speed + 0.5f,
                def.healthDamage, player);
            var hz = h.GetComponent<YuanpeiHazard>();
            hz.Configure(YuanpeiHazard.Kind.ExpandingRing, ringPos, maxR, 0f, maxR / speed + 0.5f,
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

            // 前搖：back off + tilt while a red DANGER LINE shows exactly where the charge will go
            // (spec §9.6.1-2 / §22.2 / user: "所有衝撞攻擊必須有前搖和預警範圍提示").
            Vector3 start = transform.position;
            Vector3 dir = (player.position + Vector3.up * 0.5f - start); dir.y *= 0.3f; dir.Normalize();
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            // wind-up back-off - eased over a few frames, NOT an instant `position -=` (that read as
            // the boss teleporting back at point-blank range - user: "順移的感覺").
            yield return EaseMove(transform.position - dir * 1.5f, 0.16f);
            float windup = Mathf.Max(0.45f, def.windupSeconds);
            yield return ChargePathTelegraph(transform.position, dir, maxDist, hitR, windup);

            // re-aim once at the player's LAST position, then lock (spec §9.6.4 - no turning after)
            start = transform.position;
            dir = (player.position + Vector3.up * 0.5f - start); dir.y *= 0.3f; dir.Normalize();
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            FaceDiscAlong(dir);   // 表面立直：the flat disc leads face-first for max frontal area (user request)

            bool hitPlayer = false, hitWall = false;
            float travelled = 0f;
            while (travelled < maxDist && !hitPlayer && !hitWall)
            {
                float step = speed * Mathf.Min(Time.deltaTime, 0.04f);   // clamp: a frame hitch must not teleport the boss across the arena
                FaceDiscAlong(dir);   // hold the face-forward orientation for the whole dash
                // wall check (spec §9.6 - only ChargeCrashSurface stuns)
                if (Physics.SphereCast(transform.position, hitR * 0.8f, dir, out var wall, step + hitR, chargeCrashMask, QueryTriggerInteraction.Ignore))
                {
                    hitWall = true;
                    transform.position = wall.point - dir * hitR;
                    break;
                }
                transform.position += dir * step;
                travelled += step;

                // wide flat catch area - the disc face is much bigger than the charge "tube"
                if (player != null && DiscFaceHitsPlayer(player, dir, hitR, hitR * 2.8f))
                {
                    hitPlayer = true;
                    DamagePlayer(player, def.healthDamage, dir);
                    var kb = player.GetComponent<Live2DAction.Combat.Boss.IKnockbackReceiver>();
                    kb?.ApplyKnockback(dir, 13f, false);   // bumped - the instant-pop fraction was cut (順移 fix), velocity carries the push now
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

        // ------------------------------------------------- 肉身衝撞 2：頭頂垂直下壓（命中＝秒殺）

        private IEnumerator ChargeCrush(YuanpeiAttackDef def, Transform player)
        {
            float overhead = def.number1 > 0 ? def.number1 : 12f;   // how high above the player to line up
            float slamSpeed = def.number2 > 0 ? def.number2 : 42f;
            float hitR = def.number3 > 0 ? def.number3 : 2.4f;

            // ground shadow marker - it TRACKS the player while the boss slides overhead, then LOCKS.
            var marker = MakeGroundMarker(ProjectToGround(player.position), hitR);
            var markerR = marker.GetComponentInChildren<Renderer>();

            // --- slide to directly above the player (breaks the "never overhead" rule for this move) ---
            float slide = Mathf.Max(0.35f, def.telegraphSeconds + def.windupSeconds + 0.4f);
            if (_boss != null) _boss.SuspendYClamp(slide + 2f);   // let it fly high for this move

            float t = 0f;
            while (t < slide)
            {
                t += Time.deltaTime;
                float k = t / slide;
                Vector3 above = player.position + Vector3.up * overhead;
                transform.position = Vector3.Lerp(transform.position, above, 6f * Time.deltaTime);
                Vector3 g = ProjectToGround(player.position);
                marker.transform.position = g;
                float pulse = 1f + Mathf.Sin(Time.time * 14f) * 0.15f;
                marker.transform.localScale = new Vector3(hitR * 2f * pulse, 0.02f, hitR * 2f * pulse);
                PaintMarker(markerR, Color.Lerp(warnColor, dangerColor, k), 0.5f + k * 3f);
                yield return null;
            }

            // --- LOCK the target under the boss (spec §9.6.4 - lock last position) ---
            Vector3 targetXZ = new Vector3(transform.position.x, 0f, transform.position.z);
            Vector3 lockGround = ProjectToGround(new Vector3(targetXZ.x, player.position.y, targetXZ.z));
            marker.transform.position = lockGround;
            marker.transform.localScale = new Vector3(hitR * 2f, 0.02f, hitR * 2f);
            PaintMarker(markerR, dangerColor, 5f);
            yield return new WaitForSeconds(0.25f);   // brief "it's coming" beat - dodge window

            // --- vertical slam --- disc lies flat (face DOWN) to pancake the biggest area
            FaceDiscAlong(Vector3.down);
            float floorY = lockGround.y;
            bool crushed = false;
            while (transform.position.y > floorY + 0.6f)
            {
                FaceDiscAlong(Vector3.down);
                transform.position += Vector3.down * slamSpeed * Time.deltaTime;
                if (!crushed && player != null)
                {
                    Vector3 flat = new Vector3(player.position.x - transform.position.x, 0f, player.position.z - transform.position.z);
                    if (flat.sqrMagnitude <= (hitR * 1.4f) * (hitR * 1.4f) && player.position.y < transform.position.y + 2f)
                    {
                        crushed = true;
                        // 100% 秒殺 (user request) - route a lethal hit through the normal pipeline
                        DamagePlayer(player, 999999f, Vector3.down);
                    }
                }
                yield return null;
            }
            transform.position = new Vector3(transform.position.x, floorY + 0.6f, transform.position.z);

            // impact - ground shockwave visual + camera shake (no extra damage)
            Live2DAction.Combat.HitStopController.Request(0.06f, 0.15f);
            var ring = SpawnHazard(YuanpeiHazard.Kind.ExpandingRing, lockGround, 4f, 0f, 0.6f, 0f, player);
            ring.GetComponent<YuanpeiHazard>().Configure(YuanpeiHazard.Kind.ExpandingRing, lockGround, 4f, 0f, 0.6f,
                0f, player, _boss.gameObject, warnColor, burstColor, 14f, 0.6f);
            if (marker != null) Destroy(marker);

            yield return new WaitForSeconds(0.5f);   // grounded beat - the player can punish here
        }

        // ------------------------------------------------- 肉身衝撞 3：繞圈後突然直衝

        private IEnumerator OrbitDash(YuanpeiAttackDef def, Transform player)
        {
            float orbitRadius = def.number1 > 0 ? def.number1 : 8f;
            float dashSpeed = def.number2 > 0 ? def.number2 : 34f;
            float hitR = def.number3 > 0 ? def.number3 : 1.9f;

            float orbitDur = 1.0f + (float)UnityEngine.Random.value * 1.6f;   // random - "某一個瞬間"
            float dashAt = 0.45f + (float)UnityEngine.Random.value * (orbitDur - 0.45f);
            float angle = Mathf.Atan2(transform.position.z - player.position.z, transform.position.x - player.position.x);
            float angSpeed = (UnityEngine.Random.value < 0.5f ? -1f : 1f) * (2.2f + (float)UnityEngine.Random.value * 1.4f);

            float floorY = ProjectToGround(player.position).y;
            float t = 0f;
            bool dashed = false;
            Vector3 dashDir = Vector3.forward;
            float travelled = 0f;

            while (t < orbitDur || (dashed && travelled < 16f))
            {
                t += Time.deltaTime;

                if (!dashed)
                {
                    angle += angSpeed * Time.deltaTime;
                    Vector3 want = player.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * orbitRadius;
                    want.y = floorY + 2.4f;
                    transform.position = Vector3.Lerp(transform.position, want, 10f * Time.deltaTime);
                    transform.rotation = Quaternion.LookRotation((player.position - transform.position).normalized, Vector3.up);
                    if (_boss != null && _boss.VisualRoot != null)
                        _boss.VisualRoot.Rotate(0f, 0f, Mathf.Sin(Time.time * 22f) * 4f * Time.deltaTime, Space.Self);

                    if (t >= dashAt)   // GO - lock direction, no turning after (spec §9.6)
                    {
                        dashed = true;
                        dashDir = (player.position + Vector3.up * 0.5f - transform.position); dashDir.y *= 0.25f; dashDir.Normalize();
                        transform.rotation = Quaternion.LookRotation(dashDir, Vector3.up);
                        Live2DAction.Combat.HitStopController.Request(0.04f, 0.35f);   // "!" beat
                        // 前搖 + 預警：hold on the orbit ring while the danger line telegraphs the
                        // dash path, so the player gets real reaction time (user request).
                        yield return ChargePathTelegraph(transform.position, dashDir, 16f, hitR, 0.5f);
                        FaceDiscSideAlong(dashDir);   // 側身衝刺 - rim leads, not the flat face
                    }
                }
                else
                {
                    FaceDiscSideAlong(dashDir);
                    float step = dashSpeed * Mathf.Min(Time.deltaTime, 0.04f);   // clamp so a frame hitch can't teleport the dash
                    if (Physics.SphereCast(transform.position, hitR * 0.8f, dashDir, out var wall, step + hitR, chargeCrashMask, QueryTriggerInteraction.Ignore))
                    {
                        transform.position = wall.point - dashDir * hitR;
                        if (_boss != null && _boss.Vitals != null && _cfg != null)
                            _boss.Vitals.AddPosture(_cfg.maxPosture * _cfg.chargeCrashPostureFraction);
                        yield return new WaitForSeconds(2.5f);
                        yield break;
                    }
                    transform.position += dashDir * step;
                    travelled += step;
                    if (player != null && DiscFaceHitsPlayer(player, dashDir, hitR, hitR * 2.8f))
                    {
                        DamagePlayer(player, def.healthDamage, dashDir);
                        var kb = player.GetComponent<Live2DAction.Combat.Boss.IKnockbackReceiver>();
                        kb?.ApplyKnockback(dashDir, 15f, false);   // bumped - see BodyCharge note
                        break;
                    }
                }
                yield return null;
            }

            yield return new WaitForSeconds(0.6f);
        }

        // Smoothstep the boss from its current position to `to` over `seconds` - used instead of a
        // bare `transform.position -= ...` so short repositions (charge wind-up back-off) don't
        // read as a teleport at point-blank range.
        private IEnumerator EaseMove(Vector3 to, float seconds)
        {
            Vector3 from = transform.position;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / seconds));
                transform.position = Vector3.Lerp(from, to, k);
                yield return null;
            }
            transform.position = to;
        }

        // A ground danger LANE showing exactly where a charge will pass. warn -> danger colour +
        // width pulse over `seconds`, then auto-fades. Used by every charge move's 前搖 window.
        private IEnumerator ChargePathTelegraph(Vector3 origin, Vector3 dir, float length, float halfWidth, float seconds)
        {
            Vector3 flat = new Vector3(dir.x, 0f, dir.z);
            if (flat.sqrMagnitude < 1e-4f) flat = Vector3.forward;
            flat.Normalize();

            Vector3 startG = ProjectToGround(origin);
            var lane = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lane.name = "YuanpeiChargeLane";
            Destroy(lane.GetComponent<Collider>());
            lane.transform.position = startG + flat * (length * 0.5f) + Vector3.up * 0.03f;
            lane.transform.rotation = Quaternion.LookRotation(flat, Vector3.up);
            var laneR = lane.GetComponent<Renderer>();
            laneR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _spawned.Add(lane);

            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float k = t / seconds;
                float w = halfWidth * 2f * (0.35f + 0.65f * k) * (1f + Mathf.Sin(Time.time * 18f) * 0.12f);
                lane.transform.localScale = new Vector3(w, 0.03f, length);
                PaintMarker(laneR, Color.Lerp(warnColor, dangerColor, k), 0.6f + k * 3.5f);
                yield return null;
            }
            Destroy(lane);
        }

        // Rotate the VisualRoot so the flat logo disc leads face-first along `dir`. The disc's
        // face normal is the mesh's thin axis = VisualRoot LOCAL Y (bounds (0.019, 0.001, 0.019)),
        // so LookRotation(_, dir) puts VisualRoot.up (= disc normal) on `dir`. (user: "衝撞時一定
        //要先把表面立直, 這樣才有更多面積打中玩家")
        private void FaceDiscAlong(Vector3 dir)
        {
            if (_boss == null || _boss.VisualRoot == null) return;
            Vector3 d = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward;
            Vector3 fwd = Vector3.Cross(d, Vector3.up);
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.Cross(d, Vector3.right);   // dir was vertical
            _boss.VisualRoot.rotation = Quaternion.LookRotation(fwd.normalized, d);
        }

        // Rotate the VisualRoot so the disc dashes EDGE-first (side profile) along `dir` - the disc
        // stands vertical like a rolling coin, flat faces pointing left/right of travel. Used by
        // OrbitDash (user: "衝刺時改為用 boss 的側身，而不是正面").
        private void FaceDiscSideAlong(Vector3 dir)
        {
            if (_boss == null || _boss.VisualRoot == null) return;
            Vector3 d = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward;
            Vector3 side = Vector3.Cross(d, Vector3.up);            // horizontal, 90° to travel
            if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
            // local +Y (disc face normal) -> side; local +Z -> travel dir => rim leads, faces sideways
            _boss.VisualRoot.rotation = Quaternion.LookRotation(d, side.normalized);
        }

        // Wide flat "disc face" hit: player within `forwardReach` ahead of the boss along `dir`
        // AND within `faceRadius` of the charge axis (the disc is much wider than the tube).
        private bool DiscFaceHitsPlayer(Transform player, Vector3 dir, float forwardReach, float faceRadius)
        {
            if (player == null) return false;
            Vector3 toP = (player.position + Vector3.up) - transform.position;
            float along = Vector3.Dot(toP, dir);
            if (along < -forwardReach || along > forwardReach * 1.6f) return false;
            Vector3 perp = toP - dir * along;
            return perp.sqrMagnitude <= faceRadius * faceRadius;
        }

        private GameObject MakeGroundMarker(Vector3 pos, float radius)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = "YuanpeiCrushMarker";
            Destroy(g.GetComponent<Collider>());
            g.transform.position = pos;
            g.transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);
            g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _spawned.Add(g);
            return g;
        }

        private void PaintMarker(Renderer r, Color c, float emi)
        {
            if (r == null) return;
            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorId, new Color(c.r, c.g, c.b, 0.55f));
            mpb.SetColor(EmissionId, c * emi);
            r.SetPropertyBlock(mpb);
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

        // Ground telegraphs must sit ON the floor. A plain downward Raycast from over the player's
        // XZ hits the PLAYER's own CharacterController (layer 0, same as the ground) first, ~1m up -
        // that was the "提示區塊懸浮在半空" bug. RaycastAll + skip the player / the boss / other
        // runtime hazards, take the first real surface.
        private Vector3 ProjectToGround(Vector3 p)
        {
            Vector3 origin = new Vector3(p.x, p.y + 30f, p.z);
            var hits = Physics.RaycastAll(origin, Vector3.down, 220f, groundMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                var col = hits[i].collider;
                if (col == null) continue;
                if (col.GetComponentInParent<Live2DAction.Input.PlayerInputProvider>() != null) continue;
                if (col.GetComponentInParent<CharacterController>() != null) continue;
                if (_boss != null && col.transform.root == _boss.transform.root) continue;
                if (col.GetComponentInParent<YuanpeiHazard>() != null || col.GetComponentInParent<YuanpeiProjectile>() != null) continue;
                return new Vector3(p.x, hits[i].point.y + 0.02f, p.z);
            }
            return new Vector3(p.x, (_cfg != null ? _cfg.arenaCenter.y : 0f) + 0.02f, p.z);
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
