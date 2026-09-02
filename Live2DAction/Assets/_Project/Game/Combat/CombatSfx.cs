using UnityEngine;

namespace Live2DAction.Combat
{
    // 2026-08-29, cat combat design (Docs/CAT_COMBAT_DESIGN.md 4.5). Minimal one-shot combat
    // audio. Clips are left unassigned by the setup for now (this project has no original combat
    // SFX yet - rule 1; the RangedWeapon gunshot is the only existing AudioSource), so this is
    // wired-but-silent until clips are dropped in, the same "optional, null = no visual/audio"
    // pattern as PlayerCombat.hitEffectPrefab.
    [RequireComponent(typeof(AudioSource))]
    public class CombatSfx : MonoBehaviour
    {
        [SerializeField] private AudioClip swingClip;
        [SerializeField] private AudioClip hitClip;
        [SerializeField] private AudioClip heavyHitClip;
        [SerializeField, Range(0f, 1f)] private float volume = 0.8f;

        private AudioSource _source;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
        }

        public void PlaySwing()
        {
            Play(swingClip);
        }

        public void PlayHit(bool heavy)
        {
            Play(heavy && heavyHitClip != null ? heavyHitClip : hitClip);
        }

        private void Play(AudioClip clip)
        {
            if (clip != null && _source != null)
            {
                _source.PlayOneShot(clip, volume);
            }
        }
    }
}
