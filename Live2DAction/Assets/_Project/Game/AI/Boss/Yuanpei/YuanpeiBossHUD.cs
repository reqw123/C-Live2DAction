using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Live2DAction.UI;

namespace Live2DAction.AI.Boss.Yuanpei
{
    // Screen-space Boss HUD (spec §16). Built from the SAME layered art + shader the player's
    // corner HUD uses (Assets/_Project/UI/Textures/HealthBarArt/): per bar - a HudRoundedRect
    // container, 00_Frame, 01_Background, 02_DelayedFill (ghost), 03_Fill_*, 05_EnergyFlow_*
    // (Live2DAction/UI/HealthEnergyFlow material), an EdgeGlow node and a 6-spark burst. Same
    // tween / delayed-fill / flow-instability / hit-flash behaviour as PlayerHealthBarFx /
    // UltimateEnergyBarFx / StancePoiseBarFx (shared HealthBarTweenUtility). Just top-centre and
    // bigger (boss-bar convention), and driven by YuanpeiBossVitals instead of player data.
    //
    // Sprites + the flow material are serialized fields, wired in the scene from the HealthBarArt
    // folder (YuanpeiBossHudArtSetup menu does it). Colour is never the only cue (spec §16 note):
    // 生命 / 能量 / 架勢 labels + the near-full posture flash.
    //
    // [F] 處決 shows only when YuanpeiExecution.PromptVisible (spec §16.6-7).
    public class YuanpeiBossHUD : MonoBehaviour
    {
        [SerializeField] private YuanpeiBossVitals vitals;
        [SerializeField] private YuanpeiExecution execution;
        [SerializeField] private string bossName = "yuanpei_LogoSky";

        [Header("Art (wired from Assets/_Project/UI/Textures/HealthBarArt)")]
        [SerializeField] private Material flowMaterial;      // Live2DAction/UI/HealthEnergyFlow
        [SerializeField] private Sprite roundedRectSprite;   // HudRoundedRect (9-sliced container)
        [SerializeField] private Sprite frameSprite;         // 00_Frame
        [SerializeField] private Sprite backgroundSprite;    // 01_Background
        [SerializeField] private Sprite delayedFillSprite;   // 02_DelayedFill
        [SerializeField] private Sprite fillHpSprite;        // 03_Fill
        [SerializeField] private Sprite fillEnergySprite;    // 03_Fill_Energy
        [SerializeField] private Sprite fillStanceSprite;    // 03_Fill_Stance
        [SerializeField] private Sprite flowHpSprite;        // 05_EnergyFlow
        [SerializeField] private Sprite flowEnergySprite;    // 05_EnergyFlow_Energy
        [SerializeField] private Sprite flowStanceSprite;    // 05_EnergyFlow_Stance
        [SerializeField] private Sprite sparkSprite;         // Spark

        static readonly Color HpGlow      = new Color(0.90f, 0.15f, 0.08f, 1f);
        static readonly Color EnergyGlow  = new Color(0.35f, 0.75f, 1.00f, 1f);
        static readonly Color PostureGlow = new Color(1.00f, 0.60f, 0.10f, 1f);

        static readonly int GlowColorId  = Shader.PropertyToID("_GlowColor");
        static readonly int GlowIntId    = Shader.PropertyToID("_GlowIntensity");
        static readonly int FlashIntId   = Shader.PropertyToID("_FlashIntensity");
        static readonly int HpRatioId    = Shader.PropertyToID("_HpRatio");
        static readonly int SpeedBoostId = Shader.PropertyToID("_SpeedBoost");

        private class BarRow
        {
            public RectTransform track;
            public Image fill, ghost, flow;
            public Material flowMat;
            public RectTransform edgeGlow;
            public RectTransform[] sparks;
            public Image[] sparkImgs;
            public Text value;
            public Vector2 trackBasePos;
        }

        private CanvasGroup _group;
        private RectTransform _panel;
        private Vector2 _panelBase;
        private BarRow _hp, _en, _po;
        private Text _nameText, _fText;

        private float _shown, _target;
        private float _hpDisp, _hpGhostV, _enDisp, _enGhostV, _poDisp, _poGhostV;
        private float _timeSinceHit = 999f, _shake, _flash, _speedBoost;
        private bool _init;

        private void Awake()
        {
            if (vitals == null) vitals = GetComponent<YuanpeiBossVitals>();
            if (execution == null) execution = GetComponent<YuanpeiExecution>();
            Build();
            SetShown(0f);
        }

        private void OnEnable()
        {
            if (vitals != null && vitals.Health != null) vitals.Health.Damaged += OnBossDamaged;
        }

