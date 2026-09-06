using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;

namespace Live2DAction.World
{
    // 2026-09-06, user request - the proper full-screen Boss-map loading screen. It EXTENDS the
    // existing ScreenFader + SceneTransitionRunner (it is NOT a new manager): ScreenFader stays
    // covered underneath for the black backing + the existing PlayerInputProvider input-lock
    // (input is zeroed while ScreenFader.IsCovered), this only adds the video canvas on top.
    // SceneTransitionRunner.Begin(..., useLoadingScreen: true) drives it (SchoolGate_Enter only).
    //
    // Self-builds LoadingScreenCanvas in Awake (like ScreenFader). Serialized asset refs (video
    // clips / RenderTexture / TMP font) are wired by BossLoadingScreenSetup.
    //
    //   LoadingScreenCanvas (sortingOrder 32100 - above ScreenFader 32000 + every HUD/dialogue)
    //   ├─ VideoRawImage   (RawImage + AspectRatioFitter EnvelopeParent - 16:9 aspect-fill, no stretch)
    //   ├─ LoadingInfo     (bottom-right, over the video's own lined panel, left of its spinner)
    //   │  ├─ LoadingText  (TMP "正在進入元培禁域……")
    //   │  └─ ProgressText (TMP "載入中 0%" - real remapped async progress)
    //   └─ FadeOverlay     (black Image - opaque until VideoPlayer.prepareCompleted, then fades out)
    [DefaultExecutionOrder(-50)]
    public class BossLoadingScreen : MonoBehaviour
    {
        public static BossLoadingScreen Instance { get; private set; }

        [Header("Assets (wired by BossLoadingScreenSetup)")]
        [Tooltip("One is picked per show, round-robin, so both variants get used (user: 輪流顯示).")]
        [SerializeField] private VideoClip[] clips;
        [SerializeField] private RenderTexture renderTexture;
        [SerializeField] private TMP_FontAsset font;

        [Header("Text")]
        [SerializeField] private string loadingLine = "正在進入元培禁域……";
        [Tooltip("{0} is the integer percent.")]
        [SerializeField] private string progressFormat = "載入中 {0}%";

        [Header("Look / timing (all unscaled)")]
        [SerializeField] private int canvasSortingOrder = 32100;
        [SerializeField] private float videoRevealSeconds = 0.35f;
        [SerializeField] private float infoFadeSeconds = 0.22f;
        [SerializeField] private float hideFadeSeconds = 0.35f;
        [SerializeField] private float prepareTimeout = 6f;
        [SerializeField] private Color textColor = new Color(0.86f, 0.92f, 1f, 1f);

        private Canvas _canvas;
        private CanvasGroup _rootGroup;
        private RawImage _raw;
        private VideoPlayer _vp;
        private CanvasGroup _fadeGroup;
        private Image _fadeImg;
        private CanvasGroup _infoGroup;
        private TMP_Text _loadingText;
        private TMP_Text _progressText;

        private bool _videoPrepared;
        private int _clipIndex = -1;

        public bool IsShowing { get; private set; }
        public bool HasClips => clips != null && clips.Length > 0 && clips[0] != null;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Build();
        }

        private void OnDestroy()
        {
            if (_vp != null) _vp.prepareCompleted -= OnPrepared;
            if (Instance == this) Instance = null;
        }

        // ---- API called by SceneTransitionRunner --------------------------------------------

        // Enable the canvas, prepare + start a (round-robin) clip, then reveal the video once it is
        // ready. Never shows a blank / white RawImage - FadeOverlay is opaque black until
        // VideoPlayer.prepareCompleted fires.
        public IEnumerator Show()
        {
            if (_canvas == null) yield break;
            IsShowing = true;
            _canvas.gameObject.SetActive(true);
            _rootGroup.alpha = 1f;
            _rootGroup.blocksRaycasts = true;
            _fadeGroup.alpha = 1f;
            _fadeImg.raycastTarget = true;
            _infoGroup.alpha = 0f;
            SetProgress(0f);
            ApplyText();

            if (HasClips)
            {
                _clipIndex = (_clipIndex + 1) % clips.Length;
                _vp.clip = clips[_clipIndex];
            }

            _videoPrepared = false;
            if (_vp.clip != null)
            {
                _vp.Prepare();
                float t0 = Time.unscaledTime;
                while (!_videoPrepared && Time.unscaledTime - t0 < prepareTimeout) yield return null;
                if (!_videoPrepared)
                    Debug.LogWarning("[BossLoadingScreen] video did not prepare within " + prepareTimeout +
                                     "s - revealing anyway (black behind).");
                _vp.frame = 0;
                _vp.Play();
            }
            else
            {
                Debug.LogWarning("[BossLoadingScreen] no VideoClip assigned - loading screen will be black.");
            }

            // reveal: fade the black overlay out (showing the prepared video), fade the info in
            yield return Fade(_fadeGroup, _fadeGroup.alpha, 0f, videoRevealSeconds);
            _fadeImg.raycastTarget = false;
            yield return Fade(_infoGroup, 0f, 1f, infoFadeSeconds);
        }

