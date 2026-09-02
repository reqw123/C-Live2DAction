using System.Collections;
using UnityEngine;
using Live2DAction.Combat;
using Live2DAction.Core;
using Live2DAction.Input;

namespace Live2DAction.Characters
{
    // 2026-08-31, user request ("讓 cat 能量滿格時可以施放 [黑暗劍氣風格] 這個技能特效"). The cat's
    // ultimate: R while its own skill-energy meter (a dedicated UltimateEnergy on the Cat/SkillEnergy
    // child, NOT the flight-energy instance) is full -> consume it, spawn the dark sword-qi cast VFX
    // on the cat, and rain a rapid multi-hit barrage on everyone in range.
    //
    // 2026-08-31 (追加80), user request ("受到傷害的敵人並非是一次減少500而是每次打擊50形成10次的
    // 追擊"): the AOE is NOT a single burst - it lands `hitCount` separate hits (each does the
    // AttackData's own damage, 50) over `hitIntervalSeconds`, so a target takes 10x50 = 500 as a
    // visible flurry, timed to run alongside the dark-qi VFX rather than a single instant chunk.
    //
    // Wired into CameraPossessionSwitcher.catControl so R only ever casts while you're the cat
    // (the player's own UltimateAbility is disabled the same way while you possess the cat).
    [DisallowMultipleComponent]
    public class CatUltimateAbility : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour inputSource;   // IInputCommand (the Cat's PlayerInputProvider)
        [SerializeField] private UltimateEnergy energy;       // the SKILL meter, not flight
        [SerializeField] private GameObject castVfxPrefab;    // DarkSwordQi flipbook prefab, self-destroys
        [SerializeField] private Vector3 castVfxLocalOffset = new Vector3(0f, 0.55f, 0f);

        // The AOE the cast lands. Per-hit balance (damage 50, range, knockback) lives on the
        // AttackData asset (rule 7); OverlapSphere queries out to Range + Radius.
        [SerializeField] private AttackData attack;

        // 追加80: the barrage. hitCount * attack.Damage is the total (10 * 50 = 500). The whole
        // flurry runs over hitCount * hitIntervalSeconds (~1.2 s) - matched to the VFX length.
        [SerializeField] private int hitCount = 10;
        [SerializeField] private float hitIntervalSeconds = 0.12f;

        // Optional gates, same null-safe pattern as PlayerCombat: a staggered or dead cat can't cast.
        [SerializeField] private StancePoise stance;
        [SerializeField] private Health health;

        private IInputCommand InputCommand => inputSource as IInputCommand;
        private bool _barrageActive;

        // Lets a HUD / SFX hook know a cast just fired (nothing subscribes yet - kept for parity
        // with the rest of this codebase's small decoupled components).
        public event System.Action Cast;

        private void Update()
        {
            IInputCommand input = InputCommand;
            if (input == null || !input.UltimatePressed)
            {
                return;
            }
            if (_barrageActive)
            {
                return;
            }
            if (energy == null || !energy.IsFull)
            {
                return;
            }
            if (stance != null && stance.IsStaggered)
            {
                return;
            }
            if (health != null && health.IsDead)
            {
                return;
            }

            energy.Consume();

            if (castVfxPrefab != null)
            {
                Instantiate(castVfxPrefab, transform.TransformPoint(castVfxLocalOffset), transform.rotation, transform);
            }

            StartCoroutine(Barrage());
            Cast?.Invoke();
        }

        private IEnumerator Barrage()
        {
            _barrageActive = true;
            int hits = Mathf.Max(1, hitCount);
            float interval = Mathf.Max(0f, hitIntervalSeconds);
            for (int i = 0; i < hits; i++)
            {
                ResolveBurst();
                if (i < hits - 1 && interval > 0f)
                {
                    yield return new WaitForSeconds(interval);
                }
            }
            _barrageActive = false;
        }

        private void OnDisable()
        {
            // Possession swap / cat death mid-barrage - stop cleanly so a re-enable starts fresh.
            _barrageActive = false;
        }

        private void ResolveBurst()
        {
            if (attack == null)
            {
                return;
            }
            float reach = attack.Range + attack.Radius;
            if (reach <= 0f)
            {
                return;
            }
            Collider[] candidates = Physics.OverlapSphere(transform.position, reach);
            AttackResolver.ResolveHits(transform.position, attack, transform.root, candidates);
        }
    }
}
