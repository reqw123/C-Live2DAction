using UnityEngine;
using Live2DAction.Core;

namespace Live2DAction.AI.Boss.Yuanpei
{
    // A light-orb from 光粒子三連射 (spec §9.1). Player can dodge it OR destroy it with a weapon
    // hit (it's IDamageable). Light homing only in the first fraction of flight, then straight.
    public class YuanpeiProjectile : MonoBehaviour, IDamageable
    {
        private Vector3 _dir;
        private float _speed;
        private float _hitRadius;
        private float _damage;
        private float _homingSeconds;
        private float _homingStrength;
        private float _life;
        private Transform _player;
        private GameObject _source;
        private bool _spent;
        // 續 136 (Crimson Void Spear) - an elongated projectile's dangerous point is its TIP, not its
        // pivot/centre. 0 = orb behaviour unchanged (check centred on transform.position).
        private float _tipForwardOffset;

        private static readonly Collider[] _overlapBuf = new Collider[8];

        public void Launch(Vector3 dir, float speed, float hitRadius, float damage,
            float homingSeconds, float homingStrength, Transform player, GameObject source, float life = 6f,
            float tipForwardOffset = 0f)
        {
            _dir = dir.normalized; _speed = speed; _hitRadius = hitRadius; _damage = damage;
            _homingSeconds = homingSeconds; _homingStrength = homingStrength;
            _player = player; _source = source; _life = life; _tipForwardOffset = tipForwardOffset;
        }

        private void Update()
        {
            if (_spent) return;
            _life -= Time.deltaTime;
            if (_life <= 0f) { Destroy(gameObject); return; }

            if (_homingSeconds > 0f && _player != null)
            {
                _homingSeconds -= Time.deltaTime;
                Vector3 want = (_player.position + Vector3.up * 1.1f - transform.position).normalized;
                _dir = Vector3.Slerp(_dir, want, _homingStrength * Time.deltaTime).normalized;
            }

            transform.position += _dir * _speed * Time.deltaTime;
            transform.forward = _dir;

            // Only damage when the orb's actual surface volume overlaps one of the player's
            // colliders (user: "衡量表面體積是否有碰撞到玩家才造成傷害") - not a loose point-to-torso
            // proximity check. `_hitRadius * 1.3` is the visible orb radius (scale = hitRadius*2.6).
            if (_player != null && OrbSurfaceHitsPlayer())
            {
                var dmg = _player.GetComponentInChildren<IDamageable>() ?? _player.GetComponent<IDamageable>();
                dmg?.ApplyDamage(new DamageInfo(_damage, transform.position, _dir, _source));
                _spent = true;
                Destroy(gameObject);
            }
        }

        private bool OrbSurfaceHitsPlayer()
        {
            float orbR = _hitRadius * 1.3f + 0.05f;   // visible radius + a hair of skin
            Vector3 checkPoint = _tipForwardOffset > 0f
                ? transform.position + transform.forward * _tipForwardOffset
                : transform.position;
            int n = Physics.OverlapSphereNonAlloc(checkPoint, orbR, _overlapBuf, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var col = _overlapBuf[i];
                if (col == null) continue;
                if (col.transform.root == _player) return true;   // the orb sphere is touching the player's body
            }
            return false;
        }

        // player weapon hit -> pop
        public void ApplyDamage(DamageInfo info)
        {
            if (_spent) return;
            _spent = true;
            Destroy(gameObject);
        }
    }
}
