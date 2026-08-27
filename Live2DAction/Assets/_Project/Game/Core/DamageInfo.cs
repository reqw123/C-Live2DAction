using UnityEngine;

namespace Live2DAction.Core
{
    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly Vector3 Point;
        public readonly Vector3 Direction;
        public readonly GameObject Source;

        // 2026-08-24, boss moveset request - every existing caller (Player/Enemy attacks) still
        // uses the 4-arg constructor below and gets null here, which StancePoise.OnDamaged
        // treats exactly as before (poise gain derived from Amount * stanceGainMultiplier). Only
        // BossHitbox passes an explicit value, for attacks whose design-specified poise damage is
        // NOT a fixed multiple of their own health damage (e.g. Punch_Combo_3's own "1.2x health /
        // 1.4x poise" split).
        public readonly float? ExplicitPoiseAmount;

        public DamageInfo(float amount, Vector3 point, Vector3 direction, GameObject source)
        {
            Amount = amount;
            Point = point;
            Direction = direction;
            Source = source;
            ExplicitPoiseAmount = null;
        }

        public DamageInfo(float amount, Vector3 point, Vector3 direction, GameObject source, float explicitPoiseAmount)
        {
            Amount = amount;
            Point = point;
            Direction = direction;
            Source = source;
            ExplicitPoiseAmount = explicitPoiseAmount;
        }
    }
}
