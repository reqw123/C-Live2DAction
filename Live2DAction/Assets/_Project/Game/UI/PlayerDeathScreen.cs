using UnityEngine;
using UnityEngine.UI;

namespace Live2DAction.UI
{
    // 2026-09-03, user request - when the player's Health hits 0, after the death animation the
    // centre of the screen shows a game-over taunt ("你菜完了"). Self-building screen-space banner
    // + dark vignette, one reused instance, unscaled fade (survives hit-stop). The caller
    // (RespawnController, which already tracks the death->respawn cycle) drives Show / Hide.
    public class PlayerDeathScreen : MonoBehaviour
    {
        private static PlayerDeathScreen _instance;

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
            var go = new GameObject("PlayerDeathScreen");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<PlayerDeathScreen>();
            _instance.Build();
        }

        private void Build()
        {
            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 880;   // above HUDs, below the load curtain
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            _cg = canvasGo.AddComponent<CanvasGroup>();
            _cg.interactable = false;
            _cg.blocksRaycasts = false;
            _cg.alpha = 0f;

            var vignette = NewRect("Vignette", canvasGo.transform);
            vignette.anchorMin = Vector2.zero;
            vignette.anchorMax = Vector2.one;
            vignette.offsetMin = vignette.offsetMax = Vector2.zero;
            var vImg = vignette.gameObject.AddComponent<Image>();
            vImg.color = new Color(0.15f, 0f, 0f, 0.55f);
            vImg.raycastTarget = false;

            var rt = NewRect("Text", canvasGo.transform);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.52f);
            rt.sizeDelta = new Vector2(1400f, 240f);
            _text = rt.gameObject.AddComponent<Text>();
            _text.text = "你菜完了";
            _text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _text.fontSize = 110;
            _text.fontStyle = FontStyle.Bold;
            _text.alignment = TextAnchor.MiddleCenter;
            _text.color = new Color(0.95f, 0.2f, 0.18f);
            _text.raycastTarget = false;
            _text.horizontalOverflow = HorizontalWrapMode.Overflow;
            _text.verticalOverflow = VerticalWrapMode.Overflow;
            var outline = rt.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.92f);
            outline.effectDistance = new Vector2(4f, -4f);
        }

        private void Update()
        {
            if (_cg == null) return;
            _cg.alpha = Mathf.MoveTowards(_cg.alpha, _target, Time.unscaledDeltaTime / 0.6f);
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }
    }
}
