using UnityEngine;
using Live2DAction.Input;

namespace Live2DAction.Combat
{
    public class PlayerCombat : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour inputSource;
        [SerializeField] private AttackData[] comboAttacks = new AttackData[3];
        [SerializeField] private Transform attackOrigin;

        private ComboAttackState _state;

        // Resolved on every use rather than cached in Awake(), so assigning inputSource
        // after the component has already Awoken (e.g. from a test) still takes effect.
        private IInputCommand InputCommand => inputSource as IInputCommand;

        public AttackPhase CurrentPhase => _state != null ? _state.Phase : AttackPhase.Idle;
        public int ComboIndex => _state != null ? _state.ComboIndex : -1;

        private void Awake()
        {
            if (attackOrigin == null)
            {
                attackOrigin = transform;
            }
        }

        private void Update()
        {
            // Built lazily rather than in Awake, same reasoning as InputCommand above: tests
            // assign comboAttacks via reflection right after AddComponent, which already runs
            // Awake synchronously.
            if (_state == null)
            {
                _state = new ComboAttackState(comboAttacks);
            }

            IInputCommand inputCommand = InputCommand;
            bool attackPressed = inputCommand != null && inputCommand.AttackPressed;
            if (_state.Tick(Time.deltaTime, attackPressed))
            {
                ResolveActiveHit(_state.CurrentAttack);
            }
        }

        private void ResolveActiveHit(AttackData attackData)
        {
            if (attackData == null)
            {
                return;
            }

            Vector3 origin = attackOrigin.position + attackOrigin.forward * attackData.Range;
            Collider[] candidates = Physics.OverlapSphere(origin, attackData.Radius);
            AttackResolver.ResolveHits(origin, attackData, transform.root, candidates);
        }
    }
}
