using UnityEngine;

namespace Live2DAction.AI.Boss.Yuanpei
{
    public enum YuanpeiAttackId
    {
        ProjectileBurst,   // 光粒子三連射
        FocusLaser,        // 聚焦雷射
        LightningMark,     // 雷擊標記
        MultiAoE,          // 多重延遲範圍光爆
        Shockwave,         // 近身震退
        BodyCharge,        // 肉身衝撞（原本的直線版）
        ChargeLine,        // 肉身衝撞：長距離高速直線衝
        ChargeCrush,       // 肉身衝撞：滑到玩家頭頂正上方後垂直下壓（命中 = 秒殺）
        OrbitDash,         // 肉身衝撞：繞玩家轉圈，某一瞬間突然直衝
    }

    // Per-attack data (spec §9, §14.1). Every timing/number lives here, not in code.
    [CreateAssetMenu(menuName = "Live2DAction/Boss/Yuanpei Attack", fileName = "YuanpeiAttack_")]
    public class YuanpeiAttackDef : ScriptableObject
    {
        public YuanpeiAttackId attackId;
        public string displayName;

        [Header("Gate (spec §10.1)")]
        [Tooltip("1 = phase 1, 2 = phase 2, 3 = phase 3 (spec §6).")]
        [Range(1, 3)] public int requiredPhase = 1;
        public float energyCost = 15f;
        public float cooldownSeconds = 2.5f;
        public float minRange = 6f;
        public float maxRange = 15f;
        [Tooltip("This attack occupies a shared 'major danger' slot - can't run while another major attack/hazard is live (spec §10.3).")]
        public bool isMajorHazard = true;

        [Header("Timeline (spec §9 Telegraph→Windup→Active→Recovery)")]
        public float telegraphSeconds = 0.5f;
        public float windupSeconds = 0.15f;
        public float activeSeconds = 0.4f;
        public float recoverySeconds = 0.6f;

        [Header("Damage")]
        public float healthDamage = 40f;
        [Tooltip("Extra posture dealt TO the player, if the project keeps a player poise bar. 0 = none.")]
        public float playerPostureDamage = 0f;
        public int maxHitsPerTarget = 1;

        [Header("Scheduler weight")]
        public float baseWeight = 1f;
        [Tooltip("Multiplier applied when the situational condition for this attack is met (spec §10.2).")]
        public float situationalWeightBonus = 2.5f;

        [Header("Attack-specific numbers")]
        public float number1;   // meaning per attack - see YuanpeiAttacks.cs
        public float number2;
        public float number3;
        public int count = 3;

        public float TotalDuration => telegraphSeconds + windupSeconds + activeSeconds + recoverySeconds;
    }
}
