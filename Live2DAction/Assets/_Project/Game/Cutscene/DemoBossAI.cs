using UnityEngine;

namespace Live2DAction.Cutscene
{
    // 2026-09-01, /grill-with-docs exploration — see Docs/BOSS_INTRO_EXPLORATION.md. NOT in the
    // shipped game. A do-nothing stand-in for the real BossStateMachine, purely so BossIntroManager
    // has a real Behaviour to flip off during the cutscene and back on after. Logs its state
    // changes so the hand-off is visible in the Console during the demo.
    public class DemoBossAI : MonoBehaviour
    {
        private void OnEnable()
        {
            Debug.Log("[BossIntro demo] DemoBossAI ENABLED - the fight would begin now.");
        }

        private void OnDisable()
        {
            Debug.Log("[BossIntro demo] DemoBossAI DISABLED - cutscene owns the frame.");
        }
    }
}
