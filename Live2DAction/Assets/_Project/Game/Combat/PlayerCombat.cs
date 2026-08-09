using UnityEngine;
using Live2DAction.Input;

namespace Live2DAction.Combat
{
    public class PlayerCombat : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour inputSource;
        [SerializeField] private AttackData attackData;
        [SerializeField] private Transform attackOrigin;

        // Resolved on every use rather than cached in Awake(), so assigning inputSource
        // after the component has already Awoken (e.g. from a test) still takes effect.
        private IInputCommand InputCommand => inputSource as IInputCommand;

        private void Awake()
        {
            if (attackOrigin == null)
            {
                attackOrigin = transform;
            }
        }

        private void Update()
        {
            IInputCommand inputCommand = InputCommand;
            if (inputCommand != null && inputCommand.AttackPressed)
            {
                PerformAttack();
            }
        }

        private void PerformAttack()
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
