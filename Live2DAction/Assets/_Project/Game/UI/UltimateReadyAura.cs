using UnityEngine;
using Live2DAction.Core;

namespace Live2DAction.UI
{
    // Visible only while UltimateEnergy.IsFull - the "necessary skill is ready" indicator.
    // Player-only (not wired to Enemy the way HealthRegeneration/the energy bar itself are) -
    // Enemy has no UltimateAbility and EnemyAI never presses the ultimate key, so a "ready" aura
    // on an enemy that can never act on it would read as a UI bug, not a hint.
    //
    // 2026-08-16..08-31: this used to ALSO drive a coiling electric-blue LineRenderer bolt
    // (奇犽風閃電繞圈). 2026-08-31 (追加81), explicit user request ("移除舊特效(白色一圈的那)") -
    // the lightning is gone; the flame aura (追加79, source-clip flipbook) is the only ready-state
    // layer now. This component just SetActive-toggles that child on energy.IsFull. Its own
    // particle systems loop; its AudioSource (playOnAwake) re-fires the "charged" cue each time
    // it's switched on.
    public class UltimateReadyAura : MonoBehaviour
    {
        [SerializeField] private UltimateEnergy energy;
        [SerializeField] private GameObject flameAura;

        private void Update()
        {
            if (energy == null || flameAura == null)
            {
                return;
            }

            bool ready = energy.IsFull;
            if (flameAura.activeSelf != ready)
            {
                flameAura.SetActive(ready);
            }
        }
    }
}
