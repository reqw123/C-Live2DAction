using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Live2DAction.Core;
using Live2DAction.Combat;

namespace Live2DAction.UI
{
    // 2026-08-25, explicit user request ("接下來同理自作架勢條ui" + reference mockup) - third of
    // the three PlayerHealthBarFx-architecture clones (health/energy/stance), same layered-art +
    // tween/delay/edge-glow/shader-flow/hit-feedback treatment, this one reading StancePoise
    // instead of Health/UltimateEnergy. Gold tint instead of red/purple.
    //
    // Key difference from both siblings: for health/energy, LOW ratio means "in danger, flow gets
    // unstable" (see HealthEnergyFlowUI.shader's own unstable=(0.7-ratio)/0.7 curve). For stance,
    // it's the OPPOSITE - per the mockup ("架勢條接近上限時會變得更亮、更躁動" / "95~100%: 快速閃爍,
    // 臨界警示"), HIGH posture means "about to break, more unstable". Rather than forking the
    // shader with a second curve direction, this just feeds it (1 - displayedFill) as its _HpRatio
    // uniform - the exact same "unstable when this value is low" formula then naturally reads as
    // "unstable when the ACTUAL posture ratio is high", with zero shader changes needed.
    public class StancePoiseBarFx : MonoBehaviour
    {
        [SerializeField] private StancePoise stance;
        // Hit-feedback (flash/shake/sparks) triggers off Health.Damaged, same event StancePoise
        // itself already subscribes to internally for stance accumulation - not stance-specific
        // (StancePoise has no public event of its own), but "took a hit" is exactly the moment
        // the mockup's "受到攻擊時的反饋效果" wants anyway.
        [SerializeField] private Health health;
        [SerializeField] private Image currentFillImage;
        [SerializeField] private Image delayedFillImage;
        [SerializeField] private Image energyFlowImage;
        [SerializeField] private RectTransform edgeGlowRect;
        [SerializeField] private RectTransform trackRect;
        [SerializeField] private Text valueText;
        [SerializeField] private RectTransform[] sparkRects;
        [SerializeField] private bool billboardToCamera;

        // Warm gold/red glow for the flow layer's high-posture warning pulse - distinct from
        // health's reddish and energy's purple. See HealthEnergyFlowUI.shader's _GlowColor.
        [SerializeField] private Color glowColor = new Color(1f, 0.55f, 0.1f, 1f);

        [SerializeField] private float fillTweenSpeed = 12f;
        [SerializeField] private float delayHoldSeconds = 0.5f;
        [SerializeField] private float delayCatchUpSpeed = 0.6f;
        // Named to match the mockup's own "接近上限" framing - ramps the shader's glow/instability
        // as displayedFill rises past (1 - this), e.g. 0.3 means the effect ramps in over the top
        // 30% of the bar, mirroring lowHealthThreshold's bottom-30% ramp exactly, just flipped end.
        [SerializeField] private float highPostureThreshold = 0.3f;
        [SerializeField] private float flashDecaySpeed = 5f;
        [SerializeField] private float glowDecaySpeed = 3f;
        [SerializeField] private float shakeDecaySpeed = 6f;
        [SerializeField] private float shakeMagnitude = 6f;
        [SerializeField] private float edgeInset = 2f;
        [SerializeField] private float sparkSpeed = 90f;
        [SerializeField] private float sparkGravity = 220f;
        [SerializeField] private float sparkLifetime = 0.35f;

        [SerializeField] private float speedBoostDecaySpeed = 2.5f;
        [SerializeField] private float edgeGlowScaleBoostMax = 0.7f;
        [SerializeField] private float edgeGlowScaleDecaySpeed = 5f;

        private static readonly int FlashIntensityId = Shader.PropertyToID("_FlashIntensity");
        private static readonly int GlowIntensityId = Shader.PropertyToID("_GlowIntensity");
        private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
        private static readonly int HpRatioId = Shader.PropertyToID("_HpRatio");
        private static readonly int SpeedBoostId = Shader.PropertyToID("_SpeedBoost");

