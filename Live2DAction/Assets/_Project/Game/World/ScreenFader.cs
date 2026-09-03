using UnityEngine;
using UnityEngine.UI;

namespace Live2DAction.World
{
    // 2026-09-02, user request (map streaming Phase 2) - a full-screen solid-colour curtain that
    // hides the pop-in while a streamed region scene loads (the ~3M-tri yuanpei MeshColliders cook
    // on the main thread the frame the additive scene activates - a visible hitch + geometry
    // appearing from nothing). MapStreamer drives it: cover before LoadSceneAsync, hold until the
    // scene is loaded + a couple of settle frames, reveal.
    //
    // Self-bootstraps its own Canvas / Image / CanvasGroup in Awake so wiring is just "put a
    // ScreenFader component on a GameObject in the persistent scene" - no prefab, no setup menu.
    // Singleton; anything that wants a fade calls ScreenFader.Instance?.SetCovered(...).
    [DefaultExecutionOrder(-50)]
    public class ScreenFader : MonoBehaviour
    {
        public static ScreenFader Instance { get; private set; }

        [SerializeField] private Color curtainColor = Color.black;
        [Tooltip("Canvas sortingOrder - above every HUD canvas in the scene so nothing pokes through.")]
        [SerializeField] private int sortingOrder = 32000;

        private CanvasGroup _group;
        private Text _label;
        private float _current;
        private float _target;
        private float _speed; // alpha units per second; 0 = snap

        public bool IsCovered => _target > 0.5f;
        public bool IsFullyCovered => _current >= 0.999f;
        public bool IsFullyClear => _current <= 0.001f;
        public float Alpha => _current;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BuildCanvas();
            ApplyAlpha(0f);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // target 1 = fully covered (opaque), 0 = clear. fadeSeconds <= 0 snaps.
        public void SetCovered(bool covered, float fadeSeconds)
        {
            _target = covered ? 1f : 0f;
            _speed = fadeSeconds > 0.0001f ? 1f / fadeSeconds : 0f;
            if (_speed <= 0f) ApplyAlpha(_target);
        }

        // Centred text on the curtain (a loading label). Fades with the curtain (it's under the
        // same CanvasGroup). Pass null/empty or call ClearLabel to hide it.
        public void SetLabel(string text)
        {
            if (_label == null) return;
            _label.text = text ?? string.Empty;
            _label.enabled = !string.IsNullOrEmpty(_label.text);
        }

        public void ClearLabel() => SetLabel(null);

        private void Update()
        {
            if (Mathf.Approximately(_current, _target)) return;

            float next = _speed <= 0f
                ? _target
                : Mathf.MoveTowards(_current, _target, _speed * Time.unscaledDeltaTime);
            ApplyAlpha(next);
        }

        private void ApplyAlpha(float a)
        {
            _current = a;
            if (_group == null) return;
            _group.alpha = a;
            _group.blocksRaycasts = a > 0.001f;
        }

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("ScreenFaderCanvas");
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            canvasGo.AddComponent<GraphicRaycaster>();
            _group = canvasGo.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            var imageGo = new GameObject("Curtain");
            imageGo.transform.SetParent(canvasGo.transform, false);
            var image = imageGo.AddComponent<Image>();
            image.color = curtainColor;
            var rt = image.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(canvasGo.transform, false);
            _label = labelGo.AddComponent<Text>();
            _label.alignment = TextAnchor.MiddleCenter;
            _label.color = Color.white;
            _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _label.fontSize = 44;
            _label.horizontalOverflow = HorizontalWrapMode.Overflow;
            _label.verticalOverflow = VerticalWrapMode.Overflow;
            _label.enabled = false;
            var lrt = _label.rectTransform;
            lrt.anchorMin = new Vector2(0.5f, 0.5f);
            lrt.anchorMax = new Vector2(0.5f, 0.5f);
            lrt.sizeDelta = new Vector2(800f, 120f);
            lrt.anchoredPosition = Vector2.zero;
        }
    }
}
