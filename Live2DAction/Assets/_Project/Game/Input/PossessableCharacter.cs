using UnityEngine;

namespace Live2DAction.Input
{
    // Shared by any system that needs to resolve "whichever character the player currently
    // controls" from a Collider, from a known root Transform, or scene-wide - regardless of
    // which of the game's possessable characters (Player/Cat/猜猜看) that happens to be right
    // now. Extracted 2026-09-12 after the same "hardcoded to a transform literally named
    // 'Player'" bug independently bit both SceneGate.cs (every gate in the game silently ignored
    // 猜猜看/Cat - no error, the interaction prompt just never appeared) and YuanpeiEncounter.cs
    // (the boss could only be triggered by Player, and worse - HandControlBackToPlayer would
    // actively discard a valid 猜猜看 root at fight-end and hand control to Player instead,
    // because it explicitly re-searched unless the name was EXACTLY "Player"). One shared source
    // of the name list means a future 4th possessable character only needs adding here.
    public static class PossessableCharacter
    {
        private static readonly string[] RootNames = { "Player", "Cat", "猜猜看" };

        public static bool IsPossessableRoot(string name)
        {
            for (int i = 0; i < RootNames.Length; i++)
                if (RootNames[i] == name) return true;
            return false;
        }

        // Walks up from any PlayerInputProvider found under `other`'s root to the nearest
        // ancestor whose name matches a known possessable character - handles the "seated in a
        // vehicle" case (VehicleEntrySystem re-parents the occupant under the car) the same way
        // every call site this replaces already did.
        public static Transform ResolveFrom(Collider other)
        {
            return other == null ? null : ResolveFrom(other.transform.root);
        }

        public static Transform ResolveFrom(Transform root)
        {
            if (root == null) return null;
            foreach (var pip in root.GetComponentsInChildren<PlayerInputProvider>(true))
                for (var t = pip.transform; t != null; t = t.parent)
                    if (IsPossessableRoot(t.name)) return t;
            return null;
        }

        // Scene-wide fallback - the first possessable character root found (order not
        // meaningful - use FindNearest when position matters).
        public static Transform FindAny()
        {
            foreach (var pip in Object.FindObjectsByType<PlayerInputProvider>(FindObjectsSortMode.None))
                for (var t = pip.transform; t != null; t = t.parent)
                    if (IsPossessableRoot(t.name)) return t;
            return null;
        }

        // Scene-wide fallback - the possessable character root nearest `near` (Y ignored). Used
        // when a teleport may have dropped the occupant somewhere without a trigger event firing.
        public static Transform FindNearest(Vector3 near)
        {
            Transform best = null;
            float bestSqrDist = float.MaxValue;
            near.y = 0f;
            foreach (var pip in Object.FindObjectsByType<PlayerInputProvider>(FindObjectsSortMode.None))
            {
                Transform p = null;
                for (var t = pip.transform; t != null; t = t.parent)
                    if (IsPossessableRoot(t.name)) { p = t; break; }
                if (p == null) continue;
                Vector3 q = p.position; q.y = 0f;
                float d = (q - near).sqrMagnitude;
                if (d < bestSqrDist) { bestSqrDist = d; best = p; }
            }
            return best;
        }
    }
}