        private void OnDisable()
        {
            if (vitals != null && vitals.Health != null) vitals.Health.Damaged -= OnBossDamaged;
        }

        private void OnDestroy()
        {
            if (_hp != null && _hp.flowMat != null) Destroy(_hp.flowMat);
            if (_en != null && _en.flowMat != null) Destroy(_en.flowMat);
            if (_po != null && _po.flowMat != null) Destroy(_po.flowMat);
        }

        private void OnBossDamaged(Live2DAction.Core.DamageInfo _)
        {
            _timeSinceHit = 0f;
            _shake = 1f;
            _flash = 1f;
            _speedBoost = 1.4f;
            SparkBurst(_hp);
        }

        public void SetVisible(bool v) => _target = v ? 1f : 0f;

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _shown = Mathf.MoveTowards(_shown, _target, dt * 3f);
            SetShown(_shown);
            if (_shown <= 0.01f || vitals == null) return;

            float hp = vitals.HealthNormalized;
            float en = vitals.EnergyNormalized;
            float po = vitals.PostureNormalized;

            if (!_init)
            {
                _hpDisp = _hpGhostV = hp;
                _enDisp = _enGhostV = en;
                _poDisp = _poGhostV = po;
                _init = true;
            }

            _timeSinceHit += dt;
            _flash = Mathf.Max(0f, _flash - dt * 5f);
            _speedBoost = Mathf.Max(0f, _speedBoost - dt * 2.5f);

            _hpDisp   = HealthBarTweenUtility.SmoothApproach(_hpDisp, hp, 11f, dt);
            _hpGhostV = HealthBarTweenUtility.ComputeDelayedFill(_hpGhostV, hp, _timeSinceHit, 0.5f, 0.55f, dt);
            _enDisp   = HealthBarTweenUtility.SmoothApproach(_enDisp, en, 13f, dt);
            _enGhostV = HealthBarTweenUtility.ComputeDelayedFill(_enGhostV, en, _timeSinceHit, 0.35f, 0.9f, dt);
            _poDisp   = HealthBarTweenUtility.SmoothApproach(_poDisp, po, 13f, dt);
            _poGhostV = HealthBarTweenUtility.ComputeDelayedFill(_poGhostV, po, _timeSinceHit, 0.35f, 0.9f, dt);

            ApplyRow(_hp, _hpDisp, _hpGhostV,
                HealthBarTweenUtility.ComputeLowHealthIntensity(_hpDisp, 0.35f),
                vitals.Health != null ? Mathf.CeilToInt(vitals.Health.CurrentHealth) : 0,
                vitals.Health != null ? Mathf.CeilToInt(vitals.Health.MaxHealth) : 0);
            ApplyRow(_en, _enDisp, _enGhostV,
                HealthBarTweenUtility.ComputeLowHealthIntensity(_enDisp, 0.35f),
                Mathf.CeilToInt(vitals.Energy), Mathf.CeilToInt(vitals.Config != null ? vitals.Config.maxEnergy : 100f));
            // posture: unstable when HIGH - feed (1 - ratio), the StancePoiseBarFx trick
            ApplyRow(_po, _poDisp, _poGhostV,
                HealthBarTweenUtility.ComputeLowHealthIntensity(1f - _poDisp, 0.30f),
                Mathf.CeilToInt(vitals.Posture), Mathf.CeilToInt(vitals.Config != null ? vitals.Config.maxPosture : 100f),
                postureRatio: _poDisp);

            // near-full posture flash (spec §16.5)
            if (_po.fill != null)
            {
                if (po > 0.8f && !vitals.PostureIsFull)
                {
                    float f = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 18f);
                    _po.fill.color = Color.Lerp(Color.white, new Color(1f, 0.9f, 0.7f), f);
                }
                else _po.fill.color = Color.white;
            }

            _shake = Mathf.Max(0f, _shake - dt * 6f);
            _panel.anchoredPosition = _panelBase + HealthBarTweenUtility.ComputeShakeOffset(_shake, Time.unscaledTime, 5f);

