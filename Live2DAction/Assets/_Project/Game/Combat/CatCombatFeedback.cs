using UnityEngine;
using Live2DAction.CameraSystem;

namespace Live2DAction.Combat
{
    // 2026-08-29, cat combat design (Docs/CAT_COMBAT_DESIGN.md 4). Turns the cat's PlayerCombat.Hit
    // events into hitstop + camera shake + SFX. This is the ONLY thing that ever calls
    // HitStopController.Request, and it does so only while the cat is possessed
    // (CameraPossessionSwitcher.Current == Cat), so the player's / boss's Time.timeScale is
    // never affected. On a switch back to the player it immediately cancels any active dip.
    //
    // Nothing subscribes PlayerCombat.Hit on the player / enemy / boss, so their behaviour is
    // completely unchanged - this component only exists on the Cat GameObject.
    public class CatCombatFeedback : MonoBehaviour
    {
        [SerializeField] private PlayerCombat combat;
        [SerializeField] private CameraPossessionSwitcher possession;
        [SerializeField] private CameraShake catCameraShake;
        [SerializeField] private CombatSfx sfx;

        [Header("Hitstop (seconds)")]
        [SerializeField] private float lightHitStop = 0.05f;
        [SerializeField] private float heavyHitStop = 0.09f;

        [Header("Camera shake")]
        [SerializeField] private float lightShakeAmplitude = 0.06f;
        [SerializeField] private float heavyShakeAmplitude = 0.14f;
        [SerializeField] private float shakeSeconds = 0.18f;

        private bool _wasPossessingCat;

        private void Awake()
        {
            if (combat == null) combat = GetComponent<PlayerCombat>();
        }

        private void OnEnable()
        {
            if (combat != null) combat.Hit += OnHit;
        }

        private void OnDisable()
        {
            if (combat != null) combat.Hit -= OnHit;
            // Never leave a dip running if this component (or the whole cat) is disabled.
            HitStopController.CancelAndRestore();
        }

        private void Update()
        {
            bool possessingCat = possession != null && possession.Current == CameraPossessionSwitcher.Possessed.Cat;
            if (_wasPossessingCat && !possessingCat)
            {
                HitStopController.CancelAndRestore();
            }
            _wasPossessingCat = possessingCat;
        }

        private bool IsHeavy(PlayerCombat.HitEvent e)
        {
            string id = e.Attack != null ? e.Attack.AttackId : null;
            return id != null && (id.Contains("Heavy") || id.Contains("Pounce"));
        }

        private void OnHit(PlayerCombat.HitEvent e)
        {
            bool heavy = IsHeavy(e);
            bool connected = e.HitCount > 0;

            if (sfx != null)
            {
                if (connected) sfx.PlayHit(heavy);
                else sfx.PlaySwing();
            }

            if (!connected)
            {
                return; // whiff: SFX only, no stop/shake
            }

            if (catCameraShake != null)
            {
                catCameraShake.Shake(heavy ? heavyShakeAmplitude : lightShakeAmplitude, shakeSeconds);
            }

            // Hitstop only while actually playing as the cat.
            if (possession == null || possession.Current == CameraPossessionSwitcher.Possessed.Cat)
            {
                HitStopController.Request(heavy ? heavyHitStop : lightHitStop);
            }
        }
    }
}
