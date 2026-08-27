using UnityEngine;

namespace Live2DAction.VFX
{
    // 2026-08-24, explicit user request ("預留 Animation Event / Skill System 呼叫介面" /
    // "Character Attack Animation -> Animation Event -> Spawn VFX Prefab -> ...") - the intended
    // hookup point for that architecture: attach this to any character alongside its Animator,
    // then add an Animation Event on each attack clip calling SpawnAttack01/02/03 (or
    // SpawnAttackByIndex(1/2/3) from a single shared event function) by name. Deliberately NOT
    // wired into any existing attack clip/Animator Controller/PlayerCombat/ComboAttackState by
    // this change itself - see class comment on SlashVfxSetup.cs for why ("避免修改既有戰鬥系統").
    //
    // Animation Events can only call a method taking zero parameters, or exactly one of
    // float/int/string/Object - that constraint is why there are three separate parameterless
    // methods (one per clip, if that's more convenient to wire) AND a single int-indexed overload
    // (if wiring one shared event function across multiple clips is more convenient instead) -
    // both paths end up at the same Spawn() call.
    public class SlashVfxSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject attack01Prefab;
        [SerializeField] private GameObject attack02Prefab;
        [SerializeField] private GameObject attack03Prefab;

        // Where the slash spawns and which way it initially faces - falls back to this
        // GameObject's own transform if left unset, so a character with no dedicated weapon-tip/
        // muzzle bone still works with zero extra setup.
        [SerializeField] private Transform spawnPoint;

        public void SpawnAttack01()
        {
            Spawn(attack01Prefab);
        }

        public void SpawnAttack02()
        {
            Spawn(attack02Prefab);
        }

        public void SpawnAttack03()
        {
            Spawn(attack03Prefab);
        }

        public void SpawnAttackByIndex(int index)
        {
            switch (index)
            {
                case 1:
                    SpawnAttack01();
                    break;
                case 2:
                    SpawnAttack02();
                    break;
                case 3:
                    SpawnAttack03();
                    break;
                default:
                    Debug.LogWarning("SlashVfxSpawner.SpawnAttackByIndex: no prefab wired for index " + index);
                    break;
            }
        }

        private void Spawn(GameObject prefab)
        {
            if (prefab == null)
            {
                return;
            }

            Transform origin = spawnPoint != null ? spawnPoint : transform;
            Object.Instantiate(prefab, origin.position, Quaternion.LookRotation(origin.forward, origin.up));
        }
    }
}
