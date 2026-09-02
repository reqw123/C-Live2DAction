using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;

namespace Live2DAction.Cutscene
{
    // 2026-09-01 — see Docs/BOSS_INTRO_EXPLORATION.md. Started as a throwaway demo
    // (SamuraiBossArena.unity); 追加92 wired it into the real GreyboxTest fight.
    //
    // Open-play controller for the samurai boss intro cutscene:
    //   1. StartIntro()          - hand OFF control: disable the player's control scripts + UI,
    //      disable the boss combat AI, swap the gameplay camera for the cutscene camera rig, play
    //      the intro Timeline.
    //   2. introTimeline.stopped - hand control BACK: re-enable everything, swap the camera back,
    //      then fire onIntroComplete (wired to BossStateMachine.ForceEngage so the fight starts
    //      the instant the演出 ends).
    //
    // Deliberately a GENERIC disable list (Behaviour[] / GameObject[] / PlayableDirector / UnityEvent),
    // not hard-wired to project types.
    public class BossIntroManager : MonoBehaviour
    {
        [Header("Player (control handed off during the cutscene)")]
        [Tooltip("The Player GameObject (informational - the trigger already knows it by Tag).")]
        [SerializeField] private GameObject player;

        [Tooltip("Every Behaviour that drives the player / camera and must stand down for the " +
                 "cutscene. GreyboxTest: PlayerInputProvider, CharacterMovement, PlayerCombat, " +
                 "TargetLockController, UltimateAbility, PlayerGuard, ExecutionAbility, " +
                 "CameraPossessionSwitcher, ViewFocusDirector.")]
        [SerializeField] private Behaviour[] playerControlScripts = new Behaviour[0];

        [Tooltip("Player HUD / UI roots. SetActive(false) for the cutscene.")]
        [SerializeField] private GameObject[] playerUi = new GameObject[0];

        [Header("Boss (kept passive during the cutscene)")]
        [Tooltip("The boss combat AI Behaviour (DemoBossAI in the demo, BossStateMachine in GreyboxTest).")]
        [SerializeField] private Behaviour bossCombatAI;

        [Tooltip("Optional. A boss health bar root to SetActive-toggle. Leave null when something " +
                 "else already gates it (GreyboxTest's WushiBossHudVisibility does).")]
        [SerializeField] private GameObject bossHealthBar;

        [Header("Timeline")]
        [SerializeField] private PlayableDirector introTimeline;

        [Header("Camera")]
        [Tooltip("The cutscene camera rig (its own Camera + CinemachineBrain + shot vcams). " +
                 "SetActive(true) for the cutscene, (false) after.")]
        [SerializeField] private GameObject cutsceneCameraRoot;

        [Tooltip("The gameplay camera GameObject. SetActive(false) for the cutscene, (true) after. " +
                 "GreyboxTest: the Main Camera. (The demo instead activates a fallback vcam here.)")]
        [SerializeField] private GameObject gameplayCamera;

        [Header("On finish")]
        [Tooltip("Fired once, right after control is handed back. Wire to BossStateMachine.ForceEngage " +
                 "so the boss commits to the fight the instant the cutscene ends.")]
        [SerializeField] private UnityEvent onIntroComplete;

        private bool _started;
        private bool _subscribed;
        private bool _finished;
        private double _failsafeAt;
        private bool _failsafeArmed;

        // Called by BossTrigger when the player crosses the boss-room threshold. Idempotent.
        public void StartIntro()
        {
            if (_started)
            {
                return;
            }
            _started = true;

            SetControl(false);
            SwapCamera(toCutscene: true);

            if (introTimeline != null && introTimeline.playableAsset != null)
            {
                if (!_subscribed)
                {
                    introTimeline.stopped += OnIntroStopped;
                    _subscribed = true;
                }
                introTimeline.time = 0.0;
                introTimeline.Play();
                // Failsafe: if the Timeline hangs / never raises 'stopped', hand control back anyway
                // a beat after its nominal end. Realtime so a slow-mo test can't strand the player.
                _failsafeAt = Time.realtimeSinceStartupAsDouble + introTimeline.duration + 1.5;
                _failsafeArmed = true;
            }
            else
            {
                RestoreControl();
            }
        }

        private void Update()
        {
            if (_failsafeArmed && Time.realtimeSinceStartupAsDouble >= _failsafeAt)
            {
                RestoreControl();
            }
        }

        private void OnIntroStopped(PlayableDirector _)
        {
            RestoreControl();
        }

        private void RestoreControl()
        {
            _failsafeArmed = false;

            if (_subscribed && introTimeline != null)
            {
                introTimeline.stopped -= OnIntroStopped;
                _subscribed = false;
            }

            SetControl(true);
            SwapCamera(toCutscene: false);

            if (!_finished)
            {
                _finished = true;
                onIntroComplete?.Invoke();
            }
        }

        // enabled == true  -> player controls the game (post-cutscene / pre-trigger)
        // enabled == false -> cutscene owns the frame
        private void SetControl(bool enabled)
        {
            foreach (Behaviour b in playerControlScripts)
            {
                if (b != null)
                {
                    b.enabled = enabled;
                }
            }
            foreach (GameObject ui in playerUi)
            {
                if (ui != null)
                {
                    ui.SetActive(enabled);
                }
            }
            if (bossCombatAI != null)
            {
                bossCombatAI.enabled = enabled;
            }
            if (bossHealthBar != null)
            {
                bossHealthBar.SetActive(enabled);
            }
        }

        private void SwapCamera(bool toCutscene)
        {
            if (cutsceneCameraRoot != null)
            {
                cutsceneCameraRoot.SetActive(toCutscene);
            }
            if (gameplayCamera != null)
            {
                gameplayCamera.SetActive(!toCutscene);
            }
        }

        // Setup-tool seam - the demo's SamuraiBossArenaSetup still calls this exact 7-arg form.
        public void EditorConfigure(GameObject playerGo, Behaviour[] controls, GameObject[] ui,
            Behaviour bossAi, GameObject bossHp, PlayableDirector timeline, GameObject gameplayCam)
        {
            player = playerGo;
            playerControlScripts = controls ?? new Behaviour[0];
            playerUi = ui ?? new GameObject[0];
            bossCombatAI = bossAi;
            bossHealthBar = bossHp;
            introTimeline = timeline;
            gameplayCamera = gameplayCam;
        }

        // For BossIntroManagerTests: drive the two halves directly, no PlayableDirector needed.
        public void ForceStartForTest() => SetControl(false);
        public void ForceStopForTest() => OnIntroStopped(null);
        public void EditorAddOnCompleteListener(UnityAction call)
        {
            if (onIntroComplete == null)
            {
                onIntroComplete = new UnityEvent();
            }
            onIntroComplete.AddListener(call);
        }
    }
}
