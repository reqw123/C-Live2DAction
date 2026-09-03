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

        // ring
        private float _ringSpeed;
        private float _ringThickness;
        private float _prevRingR;

        private Transform _fill;   // scaling disc visual
        private Renderer _fillR;
        private MaterialPropertyBlock _mpb;
        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmiId = Shader.PropertyToID("_EmissionColor");

        public bool IsMajorActive => !_burstDone;

        public void Configure(Kind kind, Vector3 pos, float radius, float warnSeconds, float activeSeconds,
            float damage, Transform player, GameObject source, Color warnColor, Color burstColor,
            float ringSpeed = 0f, float ringThickness = 0f)
        {
            _kind = kind; transform.position = pos; _radius = radius;
            _warnSeconds = warnSeconds; _activeSeconds = activeSeconds; _damage = damage;
            _player = player; _source = source;
            _ringSpeed = ringSpeed; _ringThickness = ringThickness;
            _warnColor = warnColor; _burstColor = burstColor;

            _fill = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
            _fill.SetParent(transform, false);
            Object.DestroyImmediate(_fill.GetComponent<Collider>());
            _fill.localScale = new Vector3(radius * 2f, 0.03f, radius * 2f);
            _fillR = _fill.GetComponent<Renderer>();
            _fillR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _mpb = new MaterialPropertyBlock();
            Paint(_warnColor, 0.15f);
        }

        private Color _warnColor, _burstColor;

        private void Paint(Color c, float emi)
        {
            if (_fillR == null) return;
            _fillR.GetPropertyBlock(_mpb);
            _mpb.SetColor(ColorId, c);
            _mpb.SetColor(EmiId, c * emi);
            _fillR.SetPropertyBlock(_mpb);
        }

        private void Update()
        {
            _t += Time.deltaTime;

            if (_kind == Kind.ExpandingRing)
            {
                TickRing();
                return;
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

            if (_t > _warnSeconds + _activeSeconds)
                Destroy(gameObject);
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
