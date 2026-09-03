using UnityEngine;
using UnityEngine.UI;

namespace Live2DAction.AI.Boss.Yuanpei
{
    // spec §8.2 - the perfect-dodge "白色閃光". A self-building screen-space white curtain that
    // pulses to a peak alpha and fades out over a short duration (unscaled, so it still animates
    // during the perfect-dodge hit-stop). Lazily created on first Flash(); one instance, reused.
    public class YuanpeiScreenFlash : MonoBehaviour
    {
        private static YuanpeiScreenFlash _instance;

        private Image _image;
        private float _t, _dur, _peak;
        private Color _color = Color.white;

        public static void Flash(float peakAlpha = 0.55f, float seconds = 0.14f, Color? color = null)
        {
            if (_instance == null)
            {
                var go = new GameObject("YuanpeiScreenFlash");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<YuanpeiScreenFlash>();
                _instance.Build();
            }
            _instance._color = color ?? Color.white;
            _instance._peak = Mathf.Clamp01(peakAlpha);
            _instance._dur = Mathf.Max(0.02f, seconds);
            _instance._t = 0f;
        }

        private void Build()
        {
            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;   // above the boss HUD (500), below any true fatal-error overlays
            var cg = canvasGo.AddComponent<CanvasGroup>();
            cg.interactable = false;
            cg.blocksRaycasts = false;

            var imgGo = new GameObject("Flash", typeof(RectTransform));
            imgGo.transform.SetParent(canvasGo.transform, false);
            var rt = (RectTransform)imgGo.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            _image = imgGo.AddComponent<Image>();
            _image.raycastTarget = false;
            _image.color = new Color(1f, 1f, 1f, 0f);
        }

        private void Update()
        {
            if (_image == null) return;
            if (_t >= _dur)
            {
                if (_image.color.a != 0f) _image.color = new Color(_color.r, _color.g, _color.b, 0f);
                return;
            }
            _t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(_t / _dur);
            // fast rise, slower fall
            float a = k < 0.25f ? Mathf.Lerp(0f, _peak, k / 0.25f)
                                : Mathf.Lerp(_peak, 0f, (k - 0.25f) / 0.75f);
            _image.color = new Color(_color.r, _color.g, _color.b, a);
        }
    }
}