        private float _displayedFill;
        private float _delayedFill;
        private float _timeSinceHit = 999f;
        private float _flashIntensity;
        private float _hitGlowIntensity;
        private float _shakeIntensity;
        private float _speedBoostIntensity;
        private float _edgeGlowScaleBoost;
        private bool _initialized;

        private Vector2 _trackBasePosition;
        private Material _flowMaterialInstance;
        private Image[] _sparkImages;
        private Coroutine _sparkCoroutine;

        // Perf fix (2026-08-27, playtest report - "play mode模式下遊戲會卡頓") - valueText.text was
        // being rebuilt via string interpolation every single Update(), which allocates a new
        // string (plus boxing for the CeilToInt args) every frame regardless of whether the
        // displayed numbers actually changed. Caching the last-written ints and only touching
        // .text when one of them differs cuts that to "once per actual value change" instead of
        // "every frame stance is being tracked at all".
        private int _lastValueTextCurrent = int.MinValue;
        private int _lastValueTextMax = int.MinValue;

        private void Awake()
        {
            if (trackRect != null)
            {
                _trackBasePosition = trackRect.anchoredPosition;
            }

            if (energyFlowImage != null && energyFlowImage.material != null)
            {
                _flowMaterialInstance = new Material(energyFlowImage.material);
                energyFlowImage.material = _flowMaterialInstance;
                _flowMaterialInstance.SetColor(GlowColorId, glowColor);
            }

            if (sparkRects != null)
            {
                _sparkImages = new Image[sparkRects.Length];
                for (int i = 0; i < sparkRects.Length; i++)
                {
                    if (sparkRects[i] != null)
                    {
                        _sparkImages[i] = sparkRects[i].GetComponent<Image>();
                        sparkRects[i].gameObject.SetActive(false);
                    }
                }
            }
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.Damaged += OnDamaged;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Damaged -= OnDamaged;
            }
        }

        private void OnDamaged(DamageInfo damageInfo)
        {
            _timeSinceHit = 0f;
            _flashIntensity = 1f;
            _hitGlowIntensity = 1f;
            _shakeIntensity = 1f;
            _speedBoostIntensity = 1f;
            _edgeGlowScaleBoost = edgeGlowScaleBoostMax;

            if (sparkRects != null && sparkRects.Length > 0)
            {
                if (_sparkCoroutine != null)
                {
                    StopCoroutine(_sparkCoroutine);
                }
                _sparkCoroutine = StartCoroutine(AnimateSparkBurst());
            }
        }

