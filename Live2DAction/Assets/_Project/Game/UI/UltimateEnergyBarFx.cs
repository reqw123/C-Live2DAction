using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Live2DAction.Core;

namespace Live2DAction.UI
{
    // 2026-08-25, explicit user request ("接下來以這樣圖渲染能量條 (所有具有能量機制的共用)" + reference
    // mockup) - REWRITES the earlier, much simpler UltimateEnergyBarFx (single flat Image.fillAmount
    // + a plain full-state color swap) to mirror PlayerHealthBarFx's full layered-art architecture
    // instead, since the mockup asks for literally the same feature set the health bar already has
    // (delayed fill, front glow node that tracks fill position + pulses, UV-scroll energy-flow
    // shader that gets faster/more distorted the LOWER the resource is, activation flash/shake/
    // sparks) - not a coincidence, "能量越低越不穩定" is the exact same curve as "血量越低越不穩定".
    // Deliberately a close structural clone of PlayerHealthBarFx (same HealthBarTweenUtility calls,
    // same layer names/roles) rather than a shared base class - matches this codebase's established
    // preference for small duplicated components until a third near-identical one actually needs
    // generalizing (see PlayerHealthBarFx's own header comment on the same point). The one real
    // difference: UltimateEnergy has no "Damaged"-equivalent event to hook the hit-reaction burst
    // off of - "受激活時" (on activation) here means the resource just reached full (READY to use),
    // edge-detected every frame instead of subscribed to an event.
    public class UltimateEnergyBarFx : MonoBehaviour
    {
        [SerializeField] private UltimateEnergy energy;
        [SerializeField] private Image currentFillImage;
        [SerializeField] private Image delayedFillImage;
        [SerializeField] private Image energyFlowImage;
        [SerializeField] private RectTransform edgeGlowRect;
        [SerializeField] private RectTransform trackRect;
        [SerializeField] private Text valueText;
        [SerializeField] private RectTransform[] sparkRects;
        [SerializeField] private bool billboardToCamera;

        // Purple tint for the flow layer's low-resource glow, distinct from the health bar's
        // reddish warning glow - see HealthEnergyFlowUI.shader's own _GlowColor comment.
        [SerializeField] private Color glowColor = new Color(0.55f, 0.15f, 0.95f, 1f);

        [SerializeField] private float fillTweenSpeed = 12f;
        [SerializeField] private float delayHoldSeconds = 0.5f;
        [SerializeField] private float delayCatchUpSpeed = 0.6f;
        [SerializeField] private float lowEnergyThreshold = 0.3f;
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

        private float _displayedFill = 1f;
        private float _delayedFill = 1f;
        private float _timeSinceActivation = 999f;
        private float _flashIntensity;
        private float _hitGlowIntensity;
        private float _shakeIntensity;
        private float _speedBoostIntensity;
        private float _edgeGlowScaleBoost;
        private bool _initialized;
        private bool _wasFull;

        private Vector2 _trackBasePosition;
        private Material _flowMaterialInstance;
        private Image[] _sparkImages;
        private Coroutine _sparkCoroutine;

        // Perf fix (2026-08-27, playtest report - "play mode模式下遊戲會卡頓") - see StancePoiseBarFx's
        // matching field for the full reasoning; same fix here since this Update() had the same
        // every-frame string-interpolation-regardless-of-change pattern.
        private int _lastValueTextCurrent = int.MinValue;
        private int _lastValueTextMax = int.MinValue;

        private void Awake()
        {
            if (trackRect != null)
            {
                _trackBasePosition = trackRect.anchoredPosition;
            }

            // Instanced, not the shared material asset - same reasoning as PlayerHealthBarFx's own
            // Awake (runtime SetFloat/SetColor calls must never bleed back into the project asset).
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

        private void OnActivated()
        {
            _timeSinceActivation = 0f;
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
            if (energy == null || currentFillImage == null)
            {
                return;
            }

            float targetFill = energy.MaxEnergy > 0f ? energy.CurrentEnergy / energy.MaxEnergy : 0f;
            if (!_initialized)
            {
                _displayedFill = targetFill;
                _delayedFill = targetFill;
                _wasFull = energy.IsFull;
                _initialized = true;
            }

            // "受激活時" for a passively-regenerating resource means "just became ready to use",
            // not "just took damage" - edge-detected here since UltimateEnergy has no event for it.
            bool isFull = energy.IsFull;
            if (isFull && !_wasFull)
            {
                OnActivated();
            }
            _wasFull = isFull;

            float deltaTime = Time.deltaTime;
            _timeSinceActivation += deltaTime;

            _displayedFill = HealthBarTweenUtility.SmoothApproach(_displayedFill, targetFill, fillTweenSpeed, deltaTime);
            // Energy typically fills UP over time rather than draining from hits, so "delayed"
            // here reads as a ghost trailing BEHIND a gain - ComputeDelayedFill's own "instant on
            // heal, held-then-catch-up on drop" logic still does the right thing for a Consume()
            // drain (full -> 0 in one frame), it just rarely has anything to visibly catch up to
            // on the far more common gradual regen case.
            _delayedFill = HealthBarTweenUtility.ComputeDelayedFill(_delayedFill, targetFill, _timeSinceActivation, delayHoldSeconds, delayCatchUpSpeed, deltaTime);

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
                int currentInt = Mathf.CeilToInt(energy.CurrentEnergy);
                int maxInt = Mathf.CeilToInt(energy.MaxEnergy);
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

            // Named "low energy" here (mirrors lowHealthThreshold) but the SHADER-side effect it
            // drives is identical to the health bar's: 0 above the threshold, ramping to 1 as the
            // bar empties - "能量越低，能量流動越強烈、越不穩定".
            float lowEnergyIntensity = HealthBarTweenUtility.ComputeLowHealthIntensity(_displayedFill, lowEnergyThreshold);
            float glow = Mathf.Clamp01(Mathf.Max(lowEnergyIntensity, _hitGlowIntensity));

            if (_flowMaterialInstance != null)
            {
                _flowMaterialInstance.SetFloat(FlashIntensityId, _flashIntensity);
                _flowMaterialInstance.SetFloat(GlowIntensityId, glow);
                _flowMaterialInstance.SetFloat(HpRatioId, _displayedFill);
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
