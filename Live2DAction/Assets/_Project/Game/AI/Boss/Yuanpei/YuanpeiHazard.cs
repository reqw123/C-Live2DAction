using UnityEngine;
using Live2DAction.Core;

namespace Live2DAction.AI.Boss.Yuanpei
{
    // A single runtime ground hazard: lightning-mark circle, delayed-AoE circle, or the expanding
    // shockwave ring. Warning phase = visual only; the Hit Window is a fixed burst. Each hazard
    // damages the player at most once (spec §9.3/§9.4/§9.5).
    public class YuanpeiHazard : MonoBehaviour
    {
        public enum Kind { StrikeCircle, DelayedAoE, ExpandingRing }

        private Kind _kind;
        private float _radius;
        private float _warnSeconds;
        private float _activeSeconds;
        private float _damage;
        private Transform _player;
        private GameObject _source;
        private float _t;
        private bool _hitPlayer;
        private bool _burstDone;

        // StrikeCircle homing (續 128, user: "紅圈攻擊太容易閃躲") - the circle chases the player for
        // the first part of the warn, then locks, so you can't just stroll out of it.
        private float _trackUntil;
        private float _trackEase;
        private LayerMask _groundMask = ~0;

        // ring
        private float _ringSpeed;
        private float _ringThickness;
        private float _prevRingR;

        private Transform _fill;   // scaling disc visual
        private Renderer _fillR;
        private Transform _ring;   // bright outline (spec §22.2 - unmistakable telegraph)
        private Renderer _ringR;
        private MaterialPropertyBlock _mpb;
        private MaterialPropertyBlock _ringMpb;
        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmiId = Shader.PropertyToID("_EmissionColor");

        public bool IsMajorActive => !_burstDone;

        // --- optional flipbook visual (紅圈攻擊特效.mp4 baked to a 6x6 atlas) as an RPG-style
        //     ground telegraph. Runtime VideoPlayers render black on this box, so it's a static
        //     atlas + a script-driven _Frame instead. ---
        private Material _flipMat;
        private Renderer _flipR;
        private int _flipFrames = 36;
        private float _flipImpactFrac = 0.55f;   // fraction of `frames` reached when the pillar lands
        private static readonly int FadeId = Shader.PropertyToID("_Fade");
        private static readonly int FrameId = Shader.PropertyToID("_Frame");
        private static readonly int ColsId = Shader.PropertyToID("_Cols");
        private static readonly int RowsId = Shader.PropertyToID("_Rows");

        // Call AFTER Configure. Replaces the solid disc with a flat ground quad showing the baked
        // flipbook (Live2DAction/GroundStrikeURP). `frameScale` = quad width / (radius*2) - the
        // atlas circle only fills part of each tile, so the quad is drawn bigger. The bright rim
        // ring from Configure stays on as a guaranteed-visible RPG telegraph.
        public void SetFlipbook(Material matTemplate, int cols, int rows, int frames, float impactFraction, float frameScale = 1.6f)
        {
            if (matTemplate == null) return;
            _flipFrames = Mathf.Max(1, frames);
            _flipImpactFrac = Mathf.Clamp01(impactFraction);

            // the atlas has its own rune ring + fill - the primitive disc/ring underneath just
            // pollutes it (a lit orange blob in the centre), so hide both.
            if (_fillR != null) _fillR.enabled = false;
            if (_ringR != null) _ringR.enabled = false;

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "StrikeFlipbook";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(transform, false);
            // sit clearly above the floor - the shader uses ZTest LEqual + polygon offset so the
            // player occludes the decal, but the physical lift also keeps it off the plaza mesh.
            quad.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // lie flat, face up
            // frameScale ~1.6: with the shader's circular crop the whole quad reads as the circle,
            // so keep it close to the hit radius (radius*2 = hit diameter) plus a small fairness margin.
            quad.transform.localScale = new Vector3(_radius * 2f * frameScale, _radius * 2f * frameScale, 1f);
            _flipR = quad.GetComponent<Renderer>();
            _flipR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            _flipMat = new Material(matTemplate);
            _flipMat.SetFloat(ColsId, cols);
            _flipMat.SetFloat(RowsId, rows);
            _flipMat.SetFloat(FrameId, 0f);
            _flipMat.SetFloat(FadeId, 1f);
            _flipR.sharedMaterial = _flipMat;
        }

        // Call AFTER Configure (StrikeCircle only). The circle eases toward the player's ground
        // position until `trackSeconds` of the warn have elapsed, then it locks.
        public void SetHoming(float trackSeconds, float easeRate, LayerMask groundMask)
        {
            _trackUntil = Mathf.Max(0f, trackSeconds);
            _trackEase = Mathf.Max(0.1f, easeRate);
            _groundMask = groundMask;
        }

        public void Configure(Kind kind, Vector3 pos, float radius, float warnSeconds, float activeSeconds,
            float damage, Transform player, GameObject source, Color warnColor, Color burstColor,
            float ringSpeed = 0f, float ringThickness = 0f)
        {
            _kind = kind; transform.position = pos; _radius = radius;
            _warnSeconds = warnSeconds; _activeSeconds = activeSeconds; _damage = damage;
            _player = player; _source = source;
            _ringSpeed = ringSpeed; _ringThickness = ringThickness;
            _warnColor = warnColor; _burstColor = burstColor;
            _trackUntil = 0f;

            _fill = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
            _fill.SetParent(transform, false);
            Object.DestroyImmediate(_fill.GetComponent<Collider>());
            _fill.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);
            _fillR = _fill.GetComponent<Renderer>();
            _fillR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _mpb = new MaterialPropertyBlock();
            Paint(_warnColor, 0.15f);

