using System;
using UnityEngine;
using UnityEngine.UI;
using Live2DAction.Characters;
using Live2DAction.Input;

namespace Live2DAction.World
{
    // 2026-08-19, explicit user request ("3D動作遊戲不是常常有那種 障礙物跨越 或是跳躍爬高的比賽嗎
    // 怎麼設計") - one gate in a Flight-based time-trial course (see TimeTrialController for the
    // run/timer/best-time logic that owns a sequence of these). Deliberately dumb on its own -
    // just reports "the player flew through me" via an event, with no opinion about ordering,
    // timing, or whether it currently "counts"; TimeTrialController is the only thing that
    // decides that, same separation-of-concerns precedent as Health not knowing about combat.
    //
    // Player-only detection mirrors HealingSpring/Portal's own "is this actually the human
    // player" check (GetComponentInParent<PlayerInputProvider>()) - an enemy or a 中立者 flying
    // (they can't, but hypothetically) through a gate shouldn't advance anyone's run.
    [RequireComponent(typeof(Collider))]
    public class CheckpointGate : MonoBehaviour
    {
        [SerializeField] private int order;
        [SerializeField] private Image ringImage;

        // Baked into the ring sprite already (see SkyIslandTimeTrialSetup's own generator
        // comment) - Image tint is what actually switches between these, same "identity-white
        // sprite, alpha/tint driven by code" convention as every other procedural UI effect in
        // this project.
        [SerializeField] private Color nextGateColor = new Color(1f, 0.85f, 0.15f, 1f); // bright gold - "go here"
        [SerializeField] private Color passedGateColor = new Color(0.5f, 0.5f, 0.5f, 0.35f); // dim gray - already used
        // 2026-08-19, explicit user request ("金色光環太淺太薄不顯眼不夠大") - alpha raised
        // 0.25 -> 0.6. With only one gate ever "Next" at a time, every OTHER gate ahead on the
        // course is Upcoming - the original faint alpha made the whole rest of the path nearly
        // invisible, which read as a course with too few/too-sparse rings even though the actual
        // gate count/spacing was reasonable.
        [SerializeField] private Color upcomingGateColor = new Color(1f, 0.85f, 0.15f, 0.6f); // visible gold - part of the route, not yet the immediate target

        // 2026-08-19, explicit user request ("光環碰到時玩家會短暫被加速"), speed follow-up
        // ("經過光環時需要有向前短距離衝刺的作用") - every gate is now also a boost pad, not just
        // a checkpoint marker. Dash direction is this GameObject's own forward, which
        // SkyIslandTimeTrialSetup already orients along the course's travel direction (previous
        // gate -> this gate) - reusing that existing data means the dash always pushes further
        // ALONG the intended route, not toward wherever the player happened to be facing/moving.
        // 2026-08-24, explicit user request ("金色光環通過時 賦予玩家的衝刺距離改為現在的0.5倍") -
        // halved from 14f together with DashInstantDisplacement below, since both scale the total
        // forward distance one dash covers linearly (see CharacterMovement.ApplyDash:
        // instantDisplacement is an immediate Move() snap, dashSpeed decays linearly to 0 over
        // DashDecaySeconds and its own area-under-the-line contributes the rest) - halving both
        // halves the total distance while keeping the same "quick pop + short follow-through"
        // shape of the dash, not just one piece of it.
        [SerializeField] private float dashSpeed = 7f;

        // 2026-08-19, explicit user request ("穿過光圈需有短位移向前衝刺") - the pure velocity-
        // decay dash (above) apparently didn't read as an actual "衝刺" (dash) on its own, likely
        // because it only ever builds up gradually through the normal SmoothDamp-eased motion
        // pipeline. Added an immediate, guaranteed instant position snap on top of it -
        // CharacterMovement.ApplyDash now also calls _controller.Move() once, right away,
        // independent of frame timing - so touching a gate always produces an unmistakable
        // "displaced forward" pop the same frame, with the velocity decay providing a few more
        // frames of lingering follow-through after that.
        // 2026-08-24 - halved alongside dashSpeed above, see that field's own comment.
        private const float DashInstantDisplacement = 1.25f;

