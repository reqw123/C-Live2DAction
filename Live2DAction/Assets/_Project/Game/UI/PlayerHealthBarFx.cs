using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Live2DAction.Core;

namespace Live2DAction.UI
{
    // 2026-08-23, explicit user request ("玩家血量條...程式即時控制/平滑Tween/Delayed Health
    // Bar/Edge Glow/Shader能量流動/受傷Glow Flash震動火花/血量越低警示感") - replaces the plain
    // instant-snap Fill Image PlayerCornerHud used to drive directly for the health row. Owns
    // that row's Fill/DelayedFill/EdgeGlow/Value text/flow-shader material completely so nothing
    // else writes healthFill.fillAmount anymore (see PlayerCornerHud.cs's own comment for why
    // its health-specific fields were removed rather than left to race this component).
    //
    // HP data and damage math are untouched - this only reads Health.CurrentHealth/MaxHealth and
    // subscribes to Health.Damaged for edge-detecting "just got hit" (same event StancePoise
    // already subscribes to), never calls ApplyDamage/Heal/etc itself.
    //
    // 2026-08-23 follow-up, explicit user request (reference mockup: "04 前端發光節點...跟隨血量
    // 位置移動,發光脈動" / "受傷時...前端發光節點放大+強化發光" / "能量層短暫加速+增強亮度") - adds
    // the edge-glow SCALE pulse on hit (on top of the flow shader's own brightness pulse, which
    // already existed) and pushes _HpRatio/_SpeedBoost into the shader so the flow's speed and
    // distortion react to low HP and to hits, not just its brightness - see
    // HealthEnergyFlowUI.shader's own comment for what those two uniforms actually drive.
    //
    // 2026-08-24 follow-up, explicit user request ("把此ui取代076的生命條") - despite the class
    // name, nothing here actually reads from the Player specifically (only ever touches whatever
    // Health/Image/RectTransform references it's wired to), so it's reused as-is for 076's
    // world-space floating bar. billboardToCamera is the one genuinely NEW behavior that use case
    // needs and the screen-space corner HUD never did - a world-space bar floating above a
    // character's head has to keep facing the camera (same LateUpdate convention
    // WorldSpaceHealthBar already used), while a Screen Space Overlay canvas is always
    // camera-facing by construction and would only get needlessly perturbed by this.
    public class PlayerHealthBarFx : MonoBehaviour
    {
        [SerializeField] private Health health;
        [SerializeField] private Image currentFillImage;
        [SerializeField] private Image delayedFillImage;
        // 2026-08-24, explicit user request ("把途中的ui結構分層...作層次渲染") - the flow-shader
        // material now lives on its OWN "05 能量流動層" art layer (drawn on top of Fill), not on
        // Fill's own material like before, so it needs its own fillAmount kept in sync with
        // currentFillImage's - see HealthEnergyFlowUI.shader's own comment for why.
        [SerializeField] private Image energyFlowImage;
        [SerializeField] private RectTransform edgeGlowRect;
        [SerializeField] private RectTransform trackRect;
        [SerializeField] private Text valueText;
        [SerializeField] private RectTransform[] sparkRects;
        [SerializeField] private bool billboardToCamera;

        [SerializeField] private float fillTweenSpeed = 12f;
        [SerializeField] private float delayHoldSeconds = 0.5f;
        [SerializeField] private float delayCatchUpSpeed = 0.6f;
        [SerializeField] private float lowHealthThreshold = 0.3f;
        [SerializeField] private float flashDecaySpeed = 5f;
        [SerializeField] private float glowDecaySpeed = 3f;
        [SerializeField] private float shakeDecaySpeed = 6f;
        [SerializeField] private float shakeMagnitude = 6f;
        // Matches PlayerCornerHudPolishSetup's own fill inset (offsetMin/Max = 2,2 / -2,-2) so
        // the edge-glow node lines up with where the fill's own visible edge actually renders.
        [SerializeField] private float edgeInset = 2f;
        [SerializeField] private float sparkSpeed = 90f;
        [SerializeField] private float sparkGravity = 220f;
        [SerializeField] private float sparkLifetime = 0.35f;

