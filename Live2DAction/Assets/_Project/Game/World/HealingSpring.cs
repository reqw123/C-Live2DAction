using System.Collections.Generic;
using UnityEngine;
using Live2DAction.Characters;
using Live2DAction.Core;
using Live2DAction.Input;

namespace Live2DAction.World
{
    // 2026-08-18, explicit user request ("幫我設置一個泉水點，待在範圍內快速回復血量和能量條至滿
    // 格(只有玩家適用)") - a bonfire/spring-style rest point: standing inside the trigger heals
    // Health and refills every energy meter at a fast per-second rate (not an instant snap - "快速"
    // reads as "quick", not "immediate", and a visibly filling bar is better feedback that
    // something is actually happening than a silent teleport-to-full).
    //
    // "只有玩家" is enforced via the GetComponentInParent<PlayerInputProvider> signal Portal.cs
    // and StancePoise's own history already use for "is this actually a player-controlled
    // character" - an enemy that wandered into the radius shouldn't get free healing.
    //
    // 2026-08-20, explicit user request ("請讓泉水點也支援回復體力條") - also refills flight energy.
    //
    // 2026-08-31, explicit user request ("讓cat也可以吃到泉水恢復效果") - the cat shares the
    // PlayerInputProvider signal (rule 8) so it already passed the "is a player" gate, but two
    // things were wrong for it: (a) a single-slot cache (_playerHealth etc.) meant only ONE
    // character could be served at a time - if the cat and the player were both inside, whoever
    // entered last won; (b) `GetComponentInParent<UltimateEnergy>()` only ever found ONE meter, so
    // the cat's dedicated SkillEnergy (大招能量, a child object) was never refilled, and its flight
    // energy on the root got hit by BOTH rates. Now every character inside is tracked, and ALL of
    // its UltimateEnergy instances are refilled - the one CharacterMovement uses for flight at
    // flightEnergyPerSecond, every other one (ultimate skill / cat skill) at energyPerSecond.
    [RequireComponent(typeof(Collider))]
    public class HealingSpring : MonoBehaviour
    {
        // 2026-08-19 ("幫我把泉水的生命每秒回復量增加") - 100/sec (5s to fill the player's 500 max).
        [SerializeField] private float healPerSecond = 100f;
        [SerializeField] private float energyPerSecond = 40f;
        // Flight energy's max (500) is 5x the skill meters' (100), so it needs its own faster rate
        // to still read as "快速" (500 / 100 per second = the same 5s fill time).
        [SerializeField] private float flightEnergyPerSecond = 100f;

        private sealed class Occupant
        {
            public GameObject Root;
            public Health Health;
            public UltimateEnergy FlightEnergy;         // may be null
            public readonly List<UltimateEnergy> OtherEnergies = new List<UltimateEnergy>();
        }

        // Keyed by the character root (the GameObject carrying PlayerInputProvider) so re-entering
        // an overlapping trigger collider doesn't stack duplicates.
        private readonly Dictionary<GameObject, Occupant> _occupants = new Dictionary<GameObject, Occupant>();

        private void Reset()
        {
            Collider trigger = GetComponent<Collider>();
            if (trigger != null)
            {
                trigger.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            PlayerInputProvider input = other.GetComponentInParent<PlayerInputProvider>();
            if (input == null)
            {
                return;
            }

            GameObject root = input.gameObject;
            if (_occupants.ContainsKey(root))
            {
                return;
            }

            var occ = new Occupant
            {
                Root = root,
                Health = root.GetComponentInChildren<Health>(),
                FlightEnergy = root.GetComponent<CharacterMovement>()?.FlightEnergy,
            };
            foreach (UltimateEnergy energy in root.GetComponentsInChildren<UltimateEnergy>(true))
            {
                if (energy != occ.FlightEnergy)
                {
                    occ.OtherEnergies.Add(energy);
                }
            }
            _occupants[root] = occ;
        }

        private void OnTriggerExit(Collider other)
        {
            PlayerInputProvider input = other.GetComponentInParent<PlayerInputProvider>();
            if (input == null)
            {
                return;
            }
            _occupants.Remove(input.gameObject);
        }

        private void Update()
        {
            if (_occupants.Count == 0)
            {
                return;
            }

            float dt = Time.deltaTime;
            foreach (Occupant occ in _occupants.Values)
            {
                if (occ.Health != null && !occ.Health.IsDead)
                {
                    occ.Health.Heal(healPerSecond * dt);
                }
                if (occ.FlightEnergy != null)
                {
                    occ.FlightEnergy.AddEnergy(flightEnergyPerSecond * dt);
                }
                for (int i = 0; i < occ.OtherEnergies.Count; i++)
                {
                    if (occ.OtherEnergies[i] != null)
                    {
                        occ.OtherEnergies[i].AddEnergy(energyPerSecond * dt);
                    }
                }
            }
        }
    }
}
