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

        public void Launch(Vector3 dir, float speed, float hitRadius, float damage,
            float homingSeconds, float homingStrength, Transform player, GameObject source, float life = 6f)
        {
            _dir = dir.normalized; _speed = speed; _hitRadius = hitRadius; _damage = damage;
            _homingSeconds = homingSeconds; _homingStrength = homingStrength;
            _player = player; _source = source; _life = life;
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

            if (_player != null)
            {
                float r = _hitRadius + 0.35f;
                if ((transform.position - (_player.position + Vector3.up * 1.0f)).sqrMagnitude <= r * r)
                {
                    var dmg = _player.GetComponentInChildren<IDamageable>() ?? _player.GetComponent<IDamageable>();
                    dmg?.ApplyDamage(new DamageInfo(_damage, transform.position, _dir, _source));
                    _spent = true;
                    Destroy(gameObject);
                }
            }
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