        [SerializeField] private float speedBoostDecaySpeed = 2.5f;
        [SerializeField] private float edgeGlowScaleBoostMax = 0.7f;
        [SerializeField] private float edgeGlowScaleDecaySpeed = 5f;

        private static readonly int FlashIntensityId = Shader.PropertyToID("_FlashIntensity");
        private static readonly int GlowIntensityId = Shader.PropertyToID("_GlowIntensity");
        private static readonly int HpRatioId = Shader.PropertyToID("_HpRatio");
        private static readonly int SpeedBoostId = Shader.PropertyToID("_SpeedBoost");

        private float _displayedFill = 1f;
        private float _delayedFill = 1f;
        private float _timeSinceDamage = 999f;
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

        // Perf fix (2026-08-27, playtest report - "play mode模式下遊戲會卡頓") - see
        // StancePoiseBarFx's matching field for the full reasoning; this component (also reused as
        // the boss's world-space health bar, see this class's own header comment) was the one
        // sibling missing the change-detection guard on valueText, so it was rebuilding a new
        // string every Update() even when the displayed number hadn't moved.
        private int _lastValueTextCurrent = int.MinValue;
        private int _lastValueTextMax = int.MinValue;

        private void Awake()
        {
            if (trackRect != null)
            {
                _trackBasePosition = trackRect.anchoredPosition;
            }

            // Instanced (not the shared material asset) so PlayerHealthBarFx's own per-frame
            // SetFloat calls never bleed into the project asset - same reasoning as any runtime
            // material tweak in this codebase (e.g. LightPillarURP's _ActiveBlend driven by
            // UpdraftActivationEffect through its own material instance).
            if (energyFlowImage != null && energyFlowImage.material != null)
            {
                _flowMaterialInstance = new Material(energyFlowImage.material);
                energyFlowImage.material = _flowMaterialInstance;
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
            _timeSinceDamage = 0f;
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
            if (health == null || currentFillImage == null)
            {
                return;
            }

            float targetFill = HealthBarUtility.ComputeFillAmount(health.CurrentHealth, health.MaxHealth);
            if (!_initialized)
            {
                _displayedFill = targetFill;
                _delayedFill = targetFill;
                _initialized = true;
            }

            float deltaTime = Time.deltaTime;
            _timeSinceDamage += deltaTime;

            _displayedFill = HealthBarTweenUtility.SmoothApproach(_displayedFill, targetFill, fillTweenSpeed, deltaTime);
            _delayedFill = HealthBarTweenUtility.ComputeDelayedFill(_delayedFill, targetFill, _timeSinceDamage, delayHoldSeconds, delayCatchUpSpeed, deltaTime);

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
                int currentInt = Mathf.CeilToInt(health.CurrentHealth);
                int maxInt = Mathf.CeilToInt(health.MaxHealth);
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
                // "受傷時...前端發光節點放大" - pulses the node's own scale up on a hit, on top
                // of the flow shader's separate brightness pulse (_GlowIntensity below).
                edgeGlowRect.localScale = Vector3.one * (1f + _edgeGlowScaleBoost);
            }

            _flashIntensity = Mathf.Max(0f, _flashIntensity - flashDecaySpeed * deltaTime);
            _hitGlowIntensity = Mathf.Max(0f, _hitGlowIntensity - glowDecaySpeed * deltaTime);
            _shakeIntensity = Mathf.Max(0f, _shakeIntensity - shakeDecaySpeed * deltaTime);
            _speedBoostIntensity = Mathf.Max(0f, _speedBoostIntensity - speedBoostDecaySpeed * deltaTime);
            _edgeGlowScaleBoost = Mathf.Max(0f, _edgeGlowScaleBoost - edgeGlowScaleDecaySpeed * deltaTime);

            float lowHealthIntensity = HealthBarTweenUtility.ComputeLowHealthIntensity(_displayedFill, lowHealthThreshold);
            float glow = Mathf.Clamp01(Mathf.Max(lowHealthIntensity, _hitGlowIntensity));

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

        // Plain "match the camera's rotation" billboard, same convention WorldSpaceHealthBar's
        // own LateUpdate already uses for exactly this purpose - a flat world-space bar should
        // stay parallel to the camera's view plane regardless of where the character stands.
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
