using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Live2DAction.Core;
using Live2DAction.Characters;
using Live2DAction.UI;

namespace Live2DAction.EditorTools
{
    // 2026-08-18, explicit user request ("接下來我想做飛行功能...按住鍵自由飛行"). Adds a
    // dedicated UltimateEnergy instance for flight (a SEPARATE component from the one the R-skill
    // ultimate already uses - see CharacterMovement.flightEnergy's own comment for why reusing
    // that generic class is fine despite the name), wires it into CharacterMovement, and adds a
    // sky-blue world-space bar stacked under the existing stance bar. Player-only - EnemyAI hard-
    // codes FlyPressed/FlyDescendPressed to false, so wiring this onto an enemy would just be
    // inert.
    internal static class FlightSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private static readonly Color FillColor = new Color(0.3f, 0.85f, 1f); // sky blue
        private static readonly Color BackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        private static readonly Vector2 BarSize = new Vector2(0.5f, 0.06f);

        // Faster regen than the ultimate's own energy (5 every 3s) - flight is meant to be a
        // frequently-reusable movement tool, not a rare burst skill, so running out shouldn't
        // lock the player out of flying again for very long.
        private const float MaxEnergy = 100f;
        private const float RegenAmount = 10f;
        private const float RegenIntervalSeconds = 1f;

        [MenuItem("Tools/Live2DAction/Add Flight (Hold Ctrl, Shift To Descend)")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("Player GameObject not found in " + ScenePath);
                return;
            }

            UltimateEnergy flightEnergy = player.AddComponent<UltimateEnergy>();
            var energySo = new SerializedObject(flightEnergy);
            energySo.FindProperty("maxEnergy").floatValue = MaxEnergy;
            energySo.FindProperty("regenAmount").floatValue = RegenAmount;
            energySo.FindProperty("regenIntervalSeconds").floatValue = RegenIntervalSeconds;
            energySo.ApplyModifiedPropertiesWithoutUndo();

            CharacterMovement movement = player.GetComponent<CharacterMovement>();
            var movementSo = new SerializedObject(movement);
            movementSo.FindProperty("flightEnergy").objectReferenceValue = flightEnergy;
            movementSo.ApplyModifiedPropertiesWithoutUndo();

            AddFlightBar(player, flightEnergy);

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Added flight (hold Left Ctrl to ascend, Left Shift to descend while flying) and its sky-blue energy bar to Player.");
        }

        private static void AddFlightBar(GameObject owner, UltimateEnergy flightEnergy)
        {
            WorldSpaceEnergyBar existingFlightBar = null;
            foreach (var candidate in owner.GetComponentsInChildren<WorldSpaceEnergyBar>(true))
            {
                if (candidate.gameObject.name == "FlightBarCanvas")
                {
                    existingFlightBar = candidate;
                    break;
                }
            }
            if (existingFlightBar != null)
            {
                Object.DestroyImmediate(existingFlightBar.gameObject);
            }

            // Stack under whatever the lowest existing bar is - stance if present, else energy,
            // else health - same fallback chain StanceBarSetup's own ResolveStanceBarLocalY uses.
            float stackedY;
            WorldSpaceStanceBar stanceBar = owner.GetComponentInChildren<WorldSpaceStanceBar>(true);
            if (stanceBar != null)
            {
                stackedY = HealthBarSetup.ComputeStackedBarLocalY(stanceBar.transform, stanceBar.GetComponent<RectTransform>(), BarSize.y);
            }
            else
            {
                WorldSpaceEnergyBar ultimateBar = null;
                foreach (var candidate in owner.GetComponentsInChildren<WorldSpaceEnergyBar>(true))
                {
                    if (candidate.gameObject.name == "EnergyBarCanvas") { ultimateBar = candidate; break; }
                }
                stackedY = ultimateBar != null
                    ? HealthBarSetup.ComputeStackedBarLocalY(ultimateBar.transform, ultimateBar.GetComponent<RectTransform>(), BarSize.y)
                    : HealthBarSetup.MeasureVisualTopLocalY(owner) + HealthBarSetup.MarginAboveHead;
            }

            var canvasGo = new GameObject("FlightBarCanvas");
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
            fill.fillAmount = flightEnergy.CurrentEnergy / flightEnergy.MaxEnergy;

            WorldSpaceEnergyBar bar = canvasGo.AddComponent<WorldSpaceEnergyBar>();
            var so = new SerializedObject(bar);
            so.FindProperty("energy").objectReferenceValue = flightEnergy;
            so.FindProperty("fillImage").objectReferenceValue = fill;
            so.ApplyModifiedPropertiesWithoutUndo();
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
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            return image;
        }
    }
}
