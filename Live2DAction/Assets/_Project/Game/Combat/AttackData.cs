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

        // Optional per-attack hit VFX (2026-08-13, explicit user request - a dedicated slash
        // effect for LightAttack3, distinct from the shared spark PlayerCombat.hitEffectPrefab
        // otherwise uses for every combo step). Null means "no override" - PlayerCombat falls
        // back to its own shared prefab, so LightAttack1/2 (and anything else that never sets
        // this) keep working unchanged.
        [SerializeField] private GameObject hitEffectOverride;

        // 2026-08-13 explicit user request ("打空氣時也有特效出來") - by default (false) the
        // hit effect only spawns where AttackResolver actually landed a hit (PlayerCombat's
        // original, still-correct behavior for the shared spark: a flash implying "I struck
        // something" shouldn't appear when nothing was struck). LightAttack3's slash VFX is
        // different in kind - it represents the swing/sword-qi release itself, not an impact,
        // so it should play every time the attack executes whether or not it connects. Kept
        // per-AttackData rather than a PlayerCombat-wide switch so this doesn't change
        // LightAttack1/2's spark behavior.
        [SerializeField] private bool alwaysSpawnHitEffect;

        // 2026-08-29, cat combat design (Docs/CAT_COMBAT_DESIGN.md 4.3) - optional knockback on
        // the target. 0 (the default) = no knockback, so every existing AttackData asset
        // (LightAttack1/2/3, EnemyAttack, TrainingDummy's shared refs) keeps its current
        // no-shove behavior with zero re-authoring. Consumed by PlayerCombat -> a target's
        // Live2DAction.Combat.Boss.IKnockbackReceiver, if it has one. knockbackForce is the
        // push speed (units/sec) fed to the receiver; knockbackLaunches adds a small vertical
        // pop (the cat's pounce / heavy).
        [SerializeField] private float knockbackForce;
        [SerializeField] private bool knockbackLaunches;

        public string AttackId => attackId;
        public float Damage => damage;
        public float Range => range;
        public float Radius => radius;
        public int StartupFrames => startupFrames;
        public int ActiveFrames => activeFrames;
        public int RecoveryFrames => recoveryFrames;
        public int ComboWindowFrames => comboWindowFrames;
        public GameObject HitEffectOverride => hitEffectOverride;
        public bool AlwaysSpawnHitEffect => alwaysSpawnHitEffect;
        public float KnockbackForce => knockbackForce;
        public bool KnockbackLaunches => knockbackLaunches;

        public float StartupSeconds => startupFrames / FramesPerSecond;
        public float ActiveSeconds => activeFrames / FramesPerSecond;
        public float RecoverySeconds => recoveryFrames / FramesPerSecond;
        public float ComboWindowSeconds => Mathf.Min(comboWindowFrames, recoveryFrames) / FramesPerSecond;
    }
}
