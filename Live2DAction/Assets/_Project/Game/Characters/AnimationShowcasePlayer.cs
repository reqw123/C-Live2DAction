using System.Collections;
using UnityEngine;

namespace Live2DAction.Characters
{
    // 2026-08-19, explicit user request ("把這一整套動作都套用在中立者身上，並且讓他依序把動作展示
    // 完，動作間隔0.5秒停頓") - plays a fixed sequence of Animator states one after another (each
    // fired via its own Trigger parameter - see SwordShowcaseAnimatorSetup.cs for how those got
    // wired into the shared AnimatorController, same AnyState-transition pattern
    // CombatAnimatorSetup already uses for Attack1-4), with a fixed pause between each clip.
    //
    // Deliberately timed off each AnimationClip's own `length` (known at edit time, stored
    // directly rather than re-derived from live Animator state info) rather than polling
    // `GetCurrentAnimatorStateInfo` - the clip reference is already sitting right here in the
    // inspector, so there's no reason to trust a possibly-desynced runtime read of "how far
    // through the current state are we" instead of the number that was true the whole time.
    //
    // 2026-08-19 follow-up, explicit user request ("讓中立者1重複撥放這四個動作") - originally
    // played exactly once and stopped on Idle (that day's own earlier explicit request); now
    // loops the whole sequence indefinitely instead. `loop` defaults true to match this latest
    // request; kept as a field (not just deleting the old single-pass behavior) since nothing
    // about the component itself is loop-specific - a future "just show it once" request doesn't
    // need this rewritten again.
    public class AnimationShowcasePlayer : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private AnimationClip[] clips;
        [SerializeField] private string[] triggerNames;
        [SerializeField] private float pauseBetweenSeconds = 0.5f;
        [SerializeField] private bool playOnStart = true;
        [SerializeField] private bool loop = true;

        private void Start()
        {
            if (playOnStart)
            {
                StartCoroutine(PlaySequence());
            }
        }

        public void Play()
        {
            StopAllCoroutines();
            StartCoroutine(PlaySequence());
        }

        private IEnumerator PlaySequence()
        {
            if (animator == null || clips == null || triggerNames == null)
            {
                yield break;
            }

            int count = Mathf.Min(clips.Length, triggerNames.Length);
            do
            {
                for (int i = 0; i < count; i++)
                {
                    if (clips[i] == null || string.IsNullOrEmpty(triggerNames[i]))
                    {
                        continue;
                    }

                    animator.SetTrigger(triggerNames[i]);
                    yield return new WaitForSeconds(clips[i].length);
                    yield return new WaitForSeconds(pauseBetweenSeconds);
                }
            }
            while (loop);
        }
    }
}
