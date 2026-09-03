using UnityEngine;
using UnityEngine.UI;

namespace Live2DAction.AI.Boss.Yuanpei
{
    // Screen-space Boss HUD (spec §16): name, HP (red, prominent), Energy (cyan sub-bar, drains
    // from full), Posture (amber sub-bar, fills from 0). Flashes near full posture. Shows [F] 處決
    // only when the execution controller says the prompt is valid (spec §16.6-7). Self-builds its
    // canvas in Awake - no prefab.
    public class YuanpeiBossHUD : MonoBehaviour
    {
        [SerializeField] private YuanpeiBossVitals vitals;
        [SerializeField] private YuanpeiExecution execution;
        [SerializeField] private string bossName = "yuanpei_LogoSky";

        private CanvasGroup _group;
        private Image _hpFill, _energyFill, _postureFill;
        private Text _nameText, _fText;
        private float _shown; // 0..1 fade

        private void Awake()
        {
            if (vitals == null) vitals = GetComponent<YuanpeiBossVitals>();
            if (execution == null) execution = GetComponent<YuanpeiExecution>();
            Build();
            SetShown(0f);
        }

        public void SetVisible(bool v) => _target = v ? 1f : 0f;
        private float _target;

        private void Update()
        {
            _shown = Mathf.MoveTowards(_shown, _target, Time.unscaledDeltaTime * 3f);
            SetShown(_shown);
            if (_shown <= 0.01f || vitals == null) return;

            _hpFill.fillAmount = Mathf.Lerp(_hpFill.fillAmount, vitals.HealthNormalized, Time.deltaTime * 8f);
            _energyFill.fillAmount = Mathf.Lerp(_energyFill.fillAmount, vitals.EnergyNormalized, Time.deltaTime * 10f);
            _postureFill.fillAmount = Mathf.Lerp(_postureFill.fillAmount, vitals.PostureNormalized, Time.deltaTime * 10f);

            // posture near-full flash (spec §16.5)
            if (vitals.PostureNormalized > 0.8f && !vitals.PostureIsFull)
            {
                float f = 0.5f + 0.5f * Mathf.Sin(Time.time * 16f);
                _postureFill.color = Color.Lerp(new Color(1f, 0.6f, 0.1f), Color.white, f);
            }
            else _postureFill.color = new Color(1f, 0.6f, 0.1f);

            bool showF = execution != null && execution.PromptVisible;
            _fText.enabled = showF;
            if (showF) _fText.text = $"[F] 處決   {Mathf.CeilToInt(execution.WindowRemaining)}";
        }

        private void SetShown(float a)
        {
            if (_group == null) return;
            _group.alpha = a;
        }

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
            _group.interactable = false; _group.blocksRaycasts = false;

            var panel = NewRect("Panel", canvasGo.transform);
            panel.anchorMin = new Vector2(0.5f, 1f); panel.anchorMax = new Vector2(0.5f, 1f);
            panel.pivot = new Vector2(0.5f, 1f);
            panel.anchoredPosition = new Vector2(0f, -28f);
            panel.sizeDelta = new Vector2(900f, 92f);

            _nameText = NewText("Name", panel, bossName, 26, TextAnchor.MiddleCenter);
            _nameText.rectTransform.anchoredPosition = new Vector2(0f, -14f);
            _nameText.rectTransform.sizeDelta = new Vector2(900f, 30f);

            _hpFill = NewBar("HP", panel, new Vector2(0f, -44f), new Vector2(880f, 20f), new Color(0.85f, 0.1f, 0.12f));
            _energyFill = NewBar("Energy", panel, new Vector2(0f, -66f), new Vector2(560f, 10f), new Color(0.2f, 0.8f, 0.95f));
            _postureFill = NewBar("Posture", panel, new Vector2(0f, -80f), new Vector2(560f, 10f), new Color(1f, 0.6f, 0.1f));
            _energyFill.fillAmount = 1f;
            _postureFill.fillAmount = 0f;

            _fText = NewText("FPrompt", canvasGo.transform, "[F] 處決", 40, TextAnchor.MiddleCenter);
            _fText.rectTransform.anchorMin = _fText.rectTransform.anchorMax = new Vector2(0.5f, 0.32f);
            _fText.rectTransform.sizeDelta = new Vector2(600f, 80f);
            _fText.color = Color.white;
            _fText.fontStyle = FontStyle.Bold;
            _fText.enabled = false;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image NewBar(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
        {
            var bg = NewRect(name + "BG", parent);
            bg.anchoredPosition = pos; bg.sizeDelta = size;
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.6f);

            var fillRt = NewRect(name + "Fill", bg);
            fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(2, 2); fillRt.offsetMax = new Vector2(-2, -2);
            var img = fillRt.gameObject.AddComponent<Image>();
            img.color = color;
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = (int)Image.OriginHorizontal.Left;
            img.fillAmount = 1f;
            img.sprite = null;
            return img;
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
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var outline = rt.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.85f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }
    }
}
