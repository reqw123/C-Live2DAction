using UnityEngine;
using Live2DAction.Combat.Boss;
using Live2DAction.Core;

namespace Live2DAction.Combat
{
    // 2026-09-01, user request ("把 PLAYER 左鍵的音效移動到右鍵(防禦)，並且只有在防禦-玩家與武士的
    // 刀刃碰撞時(隻狼機制)"). Replaces PlayerMeleeSfx (which played KatanaClash.mp3 on every
    // PlayerCombat.Hit combo segment). Now the clash sound only fires when the player is GUARDING
    // (PlayerGuard.Blocked) AND the thing they blocked was a boss SWORD strike - i.e. blade meets
    // blade. A blocked kick / a landed player swing makes no sound here anymore.
    //
    // PlayerGuard.Blocked hands us the ORIGINAL (pre-mitigation) DamageInfo; its Source is the
    // attacker's root. We confirm "that was a blade" by finding an ACTIVE BossHitbox on the source
    // whose current window part is Weapon (BossHitbox.ActiveWindowPart) - true at the moment the
    // hit resolves, since BossHitbox calls ApplyDamage synchronously from inside its own live window.
    [RequireComponent(typeof(AudioSource))]
    public class PlayerGuardClashSfx : MonoBehaviour
    {
        [Tooltip("The PlayerGuard whose Blocked event drives the clash. Auto-found on this GameObject / its parents if unset.")]
        [SerializeField] private PlayerGuard guard;

        [Tooltip("The short blade-on-blade clash clip (刀碰撞聲效.mp3 / KatanaClash.mp3).")]
        [SerializeField] private AudioClip clashClip;

        [SerializeField, Range(0f, 1f)] private float volume = 0.9f;

        [Tooltip("Random pitch jitter per clash so repeated deflects don't sound identical. (min, max) multiplier.")]
        [SerializeField] private Vector2 pitchRange = new Vector2(0.94f, 1.08f);

        [Tooltip("If true, also clash when the blocked hit's part can't be determined (e.g. a non-boss " +
                 "attacker with no BossHitbox) - as long as it was a real frontal block. Default false = " +
                 "strictly blade-vs-blade against a boss weapon window.")]
        [SerializeField] private bool clashOnAnyBlock;

        private AudioSource _source;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            if (guard == null)
            {
                guard = GetComponentInParent<PlayerGuard>();
            }
        }

        private void OnEnable()
        {
            if (guard != null)
            {
                guard.Blocked += OnBlocked;
            }
        }

        private void OnDisable()
        {
            if (guard != null)
            {
                guard.Blocked -= OnBlocked;
            }
        }

        private void OnBlocked(DamageInfo info)
        {
            if (clashClip == null || _source == null)
            {
                return;
            }
            if (!clashOnAnyBlock && !WasBossWeaponStrike(info))
            {
                return;
            }

            _source.pitch = Random.Range(pitchRange.x, pitchRange.y);
            _source.PlayOneShot(clashClip, volume);
        }

        private static bool WasBossWeaponStrike(DamageInfo info)
        {
            if (info.Source == null)
            {
                return false;
            }

            BossHitbox[] hitboxes = info.Source.GetComponentsInChildren<BossHitbox>(true);
            foreach (BossHitbox hb in hitboxes)
            {
                if (hb.IsActive && hb.ActiveWindowPart == BossHitboxPart.Weapon)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
