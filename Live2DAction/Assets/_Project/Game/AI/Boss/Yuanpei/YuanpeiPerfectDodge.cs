using UnityEngine;
using Live2DAction.Input;

namespace Live2DAction.AI.Boss.Yuanpei
{
    // Perfect-dodge -> counter window (spec §8.2). Greybox rule: if the player dodges while a
    // boss danger is genuinely imminent (a projectile / hazard close, or a boss attack in its
    // Active phase near the player), it's a perfect dodge - brief slow-mo flash + the next
    // player attack inside perfectDodgeCounterWindowSeconds gets 1.5x posture damage
    // (YuanpeiBossHitReceiver consumes boss.ConsumePerfectCounterFlag()).
    //
    // Only the dodge + attack buttons are used (spec §8.2 "仍只使用閃避與攻擊兩個原有按鍵").
    public class YuanpeiPerfectDodge : MonoBehaviour
    {
        [SerializeField] private YuanpeiBoss boss;
        [SerializeField] private float dangerRadius = 3.5f;

        private PlayerInputProvider _input;
        private bool _armed;

        private void Awake()
        {
            if (boss == null) boss = GetComponentInParent<YuanpeiBoss>() ?? FindFirstObjectByType<YuanpeiBoss>();
        }

        private void Update()
        {
            if (boss == null || boss.Player == null || boss.BattleOver) return;
            if (_input == null)
                _input = boss.Player.GetComponentInChildren<PlayerInputProvider>();
            if (_input == null || !_input.DodgePressed) return;

            if (DangerImminent())
            {
                boss.FlagPerfectDodge();
                Live2DAction.Combat.HitStopController.Request(0.10f, 0.15f);
                YuanpeiScreenFlash.Flash(0.5f, 0.13f);   // spec §8.2 白色閃光
            }
        }

        private bool DangerImminent()
        {
            Vector3 p = boss.Player.position + Vector3.up;

            // any light orb close and closing
            foreach (var orb in FindObjectsByType<YuanpeiProjectile>(FindObjectsSortMode.None))
                if ((orb.transform.position - p).sqrMagnitude < dangerRadius * dangerRadius) return true;

            // any hazard mid-warn nearby
            foreach (var hz in FindObjectsByType<YuanpeiHazard>(FindObjectsSortMode.None))
                if (hz.IsMajorActive && (hz.transform.position - new Vector3(p.x, hz.transform.position.y, p.z)).sqrMagnitude < 9f) return true;

            // boss attack active + boss close (charge / shockwave)
            if (boss.State == YuanpeiState.Attacking &&
                (boss.transform.position - p).sqrMagnitude < (dangerRadius * 2f) * (dangerRadius * 2f))
                return true;

            return false;
        }
    }
}
