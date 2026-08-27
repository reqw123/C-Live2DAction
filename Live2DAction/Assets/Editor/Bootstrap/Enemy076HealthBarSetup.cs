using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Live2D.Cubism.Core;
using Live2DAction.AI;
using Live2DAction.Combat;
using Live2DAction.Core;
using Live2DAction.UI;

namespace Live2DAction.EditorTools
{
    // 2026-08-24, explicit user request ("接下來把此ui取代076的生命條 並且根據076的身高 體型 決定
    // 適當的ui尺寸長度") - replaces 076's existing plain WorldSpaceHealthBar (a bare red
    // Image.Type.Filled bar, see HealthBarSetup.cs) with the same reference-art layered system
    // PlayerHealthBarFxSetup built for the player's corner HUD (Frame/Background/DelayedFill/
    // Fill/EnergyFlow/EdgeGlow, all baked from the mockup, same PlayerHealthBarFx component -
    // it only reads Health and writes Image/RectTransform properties, nothing about it is
    // actually player-specific despite the name).
    //
    // 076 is found by COMPONENT SIGNATURE, not name or position - the user separately confirmed
    // 076's own GameObject name keeps getting reset to empty by a known recurring Cubism/reimport
    // issue (see FixLive2DStandeeNames.cs's own comment for the established position-based
    // workaround this project already uses elsewhere), and this session's own investigation found
    // its position has also drifted from the original (-6,0,-8) spawn constant to (-6.04,1.98,-14)
    // at some point - so neither name nor a fixed position is reliable. What DOES reliably
    // distinguish it from 077 (the other Live2D standee) is that 076 alone carries EnemyAI +
    // Health + PlayerCombat (077 "stays a pure visual standee" per Give076CombatSetup.cs's own
    // comment) - confirmed directly against the live scene before writing this.
    //
    // Sizing: the bar's WORLD width is computed as a fraction of 076's own measured visual
    // bounds width (not a fixed magic number) - see BarWidthFraction's own comment - and every
    // other pixel-tuned PlayerHealthBarFx constant (shake magnitude, spark speed/gravity, edge
    // inset, node sizes) is scaled down by the same ratio this bar's width is to the player
    // corner HUD's own 176px row width, since those constants were tuned for that pixel space.
    internal static class Enemy076HealthBarSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string ArtFolder = "Assets/_Project/UI/Textures/HealthBarArt/";
        private const string FlowMaterialPath = "Assets/_Project/VFX/Materials/HealthEnergyFlowUI.mat";

        // The reference pixel width every PlayerHealthBarFx tuning constant below was designed
        // against (PlayerHealthBarFxSetup.RowWidth) - used purely to derive a scale ratio, not
        // copied verbatim anywhere.
        private const float ReferenceHudRowWidth = 176f;

        // 2026-08-24 design call - a boss-style floating bar roughly matching the character's
        // own silhouette width reads better than one that either dwarfs or disappears against a
        // 4+ world-unit-tall standee; 60% keeps it clearly narrower than the full body.
        private const float BarWidthFraction = 0.6f;
        private const float MarginAboveHeadWorld = 0.25f;

        private static readonly Color DelayedFillColor = new Color(1f, 0.55f, 0.3f, 0.65f);
        private static readonly Color EdgeGlowColor = new Color(1f, 0.95f, 0.85f, 1f);
        private static readonly Color SparkColor = new Color(1f, 0.75f, 0.35f, 1f);
        private const int SparkCount = 6;

