using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Live2DAction.Combat;
using Live2DAction.UI;

namespace Live2DAction.EditorTools
{
    // 2026-08-18, explicit user request ("架勢條滿格時，角色的胸口上出現紅色圓圈 圓圈內有個紅色小
    // 點在閃爍，整體視覺幫我用最專業的精緻外觀來設計") - adds a world-space "execution ready"
    // indicator (soft-glow red ring + blinking core dot, see ExecutionRing.png/ExecutionDot.png's
    // own procedural-generation comment for the visual design) at chest height on every character
    // that has a StancePoise component, rather than a hardcoded name list like StanceBarSetup's
    // own Apply()/ApplyToPlayer2 split - this indicator only ever needs to exist alongside
    // StancePoise itself, so deriving the target list from that component directly means a new
    // stance-bearing character automatically gets it without a matching new menu item.
    internal static class ExecutionIndicatorSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string RingSpritePath = "Assets/_Project/UI/Textures/ExecutionRing.png";
        private const string DotSpritePath = "Assets/_Project/UI/Textures/ExecutionDot.png";

        private static readonly Vector2 RingSize = new Vector2(0.42f, 0.42f);
        private static readonly Vector2 DotSize = new Vector2(0.15f, 0.15f);

        // Fraction of MeasureVisualTopLocalY (the head-top reference HealthBarSetup's own bar
        // stack is measured from) used to place this at chest height instead - roughly the
        // torso/sternum band on a humanoid figure, well below the head-level bar stack
        // (Health/Energy/Stance bars all sit AT headTop + a small margin) and well above the hips.
        private const float ChestHeightFraction = 0.6f;

        // 2026-08-18, real bug caught by screenshot verification - the Canvas's local X/Z
        // defaulted to (0, 0), i.e. the character's own root pivot, which sits INSIDE the torso
        // volume (the ring/dot are a paper-thin quad, not a real 3D object) - the opaque body
        // mesh fully occluded it from every angle tested, so it never actually became visible
        // despite its alpha being computed correctly. Offsetting forward (local +Z, this rig's
        // own forward axis regardless of current world yaw) by this amount pushes it clear of the
        // chest surface so it renders in front of the body instead of buried inside it.
        private const float ChestForwardOffset = 0.28f;

        [MenuItem("Tools/Live2DAction/Add Execution Ready Indicator To All Stance Characters")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            StancePoise[] allStance = Object.FindObjectsByType<StancePoise>(FindObjectsSortMode.None);
            if (allStance.Length == 0)
            {
                Debug.LogError("No StancePoise components found in " + ScenePath);
                return;
            }

            Sprite ringSprite = AssetDatabase.LoadAssetAtPath<Sprite>(RingSpritePath);
            Sprite dotSprite = AssetDatabase.LoadAssetAtPath<Sprite>(DotSpritePath);
            if (ringSprite == null || dotSprite == null)
            {
                Debug.LogError("Execution indicator sprites not found - expected " + RingSpritePath + " and " + DotSpritePath);
                return;
            }

            foreach (StancePoise stance in allStance)
            {
                AddIndicator(stance, ringSprite, dotSprite);
            }

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Added execution-ready indicators to " + allStance.Length + " StancePoise character(s).");
        }

        private static void AddIndicator(StancePoise stance, Sprite ringSprite, Sprite dotSprite)
        {
            GameObject owner = stance.gameObject;

            ExecutionReadyIndicator existing = owner.GetComponentInChildren<ExecutionReadyIndicator>(true);
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            float chestY = HealthBarSetup.MeasureVisualTopLocalY(owner) * ChestHeightFraction;

            var canvasGo = new GameObject("ExecutionReadyIndicatorCanvas");
            canvasGo.transform.SetParent(owner.transform, false);
            canvasGo.transform.localPosition = new Vector3(0f, chestY, ChestForwardOffset);
            canvasGo.transform.localRotation = Quaternion.identity;
            canvasGo.transform.localScale = Vector3.one;

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.sizeDelta = RingSize;

            Image ring = CreateCenteredImage(canvasGo.transform, "Ring", ringSprite, RingSize);
            Image dot = CreateCenteredImage(canvasGo.transform, "Dot", dotSprite, DotSize);

            // Deliberately NOT pre-zeroed here (an earlier version of this method did, to start
            // invisible before IsStaggered ever goes true) - ExecutionReadyIndicator captures
            // whatever color is on the Image the first time its own Update() runs and treats that
            // as the "fully visible" reference alpha to pulse/blink against (see its own
            // _colorsCaptured comment). Zeroing it here meant it captured 0 as that reference and
            // multiplying by 0 forever, so the ring/dot NEVER became visible even while genuinely
            // staggered - real bug caught the first time this was actually screenshotted. Leaving
            // full alpha (whatever the sprite's own baked color is) here and trusting
            // ExecutionReadyIndicator's own Update() to zero it out on frame one while not
            // staggered instead.
            ExecutionReadyIndicator indicator = canvasGo.AddComponent<ExecutionReadyIndicator>();
            var so = new SerializedObject(indicator);
            so.FindProperty("stance").objectReferenceValue = stance;
            so.FindProperty("ringImage").objectReferenceValue = ring;
            so.FindProperty("dotImage").objectReferenceValue = dot;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Image CreateCenteredImage(Transform parent, string name, Sprite sprite, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;

            Image image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white; // sprite already carries the red/glow color baked in
            image.raycastTarget = false;
            return image;
        }
    }
}
