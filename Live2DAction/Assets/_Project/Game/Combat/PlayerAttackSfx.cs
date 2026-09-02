using UnityEngine;

namespace Live2DAction.Combat
{
    // 2026-09-01, user request ("把 9月1日.mp3 做為左鍵攻擊音效"). The player's left-click katana
    // swing sound. 追加85 originally had PlayerMeleeSfx play KatanaClash.mp3 on every PlayerCombat.Hit
    // segment; 2026-09-01 that clip + child moved to PlayerGuard (blade-vs-blade clash only, see
    // PlayerGuardClashSfx), leaving the left-click swing silent. This puts a dedicated swing clip
    // back on PlayerCombat.Hit.
    //
    // Fires once per resolved Active combo segment - whiff INCLUDED (PlayerCombat.Hit is raised on
    // every swing regardless of whether it connected), because this is the sound of the blade
    // cutting air, not an impact. Nothing else subscribes PlayerCombat.Hit on the player, so this
    // component is the whole feature.
    [RequireComponent(typeof(AudioSource))]
    public class PlayerAttackSfx : MonoBehaviour
    {
        [Tooltip("The PlayerCombat whose Hit event drives the swing sound. Auto-found on this " +
                 "GameObject / its parents if unset.")]
        [SerializeField] private PlayerCombat combat;

        [Tooltip("The short katana swing / whoosh clip (KatanaSwing.mp3).")]
        [SerializeField] private AudioClip swingClip;

        [SerializeField, Range(0f, 1f)] private float volume = 0.85f;

        [Tooltip("Random pitch jitter per swing so a fast combo doesn't sound like one repeated " +
                 "sample. (min, max) multiplier.")]
        [SerializeField] private Vector2 pitchRange = new Vector2(0.92f, 1.10f);

        private AudioSource _source;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            if (combat == null)
            {
                combat = GetComponentInParent<PlayerCombat>();
            }
        }

        private void OnEnable()
        {
            if (combat != null)
            {
                combat.Hit += OnHit;
            }
        }

        private void OnDisable()
        {
            if (combat != null)
            {
                combat.Hit -= OnHit;
            }
        }

        private void OnHit(PlayerCombat.HitEvent e)
        {
            if (swingClip == null || _source == null)
            {
                return;
            }
            _source.pitch = Random.Range(pitchRange.x, pitchRange.y);
            _source.PlayOneShot(swingClip, volume);
        }
    }
}