        // 2026-08-19, explicit user request ("被觸碰的光圈做一個向外擴大然後淡化成藍色消失的特效") -
        // touching a gate plays a one-shot "consumed" animation (scale up, tint toward blue, fade
        // to alpha 0) instead of just silently switching to the dim "Passed" tint. Captured BEFORE
        // Entered fires (see OnTriggerEnter) so the tween starts from whatever bright color the
        // player actually saw at the moment of touch, not whatever RefreshGateVisuals's own
        // SetState(Passed) call would otherwise stamp over it in the same frame.
        private static readonly Color VanishTargetColor = new Color(0.3f, 0.6f, 1f, 0f);
        private const float VanishExpandScale = 1.6f;
        private const float VanishDurationSeconds = 0.6f;

        private bool _vanishing;
        private float _vanishTimer;
        private Color _vanishStartColor;

        public int Order => order;

        public event Action<CheckpointGate> Entered;

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerInputProvider>() == null)
            {
                return;
            }

            // Dash applies on ANY gate touch, including re-entering an already-passed gate or
            // flying ahead out of order - it's a flight-course pickup, not a run-progress signal;
            // TimeTrialController (via Entered, below) is what separately decides whether this
            // touch actually advances the run.
            var movement = other.GetComponentInParent<CharacterMovement>();
            if (movement != null)
            {
                movement.ApplyDash(transform.forward, dashSpeed, DashInstantDisplacement);
            }

            if (ringImage != null && ringImage.gameObject.activeSelf && !_vanishing)
            {
                _vanishing = true;
                _vanishTimer = 0f;
                _vanishStartColor = ringImage.color;
            }

            Entered?.Invoke(this);
        }

        private void Update()
        {
            if (!_vanishing || ringImage == null)
            {
                return;
            }

            _vanishTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_vanishTimer / VanishDurationSeconds);

            float scale = Mathf.Lerp(1f, VanishExpandScale, t);
            ringImage.rectTransform.localScale = new Vector3(scale, scale, 1f);
            ringImage.color = Color.Lerp(_vanishStartColor, VanishTargetColor, t);

            if (t >= 1f)
            {
                _vanishing = false;
                ringImage.gameObject.SetActive(false);
            }
        }

        // Called by TimeTrialController whenever the run's overall state changes (a checkpoint
        // was passed, the run reset, etc.) - three states cover the whole gate sequence: this is
        // the one to fly through next (bright), already passed this run (dim), or still waiting
        // further down the course (faint, so the player can see the route ahead without it
        // competing visually with the actual next target).
        //
        // Skipped entirely while _vanishing - the touch animation above owns the ring's color/
        // scale for its own short duration and would otherwise get stomped mid-tween by
        // RefreshGateVisuals's own synchronous SetState(Passed) call on the very same frame.
        public void SetState(GateState state)
        {
            if (ringImage == null || _vanishing)
            {
                return;
            }

            ringImage.color = state switch
            {
                GateState.Next => nextGateColor,
                GateState.Passed => passedGateColor,
                _ => upcomingGateColor,
            };
        }

        // 2026-08-19, explicit user request ("光圈等待10秒後再復原出現") - called by
        // TimeTrialController once the post-finish cooldown elapses, bringing a vanished ring back
        // so the course can be re-attempted. Leaves the actual color to the SetState call
        // RefreshGateVisuals makes right after resetting every gate - no need to guess a color
        // here when the caller already knows the correct one.
        public void ResetVisual()
        {
            _vanishing = false;
            _vanishTimer = 0f;
            if (ringImage != null)
            {
                ringImage.gameObject.SetActive(true);
                ringImage.rectTransform.localScale = Vector3.one;
            }
        }

        public enum GateState
        {
            Upcoming,
            Next,
            Passed,
        }
    }
}
