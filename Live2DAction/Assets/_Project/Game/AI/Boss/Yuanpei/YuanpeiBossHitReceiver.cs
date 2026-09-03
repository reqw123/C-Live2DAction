using UnityEngine;
using Live2DAction.Core;

namespace Live2DAction.AI.Boss.Yuanpei
{
    // One per hittable collider (BodyCollider / CoreWeakPointCollider - spec §13). Player attacks
    // land here via AttackResolver -> IDamageable.ApplyDamage; this routes the raw damage into
    // YuanpeiBossVitals with the right situational multipliers (spec §5.1 / §5.3).
    //
    // NOT active during Falling / on the boss's own body while it moves - it only ever receives
    // real player attack casts (AttackResolver already filters own-root), never "body touch"
    // damage.
    public class YuanpeiBossHitReceiver : MonoBehaviour, IDamageable
    {
        [SerializeField] private YuanpeiBossVitals vitals;
        [SerializeField] private YuanpeiBoss boss;
        [Tooltip("This collider is the back/centre weak core - applies the back-core multiplier when hit from behind.")]
        [SerializeField] private bool isWeakCore;
        [Tooltip("Dot(bossForward, hitDir) below this counts as 'from behind' for the weak core.")]
        [SerializeField] private float behindDotThreshold = -0.1f;

        private void Awake()
        {
            if (vitals == null) vitals = GetComponentInParent<YuanpeiBossVitals>();
            if (boss == null) boss = GetComponentInParent<YuanpeiBoss>();
        }

        public void ApplyDamage(DamageInfo info)
        {
            if (vitals == null || vitals.IsDead) return;
            if (boss != null && !boss.AcceptsDamageNow) return;

            var ctx = new YuanpeiBossVitals.HitContext
            {
                downed = boss != null && boss.IsDowned,
                perfectCounter = boss != null && boss.ConsumePerfectCounterFlag(),
            };

            if (isWeakCore)
            {
                Vector3 toHitter = info.Source != null
                    ? (info.Source.transform.position - transform.position)
                    : -transform.root.forward;
                toHitter.y = 0f;
                Vector3 bossFwd = transform.root.forward; bossFwd.y = 0f;
                if (toHitter.sqrMagnitude > 0.0001f && bossFwd.sqrMagnitude > 0.0001f)
                {
                    float d = Vector3.Dot(bossFwd.normalized, toHitter.normalized);
                    ctx.backCore = d < behindDotThreshold;
                }
                else ctx.backCore = true;
            }

            bool crossedPosture = vitals.ApplyPlayerHit(info.Amount, info.Source, info.Point, in ctx);
            if (boss != null) boss.NotifyPlayerHitLanded(crossedPosture);
        }
    }
}
