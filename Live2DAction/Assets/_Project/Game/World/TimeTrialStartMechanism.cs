using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Live2DAction.Input;

namespace Live2DAction.World
{
    // 2026-08-20, explicit user request ("我希望改為互動式，與機關對話後，會觸發短時間內的跑酷計時")
    // - the "機關" the player walks up to and presses E on to arm TimeTrialController's course.
    // Press-to-activate while standing in range mirrors Portal.cs's own existing "hold-to-enter,
    // press E to actually trigger" convention (same physical key, read the same direct
    // Keyboard.current way) rather than inventing a second interact-key convention for this
    // project.
    [RequireComponent(typeof(Collider))]
    public class TimeTrialStartMechanism : MonoBehaviour
    {
        [SerializeField] private TimeTrialController controller;

        // 2026-08-22 - the always-active Canvas host that promptText and promptVisualRoot both sit
        // under as children, billboarded every frame in LateUpdate regardless of which of them is
        // currently shown. Split out from promptText itself (which used to double as the Canvas
        // host - see LateUpdate's own 2026-08-20 comment) once promptVisualRoot needed to be visible
        // at times promptText is hidden and vice versa: SetActive(false) on whichever the Canvas
        // host used to also be would have taken every child down with it.
        [SerializeField] private Transform promptCanvasRoot;

        // World-space "空島競速 / 按 E 開始挑戰" prompt, shown only while the player is in range AND
        // controller.CanBeginChallenge is true (not, e.g., mid-challenge or mid-result-message) -
        // same world-space-Canvas-attached-to-the-interactable-object convention as this project's
        // other per-object UI (health bars, CheckpointGate's own ring).
        [SerializeField] private Text promptText;

        // 2026-08-20, explicit user request ("靠近機關能否在機關旁邊顯示按鍵提示和遊戲名稱") - shown
        // as the prompt's own first line, above whichever action line E currently performs. Same
        // name TimeTrialController's HUD opens every one of its own lines with, kept as a separate
        // field (not hardcoded twice) so renaming the challenge only means editing it in one place.
        [SerializeField] private string challengeName = "空島競速";

        // 2026-08-22, explicit user request ("讓[圖片]作為空中飛行小遊戲機關互動時的文字ui") - the
        // "想要起飛唯" fire-text image replaces promptText as the BEGIN-state prompt (shown only
        // while canBegin is true; the mid-run "按 E 結束挑戰" cancel prompt below still uses the
        // plain text, since the image itself only reads as a "want to fly?" invitation). Scaled via
        // promptVisualRoot below, not its own RectTransform, so the accompanying action-hint text
        // collapses together with it.
        [SerializeField] private RectTransform promptVisualRoot;

        // Small "按 E 開始挑戰" line under the image inside promptVisualRoot.
        [SerializeField] private Text actionHintText;

        // 2026-08-22, explicit user request ("按e確認開始遊戲時快速將文字收起，像是關閉電視螢幕那樣")
        // - true only while the TV-off collapse coroutine owns promptVisualRoot's scale. Update()
        // must not touch promptVisualRoot's active state while this is true, or the very next
        // frame's canBegin==false (BeginChallenge() already flipped _locked) would instantly
        // SetActive(false) it and skip the animation entirely.
        private bool _closingPrompt;
        private Coroutine _closeCoroutine;

        private bool _playerInside;

        private void Reset()
        {
            Collider trigger = GetComponent<Collider>();
            if (trigger != null)
            {
                trigger.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerInputProvider>() == null)
            {
                return;
            }

            _playerInside = true;
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponentInParent<PlayerInputProvider>() == null)
            {
                return;
            }

            _playerInside = false;
        }

        // 2026-08-20, explicit user request ("再按一次e能直接結束機關") - E now does double duty:
        // starts a challenge while locked and in range, or cancels an already-running one while
        // in range (CancelChallenge re-locks the course immediately, same as a real fail, just
        // without the "超過時限" framing - see TimeTrialController's own comment on it). The prompt
        // text itself swaps to reflect whichever action E would actually perform right now, so the
        // player isn't guessing.
        // Perf fix (2026-08-27, playtest report - "play mode模式下遊戲會卡頓") - actionHintText was a
        // constant literal (Text.text's own setter already no-ops on an unchanged value, so that
        // one was cheap, but promptText below built a NEW string via "+" concatenation every single
        // Update() while canCancel was true, even though challengeName never changes mid-run).
        // Edge-detecting canBegin/canCancel and only touching .text on the transition into true
        // turns both into "write once per prompt becoming visible" instead of "write every frame
        // it's visible".
        private bool _wasCanBegin;
        private bool _wasCanCancel;

