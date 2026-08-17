using UnityEngine;
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
    // Deliberately tracks the player via Enter/Exit rather than re-resolving Health/UltimateEnergy
    // every frame - same reasoning Portal.cs's own _playerInside field has: cheap to cache once,
    // no reason to repeat a GetComponentInParent lookup 60 times a second while standing still.
    [RequireComponent(typeof(Collider))]
    public class HealingSpring : MonoBehaviour
    {
        [SerializeField] private float healPerSecond = 40f;
        [SerializeField] private float energyPerSecond = 40f;

        private Health _playerHealth;
        private UltimateEnergy _playerEnergy;

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
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponentInParent<PlayerInputProvider>() == null)
            {
                return;
            }

            _playerHealth = null;
            _playerEnergy = null;
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
        }
    }
}
