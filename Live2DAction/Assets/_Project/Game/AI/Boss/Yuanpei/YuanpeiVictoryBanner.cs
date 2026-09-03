using UnityEngine;
using UnityEngine.UI;

namespace Live2DAction.AI.Boss.Yuanpei
{
    // spec §20 - the centre-screen "戰鬥勝利" banner shown after the boss dissolves. Self-building
    // screen-space text, one reused instance, unscaled fade so it still animates through any
    // hit-stop. The encounter owns the timing (show -> hold -> hide -> return the player).
    public class YuanpeiVictoryBanner : MonoBehaviour
    {
        private static YuanpeiVictoryBanner _instance;

        private CanvasGroup _cg;
        private Text _text;
        private float _target;

        public static void Show(string message)
        {
            Ensure();
            _instance._text.text = message;
            _instance._target = 1f;
        }

        public static void Hide()
        {
            if (_instance != null) _instance._target = 0f;
        }

        private static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("YuanpeiVictoryBanner");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<YuanpeiVictoryBanner>();
            _instance.Build();
        }

        private void Build()
        {
            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 850; // above the boss HUD (500), below the load curtain (32000)
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            _cg = canvasGo.AddComponent<CanvasGroup>();
            _cg.interactable = false;
            _cg.blocksRaycasts = false;
            _cg.alpha = 0f;

            var shade = NewRect("Shade", canvasGo.transform);
            shade.anchorMin = new Vector2(0f, 0.34f);
            shade.anchorMax = new Vector2(1f, 0.66f);
            shade.offsetMin = shade.offsetMax = Vector2.zero;
            var shadeImg = shade.gameObject.AddComponent<Image>();
            shadeImg.color = new Color(0f, 0f, 0f, 0.42f);
            shadeImg.raycastTarget = false;

            var rt = NewRect("Text", canvasGo.transform);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1400f, 220f);
            _text = rt.gameObject.AddComponent<Text>();
            _text.text = "戰鬥勝利";
            _text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _text.fontSize = 96;
            _text.fontStyle = FontStyle.Bold;
            _text.alignment = TextAnchor.MiddleCenter;
            _text.color = new Color(1f, 0.95f, 0.8f);
            _text.raycastTarget = false;
            _text.horizontalOverflow = HorizontalWrapMode.Overflow;
            _text.verticalOverflow = VerticalWrapMode.Overflow;
            var outline = rt.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(3f, -3f);
        }

        private void Update()
        {
            if (_cg == null) return;
            _cg.alpha = Mathf.MoveTowards(_cg.alpha, _target, Time.unscaledDeltaTime / 0.5f);
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }
    }
}
