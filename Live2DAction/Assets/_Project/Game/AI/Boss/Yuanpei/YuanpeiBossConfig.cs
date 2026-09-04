using UnityEngine;

namespace Live2DAction.AI.Boss.Yuanpei
{
    // Global tuning for the yuanpei_LogoSky aerial boss - one ScriptableObject, no numbers
    // hardcoded across the scripts (spec §14 "所有招式速度、傷害、範圍、能量與時間應放在
    // ScriptableObject"). Ranges/heights: spec §7.1. Resources: spec §5.
    [CreateAssetMenu(menuName = "Live2DAction/Boss/Yuanpei Boss Config", fileName = "YuanpeiBossConfig")]
    public class YuanpeiBossConfig : ScriptableObject
    {
        [Header("Vitals (spec §5)")]
        public float maxHealth = 1200f;
        public float maxEnergy = 100f;
        public float maxPosture = 100f;

        [Header("Energy (spec §5.2)")]
        public float energyRegenPerSecond = 5f;          // hover / reposition
        public float energyRegenPhase3Bonus = 0.2f;      // +20% in phase 3 (spec §6)
        public float energyRechargeExitThreshold = 50f;  // recharge ends at this energy...
        public float energyRechargeMaxSeconds = 3f;       // ...or this long, whichever first
        public float lowEnergyThreshold = 15f;           // below this + no move => forced recharge

        [Header("Posture (spec §5.3)")]
        [Tooltip("Posture from a player hit = health damage dealt * this. Heavy attacks deal more, so they give more.")]
        public float postureGainPerDamage = 0.55f;
        [Tooltip("追加94 續 120 (user): posture also creeps up on its own while the boss is fighting, so a " +
                 "patient player still eventually gets a fall + F-execution window even without landing many hits. " +
                 "Units = posture/sec; 0 disables. Only ticks in air-combat / attack states, not while downed / recharging.")]
        public float postureRegenPerSecond = 1.6f;
        public float perfectDodgeCounterPostureMultiplier = 1.5f;
        public float backCorePostureMultiplier = 1.6f;
        public float lowEnergyPostureMultiplier = 1.4f;
        [Tooltip("Fraction of max posture added when the boss charge-crashes a valid wall.")]
        public float chargeCrashPostureFraction = 0.3f;

        [Header("HP damage multipliers (spec §5.1)")]
        public float backCoreHealthMultiplier = 1.5f;
        public float lowEnergyHealthMultiplier = 1.25f;
        public float downedHealthMultiplier = 1.5f;      // while posture-broken / on the ground
        [Range(0.05f, 0.5f)] public float executionHealthFraction = 0.22f; // 20-25% of max HP

        [Header("Movement / range (spec §7.1)")]
        public float idealCombatDistanceMin = 8f;
        public float idealCombatDistanceMax = 14f;
        public float maxAttackDistance = 18f;
        public float meleeRange = 4.5f;
        public float hoverHeight = 3.0f;                 // combat height above the arena floor
        public float rechargeHeight = 1.7f;             // descends to a hittable height
        [Tooltip("Absolute world Y ceiling - the boss's root is clamped to this every frame it " +
                 "sets its own height, so a bad floor sample (e.g. a raycast hitting a building " +
                 "roof) can't fling it into the sky. Plaza floor ≈ 0.5, hoverHeight 2.6 → normal " +
                 "hover ≈ 3.1; 8 leaves headroom for ChargeCrush lining up over the player.")]
        public float maxWorldY = 8f;
        public Vector2 hoverBobAmplitudeSpeed = new Vector2(0.35f, 0.8f); // (metres, Hz)
        public float repositionSpeed = 6f;
        public float faceTurnSpeedDegPerSec = 220f;
        public float minPitchToPlayerDeg = 15f;         // never straight overhead (spec §7.2)
        public float maxPitchToPlayerDeg = 32f;

        [Header("Scheduler (spec §10.3)")]
        public float globalAttackIntervalMin = 0.7f;
        public float globalAttackIntervalMax = 1.0f;
        public float phase3IntervalScale = 0.8f;        // phase 3 tightens spacing (spec §6)
        [Tooltip("An attack used within this many seconds has its selection weight cut (avoid isolating moves).")]
        public float rotationRecoverySeconds = 6f;
        [Range(0f, 1f)] public float rotationRecentWeightFactor = 0.35f;
        [Tooltip("Seconds after re-entering the camera view before the boss may attack again (spec §7.2).")]
        public float onScreenGraceBeforeAttack = 0.5f;

        [Header("Phase HP thresholds (spec §6, fractions of max)")]
        [Range(0f, 1f)] public float phase2HealthFraction = 0.70f;
        [Range(0f, 1f)] public float phase3HealthFraction = 0.35f;

        [Header("Posture break / fall (spec §11.3)")]
        public float fallSeconds = 1.1f;
        public float fallSpinSpeedDeg = 540f;
        public float executionWindowSeconds = 5f;
        public float executionInteractDistance = 2.4f;
        public float executionAnimationSeconds = 1.6f;
        public float reAscendSeconds = 1.4f;
        public float postExecutionInvulnSeconds = 1f;
        public float energyAfterExecution = 50f;
        public float energyAfterMissedExecution = 40f;

        [Header("Perfect dodge (spec §8.2)")]
        public float perfectDodgeWindowSeconds = 0.18f;   // dodge within this of an incoming hit
        public float perfectDodgeCounterWindowSeconds = 0.5f; // attack within this to count as a counter

        [Header("Arena")]
        [Tooltip("Boss combat anchor (centre of the fight). If null, the encounter uses its own transform.")]
        public Vector3 arenaCenter = new Vector3(0f, 0f, -114f);
        public float arenaRadius = 11f;                  // ~22m diameter (spec §18)
    }
}