        private void Update()
        {
            if (stance == null || currentFillImage == null)
            {
                return;
            }

            float targetFill = stance.MaxStance > 0f ? stance.CurrentStance / stance.MaxStance : 0f;
            if (!_initialized)
            {
                _displayedFill = targetFill;
                _delayedFill = targetFill;
                _initialized = true;
            }

            float deltaTime = Time.deltaTime;
            _timeSinceHit += deltaTime;

            _displayedFill = HealthBarTweenUtility.SmoothApproach(_displayedFill, targetFill, fillTweenSpeed, deltaTime);
            _delayedFill = HealthBarTweenUtility.ComputeDelayedFill(_delayedFill, targetFill, _timeSinceHit, delayHoldSeconds, delayCatchUpSpeed, deltaTime);

            currentFillImage.fillAmount = _displayedFill;
            if (delayedFillImage != null)
            {
                delayedFillImage.fillAmount = _delayedFill;
            }
            if (energyFlowImage != null)
            {
                energyFlowImage.fillAmount = _displayedFill;
            }

            if (valueText != null)
            {
                int currentInt = Mathf.CeilToInt(stance.CurrentStance);
                int maxInt = Mathf.CeilToInt(stance.MaxStance);
                if (currentInt != _lastValueTextCurrent || maxInt != _lastValueTextMax)
                {
                    valueText.text = $"{currentInt}/{maxInt}";
                    _lastValueTextCurrent = currentInt;
                    _lastValueTextMax = maxInt;
                }
            }

            if (edgeGlowRect != null && trackRect != null)
            {
                float x = HealthBarTweenUtility.ComputeEdgeGlowLocalX(_displayedFill, trackRect.rect.width, edgeInset, edgeInset);
                Vector2 pos = edgeGlowRect.anchoredPosition;
                pos.x = x;
                edgeGlowRect.anchoredPosition = pos;
                edgeGlowRect.localScale = Vector3.one * (1f + _edgeGlowScaleBoost);
            }

            _flashIntensity = Mathf.Max(0f, _flashIntensity - flashDecaySpeed * deltaTime);
            _hitGlowIntensity = Mathf.Max(0f, _hitGlowIntensity - glowDecaySpeed * deltaTime);
            _shakeIntensity = Mathf.Max(0f, _shakeIntensity - shakeDecaySpeed * deltaTime);
            _speedBoostIntensity = Mathf.Max(0f, _speedBoostIntensity - speedBoostDecaySpeed * deltaTime);
            _edgeGlowScaleBoost = Mathf.Max(0f, _edgeGlowScaleBoost - edgeGlowScaleDecaySpeed * deltaTime);

            // Inverted vs health/energy - see this class's own header comment. ComputeLowHealthIntensity
            // ramps 0->1 as its first argument falls below the threshold; feeding it (1-displayedFill)
            // against highPostureThreshold makes it ramp 0->1 as the ACTUAL posture ratio rises above
            // (1 - highPostureThreshold), i.e. "接近上限時變得更亮更躁動".
            float highPostureIntensity = HealthBarTweenUtility.ComputeLowHealthIntensity(1f - _displayedFill, highPostureThreshold);
            float glow = Mathf.Clamp01(Mathf.Max(highPostureIntensity, _hitGlowIntensity));

            if (_flowMaterialInstance != null)
            {
                _flowMaterialInstance.SetFloat(FlashIntensityId, _flashIntensity);
                _flowMaterialInstance.SetFloat(GlowIntensityId, glow);
                // Inverted (see header comment) so the shader's own "unstable when low" curve
                // reads as "unstable when posture is HIGH" without touching the shader itself.
                _flowMaterialInstance.SetFloat(HpRatioId, 1f - _displayedFill);
                _flowMaterialInstance.SetFloat(SpeedBoostId, _speedBoostIntensity);
            }

            if (trackRect != null)
            {
                Vector2 shakeOffset = HealthBarTweenUtility.ComputeShakeOffset(_shakeIntensity, Time.time, shakeMagnitude);
                trackRect.anchoredPosition = _trackBasePosition + shakeOffset;
            }
        }

        private void LateUpdate()
        {
            if (!billboardToCamera)
            {
                return;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            transform.rotation = mainCamera.transform.rotation;
        }

        private IEnumerator AnimateSparkBurst()
        {
            var random = new System.Random();
            var angles = new float[sparkRects.Length];
            for (int i = 0; i < sparkRects.Length; i++)
            {
                angles[i] = (float)(random.NextDouble() * Mathf.PI * 2f);
                if (sparkRects[i] != null)
                {
                    sparkRects[i].gameObject.SetActive(true);
                }
            }

            float t = 0f;
            while (t < sparkLifetime)
            {
                t += Time.deltaTime;
                float t01 = Mathf.Clamp01(t / sparkLifetime);
                Vector2 origin = edgeGlowRect != null ? edgeGlowRect.anchoredPosition : Vector2.zero;

                for (int i = 0; i < sparkRects.Length; i++)
                {
                    if (sparkRects[i] == null)
                    {
                        continue;
                    }

                    Vector2 offset = HealthBarTweenUtility.ComputeSparkOffset(t01, angles[i], sparkSpeed, sparkGravity);
                    sparkRects[i].anchoredPosition = origin + offset;

                    if (_sparkImages[i] != null)
                    {
                        Color c = _sparkImages[i].color;
                        c.a = 1f - t01;
                        _sparkImages[i].color = c;
                    }
                }

                yield return null;
            }

            for (int i = 0; i < sparkRects.Length; i++)
            {
                if (sparkRects[i] != null)
                {
                    sparkRects[i].gameObject.SetActive(false);
                }
            }

            _sparkCoroutine = null;
        }
    }
}
