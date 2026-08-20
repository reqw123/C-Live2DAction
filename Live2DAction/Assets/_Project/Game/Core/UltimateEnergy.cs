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

        // 2026-08-20, explicit user request for the flight-energy instance specifically
        // ("設計為飛行體力500 只有在閒置3秒沒有消耗體力後才會逐漸恢復體力") - 0 (default) preserves
        // the original always-regenerating behavior every other instance of this class already
        // relies on (the ultimate skill's own energy, wired completely independently, was never
        // asked to change and shouldn't - see this class's own header comment on being reused
        // generically). Set only on the flight instance via CharacterMovement's flightEnergy
        // reference.
        [SerializeField] private float regenIdleDelaySeconds = 0f;

        private float _currentEnergy;
        private float _regenTimer;

        // Seconds since the last Drain() call - Drain() is the only way this class's energy
        // decreases, so tracking it directly here (reset on every call) is simpler than
        // HealthRegeneration's polling approach (that one polls Health.CurrentHealth because it's
        // a SEPARATE component from Health with an AddComponent-then-wire ordering hazard - this
        // class owns _currentEnergy directly, no such hazard exists here).
        private float _timeSinceLastDrain;

        public float CurrentEnergy => _currentEnergy;
        public float MaxEnergy => maxEnergy;
        public bool IsFull => _currentEnergy >= maxEnergy;

        private void Update()
        {
            _timeSinceLastDrain += Time.deltaTime;

            if (_currentEnergy >= maxEnergy)
            {
                return;
            }

            // regenIdleDelaySeconds=0 (every instance except flight) never blocks here -
            // _timeSinceLastDrain starts at 0 and only grows, so it's never < 0. For an instance
            // that DOES set a real delay, this holds off the regen timer itself from even
            // accumulating while still within the "just used it" window, rather than letting it
            // accumulate silently and dump a burst the instant the delay passes.
            if (_timeSinceLastDrain < regenIdleDelaySeconds)
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
        // to zero. Doesn't touch _regenTimer (the interval-tick countdown keeps counting
        // independently regardless, same reasoning as AddEnergy).
        //
        // 2026-08-20, explicit user request ("只有在閒置3秒沒有消耗體力後才會逐漸恢復體力") - DOES
        // reset _timeSinceLastDrain, unlike _regenTimer above - for any instance with a real
        // regenIdleDelaySeconds set (currently just flight), every Drain() call pushes the "may
        // regen again" moment back out, so draining and regen are no longer simply independent
        // and netting against each other (the old model, still exactly true for every OTHER
        // instance that leaves regenIdleDelaySeconds at 0) - continuous drain now fully suppresses
        // regen for as long as it keeps happening, then regen only starts once idle.
        public void Drain(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            _currentEnergy = Mathf.Max(0f, _currentEnergy - amount);
            _timeSinceLastDrain = 0f;
        }
    }
}