        private void Update()
        {
            bool canBegin = _playerInside && controller != null && controller.CanBeginChallenge;
            bool canCancel = _playerInside && controller != null && controller.IsRunning;

            // Skip while the TV-off coroutine owns promptVisualRoot's active state - see
            // _closingPrompt's own comment for why touching it here would race the animation.
            // The image and actionHintText inside it stay permanently active; promptVisualRoot
            // (their shared parent) is the single switch that shows or hides both at once.
            if (!_closingPrompt && promptVisualRoot != null)
            {
                promptVisualRoot.gameObject.SetActive(canBegin);
            }
            if (canBegin && !_wasCanBegin && actionHintText != null)
            {
                actionHintText.text = "按 E 開始挑戰";
            }

            if (promptText != null)
            {
                promptText.gameObject.SetActive(canCancel);
                if (canCancel && !_wasCanCancel)
                {
                    promptText.text = challengeName + "\n按 E 結束挑戰";
                }
            }

            _wasCanBegin = canBegin;
            _wasCanCancel = canCancel;

            if (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame)
            {
                return;
            }

            if (canBegin)
            {
                controller.BeginChallenge();
                StartClosePromptAnimation();
            }
            else if (canCancel)
            {
                controller.CancelChallenge();
            }
        }

        // 2026-08-22, explicit user request ("按e確認開始遊戲時快速將文字收起，像是關閉電視螢幕那樣")
        // - classic CRT power-off: collapse to a thin horizontal line first (scale Y to 0 while X
        // holds), then collapse that line away (scale X to 0), all inside ~0.18s ("快速"). Runs
        // independently of BeginChallenge() (already called by the time this starts) so the
        // animation is purely cosmetic and never delays the actual challenge start.
        private void StartClosePromptAnimation()
        {
            if (promptVisualRoot == null)
            {
                return;
            }

            if (_closeCoroutine != null)
            {
                StopCoroutine(_closeCoroutine);
            }
            _closeCoroutine = StartCoroutine(ClosePromptCoroutine());
        }

        private System.Collections.IEnumerator ClosePromptCoroutine()
        {
            _closingPrompt = true;
            const float duration = 0.18f;
            Vector3 startScale = promptVisualRoot.localScale;

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / duration);
                float yScale = Mathf.Lerp(1f, 0f, Mathf.Clamp01(p / 0.55f)); // vertical collapse finishes first
                float xScale = Mathf.Lerp(1f, 0f, Mathf.Clamp01((p - 0.45f) / 0.55f)); // then the thin line collapses away
                promptVisualRoot.localScale = new Vector3(xScale, yScale, 1f);
                yield return null;
            }

            promptVisualRoot.gameObject.SetActive(false);
            promptVisualRoot.localScale = startScale; // reset so the next approach shows it at full size again
            _closingPrompt = false;
            _closeCoroutine = null;
        }

        // Same "match the camera's rotation" billboard as WorldSpaceHealthBar.LateUpdate - a flat
        // World Space Canvas should stay parallel to the camera's view plane, not just face it on
        // one axis.
        //
        // 2026-08-20, real bug fix (Console spam: "NullReferenceException" every frame at this
        // line) - was rotating promptText.canvas.transform. Text.canvas returns null whenever the
        // Text's own GameObject is inactive (which it starts as, and returns to every time the
        // player walks out of range - see Update() above), so this threw the instant the prompt
        // hid itself.
        //
        // 2026-08-22 - now rotates promptCanvasRoot (always active) instead of promptText.transform
        // directly. promptText's GameObject used to double as the Canvas host so this worked, but
        // now promptText and promptVisualRoot toggle independently (see promptCanvasRoot's own
        // comment) and can both be inactive at once - rotating a fixed, always-on host avoids
        // reintroducing the same activeInHierarchy-gated null risk this fix originally addressed.
        private void LateUpdate()
        {
            if (promptCanvasRoot == null)
            {
                return;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            promptCanvasRoot.rotation = mainCamera.transform.rotation;
        }
    }
}
