using UnityEngine;
using Live2DAction.AI.Boss;

namespace Live2DAction.UI
{
    // 2026-08-28, explicit user request ("武士的狀態條應該只在戰鬥狀態才出現在螢幕") - the WushiBossHud
    // screen-space bars are hidden until 武士 is actually fighting, and hidden again once the fight
    // is over. "In combat" is the exact same condition BossStateMachine uses to drive its
    // CombatActive animator bool: any state except Dormant / Dead / Victory.
    //
    // Toggles Canvas.enabled (not GameObject.SetActive) so the child *BarFx components keep their
    // Update running while hidden - the bars are already at the right fill the instant the HUD
    // reappears, no snap-in.
    [RequireComponent(typeof(Canvas))]
    public class WushiBossHudVisibility : MonoBehaviour
    {
        [SerializeField] private BossStateMachine boss;

        private Canvas _canvas;

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
            if (_canvas != null)
            {
                _canvas.enabled = false; // hidden until the boss engages
            }
        }

        private void LateUpdate()
        {
            if (_canvas == null)
            {
                return;
            }

            bool inCombat = boss != null
                            && boss.CurrentState != BossState.Dormant
                            && boss.CurrentState != BossState.Dead
                            && boss.CurrentState != BossState.Victory
                            && boss.CurrentState != BossState.ReturnHome; // disengaged, jogging back

            if (_canvas.enabled != inCombat)
            {
                _canvas.enabled = inCombat;
            }
        }
    }
}
