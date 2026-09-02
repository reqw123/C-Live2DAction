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

        // 2026-09-01, user request ("PLAYER則做為 F 處決加入" the 連續刺刀 Meshy clip) - the Player
        // and 中立者1 SHARE the same Maya NewAnimator controller, so its single "Execute" state
        // (Flying Kick) can't just be repointed at the thrust without also changing 中立者1's
        // execution. The Player is wired to its own "ExecuteThrust" trigger/state instead; every
        // other ExecutionAbility (中立者1) keeps the default "Execute"/Flying Kick. EnemyExecutionAbility
        // is a separate class on a separate controller and is unaffected either way.
        [SerializeField] private string executeTriggerName = "Execute";

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

        // 2026-09-01, spec item 7 (M4). A target that implements IExecutable (a multi-phase boss with
        // Deathblow life nodes - BossLifeNodeController) owns its own execution outcome; this ability
        // just plays the finisher and calls into it. For everything ELSE (ordinary enemies, which
        // have no IExecutable), the fallback below still runs - by default the 2026-08-18
        // "扣除當前血量50%" behaviour, unchanged. Flip this on to make a finisher an outright kill for
        // non-executable targets instead (spec §8.2 "普通敵人處決直接死亡").
        [SerializeField] private bool instantKillNonExecutableTargets;

        private int _executeTrigger;

        private StancePoise _pendingTarget;
        private IExecutable _pendingExecutable;
        private float _elapsed;

        public bool IsExecuting => _pendingTarget != null;

        private void Awake()
        {
            if (attackOrigin == null)
            {
                attackOrigin = transform;
            }

            _executeTrigger = Animator.StringToHash(
                string.IsNullOrEmpty(executeTriggerName) ? "Execute" : executeTriggerName);
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
            _pendingExecutable = null;
        }

        private void BeginExecution(StancePoise target)
        {
            _pendingTarget = target;
            _elapsed = 0f;

            // 2026-09-01 (spec item 2): a finisher owns the player fully - drop any guard first so
            // the guard volume / pose / slowdown don't linger through the execution animation.
            GetComponent<PlayerGuard>()?.CancelDefenseAction();

            // 2026-09-01, user report ("很像是在打空氣...要先鎖定好目標的方向再進行施展") - the
            // execution clip (連續刺刀, a directional thrust flurry) played in whatever direction the
            // player happened to be facing when F was pressed, so it routinely stabbed past a target
            // standing off to the side. Snap-face the victim (flattened) before the anim starts so
            // the whole finisher points AT them. The clip is imported in-place (lockRootPositionXZ),
            // so this rotation is all the aiming it needs.
            Vector3 toTarget = target.transform.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            }

            // spec item 7 - a Deathblow-capable target (boss with life nodes) takes over its own
            // outcome. Notify it that the finisher has started so it can hold + go invulnerable for
            // the windup; ResolveExecution below then routes the deathblow into it.
            var executable = target.GetComponentInParent<IExecutable>();
            if (executable != null && executable.CanBeExecuted(gameObject))
            {
                _pendingExecutable = executable;
                executable.OnExecutionStarted(gameObject);
            }

            if (animator != null)
            {
                animator.SetTrigger(_executeTrigger);
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
            // spec item 7 - a Deathblow-capable target consumes a life node and drives its own phase
            // change / permanent death; it also owns ending its own stagger, so nothing else here.
            if (_pendingExecutable != null)
            {
                _pendingExecutable.ResolveExecution(gameObject);
                return;
            }

            if (target.TryGetComponent(out Health health) && !health.IsDead)
            {
                float damage = instantKillNonExecutableTargets
                    ? health.CurrentHealth
                    : health.CurrentHealth * executionDamagePercentOfCurrentHealth;
                health.ApplyDamage(new DamageInfo(damage, target.transform.position, Vector3.zero, gameObject));
            }

            target.EndStagger();
        }
    }
}
