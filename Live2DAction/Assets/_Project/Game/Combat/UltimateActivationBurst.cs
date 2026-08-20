using UnityEngine;

namespace Live2DAction.Combat
{
    // 2026-08-16, explicit user request: 施放必殺技瞬間，身體周圍散發一瞬間的霸氣感 - a one-shot
    // expanding shockwave ring plus straight radiating "burst" rays, both fading out over
    // ~0.5s, played once at the exact frame UltimateAbility.Activate() fires. Deliberately a
    // sudden outward burst (straight rays, smooth ring) rather than reusing
    // UltimateReadyAura's jagged circling lightning bolts - that effect communicates "ready
    // and waiting" (continuous, electric), this one needs to read as "released, right now"
    // (one-shot, explosive), so they use different shapes/motion instead of looking like the
    // same effect just triggered twice.
    public class UltimateActivationBurst : MonoBehaviour
    {
        [SerializeField] private LineRenderer ring;
        [SerializeField] private LineRenderer[] rays;
        [SerializeField] private float duration = 0.5f;
        [SerializeField] private float maxRadius = 2.5f;
        [SerializeField] private float height = 0.1f;
        [SerializeField] private int ringSegments = 24;
        [SerializeField] private float rayInnerRadius = 0.15f;

        private bool _playing;
        private float _elapsed;
        private Color _ringBaseColor;
        private Color _rayBaseColor;

        private void Awake()
        {
            if (ring != null)
            {
                _ringBaseColor = ring.startColor;
            }

            if (rays != null && rays.Length > 0 && rays[0] != null)
            {
                _rayBaseColor = rays[0].startColor;
            }

            SetVisible(false);
        }

        // Called by UltimateAbility.Activate() - restarts the burst even if a previous one
        // hasn't fully faded yet (there's no cooldown-stacking concern, the ability itself
        // can't be re-triggered while already active, see UltimateAbility's own comment).
        public void Play()
        {
            _playing = true;
            _elapsed = 0f;
            SetVisible(true);
        }

        private void Update()
        {
            if (!_playing)
            {
                return;
            }

            _elapsed += Time.deltaTime;
            float t01 = duration > 0f ? _elapsed / duration : 1f;

            if (t01 >= 1f)
            {
                _playing = false;
                SetVisible(false);
                return;
            }

            float radius = UltimateBurstUtility.ComputeRadius(t01, maxRadius);
            float brightness = UltimateBurstUtility.ComputeBrightnessMultiplier(t01);

            if (ring != null)
            {
                Vector3[] points = UltimateBurstUtility.BuildRingPoints(radius, height, ringSegments);
                ring.positionCount = points.Length;
                ring.SetPositions(points);
                ApplyBrightness(ring, _ringBaseColor, brightness);
            }

            if (rays != null)
            {
                for (int i = 0; i < rays.Length; i++)
                {
                    if (rays[i] == null)
                    {
                        continue;
                    }

                    Vector3 direction = UltimateBurstUtility.ComputeRayDirection(i, rays.Length);
                    rays[i].SetPosition(0, direction * rayInnerRadius + new Vector3(0f, height, 0f));
                    rays[i].SetPosition(1, direction * radius + new Vector3(0f, height, 0f));
                    ApplyBrightness(rays[i], _rayBaseColor, brightness);
                }
            }
        }

        private static void ApplyBrightness(LineRenderer line, Color baseColor, float brightness)
        {
            Color scaled = new Color(baseColor.r * brightness, baseColor.g * brightness, baseColor.b * brightness, baseColor.a);
            line.startColor = scaled;
            line.endColor = scaled;
        }

        private void SetVisible(bool visible)
        {
            if (ring != null)
            {
                ring.gameObject.SetActive(visible);
            }

            if (rays == null)
            {
                return;
            }

            foreach (LineRenderer ray in rays)
            {
                if (ray != null)
                {
                    ray.gameObject.SetActive(visible);
                }
            }
        }
    }
}
