using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Live2DAction.CameraSystem;
using Live2DAction.Characters;
using Live2DAction.Combat;
using Live2DAction.Core;
using Live2DAction.UI;

namespace Live2DAction.EditorTools
{
    // 2026-08-31, user request ("為貓咪補上三個血量條 能量條 架式條" -> chose "只在操控貓時顯示").
    // Builds a CatCornerHud (生命 / 能量 / 架式) by CLONING the player's finished PlayerCornerHud
    // subtree and re-pointing the three *BarFx components at the Cat's Health / UltimateEnergy
    // (its flight-energy instance - the only one it has) / StancePoise. A PossessionHud then shows
    // exactly one of the two corner HUDs at a time, following CameraPossessionSwitcher.Current -
    // NOT gated on combat state (unlike the boss's WushiBossHudVisibility).
    //
    // Same cross-wire pattern as WatcherCatWiring / VehicleCatWiring: a Wire() that is a no-op when
    // the Cat or PlayerCornerHud isn't in the scene, called from the end of CatCharacterSetup, plus
    // a standalone menu item. Re-runnable - destroys and rebuilds CatCornerHud each time.
    internal static class CatBarsWiring
    {
        private const string CatHudName = "CatCornerHud";

        [MenuItem("Tools/Live2DAction/Add Cat Bars (HUD - shown while possessing the cat)")]
        public static void Menu()
        {
            const string scenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Exit Play Mode first - EditorSceneManager.OpenScene throws mid-Play.");
                return;
            }
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Wire();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        public static void Wire()
        {
            GameObject cat = FindRoot("Cat");
            GameObject playerHudGo = GameObject.Find("PlayerCornerHud");
            if (cat == null || playerHudGo == null)
            {
                return; // no-op - nothing to build against
            }

            Health catHealth = cat.GetComponent<Health>();
            StancePoise catStance = EnsureCatStance(cat, catHealth);
            UltimateEnergy catEnergy = cat.GetComponent<UltimateEnergy>(); // flight-energy instance (only one)

            DestroyByName(CatHudName);

            // ---- clone the whole player HUD, then trim it to the cat's three rows ----
            GameObject hud = Object.Instantiate(playerHudGo);
            hud.name = CatHudName;

            // The player-only component that drives the 飛行 row - not wanted on the cat's copy.
            var playerHudComp = hud.GetComponent<PlayerCornerHud>();
            if (playerHudComp != null)
            {
                Object.DestroyImmediate(playerHudComp);
            }

            Transform panel = hud.transform.Find("Panel");
            if (panel == null)
            {
                Debug.LogError("CatBarsWiring: cloned PlayerCornerHud has no 'Panel' child - layout changed?");
                Object.DestroyImmediate(hud);
                return;
            }

            // Drop the 飛行 row (the cat's only energy IS its flight energy - it goes in 能量 below).
            DestroyChild(panel, "飛行Label");
            DestroyChild(panel, "飛行Track");

            // Re-label 必殺 -> 能量, 架勢 -> 架式 (user's wording).
            SetLabelText(panel, "必殺Label", "能量");
            SetLabelText(panel, "架勢Label", "架式");

            // Tighten the panel to the three remaining rows (row pitch 34px, first at -25, +~16 pad).
            var panelRect = (RectTransform)panel;
            panelRect.sizeDelta = new Vector2(panelRect.sizeDelta.x, 122f);

            // ---- re-point the three Fx at the cat ----
            RepointFx(panel, "生命Track", catHealth, null, null);
            RepointFx(panel, "必殺Track", null, catEnergy, null);   // UltimateEnergyBarFx.energy
            RepointFx(panel, "架勢Track", catHealth, null, catStance);

            // ---- possession-driven visibility ----
            var possession = FindInScene<CameraPossessionSwitcher>();
            var vis = hud.AddComponent<PossessionHud>();
            var visSo = new SerializedObject(vis);
            visSo.FindProperty("possession").objectReferenceValue = possession;
            visSo.FindProperty("playerHud").objectReferenceValue = playerHudGo.GetComponent<Canvas>();
            visSo.FindProperty("catHud").objectReferenceValue = hud.GetComponent<Canvas>();
            visSo.ApplyModifiedPropertiesWithoutUndo();

            // Start hidden - the game opens on the player (CameraPossessionSwitcher.startPossessed).
            var catCanvas = hud.GetComponent<Canvas>();
            if (catCanvas != null)
            {
                catCanvas.enabled = false;
            }

            EditorUtility.SetDirty(hud);
            Debug.Log("CatBarsWiring: built " + CatHudName + " (生命/能量/架式) from PlayerCornerHud, " +
                      "wired to the Cat's Health/UltimateEnergy/StancePoise, shown only while possessing the cat.");
        }

        private static StancePoise EnsureCatStance(GameObject cat, Health catHealth)
        {
            StancePoise stance = cat.GetComponent<StancePoise>();
            if (stance == null)
            {
                // Older cat rig (built before 2026-08-31) - add + configure to match CatCharacterSetup.
                stance = cat.AddComponent<StancePoise>();
                var so = new SerializedObject(stance);
                so.FindProperty("maxStance").floatValue = 50f;
                so.FindProperty("staggerDurationSeconds").floatValue = 4f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            WirePropIfPresent(cat.GetComponent<PlayerCombat>(), "stance", stance);
            WirePropIfPresent(cat.GetComponent<CharacterMovement>(), "stance", stance);
            return stance;
        }

        private static void RepointFx(Transform panel, string trackName, Health health, UltimateEnergy energy, StancePoise stance)
        {
            Transform track = panel.Find(trackName);
            if (track == null)
            {
                Debug.LogWarning("CatBarsWiring: no '" + trackName + "' under the cloned Panel - skipped.");
                return;
            }
            foreach (MonoBehaviour mb in track.GetComponents<MonoBehaviour>())
            {
                if (mb == null || mb is Image)
                {
                    continue;
                }
                var so = new SerializedObject(mb);
                if (health != null) SetRefIfPresent(so, "health", health);
                if (energy != null) SetRefIfPresent(so, "energy", energy);
                if (stance != null) SetRefIfPresent(so, "stance", stance);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(mb);
            }
        }

        private static void SetRefIfPresent(SerializedObject so, string prop, Object value)
        {
            SerializedProperty p = so.FindProperty(prop);
            if (p != null && p.propertyType == SerializedPropertyType.ObjectReference)
            {
                p.objectReferenceValue = value;
            }
        }

        private static void WirePropIfPresent(Component c, string prop, Object value)
        {
            if (c == null)
            {
                return;
            }
            var so = new SerializedObject(c);
            SerializedProperty p = so.FindProperty(prop);
            if (p != null && p.propertyType == SerializedPropertyType.ObjectReference)
            {
                p.objectReferenceValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(c);
            }
        }

        private static void SetLabelText(Transform panel, string labelName, string text)
        {
            Transform label = panel.Find(labelName);
            if (label == null)
            {
                return;
            }
            var t = label.GetComponent<Text>();
            if (t != null)
            {
                t.text = text;
            }
        }

        private static void DestroyChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null)
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }

        private static void DestroyByName(string name)
        {
            foreach (GameObject g in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (g != null && g.name == name && g.transform.parent == null)
                {
                    Object.DestroyImmediate(g);
                }
            }
        }

        private static GameObject FindRoot(string name)
        {
            foreach (GameObject g in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (g != null && g.name == name && g.transform.parent == null)
                {
                    return g;
                }
            }
            return null;
        }

        private static T FindInScene<T>() where T : Component
        {
            foreach (T c in Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                return c;
            }
            return null;
        }
    }
}
