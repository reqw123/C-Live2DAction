using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Live2DAction.World
{
    // 2026-09-06, user request - "動態互動提示 UI" for the Boss-map portal. When the player stands
    // in a SceneGate's interaction trigger, this shows a dialogue-frame video (對話系統ui框.mp4,
    // black background keyed to transparent by Live2DAction/UI/PortalDialogueFrame) + a centred
    // legacy-Text prompt ("按下 E 進入 Boss 地圖"). It ONLY draws UI: it never teleports, never
    // knows the scene name, never reads the keyboard. SceneGate owns the trigger, the key press
    // and the existing SceneTransitionRunner call, and drives this via Show / Hide / Confirm.
    //
    // One SceneGate "owns" the UI at a time (spec 10) - a second gate calling Show() while another
    // owns it is ignored.
    //
    // State machine (spec 五):
    //   Hidden          -> nothing visible, video stopped at frame 0
    //   Showing         -> video Play() from frame 0, CanvasGroup + text fading in
    //   WaitingForInput -> fully shown; video runs once then holds its last frame (isLooping=false).
    //                      A re-Show() from the same owner here is a no-op (no replay - spec 4/8).
    //   Confirmed       -> E was pressed: fast fade, video Stop(), owner released. Never delays the teleport.
    //   Hiding          -> player left the range: fade out, video Stop(), owner released
    //
    // Built + wired by PortalInteractionUISetup ("Tools/Live2DAction/Setup Portal Interaction UI").
    // Follows the self-contained screen-space pattern of YuanpeiVictoryBanner / PlayerDeathScreen,
    // but as a scene object so SceneGate can hold a same-scene serialized reference.
    public class PortalInteractionUIController : MonoBehaviour
    {
        public enum UIState { Hidden, Showing, WaitingForInput, Confirmed, Hiding }

        [Header("Refs (wired by PortalInteractionUISetup)")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private GameObject videoContainer;
        [SerializeField] private RawImage animatedFrameVideo;
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private CanvasGroup promptTextGroup;
        [SerializeField] private Text promptText;

        [Header("Text")]
        [Tooltip("Fallback message when a SceneGate doesn't pass one. Each gate normally supplies " +
                 "its own (enter vs exit differ). {KEY} is replaced with the interact-key label.")]
        [SerializeField] private string promptMessage = "按下 {KEY} 互動";
        [SerializeField] private string keyToken = "{KEY}";
        [SerializeField] private string fallbackKeyLabel = "F";

        [Header("Timing (unscaled - survives hit-stop / timeScale)")]
        [SerializeField] private float fadeInSeconds = 0.18f;
        [SerializeField] private float fadeOutSeconds = 0.20f;
        [SerializeField] private float confirmFadeSeconds = 0.10f;
        [Tooltip("2026-09-06 user request: the dialogue frame in the video 'stands up' over the " +
                 "first ~1.3s. Hold the prompt text hidden until it has finished rising, then fade " +
                 "it in. Measured from the start of Show() (frame 0 of the clip).")]
        [SerializeField] private float frameRiseSeconds = 1.4f;
        [SerializeField] private float textFadeSeconds = 0.20f;
        [Tooltip("Seconds to wait for VideoPlayer.Prepare() before playing anyway.")]
        [SerializeField] private float prepareTimeout = 5f;

        public UIState State { get; private set; } = UIState.Hidden;

        // 2026-09-06 - singleton so a SceneGate in ANY scene (e.g. SchoolGate_Exit inside
        // Map_School) can drive the shared prompt without a cross-scene serialized reference.
        // The Canvas lives in the persistent GreyboxTest scene.
        public static PortalInteractionUIController Instance { get; private set; }

        private SceneGate _owner;
        private string _ownerMessage;
        private Coroutine _anim;
        private bool _videoPrepared;
        private bool _quitting;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
            if (promptTextGroup != null) promptTextGroup.alpha = 0f;
            if (videoContainer != null) videoContainer.SetActive(true);
            SetPrompt(null);
            ConfigureVideo();
        }

        private void OnApplicationQuit() => _quitting = true;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (videoPlayer != null) videoPlayer.prepareCompleted -= OnPrepared;
        }

        private void ConfigureVideo()
        {
            if (videoPlayer == null) return;
            videoPlayer.playOnAwake = false;
            videoPlayer.waitForFirstFrame = true;
            videoPlayer.skipOnDrop = true;
            videoPlayer.isLooping = false;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            for (ushort i = 0; i < videoPlayer.controlledAudioTrackCount; i++)
                videoPlayer.SetDirectAudioMute(i, true);
            videoPlayer.prepareCompleted -= OnPrepared;
            videoPlayer.prepareCompleted += OnPrepared;
            videoPlayer.Prepare();
        }

        private void OnPrepared(VideoPlayer vp) => _videoPrepared = true;

        // ---- API called by SceneGate --------------------------------------------------------

        // Player is standing in owner's interaction range. keyLabel = the interact-key display
        // string ("F"); message = that gate's own prompt line ({KEY} gets substituted). Enter and
        // exit gates pass different messages.
        public void Show(SceneGate owner, string keyLabel, string message)
        {
            if (owner == null || _quitting) return;
            if (_owner != null && _owner != owner) return;   // another gate owns the UI (spec 10)

            _owner = owner;
            _ownerMessage = message;
            SetPrompt(keyLabel, message);

            // Already up (or animating in, or holding the last frame): leave it. Do NOT replay
            // the intro - the frame stays put while the player lingers (spec 4 + 8).
            if (State == UIState.Showing || State == UIState.WaitingForInput) return;

            // Caught it mid-fade-out: fade straight back in, do NOT rewind + replay the rise.
            if (State == UIState.Hiding) { StartAnim(ResumeRoutine()); return; }

            StartAnim(ShowRoutine());
        }

        // Player left the range without pressing the key (spec 6).
        public void Hide(SceneGate owner)
        {
            if (_owner != owner) return;
            if (State == UIState.Hidden || State == UIState.Hiding || State == UIState.Confirmed)
            {
                if (State == UIState.Hidden) _owner = null;
                return;
            }
            StartAnim(HideRoutine(fadeOutSeconds, confirmed: false));
        }

        // Player pressed the interact key - SceneGate is starting the teleport this same frame.
        // Fast dismiss; must never block or delay the teleport (spec 6).
        public void Confirm(SceneGate owner)
        {
            if (_owner != owner) return;
            State = UIState.Confirmed;
            StartAnim(HideRoutine(confirmFadeSeconds, confirmed: true));
        }

        // Hard reset - owner destroyed / scene torn down / re-arming defensively.
        public void ForceReset()
        {
            _owner = null;
            if (_anim != null) { StopCoroutine(_anim); _anim = null; }
            StopVideo();
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            if (promptTextGroup != null) promptTextGroup.alpha = 0f;
            State = UIState.Hidden;
        }

        // -----------------------------------------------------------------------------------

        private void StartAnim(IEnumerator routine)
        {
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(routine);
        }

        private IEnumerator ShowRoutine()
        {
            State = UIState.Showing;

            if (videoPlayer != null)
            {
                if (!_videoPrepared)
                {
                    videoPlayer.Prepare();
                    float t0 = Time.unscaledTime;
                    while (!_videoPrepared && Time.unscaledTime - t0 < prepareTimeout) yield return null;
                }
                videoPlayer.frame = 0;      // spec 2/7 - always from the first frame
                videoPlayer.Play();
            }

            // text stays hidden until the frame has finished "standing up" in the video (below)
            if (promptTextGroup != null) promptTextGroup.alpha = 0f;

            // fade the frame (video) in
            float t = 0f;
            float startAlpha = canvasGroup != null ? canvasGroup.alpha : 0f;
            while (t < fadeInSeconds)
            {
                t += Time.unscaledDeltaTime;
                float k = fadeInSeconds > 0f ? Mathf.Clamp01(t / fadeInSeconds) : 1f;
                if (canvasGroup != null) canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, k);
                yield return null;
            }
            if (canvasGroup != null) canvasGroup.alpha = 1f;

            // 2026-09-06 user request - wait for the dialogue frame's rise animation to finish,
            // THEN reveal the prompt text (measured from Show() start, so a re-Show that rewinds
            // the clip to frame 0 waits again).
            float remaining = frameRiseSeconds - fadeInSeconds;
            while (remaining > 0f) { remaining -= Time.unscaledDeltaTime; yield return null; }
            yield return FadeText(1f);

            State = UIState.WaitingForInput;
            _anim = null;
        }

        // Show() called while a hide fade was in progress - just fade back to full, keep the video
        // running where it is (no rewind, no re-rise, no text delay).
        private IEnumerator ResumeRoutine()
        {
            State = UIState.Showing;
            if (videoPlayer != null && _videoPrepared && !videoPlayer.isPlaying) videoPlayer.Play();

            float t = 0f;
            float ca = canvasGroup != null ? canvasGroup.alpha : 0f;
            float ta = promptTextGroup != null ? promptTextGroup.alpha : 0f;
            while (t < fadeInSeconds)
            {
                t += Time.unscaledDeltaTime;
                float k = fadeInSeconds > 0f ? Mathf.Clamp01(t / fadeInSeconds) : 1f;
                if (canvasGroup != null) canvasGroup.alpha = Mathf.Lerp(ca, 1f, k);
                if (promptTextGroup != null) promptTextGroup.alpha = Mathf.Lerp(ta, 1f, k);
                yield return null;
            }
            if (canvasGroup != null) canvasGroup.alpha = 1f;
            if (promptTextGroup != null) promptTextGroup.alpha = 1f;

            State = UIState.WaitingForInput;
            _anim = null;
        }

        private IEnumerator HideRoutine(float seconds, bool confirmed)
        {
            if (!confirmed) State = UIState.Hiding;

            float t = 0f;
            float startAlpha = canvasGroup != null ? canvasGroup.alpha : 0f;
            float startText = promptTextGroup != null ? promptTextGroup.alpha : 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                float k = seconds > 0f ? Mathf.Clamp01(t / seconds) : 1f;
                if (canvasGroup != null) canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, k);
                if (promptTextGroup != null) promptTextGroup.alpha = Mathf.Lerp(startText, 0f, k);
                yield return null;
            }
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            if (promptTextGroup != null) promptTextGroup.alpha = 0f;

            StopVideo();
            State = UIState.Hidden;
            _owner = null;
            _anim = null;
        }

        private IEnumerator FadeText(float target)
        {
            if (promptTextGroup == null) yield break;
            float t = 0f;
            float start = promptTextGroup.alpha;
            while (t < textFadeSeconds)
            {
                t += Time.unscaledDeltaTime;
                float k = textFadeSeconds > 0f ? Mathf.Clamp01(t / textFadeSeconds) : 1f;
                promptTextGroup.alpha = Mathf.Lerp(start, target, k);
                yield return null;
            }
            promptTextGroup.alpha = target;
        }

        private void StopVideo()
        {
            if (videoPlayer == null) return;
            if (videoPlayer.isPlaying) videoPlayer.Stop();     // spec: Stop() on close
            if (_videoPrepared) videoPlayer.frame = 0;         // spec: rewind before the next open
        }

        private void SetPrompt(string keyLabel, string message = null)
        {
            if (promptText == null) return;
            string label = string.IsNullOrEmpty(keyLabel) ? fallbackKeyLabel : keyLabel;
            string msg = string.IsNullOrEmpty(message) ? promptMessage : message;
            if (!string.IsNullOrEmpty(keyToken) && msg.Contains(keyToken))
                msg = msg.Replace(keyToken, label);
            if (promptText.text != msg) promptText.text = msg;
        }
    }
}
