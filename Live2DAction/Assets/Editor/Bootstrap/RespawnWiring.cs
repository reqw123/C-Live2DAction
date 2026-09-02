using UnityEditor;
using UnityEngine;
using Live2DAction.Characters;
using Live2DAction.Core;

namespace Live2DAction.EditorTools
{
    // 2026-08-29 - shared "wire a RespawnController onto GameManager for this character" helper,
    // used by CatCharacterSetup (player cat) and EnemyCatSetup (the enemy cats). Same
    // reclaim-orphan-then-add pattern as EnemyRespawnSetup / MechaRespawnSetup: one
    // RespawnController per revivable character, all hosted on the always-active "GameManager"
    // (Health.ApplyDamage SetActive(false)s the character synchronously, killing any coroutine
    // on it - the respawn timer has to live elsewhere). In-place respawn, matching every other
    // character in this scene.
    internal static class RespawnWiring
    {
        public static void EnsureRespawnController(GameObject target, Health health, float delaySeconds = 5f)
        {
            if (target == null || health == null)
            {
                Debug.LogError("RespawnWiring: target / health is null for " + (target != null ? target.name : "?"));
                return;
            }

            GameObject managerGo = GameObject.Find("GameManager");
            if (managerGo == null)
            {
                managerGo = new GameObject("GameManager");
            }

            RespawnController match = null;
            RespawnController orphan = null;
            foreach (RespawnController candidate in managerGo.GetComponents<RespawnController>())
            {
                var so = new SerializedObject(candidate);
                UnityEngine.Object t = so.FindProperty("target").objectReferenceValue;
                if (t == target)
                {
                    match = candidate;
                    break;
                }
                if (t == null && orphan == null)
                {
                    orphan = candidate;
                }
            }

            RespawnController controller = match != null ? match : orphan;
            if (controller == null)
            {
                controller = managerGo.AddComponent<RespawnController>();
            }

            var cso = new SerializedObject(controller);
            cso.FindProperty("target").objectReferenceValue = target;
            cso.FindProperty("targetHealth").objectReferenceValue = health;
            cso.FindProperty("respawnDelaySeconds").floatValue = delaySeconds;
            var stance = target.GetComponent<Live2DAction.Combat.StancePoise>();
            if (stance != null)
            {
                cso.FindProperty("targetStance").objectReferenceValue = stance;
            }
            cso.ApplyModifiedPropertiesWithoutUndo();
        }

        // Removes any RespawnController on GameManager pointing at a now-destroyed / to-be-rebuilt
        // target - call BEFORE DestroyImmediate(target) on a re-run so the controller doesn't
        // become an orphan that a later EnsureRespawnController silently reclaims for the wrong
        // character.
        public static void RemoveRespawnController(GameObject target)
        {
            GameObject managerGo = GameObject.Find("GameManager");
            if (managerGo == null || target == null)
            {
                return;
            }
            foreach (RespawnController candidate in managerGo.GetComponents<RespawnController>())
            {
                var so = new SerializedObject(candidate);
                if (so.FindProperty("target").objectReferenceValue == target)
                {
                    Object.DestroyImmediate(candidate);
                    return;
                }
            }
        }
    }
}
