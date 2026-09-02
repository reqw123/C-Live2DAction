using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.Combat;

namespace Live2DAction.EditorTools
{
    // 2026-09-01, spec WUSHI_COMBAT_ENGINEERING_SPEC.md §5 (M2 項目 4), step 2. Wires the swept-blade
    // melee hitbox onto the GreyboxTest Player:
    //   - BladeSamples/BladeRoot + BladeTip empties parented to the katana wrapper (WolfsGravestone)
    //     so they ride the swing animation. Placed in WORLD space then let Unity derive the tiny
    //     local offset - the wrapper's ~80x bone lossyScale makes hand-authored localPosition
    //     unusable (see memory player-weapon-mount).
    //   - Player/WeaponHitbox (PlayerWeaponHitbox), wired to PlayerCombat + the two sample transforms
    //     + PlayerCombat's own shared hit spark.
    //   - PlayerCombat.useSweptBladeHitbox = true (the legacy flag; Remove sets it back to false and
    //     deletes the children - a full revert to the OverlapCapsule path).
    // Re-runnable. Blade line is measured at the scene's bind pose; tune BladeRoot/BladeTip in the
    // hierarchy afterward (green gizmos while WeaponHitbox is selected).
    internal static class PlayerWeaponHitboxSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string WrapperName = "WolfsGravestone"; // katana mount wrapper (name kept from the old greatsword)
        private const string SamplesRoot = "BladeSamples";
        private const string HitboxChild = "WeaponHitbox";

        // Blade geometry at bind pose, world metres from the wrapper origin (hilt), along -wrapper.up
        // (measured: BloodKatana blade runs down the wrapper's local -Y, like Genshin_WGS).
        private const float BladeRootOffset = 0.12f; // skip the hilt
        private const float BladeLength = 0.90f;     // hilt -> visible tip
        // 0.25, not a hair-thin 0.12: the placeholder 武士 is 4x scale and only damageable through
        // its CharacterController (its BodyHurtbox floats ~8 units overhead - a pre-existing rig
        // issue), so the player's blade needs a forgiving cross-section to connect at all.
        private const float SweepRadius = 0.25f;

        [MenuItem("Tools/Live2DAction/Add Player Swept Blade Hitbox")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Exit Play Mode first - this touches the scene.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("PlayerWeaponHitboxSetup: no Player in " + ScenePath);
                return;
            }

            PlayerCombat combat = player.GetComponent<PlayerCombat>();
            if (combat == null)
            {
                Debug.LogError("PlayerWeaponHitboxSetup: Player has no PlayerCombat.");
                return;
            }

            Transform wrapper = FindDescendant(player.transform, WrapperName);
            if (wrapper == null)
            {
                Debug.LogError("PlayerWeaponHitboxSetup: no '" + WrapperName + "' katana wrapper under Player.");
                return;
            }

            // --- blade sample transforms, parented to the katana wrapper ---
            Transform oldSamples = FindDescendant(wrapper, SamplesRoot);
            if (oldSamples != null)
            {
                Object.DestroyImmediate(oldSamples.gameObject);
            }

            var samples = new GameObject(SamplesRoot);
            samples.transform.SetParent(wrapper, false);

            Vector3 bladeDir = (-wrapper.up).normalized;
            Vector3 hilt = wrapper.position;

            var rootGo = new GameObject("BladeRoot");
            rootGo.transform.SetParent(samples.transform, false);
            rootGo.transform.position = hilt + bladeDir * BladeRootOffset;

            var tipGo = new GameObject("BladeTip");
            tipGo.transform.SetParent(samples.transform, false);
            tipGo.transform.position = hilt + bladeDir * BladeLength;

            // --- the hitbox component on a dedicated Player child ---
            Transform oldHitbox = player.transform.Find(HitboxChild);
            if (oldHitbox != null)
            {
                Object.DestroyImmediate(oldHitbox.gameObject);
            }

            var hitboxGo = new GameObject(HitboxChild);
            hitboxGo.transform.SetParent(player.transform, false);
            var hitbox = hitboxGo.AddComponent<PlayerWeaponHitbox>();

            GameObject sharedSpark = new SerializedObject(combat).FindProperty("hitEffectPrefab").objectReferenceValue as GameObject;

            var hbo = new SerializedObject(hitbox);
            hbo.FindProperty("combat").objectReferenceValue = combat;
            hbo.FindProperty("attackerRoot").objectReferenceValue = player.transform;
            hbo.FindProperty("bladeRoot").objectReferenceValue = rootGo.transform;
            hbo.FindProperty("bladeTip").objectReferenceValue = tipGo.transform;
            hbo.FindProperty("bladeMid").objectReferenceValue = null; // geometric midpoint is fine
            hbo.FindProperty("sweepRadius").floatValue = SweepRadius;
            hbo.FindProperty("hitEffectPrefab").objectReferenceValue = sharedSpark;
            hbo.ApplyModifiedPropertiesWithoutUndo();

            // --- flip PlayerCombat onto the swept path ---
            var co = new SerializedObject(combat);
            co.FindProperty("useSweptBladeHitbox").boolValue = true;
            co.FindProperty("sweptBladeHitbox").objectReferenceValue = hitbox;
            co.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("PlayerWeaponHitboxSetup: Player/WeaponHitbox wired; BladeRoot/Tip under " + WrapperName
                      + "; PlayerCombat.useSweptBladeHitbox = true. Tune the sample transforms in the hierarchy.");
        }

        [MenuItem("Tools/Live2DAction/Remove Player Swept Blade Hitbox")]
        public static void Remove()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Exit Play Mode first.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("PlayerWeaponHitboxSetup.Remove: no Player.");
                return;
            }

            PlayerCombat combat = player.GetComponent<PlayerCombat>();
            if (combat != null)
            {
                var co = new SerializedObject(combat);
                co.FindProperty("useSweptBladeHitbox").boolValue = false;
                co.FindProperty("sweptBladeHitbox").objectReferenceValue = null;
                co.ApplyModifiedPropertiesWithoutUndo();
            }

            Transform hitbox = player.transform.Find(HitboxChild);
            if (hitbox != null)
            {
                Object.DestroyImmediate(hitbox.gameObject);
            }
            Transform wrapper = FindDescendant(player.transform, WrapperName);
            Transform samples = wrapper != null ? FindDescendant(wrapper, SamplesRoot) : null;
            if (samples != null)
            {
                Object.DestroyImmediate(samples.gameObject);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("PlayerWeaponHitboxSetup.Remove: reverted to the OverlapCapsule melee path.");
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t != root && t.name == name)
                {
                    return t;
                }
            }
            return null;
        }
    }
}