            // bright rim so the warning is unmistakable even on a busy floor (spec §22.2). A thin
            // ring of the disc sitting slightly proud of the fill - not a separate hit volume.
            _ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
            _ring.SetParent(transform, false);
            Object.DestroyImmediate(_ring.GetComponent<Collider>());
            _ring.localPosition = new Vector3(0f, 0.03f, 0f);
            _ring.localScale = new Vector3(radius * 2f + 0.35f, 0.015f, radius * 2f + 0.35f);
            _ringR = _ring.GetComponent<Renderer>();
            _ringR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ringMpb = new MaterialPropertyBlock();
            _ringR.GetPropertyBlock(_ringMpb);
            _ringMpb.SetColor(ColorId, _warnColor);
            _ringMpb.SetColor(EmiId, _warnColor * 2f);
            _ringR.SetPropertyBlock(_ringMpb);
        }

        private Color _warnColor, _burstColor;

        private void Paint(Color c, float emi)
        {
            if (_fillR != null)
            {
                _fillR.GetPropertyBlock(_mpb);
                _mpb.SetColor(ColorId, new Color(c.r, c.g, c.b, 0.5f));
                _mpb.SetColor(EmiId, c * emi);
                _fillR.SetPropertyBlock(_mpb);
            }
            if (_ringR != null)
            {
                // ring pulses brighter as the timer runs down (warn phase) and blazes on burst
                float pulse = 1.6f + 1.4f * Mathf.Abs(Mathf.Sin(_t * 6f));
                _ringR.GetPropertyBlock(_ringMpb);
                _ringMpb.SetColor(ColorId, c);
                _ringMpb.SetColor(EmiId, c * (emi > 2f ? emi : pulse));
                _ringR.SetPropertyBlock(_ringMpb);
            }
        }

        private void Update()
        {
            _t += Time.deltaTime;

            if (_kind == Kind.ExpandingRing)
            {
                TickRing();
                return;
            }

            // StrikeCircle homing: chase the player's ground position until the track window closes.
            if (_trackUntil > 0f && _t < _trackUntil && _player != null && !_burstDone)
            {
                Vector3 want = new Vector3(_player.position.x, transform.position.y, _player.position.z);
                var o = new Vector3(want.x, want.y + 25f, want.z);
                if (Physics.Raycast(o, Vector3.down, out var hit, 120f, _groundMask, QueryTriggerInteraction.Ignore)
                    && hit.collider.GetComponentInParent<CharacterController>() == null)
                    want.y = hit.point.y + 0.02f;
                transform.position = Vector3.Lerp(transform.position, want, _trackEase * Time.deltaTime);
            }

            // flipbook: play the warn-up frames over _warnSeconds so the pillar frame lands exactly
            // when the burst fires, then the remaining frames over a short tail, then fade + die.
            const float flipTail = 1.3f;
            if (_flipMat != null)
            {
                float impactFrame = _flipImpactFrac * (_flipFrames - 1);
                float frame;
                if (_t < _warnSeconds)
                    frame = Mathf.Lerp(0f, impactFrame, _t / Mathf.Max(0.01f, _warnSeconds));
                else
                {
                    float tk = (_t - _warnSeconds) / flipTail;
                    frame = Mathf.Lerp(impactFrame, _flipFrames - 1, Mathf.Clamp01(tk));
                    _flipMat.SetFloat(FadeId, Mathf.Clamp01((1f - tk) / 0.3f));
                }
                _flipMat.SetFloat(FrameId, frame);
            }

            if (_t < _warnSeconds)
            {
                float k = _t / Mathf.Max(0.01f, _warnSeconds);
                Paint(Color.Lerp(_warnColor, _burstColor, k * 0.5f), 0.1f + k * 0.6f);
                return;
            }

            if (!_burstDone)
            {
                _burstDone = true;
                Paint(_burstColor, 3f);
                TryHitPlayerInCircle(_radius);
            }

            if (_flipMat != null)
            {
                if (_t > _warnSeconds + flipTail) Destroy(gameObject);
                return;
            }

            if (_t > _warnSeconds + _activeSeconds)
                Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_flipMat != null) Destroy(_flipMat);
        }

        private void TickRing()
        {
            float r = _t * _ringSpeed;
            if (_fill != null) _fill.localScale = new Vector3(r * 2f, 0.03f, r * 2f);
            Paint(_burstColor, 2.5f);
            // damage when the ring BAND passes through the player (spec §9.5 - moving ring, not full disc)
            if (!_hitPlayer && _player != null)
            {
                float d = Vector3.Distance(new Vector3(_player.position.x, transform.position.y, _player.position.z), transform.position);
                if (d >= _prevRingR - _ringThickness && d <= r + _ringThickness)
                {
                    HitPlayer();
                }
            }
            _prevRingR = r;
            if (r > _radius + _ringThickness + 1f) Destroy(gameObject);
        }

        private void TryHitPlayerInCircle(float r)
        {
            if (_hitPlayer || _player == null) return;
            Vector3 flat = new Vector3(_player.position.x, transform.position.y, _player.position.z);
            if ((flat - transform.position).sqrMagnitude <= r * r) HitPlayer();
        }

        private void HitPlayer()
        {
            if (_hitPlayer || _player == null) return;
            _hitPlayer = true;
            var dmg = _player.GetComponentInChildren<IDamageable>() ?? _player.GetComponent<IDamageable>();
            dmg?.ApplyDamage(new DamageInfo(_damage, _player.position, Vector3.up, _source));
        }
    }
}
