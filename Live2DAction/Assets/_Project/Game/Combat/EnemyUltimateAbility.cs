using UnityEngine;
using Live2DAction.AI;
using Live2DAction.Core;

namespace Live2DAction.Combat
{
    // Enemy "必殺技" (2026-08-17, explicit user request: "敵人也要有必殺技(能量條滿格且攻擊範圍
    // 內存在玩家)" - scoped to Player4 only, see this session's own clarification). Mirrors the
    // player's UltimateAbility in spirit (consumes a full UltimateEnergy bar, plays a distinct
    // flashy animation) but the trigger condition is AI-driven rather than a key press, and
    // unlike the player's buff-the-current-swing design, this IS its own attack - a bespoke
    // Breakdance move with real hit resolution, so it needs its own tiny Startup/Active/Recovery
    // state machine rather than piggybacking on ComboAttackState (which only ever advances
    // through comboAttacks in fixed index order - there's no clean way to make it jump to "play
    // this ONE special attack instead" without complicating its combo-chaining logic for every
    // other caller). While this ultimate is playing, the normal PlayerCombat component is
    // disabled outright (Unity simply stops calling Update on a disabled MonoBehaviour) so the
    // regular 4-hit combo can't also fire mid-ultimate and fight over the same Animator
    // parameters/attackOrigin.
    [RequireComponent(typeof(EnemyAI))]
    [RequireComponent(typeof(PlayerCombat))]
    [RequireComponent(typeof(UltimateEnergy))]
    public class EnemyUltimateAbility : MonoBehaviour
    {
        [SerializeField] private AttackData ultimateAttack;
        [SerializeField] private Transform attackOrigin;
        [SerializeField] private Animator animator;
        [SerializeField] private GameObject hitEffectPrefab;

        private EnemyAI _enemyAI;
        private PlayerCombat _combat;
        private UltimateEnergy _energy;

        private AttackPhase _phase = AttackPhase.Idle;
        private float _elapsed;
        private bool _hitResolved;

        private static readonly int AttackUltimateTrigger = Animator.StringToHash("AttackUltimate");

        public bool IsActive => _phase != AttackPhase.Idle;

        private void Awake()
        {
            _enemyAI = GetComponent<EnemyAI>();
            _combat = GetComponent<PlayerCombat>();
            _energy = GetComponent<UltimateEnergy>();
            if (attackOrigin == null)
            {
                attackOrigin = transform;
            }
        }

        private void Update()
        {
            if (_phase != AttackPhase.Idle)
            {
                TickUltimate();
                return;
            }

            // Same "攻擊範圍內存在玩家" signal EnemyAI itself uses to decide whether to swing at
            // all (CurrentState == Attacking already means "target is within the real attack
            // capsule's reach", see EnemyAI.ResolveEffectiveAttackRange) - not re-deriving a
            // second distance check here would risk drifting out of sync with it the same way
            // the old attackRange float used to (see EnemyAI's own "combat" field comment for
            // that history). Also requires the normal combo to be at rest (Idle) so the ultimate
            // never interrupts a swing that's already mid-animation.
            if (ultimateAttack != null && _energy.IsFull &&
                _enemyAI.CurrentState == EnemyState.Attacking &&
                _combat.CurrentPhase == AttackPhase.Idle)
            {
                Activate();
            }
        }

        private void Activate()
        {
            _energy.Consume();
            _combat.enabled = false;
            _phase = AttackPhase.Startup;
            _elapsed = 0f;
            _hitResolved = false;

            if (animator != null)
            {
                animator.SetTrigger(AttackUltimateTrigger);
            }
        }

        private void TickUltimate()
        {
            _elapsed += Time.deltaTime;

            switch (_phase)
            {
                case AttackPhase.Startup:
                    if (_elapsed >= ultimateAttack.StartupSeconds)
                    {
                        _phase = AttackPhase.Active;
                    }
                    break;

                case AttackPhase.Active:
                    if (!_hitResolved)
                    {
                        _hitResolved = true;
                        ResolveHit();
                    }
                    if (_elapsed >= ultimateAttack.StartupSeconds + ultimateAttack.ActiveSeconds)
                    {
                        _phase = AttackPhase.Recovery;
                    }
                    break;

                case AttackPhase.Recovery:
                    float totalSeconds = ultimateAttack.StartupSeconds + ultimateAttack.ActiveSeconds + ultimateAttack.RecoverySeconds;
                    if (_elapsed >= totalSeconds)
                    {
                        _phase = AttackPhase.Idle;
                        _combat.enabled = true;
                    }
                    break;
            }
        }

        // Same shape as PlayerCombat.ResolveActiveHit (capsule from attackOrigin out to Range,
        // Radius thick) - deliberately not routed through PlayerCombat itself since that method
        // is private and this attack isn't one of PlayerCombat's own comboAttacks steps.
        private void ResolveHit()
        {
            Vector3 near = attackOrigin.position;
            Vector3 far = near + attackOrigin.forward * ultimateAttack.Range;
            Collider[] candidates = Physics.OverlapCapsule(near, far, ultimateAttack.Radius);
            var hitPoints = AttackResolver.ResolveHits(far, ultimateAttack, transform.root, candidates);

            if (hitEffectPrefab == null)
            {
                return;
            }

            foreach (Vector3 point in hitPoints)
            {
                Instantiate(hitEffectPrefab, point, attackOrigin.rotation);
            }
        }
    }
}