        // Real async load progress, 0..1, already remapped past Unity's 0.9 activation stall by the
        // runner. Never a fake random number.
        public void SetProgress(float t01)
        {
            if (_progressText == null) return;
            int pct = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(t01) * 100f), 0, 100);
            _progressText.text = string.Format(progressFormat, pct);
        }

        // Fade the whole screen out, stop the video, disable the canvas.
        public IEnumerator Hide()
        {
            if (_canvas == null || !IsShowing) yield break;
            yield return Fade(_rootGroup, _rootGroup.alpha, 0f, hideFadeSeconds);
            StopAndDisable();
        }

        // Load failed - drop everything immediately; the runner then uncovers ScreenFader so the
        // player is never stuck on black.
        public void AbortImmediate() => StopAndDisable();

        // -------------------------------------------------------------------------------------

        private void StopAndDisable()
        {
            if (_vp != null && _vp.isPlaying) _vp.Stop();
            if (_rootGroup != null) { _rootGroup.alpha = 0f; _rootGroup.blocksRaycasts = false; }
            if (_infoGroup != null) _infoGroup.alpha = 0f;
            if (_canvas != null) _canvas.gameObject.SetActive(false);
            IsShowing = false;
        }

        private void OnPrepared(VideoPlayer vp) => _videoPrepared = true;

        private void ApplyText()
        {
            if (_loadingText != null) _loadingText.text = loadingLine;
        }

        private static IEnumerator Fade(CanvasGroup g, float from, float to, float seconds)
        {
            if (g == null) yield break;
            g.alpha = from;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                g.alpha = Mathf.Lerp(from, to, seconds > 0f ? Mathf.Clamp01(t / seconds) : 1f);
                yield return null;
            }
            g.alpha = to;
        }

        // ---- build --------------------------------------------------------------------------

        private void Build()
        {
            var canvasGo = new GameObject("LoadingScreenCanvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            canvasGo.transform.SetParent(transform, false);

            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = canvasSortingOrder;   // above ScreenFader (32000) and every HUD

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            _rootGroup = canvasGo.GetComponent<CanvasGroup>();
            _rootGroup.alpha = 0f;
            _rootGroup.interactable = false;
            _rootGroup.blocksRaycasts = false;

            // --- VideoRawImage: 16:9 aspect-FILL (envelope) so it fills the screen without stretch
            var videoGo = NewRect("VideoRawImage", canvasGo.transform);
            videoGo.anchorMin = videoGo.anchorMax = new Vector2(0.5f, 0.5f);
            videoGo.pivot = new Vector2(0.5f, 0.5f);
            videoGo.sizeDelta = new Vector2(1920f, 1080f);
            var arf = videoGo.gameObject.AddComponent<AspectRatioFitter>();
            arf.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            arf.aspectRatio = 16f / 9f;
            _raw = videoGo.gameObject.AddComponent<RawImage>();
            _raw.texture = renderTexture;
            _raw.raycastTarget = false;
            _raw.color = Color.white;

            _vp = videoGo.gameObject.AddComponent<VideoPlayer>();
            _vp.source = VideoSource.VideoClip;
            _vp.renderMode = VideoRenderMode.RenderTexture;
            _vp.targetTexture = renderTexture;
            _vp.playOnAwake = false;
            _vp.waitForFirstFrame = true;
            _vp.skipOnDrop = true;
            _vp.isLooping = true;                 // loop while the scene loads (seam is ffmpeg-crossfaded)
            _vp.audioOutputMode = VideoAudioOutputMode.None;
            _vp.prepareCompleted -= OnPrepared;
            _vp.prepareCompleted += OnPrepared;

            // --- LoadingInfo: bottom-right, over the video's lined panel, left of its spinner
            var infoGo = NewRect("LoadingInfo", canvasGo.transform);
            infoGo.anchorMin = infoGo.anchorMax = new Vector2(1f, 0f);
            infoGo.pivot = new Vector2(1f, 0f);
            infoGo.sizeDelta = new Vector2(440f, 130f);
            infoGo.anchoredPosition = new Vector2(-78f, 112f);
            _infoGroup = infoGo.gameObject.AddComponent<CanvasGroup>();
            _infoGroup.alpha = 0f;
            _infoGroup.interactable = false;
            _infoGroup.blocksRaycasts = false;

            _loadingText = MakeText("LoadingText", infoGo, loadingLine, 30f, top: true, height: 46f);
            _progressText = MakeText("ProgressText", infoGo, string.Format(progressFormat, 0), 26f, top: false, height: 42f);

            // --- FadeOverlay: black on top until the video is prepared
            var fadeGo = NewRect("FadeOverlay", canvasGo.transform);
            Stretch(fadeGo);
            _fadeImg = fadeGo.gameObject.AddComponent<Image>();
            _fadeImg.color = Color.black;
            _fadeImg.raycastTarget = true;
            _fadeGroup = fadeGo.gameObject.AddComponent<CanvasGroup>();
            _fadeGroup.alpha = 1f;
            _fadeGroup.interactable = false;

            canvasGo.SetActive(false);
        }

        private TMP_Text MakeText(string name, RectTransform parent, string text, float size, bool top, float height)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = new Vector2(0f, top ? 1f : 0f);
            rt.anchorMax = new Vector2(1f, top ? 1f : 0f);
            rt.pivot = new Vector2(0.5f, top ? 1f : 0f);
            rt.offsetMin = new Vector2(0f, 0f);
            rt.offsetMax = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(0f, height);
            rt.anchoredPosition = new Vector2(0f, top ? -4f : 6f);

            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = size;
            t.alignment = TextAlignmentOptions.Left;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow;
            t.color = textColor;
            t.raycastTarget = false;

            // subtle soft drop-shadow (TMP underlay - not the uGUI Shadow effect, which mis-renders on TMP)
            var mat = t.fontMaterial;   // instances the shared material
            mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            mat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0.04f, 0.10f, 0.9f));
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.75f);
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.75f);
            mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.2f);
            return t;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
    }
}
