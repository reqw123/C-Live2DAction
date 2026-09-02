using UnityEngine;
using Unity.Cinemachine;

namespace Live2DAction.Cutscene
{
    // 2026-09-01, /grill-with-docs exploration — see Docs/BOSS_INTRO_EXPLORATION.md. NOT in the
    // shipped game.
    //
    // Sits on 武士. The intro Timeline's Signal Track fires OnBladeDrawSignal() at the apex of the
    // ready-stance (刀舉到頂點的那一幀) via a UnityEngine.Timeline.SignalReceiver whose reaction is
    // wired to this method. It fires the three "the blade is out" beats together:
    //   - blade-flash flipbook  (ParticleSystem.Play)
    //   - the metallic 鏘 draw sound  (AudioSource.Play)
    //   - a Cinemachine Impulse camera kick  (CinemachineImpulseSource.GenerateImpulse)
    // Every field is optional / null-safe so a half-wired scene still runs.
    public class BossSignalReceiver : MonoBehaviour
    {
        [Tooltip("One-shot blade-flash flipbook burst on the blade (SlashFlipbookURP particle).")]
        [SerializeField] private ParticleSystem bladeDrawVfx;

        [Tooltip("The 拔刀 'shing' sound. A procedurally-synthesized placeholder (KatanaDraw.wav) - swappable.")]
        [SerializeField] private AudioSource drawSfx;

        [Tooltip("Kicks the cutscene camera on the draw. Listener sits on CutsceneCamera.")]
        [SerializeField] private CinemachineImpulseSource impulseSource;

        // Public so the Timeline SignalReceiver's UnityEvent can bind to it.
        public void OnBladeDrawSignal()
        {
            if (bladeDrawVfx != null)
            {
                bladeDrawVfx.Play();
            }
            if (drawSfx != null)
            {
                drawSfx.Play();
            }
            if (impulseSource != null)
            {
                impulseSource.GenerateImpulse();
            }
        }

        // Setup-tool seam.
        public void EditorConfigure(ParticleSystem vfx, AudioSource sfx, CinemachineImpulseSource impulse)
        {
            bladeDrawVfx = vfx;
            drawSfx = sfx;
            impulseSource = impulse;
        }
    }
}
