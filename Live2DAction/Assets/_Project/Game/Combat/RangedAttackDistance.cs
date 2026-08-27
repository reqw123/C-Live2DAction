using UnityEngine;

namespace Live2DAction.Combat
{
    // 2026-08-23, explicit user request ("要給這個射擊的攻擊距離元件 獨立新的元件管理攻擊距離 並把射擊
    // 距離大幅延長") - pulls maxRange out of RangedWeapon into its own dedicated component, the same
    // "one small component owns a single number, the weapon just reads it" pattern this project's
    // melee side already uses (PlayerCombat reads Range/Radius from its own AttackData asset
    // rather than owning that number itself). Kept as a plain MonoBehaviour rather than a
    // ScriptableObject like AttackData - there's only one ranged weapon in the project right now,
    // no need for a shareable/reusable data asset yet, and the explicit ask was for a component.
    public class RangedAttackDistance : MonoBehaviour
    {
        // 500 (up from RangedWeapon's old inline 60) - explicit user request to "大幅延長"
        // (substantially extend) the shooting range: sniper/hitscan-across-the-map scale rather
        // than a room-sized check.
        [SerializeField] private float maxRange = 500f;

        public float MaxRange => maxRange;
    }
}
