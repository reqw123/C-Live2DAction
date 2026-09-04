using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Live2DAction.Core;
using Live2DAction.Characters;

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

        // ChargeCrush void-punt: components we turned off on the player, restored even if the
        // punt coroutine is stopped mid-flight (see RestorePuntedPlayer / CancelAll).
        private CharacterController _puntCC;
        private Behaviour _puntMove;
        private bool _puntCCWas, _puntMoveWas;

        public bool MajorHazardActive => _majorHazardActive;

        private void Awake()
        {
            _boss = GetComponent<YuanpeiBoss>();
            if (projectileOrigin == null) projectileOrigin = transform;
            if (laserOrigin == null) laserOrigin = transform;
        }

        public void CancelAll()
        {
            RestorePuntedPlayer();
            RestoreCrushCam();
            if (_chargeLane != null) { Destroy(_chargeLane); _chargeLane = null; }
            for (int i = _spawned.Count - 1; i >= 0; i--)
                if (_spawned[i] != null) Destroy(_spawned[i]);
            _spawned.Clear();
            _majorHazardActive = false;
            StopAllCoroutines();
        }

        private void RestorePuntedPlayer()
        {
            if (_puntCC != null) { _puntCC.enabled = _puntCCWas; _puntCC = null; }
            if (_puntMove != null) { _puntMove.enabled = _puntMoveWas; _puntMove = null; }
        }

        // ChargeCrush clean hit: the disc PRESSES the player straight down through the floor and
        // they sink into the void, THEN the 秒殺 lands (user: not "flung sideways", it should read
        // as "pressed down into the underground void"). Safe because YuanpeiEncounter.Defeat()
        // teleports the dead player back to the road ~5.6s later and the death screen covers the fall.
        private IEnumerator VoidPunt(Transform player, Vector3 groundPoint)
        {
            _puntCC = player.GetComponent<CharacterController>();
            _puntMove = player.GetComponent<CharacterMovement>();
            _puntCCWas = _puntCC != null && _puntCC.enabled;
            _puntMoveWas = _puntMove != null && _puntMove.enabled;
            if (_puntCC != null) _puntCC.enabled = false;
            if (_puntMove != null) _puntMove.enabled = false;

            float groundY = groundPoint.y;
            Vector3 pStart = player.position;
            Vector3 hole = new Vector3(groundPoint.x, groundY, groundPoint.z);

            // a dark "void hole" in the floor the player is driven into - so the fall reads from any
            // camera angle even though an opaque floor occludes anything below it (續 130, user:
            // "有時看不到玩家掉落虛空").
            var voidHole = MakeVoidHole(hole);

            // --- press: the disc crushes the player flat AT the surface (still visible), disc lands ---
            const float press = 0.36f;
            float bossY0 = transform.position.y;
            float t = 0f;
            while (t < press)
            {
                t += Time.deltaTime;
                float k = t / press;
                float py = Mathf.Lerp(pStart.y, groundY + 0.12f, k * k);
                player.position = new Vector3(pStart.x, py, pStart.z);
                float by = Mathf.Lerp(bossY0, groundY + 0.8f, Mathf.SmoothStep(0f, 1f, k));
                transform.position = new Vector3(transform.position.x, Mathf.Max(by, player.position.y + 1.4f), transform.position.z);
                FaceDiscAlong(Vector3.down);
                GrowVoidHole(voidHole, k * 0.6f);
                yield return null;
            }
            transform.position = new Vector3(transform.position.x, groundY + 0.8f, transform.position.z);

            // --- brief pin, then the void SWALLOWS them (fast accelerating plunge) ---
            yield return new WaitForSeconds(0.14f);
            const float sink = 0.42f;
            float sy0 = player.position.y;
            t = 0f;
            while (t < sink)
            {
                t += Time.deltaTime;
                float k = t / sink;
                player.position = new Vector3(pStart.x, sy0 - 46f * (k * k), pStart.z);
                GrowVoidHole(voidHole, 0.6f + k * 0.4f);
                yield return null;
            }

            if (voidHole != null) StartCoroutine(FadeVoidHole(voidHole, 0.5f));
            RestorePuntedPlayer();
        }

        private GameObject MakeVoidHole(Vector3 groundPos)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
            g.name = "YuanpeiVoidHole";
            Destroy(g.GetComponent<Collider>());
            g.transform.SetPositionAndRotation(groundPos + Vector3.up * 0.05f, Quaternion.Euler(90f, 0f, 0f));
            g.transform.localScale = Vector3.one * 0.2f;
            var r = g.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor(BaseColorId, new Color(0.02f, 0f, 0.05f, 1f));
            mpb.SetColor(EmissionId, new Color(0.06f, 0.02f, 0.12f));   // faint violet edge glow
            r.SetPropertyBlock(mpb);
            _spawned.Add(g);
            return g;
        }
        private void GrowVoidHole(GameObject h, float frac01)
        {
            if (h == null) return;
            float d = Mathf.Lerp(0.2f, 5.5f, Mathf.Clamp01(frac01));
            h.transform.localScale = new Vector3(d, d, 1f);
        }
        private IEnumerator FadeVoidHole(GameObject h, float seconds)
        {
            if (h == null) yield break;
            var r = h.GetComponent<Renderer>();
            float t = 0f;
            while (t < seconds && h != null)
            {
                t += Time.deltaTime;
                float d = Mathf.Lerp(5.5f, 0f, t / seconds);
                h.transform.localScale = new Vector3(d, d, 1f);
                yield return null;
            }
            if (h != null) Destroy(h);
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
            float speed = def.number1 > 0 ? def.number1 : 27f;
            float radius = def.number2 > 0 ? def.number2 : 0.5f;
            int shots = Mathf.Max(1, def.count);
            int half = Mathf.CeilToInt(shots * 0.5f);   // fired as two tight bursts with a short gap between

            // 續 125 (user: "缺少 boss 要施放此招的專有辨識手段") - a charge-up unique to this move:
            // `shots` light motes spiral inward and pack into a bright core at the muzzle before the
            // volley. Reads as "projectiles are being formed", unlike any other attack's telegraph.
            yield return MuzzleCharge(shots, radius, 0.55f);

            for (int i = 0; i < shots; i++)
            {
                Vector3 origin = projectileOrigin.position;
                // user: every shot locks the player's CURRENT centre-of-mass at fire time - no lead,
                // no prediction. Re-read per shot (the loop already spaces them out over ~1s).
                Vector3 aim = PlayerCenter(player);
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
                // locked aimed shot - no homing (the direction is already the player's current centre)
                proj.Launch(dir, speed, radius, def.healthDamage, 0f, 0f, player, _boss.gameObject);
                _spawned.Add(go);

                // Two tight bursts (3+3 for count 6) - a single Shift dodge (~0.5s i-frames) can
                // clear one burst but not both, so you have to time each dodge (user: "很容易透過
                // shift 躲避 沒有難度"). Each orb re-locks the player's centre at fire time.
                bool lastOfBurst = (i + 1) == half || (i + 1) == shots;
                yield return new WaitForSeconds(i == 0 ? 0f : (lastOfBurst ? 0.34f : 0.09f));
            }
            yield return null;
        }

        // ProjectileBurst's signature telegraph: `count` motes spiral inward to a bright core at
        // the muzzle. Self-cleaning.
        private IEnumerator MuzzleCharge(int count, float orbRadius, float seconds)
        {
            var root = new GameObject("YuanpeiMuzzleCharge");
            root.transform.SetParent(projectileOrigin, true);
            root.transform.position = projectileOrigin.position;
            _spawned.Add(root);

            var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(core.GetComponent<Collider>());
            core.transform.SetParent(root.transform, false);
            var coreR = core.GetComponent<Renderer>();
            coreR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            int n = Mathf.Clamp(count, 3, 10);
            var motes = new Transform[n];
            var ang0 = new float[n];
            for (int i = 0; i < n; i++)
            {
                var m = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(m.GetComponent<Collider>());
                m.transform.SetParent(root.transform, false);
                m.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Tint(m.gameObject, castColor, 4f);
                motes[i] = m.transform;
                ang0[i] = (i / (float)n) * Mathf.PI * 2f;
            }

            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / seconds);
                float r = Mathf.Lerp(2.4f, 0.05f, k * k);
                for (int i = 0; i < n; i++)
                {
                    float a = ang0[i] + k * 9f;   // spiral in
                    motes[i].localPosition = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a * 1.3f) * r * 0.4f, Mathf.Sin(a) * r);
                    motes[i].localScale = Vector3.one * (orbRadius * 1.2f * (0.4f + 0.6f * (1f - k)));
                }
                core.transform.localScale = Vector3.one * (orbRadius * (0.3f + 3.2f * k));
                Tint(core.gameObject, Color.Lerp(castColor, dangerColor, k), 3f + 8f * k);
                yield return null;
            }
            Destroy(root);
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
            float radius = def.number1 > 0 ? def.number1 : 2.4f;
            float warn = def.number2 > 0 ? def.number2 : 1.1f;    // rune-circle wind-up = video warn phase
            float between = def.number3 > 0 ? def.number3 : 0.4f;
            int strikes = Mathf.Max(1, def.count);
            for (int i = 0; i < strikes; i++)
            {
                if (player == null) yield break;
                Vector3 pos = ProjectToGround(player.position);
                var go = SpawnHazard(YuanpeiHazard.Kind.StrikeCircle, pos, radius, warn, 0.25f, def.healthDamage, player);
                var hz = go.GetComponent<YuanpeiHazard>();
                // chase the player for the first ~55% of the warn, then lock (user: "太容易閃躲")
                if (hz != null) hz.SetHoming(warn * 0.55f, 4f, groundMask);
                if (strikeFlipbookMaterial != null && hz != null)
                    hz.SetFlipbook(strikeFlipbookMaterial, strikeFlipbookCols, strikeFlipbookRows,
                        strikeFlipbookFrames, strikeFlipbookImpactFraction);
                yield return new WaitForSeconds(between);
            }
            yield return new WaitForSeconds(warn + 0.35f);   // let the final mark resolve before Recovery
        }

        // 9.4 多重延遲範圍光爆 (MultiAoE) removed 追加94 續 119 - user: low player pressure, cut it
        // from the pool. YuanpeiAoePlacement + its tests deleted with it. Enum value kept (harmless
        // label); the switch above has no arm for it so a stray selection is a no-op.

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
            float speed = def.number1 > 0 ? def.number1 : 18f;
            float maxDist = def.number2 > 0 ? def.number2 : 15f;
            float hitR = def.number3 > 0 ? def.number3 : 1.8f;
            const float runway = 6.5f;          // guaranteed distance to cover so the dash always reads as travel
            const float skimHeight = 1.1f;      // Y the charge levels out at once it has dived to the player

            // This move owns its own Y from here until it finishes - otherwise HoldHover() drags
            // the boss back up to hover height every frame and the "charge" is a flat twitch at
            // altitude (user: "剛看到動畫玩家就受到攻擊", "感覺只有頭跟尾").
            float windup = Mathf.Max(0.45f, def.windupSeconds);
            float budget = def.telegraphSeconds + windup + 1.2f + (maxDist / Mathf.Max(1f, speed)) + 2f;
            if (_boss != null) _boss.SuspendHover(budget);

            // 前搖：back off + tilt while a red DANGER LINE shows exactly where the charge will go
            // (spec §9.6.1-2 / §22.2 / user: "所有衝撞攻擊必須有前搖和預警範圍提示").
            Vector3 start = transform.position;
            Vector3 flatDir = player.position - start; flatDir.y = 0f;
            if (flatDir.sqrMagnitude < 1e-4f) flatDir = transform.forward;
            flatDir.Normalize();
            transform.rotation = Quaternion.LookRotation(flatDir, Vector3.up);

            // Ease back so there is always ~runway metres of clear track ahead, even if the player
            // crept in during the telegraph. Eased (not an instant `position -=`) so the back-off
            // itself doesn't read as a teleport (順移 fix, 追加94 續 116).
            float gap = Vector3.Distance(new Vector3(start.x, 0f, start.z), new Vector3(player.position.x, 0f, player.position.z));
            float backoff = Mathf.Clamp(runway - gap, 0f, 7f);
            if (backoff > 0.05f)
            {
                Vector3 backTarget = start - flatDir * backoff;
                Vector3 c = _cfg != null ? _cfg.arenaCenter : start;
                float ar = _cfg != null ? _cfg.arenaRadius : 11f;
                Vector3 fromC = backTarget - c; fromC.y = 0f;
                if (fromC.magnitude > ar) backTarget = c + fromC.normalized * ar;
                backTarget.y = start.y;
                yield return EaseMove(backTarget, 0.32f);
            }

            // narrow lane - the straight charge now leads EDGE-first (硬幣最窄那面, user 續 130), so the
            // dangerous strip is thin: player + a little fairness.
            const float laneCheckR = 1.1f;
            yield return ChargePathTelegraph(transform.position, flatDir, maxDist, laneCheckR, windup);

            // re-aim once at the player's LAST position, then lock (spec §9.6.4 - no turning after).
            // FULL vertical this time: the charge dives at the player, then skims the ground.
            start = transform.position;
            Vector3 aim = player.position + Vector3.up * 0.9f;
            Vector3 dir = (aim - start).normalized;
            flatDir = new Vector3(dir.x, 0f, dir.z);
            if (flatDir.sqrMagnitude < 1e-4f) flatDir = transform.forward;
            flatDir.Normalize();
            transform.rotation = Quaternion.LookRotation(flatDir, Vector3.up);
            float groundY = ProjectToGround(player.position).y;

            // ease the disc into its EDGE-first attitude (rim leads, like a rolling coin), with a
            // small wind-up recoil, so the launch reads as loading-then-firing not a 0→full pop.
            yield return SlerpDiscInto(dir, 0.2f, edgeFirst: true);
            yield return EaseMove(transform.position - flatDir * 0.7f, 0.12f);   // recoil back
            StartCoroutine(FadeChargeLane(Mathf.Min(0.4f, maxDist / Mathf.Max(1f, speed))));

            bool hitPlayer = false, hitWall = false;
            float travelled = 0f;
            float rampDist = Mathf.Min(3.5f, maxDist * 0.3f);   // accelerate over the first few metres
            while (travelled < maxDist && !hitPlayer && !hitWall)
            {
                float ramp = Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(travelled / rampDist));
                float step = speed * ramp * Mathf.Min(Time.deltaTime, 0.04f);   // clamp: a frame hitch must not teleport the boss across the arena
                FaceDiscSideAlong(dir);   // rim leads (硬幣最窄那面) for the whole dash
                // wall check (spec §9.6 - only ChargeCrashSurface stuns)
                if (Physics.SphereCast(transform.position, hitR * 0.8f, flatDir, out var wall, step + hitR, chargeCrashMask, QueryTriggerInteraction.Ignore))
                {
                    hitWall = true;
                    transform.position = wall.point - flatDir * hitR;
                    break;
                }
                Vector3 next = transform.position + dir * step;
                // dive toward the player, then hug skimHeight above the ground - never plough under it
                if (next.y < groundY + skimHeight)
                {
                    next.y = Mathf.MoveTowards(transform.position.y, groundY + skimHeight, Mathf.Max(step, 0.15f));
                    dir = flatDir;   // levelled out - carry on flat
                }
                transform.position = next;
                travelled += step;

                // hit only where the leading RIM actually is (a sphere at the disc's front edge)
                if (player != null && DiscFaceHitsPlayer(player, flatDir, hitR * 0.9f, laneCheckR))
                {
                    hitPlayer = true;
                    DamagePlayer(player, def.healthDamage, flatDir);
                    var kb = player.GetComponent<Live2DAction.Combat.Boss.IKnockbackReceiver>();
                    kb?.ApplyKnockback(flatDir, 6f, false);   // 續 128: a firm STAGGER only - just the 秒殺 (ChargeCrush) throws the player off the map (user)
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
            if (_boss != null)
            {
                _boss.SuspendYClamp(slide + 3f);    // let it fly high for this move
                _boss.SuspendHover(slide + 3f);     // and stop HoldHover dragging it back to hover height mid-climb
            }

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

            // --- vertical slam --- disc lies flat (face DOWN), drives ALL the way to the ground so
            // it "完全蓋地" - the crush check only happens once the disc is actually flat on the floor
            // (user: "boss 真的壓到玩家且完全蓋地時" - not a mid-air proximity guess).
            FaceDiscAlong(Vector3.down);
            float floorY = lockGround.y;
            while (transform.position.y > floorY + 0.15f)
            {
                FaceDiscAlong(Vector3.down);
                transform.position += Vector3.down * slamSpeed * Time.deltaTime;
                yield return null;
            }
            transform.position = new Vector3(transform.position.x, floorY + 0.15f, transform.position.z);
            FaceDiscAlong(Vector3.down);

            // disc is now pancaked on the ground - is the player caught inside its footprint?
            bool crushed = player != null
                && new Vector2(player.position.x - transform.position.x, player.position.z - transform.position.z).sqrMagnitude
                   <= hitR * hitR
                && player.position.y < floorY + 3f;

            if (marker != null) Destroy(marker);

            if (crushed)
            {
                // real contact under a fully-landed disc: camera pulls WIDE to show the disc press
                // the player through the floor and off the map, then smoothly returns; meanwhile
                // the disc presses them straight through into the void, then 秒殺.
                StartCoroutine(CrushEjectCam(player, lockGround));
                yield return VoidPunt(player, lockGround);
                SpawnCrushImpact(lockGround, player);
                DamagePlayer(player, 999999f, Vector3.down);
            }
            else
            {
                transform.position = new Vector3(transform.position.x, floorY + 0.6f, transform.position.z);
                SpawnCrushImpact(lockGround, player);
            }

            yield return new WaitForSeconds(0.5f);   // grounded beat - the player can punish here
        }

        // 續 125 (user): pull to a WIDE far shot so the whole "disc pancakes the player through the
        // floor and off the map" reads, then a smooth quick return toward the player's spot. Both
        // moves are eased (SmoothStep) so nothing snaps. Leaves ThirdPersonCameraController OFF -
        // the player is dead + 36 m down in the void by now; YuanpeiEncounter.Defeat() re-enables
        // the controller after the death-screen hold + teleport (re-enabling here would yank the
        // camera to the corpse).
        private Behaviour _crushCamCtrl;
        private bool _crushCamCtrlWas;

        private IEnumerator CrushEjectCam(Transform player, Vector3 crushGround)
        {
            var cam = Camera.main;
            if (cam == null) yield break;
            _crushCamCtrl = cam.GetComponent(typeof(Live2DAction.CameraSystem.ThirdPersonCameraController)) as Behaviour;
            _crushCamCtrlWas = _crushCamCtrl != null && _crushCamCtrl.enabled;
            if (_crushCamCtrl != null) _crushCamCtrl.enabled = false;

            // horizontal "player side" of the crush point (keeps both the wide and the return shot
            // on the same side, so the return doesn't cross the line)
            Vector3 backDir = cam.transform.position - crushGround; backDir.y = 0f;
            if (backDir.sqrMagnitude < 0.04f) { backDir = -transform.forward; backDir.y = 0f; }
            if (backDir.sqrMagnitude < 0.04f) backDir = Vector3.back;
            backDir.Normalize();

            Vector3 startPos = cam.transform.position;
            Quaternion startRot = cam.transform.rotation;

            // --- phase 1: WIDE + HIGH, framing the disc + the player driven into the void hole ---
            const float wideDur = 1.0f;
            Vector3 widePos = crushGround + backDir * 13f + Vector3.up * 10f;
            // don't let the wide shot sit inside a building / behind a wall (續 130, "有時看不到")
            for (int tries = 0; tries < 4; tries++)
            {
                Vector3 toFocus = (crushGround + Vector3.up * 0.5f) - widePos;
                if (!Physics.Raycast(widePos, toFocus.normalized, toFocus.magnitude - 0.5f, ~0, QueryTriggerInteraction.Ignore))
                    break;
                widePos = crushGround + backDir * (10f - tries * 1.5f) + Vector3.up * (13f + tries * 2f);   // pull in + up
            }
            Quaternion wideRot = Quaternion.LookRotation((crushGround + Vector3.down * 1f - widePos).normalized, Vector3.up);
            float t = 0f;
            while (t < wideDur)
            {
                t += Time.deltaTime;
                // front-load: reach the wide framing by ~40% of the phase, then hold on it so the
                // press + plunge actually play on screen
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / (wideDur * 0.4f)));
                cam.transform.position = Vector3.Lerp(startPos, widePos, k);
                cam.transform.rotation = Quaternion.Slerp(startRot, wideRot, k);
                yield return null;
            }

            // --- phase 2: smooth quick return to a normal over-the-shoulder framing of the spot ---
            const float returnDur = 0.5f;
            Vector3 rFocus = crushGround + Vector3.up * 1.3f;
            Vector3 rPos = rFocus + backDir * 4.5f + Vector3.up * 1.9f;
            Quaternion rRot = Quaternion.LookRotation((rFocus - rPos).normalized, Vector3.up);
            Vector3 w0 = cam.transform.position;
            Quaternion q0 = cam.transform.rotation;
            t = 0f;
            while (t < returnDur)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / returnDur));
                cam.transform.position = Vector3.Lerp(w0, rPos, k);
                cam.transform.rotation = Quaternion.Slerp(q0, rRot, k);
                yield return null;
            }

            // done - leave the controller off (see method comment); just clear the field so a later
            // CancelAll() doesn't double-toggle it.
            _crushCamCtrl = null;
        }

        private void RestoreCrushCam()
        {
            if (_crushCamCtrl != null) { _crushCamCtrl.enabled = _crushCamCtrlWas; _crushCamCtrl = null; }
        }

        private void SpawnCrushImpact(Vector3 lockGround, Transform player)
        {
            Live2DAction.Combat.HitStopController.Request(0.06f, 0.15f);
            var ring = SpawnHazard(YuanpeiHazard.Kind.ExpandingRing, lockGround, 4f, 0f, 0.6f, 0f, player);
            ring.GetComponent<YuanpeiHazard>().Configure(YuanpeiHazard.Kind.ExpandingRing, lockGround, 4f, 0f, 0.6f,
                0f, player, _boss.gameObject, warnColor, burstColor, 14f, 0.6f);
        }

        // ------------------------------------------------- 肉身衝撞 3：繞圈後突然直衝

        private IEnumerator OrbitDash(YuanpeiAttackDef def, Transform player)
        {
            float orbitRadius = def.number1 > 0 ? def.number1 : 8f;
            float dashSpeed = def.number2 > 0 ? def.number2 : 24f;
            float hitR = def.number3 > 0 ? def.number3 : 1.9f;
            const float skimHeight = 1.1f;

            float orbitDur = 1.0f + (float)UnityEngine.Random.value * 1.6f;   // random - "某一個瞬間"
            float dashAt = 0.45f + (float)UnityEngine.Random.value * (orbitDur - 0.45f);
            float angle = Mathf.Atan2(transform.position.z - player.position.z, transform.position.x - player.position.x);
            float angSpeed = (UnityEngine.Random.value < 0.5f ? -1f : 1f) * (2.2f + (float)UnityEngine.Random.value * 1.4f);

            // own the boss's Y for the whole move so HoldHover doesn't fight the orbit height or the
            // dive (user: "感覺只有頭跟尾"). Generous budget - orbit is up to ~2.6s + telegraph + dash.
            if (_boss != null) _boss.SuspendHover(orbitDur + 1.0f + (16f / Mathf.Max(1f, dashSpeed)) + 2f);

            float floorY = ProjectToGround(player.position).y;
            float t = 0f;
            bool dashed = false;
            Vector3 dashDir = Vector3.forward;
            Vector3 dashFlat = Vector3.forward;
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
                        dashDir = (player.position + Vector3.up * 0.9f - transform.position).normalized;   // full dive
                        dashFlat = new Vector3(dashDir.x, 0f, dashDir.z);
                        if (dashFlat.sqrMagnitude < 1e-4f) dashFlat = transform.forward;
                        dashFlat.Normalize();
                        transform.rotation = Quaternion.LookRotation(dashFlat, Vector3.up);
                        Live2DAction.Combat.HitStopController.Request(0.04f, 0.35f);   // "!" beat
                        // 前搖 + 預警：hold on the orbit ring while the danger line telegraphs the
                        // dash path, so the player gets real reaction time (user request).
                        yield return ChargePathTelegraph(transform.position, dashFlat, 16f, 1.1f, 0.5f);
                        yield return SlerpDiscInto(dashFlat, 0.14f, edgeFirst: true);
                        StartCoroutine(FadeChargeLane(0.35f));
                        FaceDiscSideAlong(dashDir);   // 側身衝刺 - rim leads, not the flat face
                    }
                }
                else
                {
                    FaceDiscSideAlong(dashFlat);
                    float dashRamp = Mathf.Lerp(0.4f, 1f, Mathf.Clamp01(travelled / 3f));
                    float step = dashSpeed * dashRamp * Mathf.Min(Time.deltaTime, 0.04f);   // clamp so a frame hitch can't teleport the dash
                    if (Physics.SphereCast(transform.position, hitR * 0.8f, dashFlat, out var wall, step + hitR, chargeCrashMask, QueryTriggerInteraction.Ignore))
                    {
                        transform.position = wall.point - dashFlat * hitR;
                        if (_boss != null && _boss.Vitals != null && _cfg != null)
                            _boss.Vitals.AddPosture(_cfg.maxPosture * _cfg.chargeCrashPostureFraction);
                        yield return new WaitForSeconds(2.5f);
                        yield break;
                    }
                    Vector3 next = transform.position + dashDir * step;
                    if (next.y < floorY + skimHeight)
                    {
                        next.y = Mathf.MoveTowards(transform.position.y, floorY + skimHeight, step);
                        dashDir = dashFlat;   // levelled out
                    }
                    transform.position = next;
                    travelled += step;
                    if (player != null && DiscFaceHitsPlayer(player, dashFlat, hitR * 0.9f, 1.1f))
                    {
                        DamagePlayer(player, def.healthDamage, dashFlat);
                        var kb = player.GetComponent<Live2DAction.Combat.Boss.IKnockbackReceiver>();
                        kb?.ApplyKnockback(dashFlat, 7f, false);   // 續 128: firm stagger only (see BodyCharge note)
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
        private GameObject _chargeLane;

        // Ground danger-lane telegraph for the charges. Bright ADDITIVE fill + two bright pulsing
        // edge rails so it reads on the sunlit plaza (user: "沒有預警範圍"). Width = 2*halfWidth,
        // matching the dash's actual hit `faceRadius`. Sets `_chargeLane` so the dash can fade it
        // out as the disc passes instead of it vanishing the instant the dash starts.
        private IEnumerator ChargePathTelegraph(Vector3 origin, Vector3 dir, float length, float halfWidth, float seconds)
        {
            Vector3 flat = new Vector3(dir.x, 0f, dir.z);
            if (flat.sqrMagnitude < 1e-4f) flat = Vector3.forward;
            flat.Normalize();

            Vector3 startG = ProjectToGround(origin) + Vector3.up * 0.06f;
            var root = new GameObject("YuanpeiChargeLane");
            root.transform.SetPositionAndRotation(startG + flat * (length * 0.5f), Quaternion.LookRotation(flat, Vector3.up));
            _spawned.Add(root);
            _chargeLane = root;

            var fill = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(fill.GetComponent<Collider>());
            fill.transform.SetParent(root.transform, false);
            fill.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // lie flat, face up
            var fillR = fill.GetComponent<Renderer>();
            fillR.sharedMaterial = LaneMat();
            fillR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Renderer[] rails = new Renderer[2];
            for (int s = 0; s < 2; s++)
            {
                var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(rail.GetComponent<Collider>());
                rail.transform.SetParent(root.transform, false);
                rails[s] = rail.GetComponent<Renderer>();
                rails[s].sharedMaterial = LaneMat();
                rails[s].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / seconds);
                float pulse = 1f + Mathf.Sin(Time.time * 16f) * 0.18f;
                float w = halfWidth * 2f * (0.55f + 0.45f * k);
                fill.transform.localScale = new Vector3(w, length, 1f);
                var fc = Color.Lerp(warnColor, dangerColor, k) * (0.55f + 1.4f * k) * pulse;
                var mpb = new MaterialPropertyBlock();
                fillR.GetPropertyBlock(mpb); mpb.SetColor(BaseColorId, fc); fillR.SetPropertyBlock(mpb);
                for (int s = 0; s < 2; s++)
                {
                    float sign = s == 0 ? -1f : 1f;
                    rails[s].transform.localPosition = new Vector3(sign * w * 0.5f, 0.09f, 0f);
                    rails[s].transform.localScale = new Vector3(0.16f, 0.22f, length);
                    var rc = Color.Lerp(warnColor, dangerColor, k) * (1.4f + 2.6f * k) * pulse;
                    var rmpb = new MaterialPropertyBlock();
                    rails[s].GetPropertyBlock(rmpb); rmpb.SetColor(BaseColorId, rc); rails[s].SetPropertyBlock(rmpb);
                }
                yield return null;
            }
            // NOT destroyed here - the dash fades _chargeLane out (FadeChargeLane).
        }

        // Fade + destroy the danger lane over `seconds` while the dash runs through it.
        private IEnumerator FadeChargeLane(float seconds)
        {
            var lane = _chargeLane;
            _chargeLane = null;
            if (lane == null) yield break;
            var rends = lane.GetComponentsInChildren<Renderer>();
            var baseCols = new Color[rends.Length];
            for (int i = 0; i < rends.Length; i++)
            {
                var m = new MaterialPropertyBlock();
                rends[i].GetPropertyBlock(m);
                baseCols[i] = m.GetColor(BaseColorId);
            }
            float t = 0f;
            while (t < seconds && lane != null)
            {
                t += Time.deltaTime;
                float f = 1f - Mathf.Clamp01(t / seconds);
                for (int i = 0; i < rends.Length; i++)
                {
                    if (rends[i] == null) continue;
                    var m = new MaterialPropertyBlock();
                    rends[i].GetPropertyBlock(m);
                    m.SetColor(BaseColorId, baseCols[i] * f);
                    rends[i].SetPropertyBlock(m);
                }
                yield return null;
            }
            if (lane != null) Destroy(lane);
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

        // Ease the VisualRoot from its current orientation into the charge attitude over `seconds`
        // (user: "沒平滑"). edgeFirst = rim leads (硬幣最窄那面); else the flat face leads.
        private IEnumerator SlerpDiscInto(Vector3 dir, float seconds, bool edgeFirst = false)
        {
            if (_boss == null || _boss.VisualRoot == null) { yield break; }
            Vector3 d = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward;
            Quaternion to;
            if (edgeFirst)
            {
                Vector3 side = Vector3.Cross(d, Vector3.up);
                if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
                to = Quaternion.LookRotation(d, side.normalized);
            }
            else
            {
                Vector3 fwd = Vector3.Cross(d, Vector3.up);
                if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.Cross(d, Vector3.right);
                to = Quaternion.LookRotation(fwd.normalized, d);
            }
            Quaternion from = _boss.VisualRoot.rotation;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                if (_boss == null || _boss.VisualRoot == null) yield break;
                _boss.VisualRoot.rotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / seconds)));
                yield return null;
            }
            if (_boss != null && _boss.VisualRoot != null) _boss.VisualRoot.rotation = to;
        }

        private static readonly Collider[] _chargeBuf = new Collider[8];

        // 續 128/130 (user: "要確定撞擊類技能只有在 boss 本體碰到玩家本體才有效果，不是站在預警範圍上
        // boss 還沒碰到就受擊"). A sphere of `checkRadius` at the disc's LEADING RIM (`leadOffset`
        // ahead of the root along `dir`) must genuinely overlap the player's collider - no geometric
        // proximity guess, and the check point is where the edge visually is.
        private bool DiscFaceHitsPlayer(Transform player, Vector3 dir, float leadOffset, float checkRadius)
        {
            if (player == null) return false;
            Vector3 c = transform.position + dir.normalized * leadOffset;
            int n = Physics.OverlapSphereNonAlloc(c, checkRadius, _chargeBuf, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var col = _chargeBuf[i];
                if (col != null && col.transform.root == player.root) return true;   // real body contact
            }
            return false;
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

        // The player's current centre-of-mass in world space - the CharacterController's collider
        // centre if there is one, else a torso-height fallback. Used by ProjectileBurst (user:
        // "每一下都要先鎖定玩家當前位置(人物中心)才施放").
        private static Vector3 PlayerCenter(Transform player)
        {
            var cc = player.GetComponentInChildren<CharacterController>();
            if (cc != null) return cc.transform.TransformPoint(cc.center);
            var col = player.GetComponentInChildren<Collider>();
            if (col != null) return col.bounds.center;
            return player.position + Vector3.up * 0.9f;
        }

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

        // Bright additive material for the charge danger-lane (URP/Lit primitives washed out on the
        // sunlit plaza - user: "沒有預警範圍"). "Live2DAction/VFX/AdditiveUnlit" = Blend One One, no
        // texture needed, driven by _BaseColor.
        private static Material _laneMat;
        private static Material LaneMat()
        {
            if (_laneMat == null)
            {
                var sh = Shader.Find("Live2DAction/VFX/AdditiveUnlit")
                         ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                _laneMat = new Material(sh);
            }
            return _laneMat;
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
