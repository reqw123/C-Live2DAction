using UnityEngine;

namespace Live2DAction.Combat
{
    [CreateAssetMenu(fileName = "AttackData", menuName = "Live2DAction/Combat/Attack Data")]
    public class AttackData : ScriptableObject
    {
        [SerializeField] private string attackId = "Attack1";
        [SerializeField] private float damage = 10f;
        [SerializeField] private float range = 1.5f;
        [SerializeField] private float radius = 0.75f;

        public string AttackId => attackId;
        public float Damage => damage;
        public float Range => range;
        public float Radius => radius;
    }
}
