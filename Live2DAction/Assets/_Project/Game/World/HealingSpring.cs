using UnityEngine;
using Live2DAction.Characters;
using Live2DAction.Core;
using Live2DAction.Input;

namespace Live2DAction.World
{
    // 2026-08-18, explicit user request ("幫我設置一個泉水點，待在範圍內快速回復血量和能量條至滿
    // 格(只有玩家適用)") - a bonfire/spring-style rest point: standing inside the trigger heals
    // Health and refills UltimateEnergy at a fast per-second rate (not an instant snap - "快速"
    // reads as "quick", not "immediate", and a visibly filling bar is better feedback that
    // something is actually happening than a silent teleport-to-full). Player-only, same
    // GetComponentInParent<PlayerInputProvider> signal Portal.cs and StancePoise's own history
    // already use for "is this actually the human player" - an enemy that wandered into the
    // radius shouldn't get free healing.
    //
    // 2026-08-20, explicit user request ("請讓泉水點也支援回復體力條") - also refills flight
    // energy ("體力條") now, the SEPARATE UltimateEnergy instance CharacterMovement's own flight
    // system drains (see flightEnergyPerSecond's own comment for why this needs its own rate and
    // its own resolved reference rather than reusing the ultimate skill's).
    //
    // Deliberately tracks the player via Enter/Exit rather than re-resolving Health/UltimateEnergy
    // every frame - same reasoning Portal.cs's own _playerInside field has: cheap to cache once,
    // no reason to repeat a GetComponentInParent lookup 60 times a second while standing still.
    [RequireComponent(typeof(Collider))]
    public class HealingSpring : MonoBehaviour
    {
        // 2026-08-19, explicit user request ("幫我把泉水的生命每秒回復量增加") - raised from the
        // original 40 (12.5s to fill Player's 500 MaxHealth from empty) to 100 (5s to fill),
        // chosen by the user from a set of options as "接近『快速』的上限" - still a visibly
        // filling bar, not an instant snap-to-full. energyPerSecond untouched (out of scope).
        [SerializeField] private float healPerSecond = 100f;
        [SerializeField] private float energyPerSecond = 40f;

        // 2026-08-20, explicit user request ("請讓泉水點也支援回復體力條") - flight energy
        // ("體力條") is a SEPARATE UltimateEnergy instance from the ultimate skill's own
        // ("能量條" - see CharacterMovement.FlightEnergy's own comment for why a plain
        // GetComponentInParent<UltimateEnergy>() can't tell the two apart). Its own rate, not
        // reusing energyPerSecond - flight energy's max (500) is 5x the ultimate skill's (100),
        // so the same flat rate would fill 5x slower and no longer read as "快速". Picked to match
        // this class's own established "5 seconds to fill from empty" pace (healPerSecond's own
        // comment: 100/sec fills Player's 500 MaxHealth in 5s) - 500 flight energy / 100 per
        // second = the same 5s fill time.
        [SerializeField] private float flightEnergyPerSecond = 100f;

        private Health _playerHealth;
        private UltimateEnergy _playerEnergy;
        private UltimateEnergy _playerFlightEnergy;

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
            if (other.GetComponentInParent<PlayerInputProvider>() == null)
            {
                return;
            }

            _playerHealth = other.GetComponentInParent<Health>();
            _playerEnergy = other.GetComponentInParent<UltimateEnergy>();
            _playerFlightEnergy = other.GetComponentInParent<CharacterMovement>()?.FlightEnergy;
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponentInParent<PlayerInputProvider>() == null)
            {
                return;
            }

            _playerHealth = null;
            _playerEnergy = null;
            _playerFlightEnergy = null;
        }

        private void Update()
        {
            if (_playerHealth != null && !_playerHealth.IsDead)
            {
                _playerHealth.Heal(healPerSecond * Time.deltaTime);
            }

            if (_playerEnergy != null)
            {
                _playerEnergy.AddEnergy(energyPerSecond * Time.deltaTime);
            }

            if (_playerFlightEnergy != null)
            {
                _playerFlightEnergy.AddEnergy(flightEnergyPerSecond * Time.deltaTime);
            }
        }
    }
}
