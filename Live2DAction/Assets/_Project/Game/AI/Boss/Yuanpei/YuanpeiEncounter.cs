using System.Collections;
using UnityEngine;
using Live2DAction.Input;

namespace Live2DAction.AI.Boss.Yuanpei
{
    // Encounter shell (spec §14 BossEncounterController, §20 victory). A trigger volume starts
    // the fight: shows the HUD, applies the "no defence" combat rule note, calls
    // YuanpeiBoss.BeginEncounter, and on defeat runs the victory flow (HUD fade, lock-on release,
    // notify). Victory is HP-only (spec §20 / §24).
    [RequireComponent(typeof(Collider))]
    public class YuanpeiEncounter : MonoBehaviour
    {
        [SerializeField] private YuanpeiBoss boss;
        [SerializeField] private YuanpeiBossHUD hud;
        [SerializeField] private Vector3 combatCenter = new Vector3(0f, 0f, -114f);
        [SerializeField] private bool startOnTrigger = true;

        public bool Started { get; private set; }
        public bool Won { get; private set; }

        private void Reset()
        {
            var c = GetComponent<Collider>();
            if (c != null) c.isTrigger = true;
        }

        private void Awake()
        {
            if (boss == null) boss = FindFirstObjectByType<YuanpeiBoss>();
            if (hud == null && boss != null) hud = boss.GetComponent<YuanpeiBossHUD>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!startOnTrigger || Started) return;
            var pip = other.GetComponentInParent<PlayerInputProvider>();
            if (pip == null) return;
            // only the actual player character (not the cat, not the buggy)
            if (pip.transform.root.name != "Player") return;
            StartEncounter(pip.transform.root);
        }

        public void StartEncounter() => StartEncounter(null);

        public void StartEncounter(Transform triggeringPlayer)
        {
            if (Started || boss == null) return;
            Started = true;
            hud?.SetVisible(true);
            boss.BeginEncounter(combatCenter, triggeringPlayer);
        }

        // YuanpeiBoss.EnterDeath() sends "OnYuanpeiBossDefeated" to its own GameObject; this
        // component listens on the same object if it's placed there, otherwise poll.
        private void Update()
        {
            if (!Started || Won || boss == null) return;
            if (boss.BattleOver && boss.Vitals != null && boss.Vitals.IsDead)
                StartCoroutine(Victory());
        }

        private IEnumerator Victory()
        {
            Won = true;
            // lock-on drops itself once the boss's LockOnTarget is disabled (YuanpeiBoss.EnterDeath).
            yield return new WaitForSeconds(1.5f);
            hud?.SetVisible(false);
            SendMessage("OnYuanpeiEncounterWon", SendMessageOptions.DontRequireReceiver);
            Debug.Log("[YuanpeiEncounter] yuanpei_LogoSky defeated - player victory.");
        }
    }
}
