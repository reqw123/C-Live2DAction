using UnityEngine;

namespace Live2DAction.Combat
{
    [CreateAssetMenu(fileName = "AttackData", menuName = "Live2DAction/Combat/Attack Data")]
    public class AttackData : ScriptableObject
    {
        // Frame counts are authored against this reference rate (standard action/fighting
        // game convention), then converted to seconds for Unity's variable-timestep Update.
        public const float FramesPerSecond = 60f;

        [SerializeField] private string attackId = "Attack1";
        [SerializeField] private float damage = 10f;
        [SerializeField] private float range = 1.5f;
        [SerializeField] private float radius = 0.75f;
        [SerializeField] private int startupFrames = 6;
        [SerializeField] private int activeFrames = 4;
        [SerializeField] private int recoveryFrames = 14;

        // How many frames after Active ends the next combo hit can be buffered. Clamped to
        // recoveryFrames in ComboWindowSeconds so an over-long value can't let the state
        // machine miss its own recovery-end check (see ComboAttackState).
        [SerializeField] private int comboWindowFrames = 10;

        public string AttackId => attackId;
        public float Damage => damage;
        public float Range => range;
        public float Radius => radius;
        public int StartupFrames => startupFrames;
        public int ActiveFrames => activeFrames;
        public int RecoveryFrames => recoveryFrames;
        public int ComboWindowFrames => comboWindowFrames;

        public float StartupSeconds => startupFrames / FramesPerSecond;
        public float ActiveSeconds => activeFrames / FramesPerSecond;
        public float RecoverySeconds => recoveryFrames / FramesPerSecond;
        public float ComboWindowSeconds => Mathf.Min(comboWindowFrames, recoveryFrames) / FramesPerSecond;
    }
}