        [MenuItem("Tools/Live2DAction/Add 076 Health Bar (Reference Art)")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Exit Play Mode before running this - EditorSceneManager.OpenScene throws mid-Play.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject standee076 = Find076();
            if (standee076 == null)
            {
                Debug.LogError("Could not find 076 (a CubismModel root with EnemyAI+Health+PlayerCombat) in the scene.");
                return;
            }

            // Real recurring bug the user flagged this same turn - the name keeps getting reset
            // to empty. Restoring it here is low-risk and makes every OTHER tool that looks it
            // up by name (GameObject.Find("076...")) work again until it next reverts.
            if (string.IsNullOrEmpty(standee076.name))
            {
                standee076.name = "076_DoNotShip";
            }

            Health health = standee076.GetComponent<Health>();

            Renderer[] renderers = standee076.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Debug.LogError(standee076.name + " has no renderers - cannot measure its visual bounds.");
                return;
            }
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            float rootScale = standee076.transform.localScale.x;
            float headTopWorldOffset = bounds.max.y - standee076.transform.position.y;
            float barWorldWidth = bounds.size.x * BarWidthFraction;
            // Everything PlayerHealthBarFx's own pixel-tuned constants (shake magnitude, spark
            // speed/gravity, edge inset, node sizes) get scaled by, so a bar built at a different
            // physical size doesn't shake/spark wildly out of proportion to itself.
            float unitScale = barWorldWidth / ReferenceHudRowWidth;

            Material flowMaterial = AssetDatabase.LoadAssetAtPath<Material>(FlowMaterialPath);
            if (flowMaterial == null)
            {
                Debug.LogError("Flow material not found at " + FlowMaterialPath + " - run 'Add Player Health Bar FX' first to bake it.");
                return;
            }

