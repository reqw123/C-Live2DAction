using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Live2DAction.Core;
using Live2DAction.UI;

namespace Live2DAction.EditorTools
{
    // 2026-08-25, explicit user request ("並參考說明文件製作血量條") - follows
    // Docs/HealthBarUISystem/README.md section 五 ("給下一個 Boss 的操作步驟") to the letter,
    // same structure as Enemy076HealthBarSetup.cs/the original PiHaiWangHealthBarSetup.cs (that
    // one was deleted along with the rest of the old 屁孩王's combat setup when the user asked
    // for a redesign - this is a fresh copy for the new "Man in Black" model, PlayerHealthBarFx
    // itself has zero Player-specific logic so it's the same reuse either way).
    //
    // 屁孩王 doesn't have 076's name-instability problem (it's a normal Humanoid GameObject, not
    // a CubismModel3Json.ToModel() root), so this finds it by plain name.
    internal static class PiHaiWangHealthBarSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string ArtFolder = "Assets/_Project/UI/Textures/HealthBarArt/";
        private const string FlowMaterialPath = "Assets/_Project/VFX/Materials/HealthEnergyFlowUI.mat";
        private const string BossName = "屁孩王";

        // The reference pixel width every PlayerHealthBarFx tuning constant below was designed
        // against (PlayerHealthBarFxSetup.RowWidth) - used purely to derive a scale ratio.
        private const float ReferenceHudRowWidth = 176f;

        // Same design call as 076's own bar - a boss-style floating bar roughly matching the
        // character's own silhouette width, clearly narrower than the full body.
        private const float BarWidthFraction = 0.6f;
        private const float MarginAboveHeadWorld = 0.25f;

        private static readonly Color DelayedFillColor = new Color(1f, 0.55f, 0.3f, 0.65f);
        private static readonly Color EdgeGlowColor = new Color(1f, 0.95f, 0.85f, 1f);
        private static readonly Color SparkColor = new Color(1f, 0.75f, 0.35f, 1f);
        private const int SparkCount = 6;

        [MenuItem("Tools/Live2DAction/Add PiHaiWang Health Bar (Reference Art)")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Exit Play Mode before running this - EditorSceneManager.OpenScene throws mid-Play.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject boss = GameObject.Find(BossName);
            if (boss == null)
            {
                Debug.LogError("Could not find " + BossName + " in the scene.");
                return;
            }

            Health health = boss.GetComponent<Health>();
            if (health == null)
            {
                Debug.LogError(boss.name + " has no Health component.");
                return;
            }

            Transform visual = boss.transform.Find("Visual");
            Renderer[] renderers = (visual != null ? visual : boss.transform).GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Debug.LogError(boss.name + " has no renderers - cannot measure its visual bounds.");
                return;
            }
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            float rootScale = boss.transform.localScale.x;
            float headTopWorldOffset = bounds.max.y - boss.transform.position.y;
            float barWorldWidth = bounds.size.x * BarWidthFraction;
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

            Transform oldCanvas = boss.transform.Find("HealthBarCanvas");
            if (oldCanvas != null)
            {
                Object.DestroyImmediate(oldCanvas.gameObject);
            }

            var canvasGo = new GameObject("HealthBarCanvas");
            canvasGo.transform.SetParent(boss.transform, false);
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
            Debug.Log(boss.name + "'s health bar rebuilt with the reference art layers - world width " + barWorldWidth.ToString("F2") + "m (character visual width " + bounds.size.x.ToString("F2") + "m x " + bounds.size.y.ToString("F2") + "m tall).");
        }

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
