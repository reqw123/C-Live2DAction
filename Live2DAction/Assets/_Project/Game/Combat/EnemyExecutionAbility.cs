using UnityEngine;
using Live2DAction.AI;
using Live2DAction.Core;

namespace Live2DAction.Combat
{
    // AI-driven counterpart to ExecutionAbility (2026-08-17, explicit user request: "敵我雙方
    // 都套用架式條 處刑...這套機制" - the stagger/execution mechanic now runs both directions).
    // ExecutionAbility itself stays F-key-driven and player-only (an AI has no key to press,
    // and this codebase's existing convention - see ExecutionAbility's own comment - is that
    // input-shaped abilities and AI-shaped abilities are separate small components even when
    // they end up doing almost the same thing, same split as UltimateAbility/
    // EnemyUltimateAbility). Reuses the same "Execute" trigger/Flying Kick animation as the
    // player's version (see SpecialMoveAnimatorSetup, now wired into both controllers) rather
    // than inventing a second finisher move - no distinct enemy execution animation was
    // provided, and re-using the same clip both directions matches how CrossPunch/HookPunch/
    // Uppercut/MmaKick are already shared between the two rigs.
    [RequireComponent(typeof(EnemyAI))]
    [RequireComponent(typeof(PlayerCombat))]
    public class EnemyExecutionAbility : MonoBehaviour
    {
        [SerializeField] private StancePoise targetStance;
        [SerializeField] private Animator animator;
        [SerializeField] private float executionRange = 2.5f;
        [SerializeField] private Transform attackOrigin;

        // See ExecutionAbility's own field comment - same FlyingKick clip, same reasoning.
        [SerializeField] private float executionAnimationSeconds = 1.5f;

        // 2026-08-18, explicit user request ("處決不要改成殺死對方 而是扣除對方總血量50%") - see
        // ExecutionAbility's own matching field comment, same reasoning applied to the AI side.
        [SerializeField] private float executionDamagePercentOfMaxHealth = 0.5f;

        private EnemyAI _enemyAI;
        private PlayerCombat _combat;
        private static readonly int ExecuteTrigger = Animator.StringToHash("Execute");

        private StancePoise _pendingTarget;
        private float _elapsed;

        private void Awake()
        {
            _enemyAI = GetComponent<EnemyAI>();
            _combat = GetComponent<PlayerCombat>();
            if (attackOrigin == null)
            {
                attackOrigin = transform;
            }
        }

        private void Update()
        {
            if (_pendingTarget != null)
            {
                TickPendingExecution();
                return;
            }

            if (targetStance == null || !targetStance.IsStaggered)
            {
                return;
            }

            // Same "is the target actually within reach" signal EnemyUltimateAbility uses
            // (EnemyAI.CurrentState == Attacking already means the real attack capsule reaches
            // the target - see EnemyAI.ResolveEffectiveAttackRange) rather than a second,
            // independently-tuned executionRange distance check that could drift out of sync
            // with it. executionRange is kept as a fallback/gizmo-free sanity radius only in
            // case CurrentState is ever Attacking from something other than proximity in the
            // future - re-checked here defensively, cheap and harmless when it already agrees.
            if (_enemyAI.CurrentState != EnemyState.Attacking)
            {
                return;
            }

            if (Vector3.Distance(attackOrigin.position, targetStance.transform.position) > executionRange)
            {
                return;
            }

            BeginExecution(targetStance);
        }

        private void TickPendingExecution()
        {
            if (_pendingTarget == null)
            {
                return;
            }

            _elapsed += Time.deltaTime;
            if (_elapsed < executionAnimationSeconds)
            {
                return;
            }

            ResolveExecution(_pendingTarget);
            _pendingTarget = null;
            _combat.enabled = true;
        }

        private void BeginExecution(StancePoise target)
        {
            _pendingTarget = target;
            _elapsed = 0f;

            // Same reasoning as EnemyUltimateAbility - the normal combo shouldn't also fire
            // (and fight over the same Animator triggers/attackOrigin) while this special
            // animation is playing.
            _combat.enabled = false;

            if (animator != null)
            {
                animator.SetTrigger(ExecuteTrigger);
            }
        }

        // See ExecutionAbility.ResolveExecution's own comment - only actually kills/ends the
        // stagger once the windup has fully played out.
        private void ResolveExecution(StancePoise target)
        {
            if (target.TryGetComponent(out Health health) && !health.IsDead)
            {
                float damage = health.MaxHealth * executionDamagePercentOfMaxHealth;
                health.ApplyDamage(new DamageInfo(damage, target.transform.position, Vector3.zero, gameObject));
            }

            target.EndStagger();
        }
    }
}
