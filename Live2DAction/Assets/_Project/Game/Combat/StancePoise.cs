using UnityEngine;
using Live2DAction.Core;

namespace Live2DAction.Combat
{
    // Souls-like "poise/架勢" bar (2026-08-17, explicit user request: "想要製作斬殺系統，像魂類遊
    // 戲一樣，有架勢條，滿格會陷入僵直，按下f進行斬殺"). Deliberately a separate component from
    // Health rather than folding this into it - Health already has its own single responsibility
    // (HP/death) shared by every damageable thing in the game (portal-teleported characters,
    // training dummies, etc.), most of which will never have a stance bar at all; this only goes
    // on whoever should support the stagger/execution mechanic.
    //
    // 2026-08-17 follow-up, explicit user request ("敵我雙方都套用架式條 處刑 蹲下動作這套機
    // 制") - originally only accumulated from the PLAYER's own hits (this was Player4-only at the
    // time). Now that BOTH the player and Player4 carry this component, "builds from the
    // player's hits specifically" no longer makes sense as a rule - the correct general rule is
    // "builds from whatever hit THIS character", full stop, since Health.Damaged only ever fires
    // from a real incoming attack by an opponent (nothing in this codebase damages its own
    // character), so no source-identity filtering is needed at all any more.
    [RequireComponent(typeof(Health))]
    public class StancePoise : MonoBehaviour
    {
        // 2026-08-18, explicit user request ("架式條改為60") - lowered from the original 100
        // (which happened to exactly match Health's own default maxHealth, meaning a plain
        // combo would usually kill before the stance bar ever filled - see this session's own
        // "balance coincidence" note) so the stagger window is reliably reachable before death.
        [SerializeField] private float maxStance = 60f;

        // How long the stagger window stays open before the character recovers on its own if
        // never executed - a Souls-like stagger isn't "stunned forever until punished", it's a
        // limited opening. EnemyAI treats CurrentState == Staggered the same way regardless of
        // whether this timer or ExecutionAbility is what eventually ends it.
        [SerializeField] private float staggerDurationSeconds = 6f;

        // 2026-08-18, explicit user request ("一段時間沒有受到攻擊就回復(符合魂類設計)") - real
        // Souls-style poise/stance passively drains back down once the character stops being hit
        // for a while, instead of staying banked forever - a slow chip-away combo should mostly
        // wash out between engagements rather than permanently stacking toward a stagger over an
        // entire fight. Regen only runs while NOT staggered (see Update) - once the bar is full
        // and the character is staggered, this timer/regen is irrelevant until EndStagger.
        [SerializeField] private float regenDelaySeconds = 3f;
        [SerializeField] private float regenPerSecond = 20f;

        private Health _health;
        private float _currentStance;
        private float _staggerElapsed;
        private float _timeSinceLastHit;

        public bool IsStaggered { get; private set; }
        public float CurrentStance => _currentStance;
        public float MaxStance => maxStance;

        private void Awake()
        {
            _health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            _health.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            _health.Damaged -= OnDamaged;
        }

        private void Update()
        {
            if (IsStaggered)
            {
                _staggerElapsed += Time.deltaTime;
                if (_staggerElapsed >= staggerDurationSeconds)
                {
                    EndStagger();
                }
                return;
            }

            if (_currentStance <= 0f)
            {
                return;
            }

            _timeSinceLastHit += Time.deltaTime;
            if (_timeSinceLastHit >= regenDelaySeconds)
            {
                _currentStance = Mathf.Max(0f, _currentStance - regenPerSecond * Time.deltaTime);
            }
        }

        private void OnDamaged(DamageInfo info)
        {
            if (IsStaggered || _health.IsDead)
            {
                return;
            }

            _timeSinceLastHit = 0f;
            _currentStance = Mathf.Min(maxStance, _currentStance + info.Amount);
            if (_currentStance >= maxStance)
            {
                IsStaggered = true;
                _staggerElapsed = 0f;
            }
        }

        // Called by ExecutionAbility once the finishing blow lands, or by the stagger-duration
        // timeout above - either way, the character leaves the stagger window and stance starts
        // accumulating from 0 again.
        public void EndStagger()
        {
            IsStaggered = false;
            _currentStance = 0f;
            _staggerElapsed = 0f;
            _timeSinceLastHit = 0f;
        }
    }
}
