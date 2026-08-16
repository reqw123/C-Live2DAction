using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Live2DAction.Core;
using Live2DAction.Combat;
using Live2DAction.UI;

namespace Live2DAction.EditorTools
{
    // Wires up Player's ultimate skill (2026-08-13, explicit user request: 藍色能量條，初始0，
    // 每三秒回復5點，最大100，滿了按R釋放必殺技 - 武器瞬間放大5倍、attack1傷害乘10倍、持續5秒).
    // Adds UltimateEnergy + UltimateAbility to Player (wired to Player's own
    // PlayerInputProvider, same pattern as PlayerCombat/CharacterMovement's own inputSource
    // wiring in GreyboxSceneBuilder) and a blue world-space bar stacked directly below the
    // existing red health bar.
    //
    // 2026-08-16: position is now HealthBarSetup.ComputeStackedBarLocalY(owner, BarSize.y),
    // read live off the actual HealthBarCanvas instead of a hand-duplicated margin constant -
    // see that method's own comment for the bug this replaces (a stale copy of the margin left
    // the bar floating well above the health bar again, reported as "太高了" a second time).
    // AddEnergyBar is internal (not private) so EnemyEnergyBarSetup.cs can reuse the exact same
    // bar construction for Player4, matching HealthBarSetup.AddHealthBar's own reuse pattern.
    internal static class UltimateAbilitySetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private static readonly Color FillColor = new Color(0.2f, 0.5f, 1f); // blue, per explicit request
        private static readonly Color BackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        private static readonly Vector2 BarSize = new Vector2(0.5f, 0.06f);

        [MenuItem("Tools/Live2DAction/Add Ultimate Ability (Blue Energy Bar + R Skill)")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("Player GameObject not found in " + ScenePath);
                return;
            }

            UltimateEnergy energy = player.GetComponent<UltimateEnergy>();
            if (energy == null)
            {
                energy = player.AddComponent<UltimateEnergy>();
            }

            UltimateAbility ability = player.GetComponent<UltimateAbility>();
            if (ability == null)
            {
                ability = player.AddComponent<UltimateAbility>();
            }

            MonoBehaviour inputProvider = player.GetComponent<Live2DAction.Input.PlayerInputProvider>();
            var abilitySo = new SerializedObject(ability);
            abilitySo.FindProperty("inputSource").objectReferenceValue = inputProvider;
            abilitySo.FindProperty("energy").objectReferenceValue = energy;
            abilitySo.ApplyModifiedPropertiesWithoutUndo();

            AddEnergyBar(player, energy);

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Added UltimateEnergy/UltimateAbility to Player (R to activate at full charge) and its blue energy bar.");
        }

        internal static void AddEnergyBar(GameObject owner, UltimateEnergy energy)
        {
            WorldSpaceEnergyBar existingBar = owner.GetComponentInChildren<WorldSpaceEnergyBar>(true);
            if (existingBar != null)
            {
                Object.DestroyImmediate(existingBar.gameObject);
            }

            float stackedY = HealthBarSetup.ComputeStackedBarLocalY(owner, BarSize.y);

            var canvasGo = new GameObject("EnergyBarCanvas");
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
            fill.fillAmount = 0f; // starts at 0 energy

            WorldSpaceEnergyBar bar = canvasGo.AddComponent<WorldSpaceEnergyBar>();
            var so = new SerializedObject(bar);
            so.FindProperty("energy").objectReferenceValue = energy;
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
            // Same fix as HealthBarSetup's own CreateStretchedImage - Image.Type.Filled needs
            // an actual sprite with UV/geometry data to generate a partial-fill mesh, the
            // built-in UI sprite provides that.
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            return image;
        }
    }
}