            Sprite frameSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "00_Frame.png");
            Sprite backgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "01_Background.png");
            Sprite delayedFillSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "02_DelayedFill.png");
            Sprite fillSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "03_Fill.png");
            Sprite energyFlowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "05_EnergyFlow.png");
            Sprite sparkSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "Spark.png");
            if (frameSprite == null || backgroundSprite == null || delayedFillSprite == null || fillSprite == null || energyFlowSprite == null || sparkSprite == null)
            {
                Debug.LogError("One or more baked health bar sprites not found under " + ArtFolder + " - run 'Add Player Health Bar FX' first to bake them.");
                return;
            }

            Transform oldCanvas = standee076.transform.Find("HealthBarCanvas");
            if (oldCanvas != null)
            {
                Object.DestroyImmediate(oldCanvas.gameObject);
            }

            var canvasGo = new GameObject("HealthBarCanvas");
            canvasGo.transform.SetParent(standee076.transform, false);
            // Cancels the standee's own 5x scale so this canvas's RectTransform units are plain
            // world meters from here on - see class comment for why this matters (the OLD
            // WorldSpaceHealthBar canvas kept localScale=1 and inherited the 5x, which is why it
            // floated noticeably higher above the head than its own localPosition.y suggested).
            canvasGo.transform.localScale = Vector3.one / Mathf.Max(0.0001f, rootScale);
            canvasGo.transform.localPosition = new Vector3(0f, (headTopWorldOffset + MarginAboveHeadWorld) / rootScale, 0f);

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(barWorldWidth, barWorldWidth * 0.15f);

            CreateArtLayer(canvasGo.transform, "Frame", frameSprite, barWorldWidth, false);
            CreateArtLayer(canvasGo.transform, "Background", backgroundSprite, barWorldWidth, false);
            Image delayedFillImage = CreateArtLayer(canvasGo.transform, "DelayedFill", delayedFillSprite, barWorldWidth, true);
            delayedFillImage.color = DelayedFillColor;
            Image fillImage = CreateArtLayer(canvasGo.transform, "Fill", fillSprite, barWorldWidth, true);
            Image energyFlowImage = CreateArtLayer(canvasGo.transform, "EnergyFlow", energyFlowSprite, barWorldWidth, true);
            energyFlowImage.material = flowMaterial;

            RectTransform edgeGlowRect = CreateEdgeGlow(canvasGo.transform, sparkSprite, barWorldWidth);
            RectTransform[] sparkRects = CreateSparks(canvasGo.transform, sparkSprite, barWorldWidth);

            var fx = canvasGo.AddComponent<PlayerHealthBarFx>();
            var so = new SerializedObject(fx);
            so.FindProperty("health").objectReferenceValue = health;
            so.FindProperty("currentFillImage").objectReferenceValue = fillImage;
            so.FindProperty("delayedFillImage").objectReferenceValue = delayedFillImage;
            so.FindProperty("energyFlowImage").objectReferenceValue = energyFlowImage;
            so.FindProperty("edgeGlowRect").objectReferenceValue = edgeGlowRect;
            so.FindProperty("trackRect").objectReferenceValue = canvasRect;
            so.FindProperty("valueText").objectReferenceValue = null;
            // Bar floats above the character in world space and is always billboarded to face
            // the camera - unlike the screen-space corner HUD, which never needs this.
            so.FindProperty("billboardToCamera").boolValue = true;
            so.FindProperty("edgeInset").floatValue = 2f * unitScale;
            so.FindProperty("shakeMagnitude").floatValue = 6f * unitScale;
            so.FindProperty("sparkSpeed").floatValue = 90f * unitScale;
            so.FindProperty("sparkGravity").floatValue = 220f * unitScale;

            SerializedProperty sparkArray = so.FindProperty("sparkRects");
            sparkArray.arraySize = sparkRects.Length;
            for (int i = 0; i < sparkRects.Length; i++)
            {
                sparkArray.GetArrayElementAtIndex(i).objectReferenceValue = sparkRects[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log(standee076.name + "'s health bar rebuilt with the reference art layers - world width " + barWorldWidth.ToString("F2") + "m (character visual width " + bounds.size.x.ToString("F2") + "m x " + bounds.size.y.ToString("F2") + "m tall).");
        }

        private static GameObject Find076()
        {
            CubismModel[] models = Object.FindObjectsByType<CubismModel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (CubismModel model in models)
            {
                GameObject go = model.gameObject;
                if (go.GetComponent<EnemyAI>() != null && go.GetComponent<Health>() != null && go.GetComponent<PlayerCombat>() != null)
                {
                    return go;
                }
            }
            return null;
        }

        // No Value text field here (076 isn't shown a numeric HP readout, matching how
        // HealthBarSetup's own WorldSpaceHealthBar never had one either - this is a floating
        // world-space silhouette bar, not a HUD panel with room for a number).
        private static Image CreateArtLayer(Transform parent, string name, Sprite sprite, float width, bool filled)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;

            float aspect = sprite.rect.width / sprite.rect.height;
            rect.sizeDelta = new Vector2(width, width / aspect);

            Image image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            if (filled)
            {
                image.type = Image.Type.Filled;
                image.fillMethod = Image.FillMethod.Horizontal;
                image.fillOrigin = (int)Image.OriginHorizontal.Left;
                image.fillAmount = 1f;
            }
            return image;
        }

        private static RectTransform CreateEdgeGlow(Transform parent, Sprite sparkSprite, float barWorldWidth)
        {
            float size = barWorldWidth * (22f / ReferenceHudRowWidth);

            var go = new GameObject("EdgeGlow");
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = new Vector2(barWorldWidth, 0f);

            Image image = go.AddComponent<Image>();
            image.sprite = sparkSprite;
            image.color = EdgeGlowColor;
            image.raycastTarget = false;
            return rect;
        }

        private static RectTransform[] CreateSparks(Transform parent, Sprite sparkSprite, float barWorldWidth)
        {
            float size = barWorldWidth * (10f / ReferenceHudRowWidth);

            var sparks = new RectTransform[SparkCount];
            for (int i = 0; i < SparkCount; i++)
            {
                var go = new GameObject("Spark" + i);
                go.transform.SetParent(parent, false);
                RectTransform rect = go.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 0.5f);
                rect.anchorMax = new Vector2(0f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(size, size);

                Image image = go.AddComponent<Image>();
                image.sprite = sparkSprite;
                image.color = SparkColor;
                image.raycastTarget = false;

                go.SetActive(false);
                sparks[i] = rect;
            }

            return sparks;
        }
    }
}
