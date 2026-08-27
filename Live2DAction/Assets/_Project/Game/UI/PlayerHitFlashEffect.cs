using UnityEngine;
using UnityEngine.UI;
using Live2DAction.Core;

namespace Live2DAction.UI
{
    // 2026-08-26, explicit user request ("做個玩家受擊特效") - full-screen red flash the instant the
    // player takes damage, the standard "you got hit" readability cue this project didn't have yet
    // (searched first - InvulnerabilityRippleEffect only reacts to Health.IsInvulnerable, not to a
    // hit landing, and PlayerHealthBarFx/StancePoiseBarFx only drive the HP/stance bars themselves,
    // nothing screen-space). Same "one condition, one component" precedent as those.
    //
    // Deliberately screen-space, not world-space like InvulnerabilityRippleEffect/
    // ExecutionReadyIndicator - a hit-taken cue needs to read even when the camera is close/behind
    // a giant boss (see this session's own big-boss camera framing work) where a body-anchored
    // effect could be occluded or off in a screen corner.
    public class PlayerHitFlashEffect : MonoBehaviour
    {
        [SerializeField] private Health health;
        [SerializeField] private Image flashImage;

        [SerializeField] private Color flashColor = new Color(0.8f, 0f, 0f, 1f);
        [Tooltip("Peak alpha immediately on hit - scales with how much damage was taken relative to max HP, clamped to this.")]
        [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.45f;
        [Tooltip("Damage fraction of MaxHealth (0-1) that already reaches maxAlpha - a light graze " +
                 "still flashes faintly, a heavy hit flashes at full maxAlpha, nothing scales past 1x.")]
        [SerializeField, Range(0.01f, 1f)] private float damageFractionForMaxAlpha = 0.25f;
        [SerializeField] private float fadeOutSeconds = 0.35f;

        private float _currentAlpha;

        private void OnEnable()
        {
            if (health != null) health.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            if (health != null) health.Damaged -= OnDamaged;
        }

        private void OnDamaged(DamageInfo info)
        {
            if (flashImage == null || health == null || health.MaxHealth <= 0f) return;

            // A one-shot poke, not a fatal-hit-only effect - fires on every hit including chip
            // damage, scaled so small hits are a subtle flicker and big hits read as a real jolt.
            float fraction = Mathf.Clamp01(info.Amount / health.MaxHealth);
            float targetAlpha = maxAlpha * Mathf.Clamp01(fraction / damageFractionForMaxAlpha);

            // Take the brighter of "already fading out from a previous hit" vs "this new hit" -
            // a fast combo shouldn't have each successive flash reset dimmer than the last one's
            // current fade-out level.
            _currentAlpha = Mathf.Max(_currentAlpha, targetAlpha);
        }

        private void Update()
        {
            if (flashImage == null) return;

            if (_currentAlpha > 0f)
            {
                _currentAlpha = Mathf.MoveTowards(_currentAlpha, 0f, (maxAlpha / Mathf.Max(0.01f, fadeOutSeconds)) * Time.deltaTime);
            }

            Color c = flashColor;
            c.a = _currentAlpha;
            flashImage.color = c;
        }
    }
}
