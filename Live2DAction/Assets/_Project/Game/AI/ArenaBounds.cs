using UnityEngine;

namespace Live2DAction.AI
{
    // 2026-08-29, user request. A "is the player inside this AI's arena" test + a soft position
    // clamp. Currently only 屁孩王 (精怪) is confined - see BossStateMachine.confineToArena. 武士
    // (the boss) and Enemy (普通怪物) are NOT confined and never touch this. 本地 = an axis-aligned
    // square on the origin; a confined AI chases up to the walls but not through the vehicle
    // doorway.
    //
    // The "give up and go home" decision is NOT here: it depends on the AI having actually walked
    // to the boundary first (it starts the chase far from the doorway), so it lives in
    // BossStateMachine.UpdateGateWatch. This file only answers the two pure geometry questions.
    // Pure so they're directly EditMode-testable, matching TargetLockUtility / AttackResolver.
    public static class ArenaBounds
    {
        // True when (x, z) is outside the axis-aligned square [center +/- halfExtent].
        public static bool IsOutside(Vector2 positionXZ, Vector2 centerXZ, float halfExtent)
        {
            return Mathf.Abs(positionXZ.x - centerXZ.x) > halfExtent
                || Mathf.Abs(positionXZ.y - centerXZ.y) > halfExtent;
        }

        public static bool IsOutside(Vector3 worldPosition, Vector2 centerXZ, float halfExtent)
        {
            return IsOutside(new Vector2(worldPosition.x, worldPosition.z), centerXZ, halfExtent);
        }

        // Pull a world position back onto the arena square (Y untouched), for soft-confining a
        // CharacterController that tried to walk through the walls.
        public static Vector3 ClampInside(Vector3 worldPosition, Vector2 centerXZ, float halfExtent)
        {
            return new Vector3(
                Mathf.Clamp(worldPosition.x, centerXZ.x - halfExtent, centerXZ.x + halfExtent),
                worldPosition.y,
                Mathf.Clamp(worldPosition.z, centerXZ.y - halfExtent, centerXZ.y + halfExtent));
        }
    }
}
