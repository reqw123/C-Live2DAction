using UnityEngine;

namespace Live2DAction.Combat
{
    // 2026-09-01, user request - Sekiro deflect, spec section 六 (「刀刃交鋒的視聽回饋」).
    //
    // Sparks + sound AT THE WEAPON CONTACT POINT (PlayerGuard.Parried / .Guarded hand it over -
    // NOT the player centre). Two tiers:
    //   - Guarded  : short white-yellow spark, blunt clip
    //   - Parried  : brighter/bigger burst, crisper clip
    // Everything (particle systems, clips, source, cooldown) is Inspector-assigned - no hard-coded
    // asset paths. Hit-stop + camera shake live on PlayerGuard itself.
    public class PlayerClashFeedback : MonoBehaviour
    {
        [SerializeField] private PlayerGuard guard;

        [Tooltip("World-simulation ParticleSystem, moved to the contact point and Play()ed on a plain guard.")]
        [SerializeField] private ParticleSystem guardSparks;
        [Tooltip("World-simulation ParticleSystem for a perfect parry - brighter / more particles.")]
        [SerializeField] private ParticleSystem parrySparks;

        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip guardClip;
        [SerializeField] private AudioClip parryClip;
        [Tooltip("2026-09-01 (用戶) - the rougher/blunter clip for a NON-blade soft block (a blocked " +
                 "kick, PlayerGuard.Blocked). Falls back to guardClip if unset.")]
        [SerializeField] private AudioClip blockClip;
        [SerializeField] private Vector2 guardPitch = new Vector2(0.90f, 1.02f);
        [SerializeField] private Vector2 parryPitch = new Vector2(1.03f, 1.14f);
        [SerializeField] private Vector2 blockPitch = new Vector2(0.82f, 0.96f);
        [SerializeField, Range(0f, 1f)] private float guardVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] private float parryVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float blockVolume = 0.75f;

        [Tooltip("Extra debounce on the AV feedback for one weapon-pair clash (spec: 0.08-0.15s). " +
                 "PlayerGuard already gates the CLASH itself on clashCooldownSeconds; this is the " +
                 "separate spark/SFX cooldown the spec calls for.")]
        [SerializeField] private float feedbackCooldownSeconds = 0.1f;

        private float _lastFeedbackTime = -999f;

        private void Awake()
        {
            if (guard == null) guard = GetComponentInParent<PlayerGuard>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
        }

        private void OnEnable()
        {
            if (guard != null)
            {
                guard.Guarded += OnGuarded;
                guard.Parried += OnParried;
                guard.Blocked += OnBlocked;
            }
        }

        private void OnDisable()
        {
            if (guard != null)
            {
                guard.Guarded -= OnGuarded;
                guard.Parried -= OnParried;
                guard.Blocked -= OnBlocked;
            }
        }

        private void OnGuarded(Vector3 point) => Fire(point, guardSparks, guardClip, guardPitch, guardVolume);
        private void OnParried(Vector3 point) => Fire(point, parrySparks, parryClip, parryPitch, parryVolume);

        // A NON-blade soft block (kick) - no clash contact point on the event, so use the hit point
        // from the DamageInfo, the blunt clip, and the guard sparks.
        private void OnBlocked(Live2DAction.Core.DamageInfo info)
            => Fire(info.Point, guardSparks, blockClip != null ? blockClip : guardClip, blockPitch, blockVolume);

        private void Fire(Vector3 point, ParticleSystem sparks, AudioClip clip, Vector2 pitch, float volume)
        {
            if (Time.time - _lastFeedbackTime < feedbackCooldownSeconds)
            {
                return;
            }
            _lastFeedbackTime = Time.time;

            if (sparks != null)
            {
                sparks.transform.position = point;
                sparks.Play(true);
            }
            if (clip != null && audioSource != null)
            {
                audioSource.transform.position = point;
                audioSource.pitch = Random.Range(pitch.x, pitch.y);
                audioSource.PlayOneShot(clip, volume);
            }
        }

        public void EditorConfigure(PlayerGuard g, ParticleSystem guardPs, ParticleSystem parryPs,
            AudioSource src, AudioClip guardAudio, AudioClip parryAudio, AudioClip blockAudio = null)
        {
            guard = g;
            guardSparks = guardPs;
            parrySparks = parryPs;
            audioSource = src;
            guardClip = guardAudio;
            parryClip = parryAudio;
            if (blockAudio != null) blockClip = blockAudio;
        }
    }
}
