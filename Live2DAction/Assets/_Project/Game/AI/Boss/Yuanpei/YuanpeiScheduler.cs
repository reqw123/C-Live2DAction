using System.Collections.Generic;
using UnityEngine;

namespace Live2DAction.AI.Boss.Yuanpei
{
    // Attack selection (spec §10). Pure so it's directly EditMode-testable: given the world
    // situation, produce the chosen attack (or null = hover/reposition/recharge).
    public static class YuanpeiScheduler
    {
        public struct Situation
        {
            public int phase;
            public float energy;
            public float playerDistance;
            public bool hasLineOfSight;
            public bool bossOnScreen;
            public float onScreenSeconds;
            public bool majorHazardActive;    // a laser/lightning/AoE is currently live
            public YuanpeiAttackId lastAttack;
            public bool hasLastAttack;
            public bool playerMovingStraightLong;
            public bool playerLingeringArea;
            public bool arenaHasGoodFloor;
            public float onScreenGrace;
            public float now;                 // Time.time, for cooldown checks
        }

        // cooldownUntil / lastUsedTime keyed by attackId - the caller owns the dictionaries.
        public static YuanpeiAttackDef Select(
            IReadOnlyList<YuanpeiAttackDef> pool,
            in Situation s,
            IReadOnlyDictionary<YuanpeiAttackId, float> cooldownUntil,
            IReadOnlyDictionary<YuanpeiAttackId, float> lastUsedTime,
            float rotationRecoverySeconds,
            float rotationRecentFactor,
            System.Random rng)
        {
            if (pool == null || pool.Count == 0) return null;
            if (!s.bossOnScreen || s.onScreenSeconds < s.onScreenGrace) return null; // spec §7.2

            var weighted = new List<(YuanpeiAttackDef def, float weight)>();
            float total = 0f;

            foreach (var def in pool)
            {
                if (def == null) continue;
                if (def.requiredPhase > s.phase) continue;                 // §10.1.1
                if (s.energy < def.energyCost) continue;                   // §10.1.2
                if (cooldownUntil != null && cooldownUntil.TryGetValue(def.attackId, out float cd) && s.now < cd) continue; // §10.1.3
                if (s.playerDistance < def.minRange || s.playerDistance > def.maxRange) continue; // §10.1.4
                if (!s.hasLineOfSight) continue;                           // §10.1.5
                if (def.isMajorHazard && s.majorHazardActive) continue;    // §10.1.7 / §10.3
                if (s.hasLastAttack && def.attackId == s.lastAttack) continue; // §10.1.8 - no repeat

                float w = def.baseWeight;
                if (w <= 0f) continue;

                // situational weight (spec §10.2)
                if (Matches(def.attackId, in s)) w *= def.situationalWeightBonus;

                // least-recently-used bias (spec §10.2 note / mirrors BossStateMachine)
                if (rotationRecoverySeconds > 0.01f && lastUsedTime != null
                    && lastUsedTime.TryGetValue(def.attackId, out float lu))
                {
                    float since = s.now - lu;
                    w *= Mathf.Lerp(rotationRecentFactor, 1f, Mathf.Clamp01(since / rotationRecoverySeconds));
                }

                weighted.Add((def, w));
                total += w;
            }

            if (weighted.Count == 0 || total <= 0f) return null;

            double roll = (rng?.NextDouble() ?? 0.5) * total;
            float acc = 0f;
            foreach (var (def, weight) in weighted)
            {
                acc += weight;
                if (roll <= acc) return def;
            }
            return weighted[weighted.Count - 1].def;
        }

        private static bool Matches(YuanpeiAttackId id, in Situation s)
        {
            switch (id)
            {
                case YuanpeiAttackId.ProjectileBurst: return s.playerDistance >= 8f && s.playerDistance <= 14f;
                case YuanpeiAttackId.FocusLaser:      return s.playerMovingStraightLong;
                case YuanpeiAttackId.LightningMark:   return s.playerLingeringArea;
                case YuanpeiAttackId.MultiAoE:        return s.arenaHasGoodFloor;
                case YuanpeiAttackId.Shockwave:       return s.playerDistance <= 4.5f;
                case YuanpeiAttackId.BodyCharge:      return (s.playerDistance >= 5f && s.playerDistance <= 12f) || s.energy < 25f;
                case YuanpeiAttackId.ChargeLine:      return s.playerDistance >= 9f;                    // reward it at range
                case YuanpeiAttackId.ChargeCrush:     return s.playerLingeringArea;                     // punish a camper
                case YuanpeiAttackId.OrbitDash:       return s.playerDistance >= 5f && s.playerDistance <= 13f;
                default: return false;
            }
        }
    }
}
