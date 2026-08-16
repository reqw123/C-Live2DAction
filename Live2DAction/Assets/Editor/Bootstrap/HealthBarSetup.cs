using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Live2DAction.Core;
using Live2DAction.UI;

namespace Live2DAction.EditorTools
{
    // Adds a red world-space health bar above Player (character 1) and Player4's heads
    // (2026-08-12, explicit user request: 100 HP, -10 per hit). Health.MaxHealth is already
    // 100 by default (Health.cs) - not touched here. Attack damage values are normalized to
    // 10 by Player4EnemyAISetup-adjacent tuning, see FixAttackDamageToTen.cs.
    //
    // 2026-08-12 revision: the first version positioned the bar from
    // CharacterController.center.y + height/2 (0.5 local units, since both Player and Player4
    // use height=1) - that's the CONTROLLER's capsule top, not the visual model's actual head
    // height. Both Maya and Arisa's rendered meshes are noticeably taller than the 1-unit
    // collision capsule (the capsule is sized for gameplay collision, not 1:1 with the
    // imported character's real proportions), so the bar ended up floating around
    // shoulder/chest height instead of above the head - reported as "血條太低 應該要在角色頭部
    // 上方". Fixed by measuring the Visual hierarchy's actual Renderer bounds instead, which
    // include hair/accessories and reflect the real rendered top regardless of collider size.
    // Also shrunk per "小一點,看的到血條減少" (smaller, and clearly visible so damage feedback
    // reads).
    internal static class HealthBarSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private static readonly Color FillColor = Color.red;
        private static readonly Color BackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        private static readonly Vector2 BarSize = new Vector2(0.5f, 0.06f);
        private const float MarginAboveHead = 0.15f;

        [MenuItem("Tools/Live2DAction/Add Health Bars To Player And Player4")]
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

            AddHealthBar(player);
            AddHealthBar(player4);

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Added red world-space health bars to Player and Player4.");
        }

        // internal (not private) so Player2DamageableSetup.cs can reuse the exact same bar
        // construction for Player2 rather than duplicating it - see that class's comment for
        // why Player2 needs one too (2026-08-13, explicit user request).
        internal static void AddHealthBar(GameObject owner)
        {
            Health health = owner.GetComponent<Health>();
            if (health == null)
            {
                Debug.LogError(owner.name + " has no Health component - cannot wire a health bar to it.");
                return;
            }

            WorldSpaceHealthBar existingBar = owner.GetComponentInChildren<WorldSpaceHealthBar>(true);
            if (existingBar != null)
            {
                Object.DestroyImmediate(existingBar.gameObject);
            }

            float headTop = MeasureVisualTopLocalY(owner);

            var canvasGo = new GameObject("HealthBarCanvas");
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
            fill.fillAmount = 1f;

            WorldSpaceHealthBar bar = canvasGo.AddComponent<WorldSpaceHealthBar>();
            var so = new SerializedObject(bar);
            so.FindProperty("health").objectReferenceValue = health;
            so.FindProperty("fillImage").objectReferenceValue = fill;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Measures the actual rendered top of the "Visual" child hierarchy (world-space
        // Renderer bounds, encapsulating every renderer so hair/accessories that stick up
        // past the main body mesh are included), converted to a Y offset local to owner - the
        // real fix for the bar floating too low (see class comment). Falls back to the
        // CharacterController's capsule top if there's no Visual/no renderers, same as the
        // pre-revision behavior, so this still degrades gracefully for a hypothetical
        // characterless test double.
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

        // Returns the local Y (relative to owner) where a second stacked bar of the given
        // height should sit, directly under owner's existing health bar with a small gap -
        // reads the real live HealthBarCanvas position/size instead of a hand-duplicated copy
        // of MarginAboveHead, so a second bar can never drift out of sync with wherever the
        // health bar actually ends up. 2026-08-16 bug this fixes: UltimateAbilitySetup carried
        // its own separately-tuned margin constant that went stale the moment this class's own
        // bar position changed, leaving the energy bar floating way above the health bar
        // instead of the "directly underneath" position its own comment claimed - same "two
        // numbers that have to be manually kept in sync" bug shape as EnemyAI.attackRange
        // (see that field's comment), just for UI instead of combat. Requires AddHealthBar to
        // have already been called on owner.
        internal static float ComputeStackedBarLocalY(GameObject owner, float barHeight, float gap = 0.02f)
        {
            WorldSpaceHealthBar healthBar = owner.GetComponentInChildren<WorldSpaceHealthBar>(true);
            if (healthBar == null)
            {
                Debug.LogError(owner.name + " has no WorldSpaceHealthBar yet - call AddHealthBar first.");
                return MeasureVisualTopLocalY(owner) + MarginAboveHead;
            }

            RectTransform healthRect = healthBar.GetComponent<RectTransform>();
            float healthBottom = healthBar.transform.localPosition.y - healthRect.sizeDelta.y / 2f;
            return healthBottom - gap - barHeight / 2f;
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
            // Image.Type.Filled silently has NO visual effect at all without an assigned
            // sprite - fillAmount still updates as a plain float property (which is why the
            // earlier "does the value change" tests all passed), but Unity's Image renderer
            // needs actual sprite geometry/UV data to generate a partial-fill mesh; with
            // sprite == null it just always draws the full rect regardless of fillAmount. Real
            // 2026-08-12 bug report ("被攻擊時血量條貼圖不會扣...血條滿格的狀態敵人直接消失
            // 了") - confirmed by screenshotting the bar at 50% HP during actual Play mode and
            // seeing an unchanged full-width bar despite fillImage.fillAmount correctly reading
            // 0.5. Unity's own built-in default UI sprite (what "GameObject > UI > Image" in
            // the Editor assigns automatically) fixes this - no custom art asset needed.
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            return image;
        }
    }
}
