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
    // wiring in GreyboxSceneBuilder) and a blue world-space bar stacked directly above the
    // existing red health bar (HealthBarSetup.AddHealthBar's own MarginAboveHead=0.15 plus
    // that bar's own height plus a small gap - not sharing HealthBarSetup's private
    // constants since a duplicated literal here is simpler than exposing them just for this).
    internal static class UltimateAbilitySetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private static readonly Color FillColor = new Color(0.2f, 0.5f, 1f); // blue, per explicit request
        private static readonly Color BackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        private static readonly Vector2 BarSize = new Vector2(0.5f, 0.06f);
        // 2026-08-13 revision: explicit user follow-up ("能量位置太高了，放在血量條下面並排即
        // 可") - was stacked above the health bar, now sits directly below it instead.
        // HealthBarSetup's own MarginAboveHead is 0.15 (not exposed publicly, duplicated here
        // rather than exposing it just for this), and both bars are 0.06 tall with a
        // center-pivot RectTransform (Unity's default) - shifting down by one full bar
        // height plus a small gap (0.15 - 0.06 - 0.02) puts this bar's row immediately under
        // the health bar's row, both still above the character's head.
        private const float MarginAboveHead = 0.15f - 0.06f - 0.02f;

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

        private static void AddEnergyBar(GameObject owner, UltimateEnergy energy)
        {
            WorldSpaceEnergyBar existingBar = owner.GetComponentInChildren<WorldSpaceEnergyBar>(true);
            if (existingBar != null)
            {
                Object.DestroyImmediate(existingBar.gameObject);
            }

            float headTop = MeasureVisualTopLocalY(owner);

            var canvasGo = new GameObject("EnergyBarCanvas");
            canvasGo.transform.SetParent(owner.transform, false);
            canvasGo.transform.localPosition = new Vector3(0f, headTop + MarginAboveHead, 0f);
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

        // Same measurement as HealthBarSetup.MeasureVisualTopLocalY (duplicated rather than
        // shared - see class comment on why this project keeps these small pieces separate).
        private static float MeasureVisualTopLocalY(GameObject owner)
        {
            Transform visual = owner.transform.Find("Visual");
            Renderer[] renderers = visual != null ? visual.GetComponentsInChildren<Renderer>() : System.Array.Empty<Renderer>();

            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }

                return bounds.max.y - owner.transform.position.y;
            }

            CharacterController controller = owner.GetComponent<CharacterController>();
            return controller != null ? controller.center.y + controller.height / 2f : 1.6f;
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
