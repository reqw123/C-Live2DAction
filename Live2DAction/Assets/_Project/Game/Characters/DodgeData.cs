using UnityEngine;

namespace Live2DAction.Characters
{
    [CreateAssetMenu(fileName = "DodgeData", menuName = "Live2DAction/Characters/Dodge Data")]
    public class DodgeData : ScriptableObject
    {
        // Same reference rate as AttackData.FramesPerSecond - kept as its own constant
        // rather than a shared reference so Characters doesn't need to depend on Combat
        // for a single conversion constant.
        public const float FramesPerSecond = 60f;

        [SerializeField] private float distance = 3f;
        [SerializeField] private int durationFrames = 12;

        // Clamped to durationFrames in InvulnerabilitySeconds, same pattern as
        // AttackData.ComboWindowFrames - defaults to the full dodge being invulnerable.
        [SerializeField] private int invulnerabilityFrames = 12;
        [SerializeField] private int cooldownFrames = 20;

        public float Distance => distance;
        public int DurationFrames => durationFrames;
        public int InvulnerabilityFrames => invulnerabilityFrames;
        public int CooldownFrames => cooldownFrames;

        public float DurationSeconds => durationFrames / FramesPerSecond;
        public float InvulnerabilitySeconds => Mathf.Min(invulnerabilityFrames, durationFrames) / FramesPerSecond;
        public float CooldownSeconds => cooldownFrames / FramesPerSecond;

        // Constant-speed burst for the whole dodge duration, not an accelerate/decelerate
        // curve - matches the "commit to it" feel of a dash/roll in most action games.
        public float Speed => durationFrames > 0 ? distance / DurationSeconds : 0f;
    }
}
