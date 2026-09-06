using UnityEngine;

namespace Live2DAction.AI.Boss.Yuanpei
{
    // 續183e - pure geometry + damage maths for the boss's opening "下馬威" barrage. No MonoBehaviour,
    // directly EditMode-testable (same idea as YuanpeiScheduler / YuanpeiIntroTimeline).
    //
    // 續183f - the runtime fires all three streams FANNED at the muzzle + HOMING onto the player, so
    // "left/mid/right" is now just the telegraph's body-marker spread; `AimPoint` gives those three
    // body points (used by the warn beams). The real hit guarantee is the homing, not a fixed offset.
    public static class YuanpeiOpeningBarrageLanes
    {
        public enum Lane { Left = 0, Mid = 1, Right = 2 }

        // One of the three telegraph body markers: the locked player centre, shifted `bodyHalfWidth`
        // metres along the axis perpendicular to the boss→player line (Left = -axis, Right = +axis).
        // Keep `bodyHalfWidth` ~= the player capsule radius so all three still read as "on the body".
        public static Vector3 AimPoint(Vector3 playerCenter, Vector3 bossPos, float bodyHalfWidth, Lane lane)
        {
            Vector3 toPlayer = playerCenter - bossPos; toPlayer.y = 0f;
            Vector3 fwd = toPlayer.sqrMagnitude > 1e-5f ? toPlayer.normalized : Vector3.forward;
            Vector3 axis = Vector3.Cross(Vector3.up, fwd).normalized;   // horizontal, 90° to the line of fire
            float s = lane == Lane.Left ? -1f : lane == Lane.Right ? 1f : 0f;
            return playerCenter + axis * (bodyHalfWidth * s);
        }

        // Total damage if every projectile / tick connects - the balance check behind "站著不動必死".
        public static float TotalDamageIfAllHit(float spearDamage, int spears,
            float laserTickDamage, int laserTicks, float orbDamage, int orbs)
            => Mathf.Max(0f, spearDamage) * Mathf.Max(0, spears)
             + Mathf.Max(0f, laserTickDamage) * Mathf.Max(0, laserTicks)
             + Mathf.Max(0f, orbDamage) * Mathf.Max(0, orbs);
    }
}