            bool showF = execution != null && execution.PromptVisible;
            _fText.enabled = showF;
            if (showF)
                _fText.text = $"[ F ]  處決     {Mathf.CeilToInt(execution.WindowRemaining)}";
        }

        private void ApplyRow(BarRow r, float f, float g, float glow, int cur, int max, float postureRatio = -1f)
        {
            if (r == null) return;
            if (r.fill != null) r.fill.fillAmount = f;
            if (r.ghost != null) r.ghost.fillAmount = g;
            if (r.flow != null) r.flow.fillAmount = f;

            if (r.flowMat != null)
            {
                r.flowMat.SetFloat(HpRatioId, Mathf.Clamp01(postureRatio >= 0f ? 1f - postureRatio : f));
                r.flowMat.SetFloat(GlowIntId, Mathf.Clamp01(Mathf.Max(glow, _flash)));
                r.flowMat.SetFloat(FlashIntId, _flash);
                r.flowMat.SetFloat(SpeedBoostId, _speedBoost);
            }

            if (r.edgeGlow != null && r.track != null)
            {
                float x = HealthBarTweenUtility.ComputeEdgeGlowLocalX(f, r.track.rect.width, 2f, 2f);
                var p = r.edgeGlow.anchoredPosition;
                p.x = x - r.track.rect.width * 0.5f;
                r.edgeGlow.anchoredPosition = p;
            }

            if (r.value != null) r.value.text = $"{cur}/{max}";
        }

        private void SetShown(float a) { if (_group != null) _group.alpha = a; }

        // ------------------------------------------------------------------ spark burst (like StancePoiseBarFx)

        private void SparkBurst(BarRow r)
        {
            if (r == null || r.sparks == null || r.sparks.Length == 0) return;
            StartCoroutine(AnimateSparks(r));
        }

        private IEnumerator AnimateSparks(BarRow r)
        {
            var rng = new System.Random();
            var angles = new float[r.sparks.Length];
            for (int i = 0; i < r.sparks.Length; i++)
            {
                angles[i] = (float)(rng.NextDouble() * Mathf.PI * 2f);
                if (r.sparks[i] != null) r.sparks[i].gameObject.SetActive(true);
            }
            float life = 0.35f, t = 0f;
            while (t < life)
            {
                t += Time.unscaledDeltaTime;
                float t01 = Mathf.Clamp01(t / life);
                Vector2 origin = r.edgeGlow != null ? r.edgeGlow.anchoredPosition : Vector2.zero;
                for (int i = 0; i < r.sparks.Length; i++)
                {
                    if (r.sparks[i] == null) continue;
                    r.sparks[i].anchoredPosition = origin + HealthBarTweenUtility.ComputeSparkOffset(t01, angles[i], 90f, 220f);
                    if (r.sparkImgs[i] != null)
                    {
                        var c = r.sparkImgs[i].color; c.a = 1f - t01; r.sparkImgs[i].color = c;
                    }
                }
                yield return null;
            }
            for (int i = 0; i < r.sparks.Length; i++)
                if (r.sparks[i] != null) r.sparks[i].gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ self-build

        private void Build()
        {
            var canvasGo = new GameObject("YuanpeiBossHUDCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            _group = canvasGo.AddComponent<CanvasGroup>();
            _group.interactable = false;
            _group.blocksRaycasts = false;

            _panel = NewRect("Panel", canvasGo.transform);
            _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 1f);
            _panel.pivot = new Vector2(0.5f, 1f);
            _panel.anchoredPosition = new Vector2(0f, -24f);
            _panel.sizeDelta = new Vector2(980f, 104f);
            _panelBase = _panel.anchoredPosition;

            _nameText = NewText("Name", _panel, bossName, 24, TextAnchor.MiddleCenter);
            _nameText.rectTransform.anchoredPosition = new Vector2(0f, -12f);
            _nameText.rectTransform.sizeDelta = new Vector2(980f, 26f);
            _nameText.color = new Color(1f, 0.94f, 0.85f);

            _hp = BuildBar("生命", new Vector2(20f, -40f), new Vector2(900f, 24f), fillHpSprite,     flowHpSprite,     HpGlow);
            _en = BuildBar("能量", new Vector2(56f, -66f), new Vector2(600f, 13f), fillEnergySprite, flowEnergySprite, EnergyGlow);
            _po = BuildBar("架勢", new Vector2(56f, -84f), new Vector2(600f, 13f), fillStanceSprite, flowStanceSprite, PostureGlow);

            if (_en.fill != null) _en.fill.fillAmount = 1f;
            if (_en.ghost != null) _en.ghost.fillAmount = 1f;
            if (_en.flow != null) _en.flow.fillAmount = 1f;
            if (_po.fill != null) _po.fill.fillAmount = 0f;
            if (_po.ghost != null) _po.ghost.fillAmount = 0f;
            if (_po.flow != null) _po.flow.fillAmount = 0f;

            _fText = NewText("FPrompt", canvasGo.transform, "[ F ]  處決", 42, TextAnchor.MiddleCenter);
            _fText.rectTransform.anchorMin = _fText.rectTransform.anchorMax = new Vector2(0.5f, 0.34f);
            _fText.rectTransform.sizeDelta = new Vector2(700f, 84f);
            _fText.color = Color.white;
            _fText.fontStyle = FontStyle.Bold;
            _fText.enabled = false;
        }

        private BarRow BuildBar(string label, Vector2 pos, Vector2 size, Sprite fillSprite, Sprite flowSprite, Color glowColor)
        {
            var r = new BarRow();

            r.track = NewRect(label + "Track", _panel);
            r.track.anchoredPosition = pos;
            r.track.sizeDelta = size;
            r.trackBasePos = pos;
            // the player bars keep a HudRoundedRect container Image but leave it DISABLED - the
            // 01_Background sprite is the real backing. No white box (user: "不要有白色背景框").
            var container = r.track.gameObject.AddComponent<Image>();
            container.sprite = roundedRectSprite;
            container.type = roundedRectSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            container.color = new Color(1f, 1f, 1f, 0.14f);
            container.raycastTarget = false;
            container.enabled = false;

            Stretch(NewImage(label + "Background", r.track, backgroundSprite, Image.Type.Simple));
            Stretch(NewImage(label + "Frame", r.track, frameSprite, Image.Type.Simple));

            r.ghost = MakeFilled(NewImage(label + "DelayedFill", r.track, delayedFillSprite, Image.Type.Filled));
            r.fill  = MakeFilled(NewImage(label + "Fill", r.track, fillSprite, Image.Type.Filled));
            r.flow  = MakeFilled(NewImage(label + "Flow", r.track, flowSprite, Image.Type.Filled));

            if (flowMaterial != null && r.flow != null)
            {
                r.flowMat = new Material(flowMaterial);
                r.flowMat.name = flowMaterial.name + " (" + label + ")";
                r.flowMat.SetColor(GlowColorId, glowColor);
                r.flow.material = r.flowMat;
            }

            r.edgeGlow = NewRect(label + "EdgeGlow", r.track);
            r.edgeGlow.anchorMin = r.edgeGlow.anchorMax = new Vector2(0.5f, 0.5f);
            r.edgeGlow.sizeDelta = new Vector2(size.y * 1.6f, size.y * 1.6f);
            var eg = r.edgeGlow.gameObject.AddComponent<Image>();
            eg.sprite = sparkSprite;
            eg.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0.9f);
            eg.raycastTarget = false;

            r.sparks = new RectTransform[6];
            r.sparkImgs = new Image[6];
            for (int i = 0; i < 6; i++)
            {
                var s = NewRect(label + "Spark" + i, r.track);
                s.anchorMin = s.anchorMax = new Vector2(0.5f, 0.5f);
                s.sizeDelta = new Vector2(size.y * 0.7f, size.y * 0.7f);
                var si = s.gameObject.AddComponent<Image>();
                si.sprite = sparkSprite;
                si.color = new Color(glowColor.r, glowColor.g, glowColor.b, 1f);
                si.raycastTarget = false;
                s.gameObject.SetActive(false);
                r.sparks[i] = s;
                r.sparkImgs[i] = si;
            }

            r.value = NewText(label + "Value", r.track, "", Mathf.Max(10, Mathf.RoundToInt(size.y * 0.72f)), TextAnchor.MiddleRight);
            r.value.rectTransform.anchorMin = new Vector2(1f, 0.5f);
            r.value.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            r.value.rectTransform.pivot = new Vector2(1f, 0.5f);
            r.value.rectTransform.anchoredPosition = new Vector2(-6f, 0f);
            r.value.rectTransform.sizeDelta = new Vector2(140f, size.y + 6f);
            r.value.color = new Color(1f, 1f, 1f, 0.85f);

            var lab = NewText(label + "Label", _panel, label, 13, TextAnchor.MiddleRight);
            lab.rectTransform.anchoredPosition = new Vector2(pos.x - size.x * 0.5f - 26f, pos.y);
            lab.rectTransform.sizeDelta = new Vector2(48f, 18f);
            lab.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0.95f);

            return r;
        }

        private static Image NewImage(string name, RectTransform parent, Sprite sprite, Image.Type type)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = sprite != null ? type : Image.Type.Simple;
            img.raycastTarget = false;
            if (sprite == null) img.color = type == Image.Type.Filled ? Color.white : new Color(1f, 1f, 1f, 0.15f);
            return img;
        }

        private static Image MakeFilled(Image img)
        {
            Stretch(img);
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = (int)Image.OriginHorizontal.Left;
            img.fillAmount = 1f;
            return img;
        }

        private static void Stretch(Image img)
        {
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(2f, 2f);
            rt.offsetMax = new Vector2(-2f, -2f);
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Text NewText(string name, Transform parent, string content, int size, TextAnchor anchor)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.text = content;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = size;
            t.alignment = anchor;
            t.color = Color.white;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var outline = rt.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }
    }
}
