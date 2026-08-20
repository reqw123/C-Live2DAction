using UnityEngine;
using UnityEngine.InputSystem;
using Live2DAction.Core;

namespace Live2DAction.Combat
{
    // Souls-like execution/deathblow (2026-08-17, explicit user request: "想要製作斬殺系統，像魂
    // 類遊戲一樣，有架勢條，滿格會陷入僵直，按下f進行斬殺並使用動作Flying Kick"). Reads the F key
    // directly via the new Input System rather than going through IInputCommand - same pattern
    // Portal.cs already uses for its own single-purpose E-key check, so this doesn't need to
    // extend the shared interface (and, unlike AttackPressed/DodgePressed/etc., EnemyAI would
    // never have anything meaningful to say for "ExecutePressed" - only the human player ever
    // performs an execution).
    public class ExecutionAbility : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private Transform attackOrigin;
        [SerializeField] private float executionRange = 2.5f;

        // 2026-08-18, real bug report ("觸發斬殺時應該要有一段動畫，動畫跑完後才真正扣除血量並消
        // 失") - the deathblow used to land instantly on the same frame Execute() ran, while
        // Flying Kick had barely started playing. Matches FlyingKick.fbx's own measured length
        // (see SpecialMoveAnimatorSetup's own comment/this session's earlier clip-length check) -
        // kept as a separate tunable rather than reading AnimationClip.length directly so this
        // stays correct even if the wired clip/its Animator state speed ever changes without
        // this file being touched.
        [SerializeField] private float executionAnimationSeconds = 1.5f;

        // 2026-08-18, explicit user request ("處決不要改成殺死對方 而是扣除對方總血量50%") -
        // supersedes the original "instant kill" design. Deducted through the normal
        // Health.ApplyDamage pipeline, so if the remaining health happens to be below this
        // amount it still dies (a natural combat outcome, not specially prevented), just no
        // longer GUARANTEED lethal on its own.
        //
        // 2026-08-18 follow-up, explicit user request ("斬殺時改為扣除對方當前血量50%") - changed
        // from a fraction of MaxHealth to a fraction of CurrentHealth (whatever HP the target
        // actually has left at the moment of the execution, not its full-health baseline). This
        // makes an execution ALWAYS take off a proportional chunk of however much fight is
        // actually left in the target, rather than a fixed MaxHealth-based amount that becomes a
        // smaller and smaller fraction of remaining HP as the fight wears on (e.g. a target
        // already at 20% HP would previously still take a 50%-of-max hit, guaranteed to kill it -
        // now it takes 50% of that remaining 20%, leaving it alive at 10%).
        [SerializeField] private float executionDamagePercentOfCurrentHealth = 0.5f;

        private static readonly int ExecuteTrigger = Animator.StringToHash("Execute");

        private StancePoise _pendingTarget;
        private float _elapsed;

        public bool IsExecuting => _pendingTarget != null;

        private void Awake()
        {
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

            if (Keyboard.current == null || !Keyboard.current.fKey.wasPressedThisFrame)
            {
                return;
            }

            StancePoise target = FindStaggeredTargetInRange();
            if (target == null)
            {
                return;
            }

            BeginExecution(target);
        }

        private void TickPendingExecution()
        {
            // The target could theoretically go away mid-animation (destroyed by something else
            // entirely) - bail out cleanly rather than throwing on a null reference on the final
            // ApplyDamage call.
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
        }

        private void BeginExecution(StancePoise target)
        {
            _pendingTarget = target;
            _elapsed = 0f;

            if (animator != null)
            {
                animator.SetTrigger(ExecuteTrigger);
            }
        }

        private StancePoise FindStaggeredTargetInRange()
        {
            Collider[] candidates = Physics.OverlapSphere(attackOrigin.position, executionRange);
            foreach (Collider candidate in candidates)
            {
                if (candidate == null || candidate.transform.root == transform.root)
                {
                    continue;
                }

                if (candidate.TryGetComponent(out StancePoise stance) && stance.IsStaggered)
                {
                    return stance;
                }

                // A staggered target's stance bar usually lives on its root, not necessarily the
                // exact collider a sphere overlap happened to catch (e.g. a CharacterController
                // vs. a separate hit-collider on a child) - GetComponentInParent covers that the
                // same way ExecutionAbility's own sibling systems already do (Portal's
                // GetComponentInParent<PlayerInputProvider>, StancePoise's own damage-source
                // check).
                StancePoise parentStance = candidate.GetComponentInParent<StancePoise>();
                if (parentStance != null && parentStance.IsStaggered)
                {
                    return parentStance;
                }
            }

            return null;
        }

        // Deals executionDamagePercentOfCurrentHealth of the target's CurrentHealth (not
        // MaxHealth - see that field's own comment) - no longer a guaranteed kill. Called only
        // once the Flying Kick animation has actually finished playing (see
        // TickPendingExecution) - the target stays staggered/kneeling for the whole windup, only
        // actually taking the hit on impact.
        private void ResolveExecution(StancePoise target)
        {
            if (target.TryGetComponent(out Health health) && !health.IsDead)
            {
                float damage = health.CurrentHealth * executionDamagePercentOfCurrentHealth;
                health.ApplyDamage(new DamageInfo(damage, target.transform.position, Vector3.zero, gameObject));
            }

            target.EndStagger();
        }
    }
}
