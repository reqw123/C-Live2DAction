using System;
using UnityEngine;
using Live2DAction.Core;

namespace Live2DAction.AI.Boss.Yuanpei
{
    // Pure phase math (spec §6) - extracted so it's directly unit-testable without a boss.
    public static class YuanpeiPhaseLogic
    {
        public static int PhaseForHealth(float healthFraction, float phase2Frac, float phase3Frac)
        {
            if (healthFraction > phase2Frac) return 1;
            if (healthFraction > phase3Frac) return 2;
            return 3;
        }
    }


    // The three-bar authority (spec §5, §24). HP is the ONLY death/victory authority; Energy
    // gates casting; Posture gates the fall + F-execution window. Nothing else decides damage,
    // death, execution or victory - they only react to this.
    //
    // HP is delegated to a Core.Health so the player's existing hit pipeline (AttackResolver ->
    // IDamageable.ApplyDamage) already reaches it via YuanpeiBossHitReceiver. Energy/Posture are
    // owned here directly.
    public class YuanpeiBossVitals : MonoBehaviour
    {
        [SerializeField] private YuanpeiBossConfig config;
        [SerializeField] private Health health;

        private float _energy;
        private float _posture;
        private bool _postureLocked;         // spec §5.3 - only one PostureBreak per fill
        private bool _energyDepletedNoMoves; // set by the controller when a forced recharge is needed

        // --- events (spec §14 "BossVitals ... 倍率與事件") ---
        public event Action PostureFull;         // fired once when posture crosses max
        public event Action<int> PhaseChanged;   // new phase (1/2/3)
        public event Action Died;                // relayed from Health

        private int _phase = 1;

        public YuanpeiBossConfig Config => config;
        public Health Health => health;
        public float Energy => _energy;
        public float EnergyNormalized => config != null && config.maxEnergy > 0f ? _energy / config.maxEnergy : 0f;
        public float Posture => _posture;
        public float PostureNormalized => config != null && config.maxPosture > 0f ? _posture / config.maxPosture : 0f;
        public float HealthNormalized => health != null && health.MaxHealth > 0f ? health.CurrentHealth / health.MaxHealth : 0f;
        public bool IsDead => health != null && health.IsDead;
        public int Phase => _phase;
        public bool IsLowEnergy => config != null && _energy < config.lowEnergyThreshold;
        public bool PostureIsFull => _postureLocked;

        private void Awake()
        {
            if (health == null) health = GetComponent<Health>();
            if (config != null) _energy = config.maxEnergy;
        }

        private void OnEnable()
        {
            if (health != null) health.Died += OnHealthDied;
        }

        private void OnDisable()
        {
            if (health != null) health.Died -= OnHealthDied;
        }

        private void OnHealthDied() => Died?.Invoke();

        // ---------------------------------------------------------------- Phase

        public void EvaluatePhase()
        {
            if (config == null || health == null) return;
            int newPhase = YuanpeiPhaseLogic.PhaseForHealth(
                HealthNormalized, config.phase2HealthFraction, config.phase3HealthFraction);
            if (newPhase != _phase)
            {
                _phase = newPhase;
                PhaseChanged?.Invoke(_phase);
            }
        }

        // ---------------------------------------------------------------- Energy

        public bool CanAfford(float cost) => _energy >= cost;

        public void SpendEnergy(float cost)
        {
            _energy = Mathf.Max(0f, _energy - cost);
        }

        public void RegenEnergy(float dt, bool phase3Bonus)
        {
            if (config == null) return;
            float rate = config.energyRegenPerSecond * (phase3Bonus ? 1f + config.energyRegenPhase3Bonus : 1f);
            _energy = Mathf.Min(config.maxEnergy, _energy + rate * dt);
        }

        public void SetEnergy(float value)
        {
            if (config == null) return;
            _energy = Mathf.Clamp(value, 0f, config.maxEnergy);
        }

        // ---------------------------------------------------------------- Posture

        // Adds posture and returns true if this call crossed the max (fires PostureFull once).
        public bool AddPosture(float amount)
        {
            if (config == null || _postureLocked || IsDead || amount <= 0f) return false;
            _posture = Mathf.Min(config.maxPosture, _posture + amount);
            if (_posture >= config.maxPosture)
            {
                _postureLocked = true;
                _posture = config.maxPosture;
                PostureFull?.Invoke();
                return true;
            }
            return false;
        }

        // spec §5.3 - posture does NOT auto-drain during air combat, so there is no regen here.

        public void ResetPosture()
        {
            _posture = 0f;
            _postureLocked = false;
        }

        // ---------------------------------------------------------------- HP damage helpers

        // Applies a player hit's health damage with the situational multiplier (spec §5.1) and
        // the matching posture gain (spec §5.3). Returns whether the hit crossed the posture max.
        public struct HitContext
        {
            public bool backCore;
            public bool downed;          // posture-broken / on the ground
            public bool perfectCounter;  // this hit is a perfect-dodge counter
        }

        public bool ApplyPlayerHit(float rawHealthDamage, GameObject source, Vector3 point, in HitContext ctx)
        {
            if (config == null || health == null || health.IsDead) return false;

            float healthMult = 1f;
            if (ctx.downed) healthMult *= config.downedHealthMultiplier;
            if (ctx.backCore) healthMult *= config.backCoreHealthMultiplier;
            if (IsLowEnergy) healthMult *= config.lowEnergyHealthMultiplier;

            float finalHealth = rawHealthDamage * healthMult;
            health.ApplyDamage(new DamageInfo(finalHealth, point, Vector3.zero, source));
            EvaluatePhase();

            float postureGain = rawHealthDamage * config.postureGainPerDamage;
            if (ctx.perfectCounter) postureGain *= config.perfectDodgeCounterPostureMultiplier;
            if (ctx.backCore) postureGain *= config.backCorePostureMultiplier;
            if (IsLowEnergy) postureGain *= config.lowEnergyPostureMultiplier;

            return AddPosture(postureGain);
        }

        // spec §5.1 - execution damage, applied at the finisher's hit event (not on F press).
        // Returns whether it was lethal.
        public bool ApplyExecutionDamage(GameObject source)
        {
            if (config == null || health == null || health.IsDead) return true;
            float dmg = health.MaxHealth * config.executionHealthFraction;
            health.ApplyDamage(new DamageInfo(dmg, transform.position, Vector3.zero, source));
            EvaluatePhase();
            return health.IsDead;
        }
    }
}
