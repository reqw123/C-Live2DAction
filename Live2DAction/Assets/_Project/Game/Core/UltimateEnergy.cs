using UnityEngine;

namespace Live2DAction.Core
{
    // Blue "ultimate skill" energy meter (2026-08-13, explicit user request: 初始0, 每三秒回
    //復5點, 最大100). Pure regen-over-time - nothing here decides WHEN it's consumed or what
    // happens at full charge, that's UltimateAbility's job (same separation-of-concerns
    // reasoning as Health not knowing about combat, WorldSpaceHealthBar not knowing about
    // Health's internals beyond CurrentHealth/MaxHealth).
    public class UltimateEnergy : MonoBehaviour
    {
        [SerializeField] private float maxEnergy = 100f;
        [SerializeField] private float regenAmount = 5f;
        [SerializeField] private float regenIntervalSeconds = 3f;

        private float _currentEnergy;
        private float _regenTimer;

        public float CurrentEnergy => _currentEnergy;
        public float MaxEnergy => maxEnergy;
        public bool IsFull => _currentEnergy >= maxEnergy;

        private void Update()
        {
            if (_currentEnergy >= maxEnergy)
            {
                return;
            }

            // Accumulates deltaTime and steps down by the full interval each time it's
            // crossed (rather than resetting to 0), so a single very long frame that jumps
            // past multiple intervals still grants every tick it earned instead of losing the
            // remainder - same pattern as ComboAttackState accumulating _elapsed continuously.
            _regenTimer += Time.deltaTime;
            while (_regenTimer >= regenIntervalSeconds && _currentEnergy < maxEnergy)
            {
                _regenTimer -= regenIntervalSeconds;
                _currentEnergy = Mathf.Min(maxEnergy, _currentEnergy + regenAmount);
            }
        }

        // Called by UltimateAbility the moment the ultimate activates. Resetting the regen
        // timer too (not just the energy value) means the next tick is a full interval away
        // rather than possibly landing a fraction of a second after activation.
        public void Consume()
        {
            _currentEnergy = 0f;
            _regenTimer = 0f;
        }

        // 2026-08-18, explicit user request ("泉水點...快速回復...能量條至滿格") - an external
        // source (HealingSpring) adding energy directly, distinct from the passive tick-based
        // regen above. Doesn't touch _regenTimer - the passive regen's own interval keeps
        // counting independently and just has less ground left to cover, same as how Health.Heal
        // doesn't interact with HealthRegeneration's own idle timer.
        public void AddEnergy(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            _currentEnergy = Mathf.Min(maxEnergy, _currentEnergy + amount);
        }

        // 2026-08-18, explicit user request (flight: "按住鍵自由飛行...消耗能量條") - the
        // inverse of AddEnergy, for a continuous per-frame cost rather than a one-shot Consume()
        // to zero. Doesn't touch _regenTimer either, same reasoning as AddEnergy - draining and
        // the passive regen tick are independent and just net against each other, no special
        // interaction needed (this is also how CharacterMovement's flight code and this class's
        // own Update() end up working together correctly without either knowing about the
        // other - drain while flying, regen resumes on its own once flight stops).
        public void Drain(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            _currentEnergy = Mathf.Max(0f, _currentEnergy - amount);
        }
    }
}
