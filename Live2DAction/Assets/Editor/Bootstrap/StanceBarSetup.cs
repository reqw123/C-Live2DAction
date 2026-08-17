using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Live2DAction.Combat;
using Live2DAction.UI;

namespace Live2DAction.EditorTools
{
    // 2026-08-18, explicit user request ("幫我製作架式條圖案，放在能量條下方，邏輯與能量條同理") -
    // adds an orange world-space stance/架式 bar stacked directly under the existing blue energy
    // bar, for both Player and Player4 (both already carry StancePoise - see this session's own
    // "敵我雙方都套用架式條" follow-up). Orange rather than reusing red (health) or blue (energy) -
    // a third bar sharing either existing color would be easy to misread as "this is health" or
    // "this is energy" at a glance; orange/amber is the common "warning, about to stagger" color
    // FromSoftware's own posture gauge popularized, and doesn't collide with this codebase's
    // existing red=enemy/green=player/cyan=detection-range Gizmo palette either.
    internal static class StanceBarSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private static readonly Color FillColor = new Color(1f, 0.6f, 0.1f);
        private static readonly Color BackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        private static readonly Vector2 BarSize = new Vector2(0.5f, 0.06f);

        [MenuItem("Tools/Live2DAction/Add Stance Bar To Player And Player4")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.Find("Player");
            GameObject player4 = GameObject.Find("Player4");
            if (player == null || player4 == null)
            {
                Debug.LogError("Player or Player4 GameObject not found in " + ScenePath);
                return;
            }

            AddStanceBar(player);
            AddStanceBar(player4);

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Added orange world-space stance bars to Player and Player4, stacked under their energy bars.");
        }

        // 2026-08-18, real bug report ("沒看到機甲的架式條顯示") - Player2 (the "機甲戰士") got a
        // StancePoise (see WanderMovement's own "stance" field comment) but never got a bar for
        // it, because AddStanceBar hard-required an existing WorldSpaceEnergyBar to stack under
        // and Player2 has neither a health bar nor an energy bar at all (it was set up as a
        // passive wandering decoration, not a full HUD-tracked combatant, before this). Separate
        // menu item rather than folding into Apply() above - that one's scoped to the two
        // characters that already have the full bar stack, this one is specifically for
        // characters that don't.
        [MenuItem("Tools/Live2DAction/Add Stance Bar To Player2 (No Energy Bar)")]
        public static void ApplyToPlayer2()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject player2 = GameObject.Find("Player2");
            if (player2 == null)
            {
                Debug.LogError("Player2 GameObject not found in " + ScenePath);
                return;
            }

            AddStanceBar(player2);

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Added an orange world-space stance bar to Player2, positioned above its head (no energy/health bar to stack under).");
        }

        private static void AddStanceBar(GameObject owner)
        {
            StancePoise stance = owner.GetComponent<StancePoise>();
            if (stance == null)
            {
                Debug.LogError(owner.name + " has no StancePoise component - cannot wire a stance bar to it.");
                return;
            }

            float stackedY = ResolveStanceBarLocalY(owner);

            WorldSpaceStanceBar existingBar = owner.GetComponentInChildren<WorldSpaceStanceBar>(true);
            if (existingBar != null)
            {
                Object.DestroyImmediate(existingBar.gameObject);
            }

            var canvasGo = new GameObject("StanceBarCanvas");
            canvasGo.transform.SetParent(owner.transform, false);
            canvasGo.transform.localPosition = new Vector3(0f, stackedY, 0f);
            canvasGo.transform.localRotation = Quaternion.identity;
            canvasGo.transform.localScale = Vector3.one;

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.sizeDelta = BarSize;

            Image background = CreateStretchedImage(canvasGo.transform, "Background", BackgroundColor);
            background.type = Image.Type.Simple;

            Image fill = CreateStretchedImage(canvasGo.transform, "Fill", FillColor);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f; // starts at 0 stance

            WorldSpaceStanceBar bar = canvasGo.AddComponent<WorldSpaceStanceBar>();
            var so = new SerializedObject(bar);
            so.FindProperty("stance").objectReferenceValue = stance;
            so.FindProperty("fillImage").objectReferenceValue = fill;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Prefers stacking under an existing energy bar (Player/Player4's usual case), falls
        // back to stacking under a health bar, and finally falls back to sitting directly above
        // the character's own visual top (Player2's case - no bar stack at all yet) - same
        // margin HealthBarSetup itself uses when IT has nothing to stack under.
        private static float ResolveStanceBarLocalY(GameObject owner)
        {
            WorldSpaceEnergyBar energyBar = owner.GetComponentInChildren<WorldSpaceEnergyBar>(true);
            if (energyBar != null)
            {
                return HealthBarSetup.ComputeStackedBarLocalY(energyBar.transform, energyBar.GetComponent<RectTransform>(), BarSize.y);
            }

            WorldSpaceHealthBar healthBar = owner.GetComponentInChildren<WorldSpaceHealthBar>(true);
            if (healthBar != null)
            {
                return HealthBarSetup.ComputeStackedBarLocalY(healthBar.transform, healthBar.GetComponent<RectTransform>(), BarSize.y);
            }

            return HealthBarSetup.MeasureVisualTopLocalY(owner) + HealthBarSetup.MarginAboveHead;
        }

        private static Image CreateStretchedImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = go.AddComponent<Image>();
            image.color = color;
            // Same fix as HealthBarSetup/UltimateAbilitySetup's own CreateStretchedImage -
            // Image.Type.Filled needs an actual sprite with UV/geometry data to generate a
            // partial-fill mesh, the built-in UI sprite provides that.
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            return image;
        }
    }
}
