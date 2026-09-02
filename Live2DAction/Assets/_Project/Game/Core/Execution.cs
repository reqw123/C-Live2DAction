using UnityEngine;

namespace Live2DAction.Core
{
    // spec WUSHI_COMBAT_ENGINEERING_SPEC.md §8 (M4 項目 7). What a deathblow does.
    public enum ExecutionOutcome
    {
        Refused,          // no node / not in a valid state - the finisher shouldn't have connected
        PhaseTransition,  // a Deathblow node spent, boss moves to the next phase and fights on
        Killed,           // the last node spent - permanent death, no revive
    }

    // Something a player finisher (ExecutionAbility) can deathblow. A plain enemy does NOT implement
    // this - ExecutionAbility keeps its own damage/kill path for anything that doesn't. Only a
    // multi-phase boss with Deathblow life nodes needs it.
    public interface IExecutable
    {
        // The finisher may begin (boss is posture-broken, a node remains, not already mid-execution).
        bool CanBeExecuted(GameObject executor);

        // Called when the finisher animation STARTS - lock / hold / grant i-frames for the windup.
        void OnExecutionStarted(GameObject executor);

        // Called when the finisher animation FINISHES - consume a node, drive the phase change or
        // the permanent kill, release the windup lock. Returns what happened.
        ExecutionOutcome ResolveExecution(GameObject executor);
    }

    // The Deathblow node bookkeeping, pulled out so it can be unit-tested without a boss FSM.
    public static class ExecutionNodeLogic
    {
        // remainingBefore = nodes left before this deathblow. Returns the new count and the outcome.
        public static (int remaining, ExecutionOutcome outcome) Deathblow(int remainingBefore)
        {
            if (remainingBefore <= 0)
            {
                return (0, ExecutionOutcome.Refused);
            }
            int after = remainingBefore - 1;
            return (after, after > 0 ? ExecutionOutcome.PhaseTransition : ExecutionOutcome.Killed);
        }
    }
}
