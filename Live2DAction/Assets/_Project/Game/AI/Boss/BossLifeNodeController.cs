using UnityEngine;
using Live2DAction.Core;

namespace Live2DAction.AI.Boss
{
    // spec WUSHI_COMBAT_ENGINEERING_SPEC.md §8.2 (M4 項目 7). The 武士's two Deathblow life nodes.
    //
    // Problem being fixed (§8.1): ExecutionAbility only deals a fraction of CURRENT health, so a
    // finisher can never end the fight on its own; and the 武士 has permanentDeath = false, so it
    // auto-revives 5s after any death. That contradicts "處決" and "boss fight over".
    //
    // With this component on the boss, a finisher routes through IExecutable instead of raw damage:
    // the first deathblow spends a node and pushes the boss to phase 2 (health optionally restored),
    // the second (last) deathblow permanently kills it. A plain enemy has no BossLifeNodeController
    // and ExecutionAbility keeps its own damage path for those.
    [RequireComponent(typeof(BossStateMachine))]
    public class BossLifeNodeController : MonoBehaviour, IExecutable
    {
        [Tooltip("How many Deathblow nodes the boss starts with (武士 = 2: one phase transition, then a kill).")]
        [SerializeField] private int maxDeathblowNodes = 2;

        [SerializeField] private int remainingNodes = 2;

        [Tooltip("Restore the boss to full health when a Deathblow moves it to the next phase " +
                 "(spec §8.3 recommends true for the first pass).")]
        [SerializeField] private bool restoreHealthOnPhaseChange = true;

        [Tooltip("How long the boss is held / invulnerable during the finisher windup. Keep this " +
                 ">= the executor's own executionAnimationSeconds so the deathblow lands mid-hold.")]
        [SerializeField] private float executionWindupSeconds = 1.7f;

        public int RemainingNodes => remainingNodes;
        public int MaxDeathblowNodes => maxDeathblowNodes;

        // Fires with the node count AFTER a deathblow - for a boss health-bar / node pip UI.
        public event System.Action<int> NodeConsumed;

        private BossStateMachine _boss;
        private bool _executing;

        private void Awake()
        {
            _boss = GetComponent<BossStateMachine>();
            if (maxDeathblowNodes < 1) maxDeathblowNodes = 1;
            remainingNodes = Mathf.Clamp(remainingNodes, 0, maxDeathblowNodes);
        }

        public bool CanBeExecuted(GameObject executor)
        {
            return !_executing
                && remainingNodes > 0
                && _boss != null
                && _boss.CurrentState == BossState.PostureBroken;
        }

        public void OnExecutionStarted(GameObject executor)
        {
            if (_boss == null) return;
            _executing = true;
            _boss.BeginExecutionHold(executionWindupSeconds);
        }

        public ExecutionOutcome ResolveExecution(GameObject executor)
        {
            _executing = false;
            var (after, outcome) = ExecutionNodeLogic.Deathblow(remainingNodes);
            if (outcome == ExecutionOutcome.Refused)
            {
                _boss?.EndExecutionHold();
                return outcome;
            }

            remainingNodes = after;
            NodeConsumed?.Invoke(remainingNodes);

            if (outcome == ExecutionOutcome.PhaseTransition)
            {
                _boss.DeathblowPhaseTransition(restoreHealthOnPhaseChange);
            }
            else
            {
                _boss.DeathblowFinalKill(executor);
            }
            return outcome;
        }

        // Editor / test seam.
        public void EditorConfigure(int max, int remaining, bool restoreHealth)
        {
            maxDeathblowNodes = Mathf.Max(1, max);
            remainingNodes = Mathf.Clamp(remaining, 0, maxDeathblowNodes);
            restoreHealthOnPhaseChange = restoreHealth;
        }
    }
}
