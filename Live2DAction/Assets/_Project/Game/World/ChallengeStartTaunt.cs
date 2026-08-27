using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Live2DAction.World
{
    // 2026-08-23, explicit user request ("當上升氣流機關啟用時 此圖片的文字ui就要出現 處理方式與
    // "想要起飛嗎" 一致") - originally fired on controller.IsRunning going false->true (i.e. the
    // moment the player pressed E at TimeTrialStartMechanism and the timed run began). Screen-
    // space rather than living on the same world-space PromptCanvas "想要起飛嗎" uses: by the
    // time the run starts the player is launching away from the pedestal, so a world-space image
    // anchored back there wouldn't stay in view.
    //
    // 2026-08-24 follow-up, explicit user request ("'還是做不到嗎'改成遊戲失敗時才顯示 並且是顯示
    // 在畫面正中央") - re-triggered on controller.IsShowingFailMessage instead (the challenge
    // actually timing out, not just starting) - "還是做不到嗎?" (are you STILL unable to do it?)
    // reads as a taunt about failing, not a "go!" flourish at the start line, so this now matches
    // what the text itself says. Same edge-detected-polling convention as before
    // (TimeTrialController still has no begin/end/fail C# events to subscribe to, just the
    // IsShowingFailMessage property added alongside this change). Centering is handled by
    // ChallengeStartTauntSetup (anchoredPosition), not here.
    public class ChallengeStartTaunt : MonoBehaviour
    {
        [SerializeField] private TimeTrialController controller;
        [SerializeField] private CanvasGroup canvasGroup;

        [SerializeField] private float fadeInSeconds = 0.25f;
        [SerializeField] private float holdSeconds = 1.6f;
        [SerializeField] private float fadeOutSeconds = 0.6f;

        private bool _wasShowingFailMessage;
        private Coroutine _sequenceCoroutine;

        private void Update()
        {
            bool isShowingFailMessage = controller != null && controller.IsShowingFailMessage;
            if (isShowingFailMessage && !_wasShowingFailMessage)
            {
                if (_sequenceCoroutine != null)
                {
                    StopCoroutine(_sequenceCoroutine);
                }
                _sequenceCoroutine = StartCoroutine(PlaySequence());
            }
            _wasShowingFailMessage = isShowingFailMessage;
        }

        private IEnumerator PlaySequence()
        {
            if (canvasGroup == null)
            {
                yield break;
            }

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.0001f, fadeInSeconds);
                canvasGroup.alpha = Mathf.Clamp01(t);
                yield return null;
            }

            yield return new WaitForSeconds(holdSeconds);

            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.0001f, fadeOutSeconds);
                canvasGroup.alpha = 1f - Mathf.Clamp01(t);
                yield return null;
            }

            _sequenceCoroutine = null;
        }
    }
}
