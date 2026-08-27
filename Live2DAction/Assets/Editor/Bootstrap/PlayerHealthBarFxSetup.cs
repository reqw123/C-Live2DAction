using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Live2DAction.Core;
using Live2DAction.UI;

namespace Live2DAction.EditorTools
{
    // 2026-08-24, explicit user request ("我是要你把途中的ui結構分層 把各階層圖扣下來 作層次渲染") -
    // this session's PREVIOUS version of this file built the "生命" row out of a procedurally
    // generated rounded-rect + rotated-square diamond accents. That's replaced entirely: BakeArt
    // below crops layer thumbnails directly OUT of the reference mockup image the user provided
    // into real alpha-baked sprite assets, and Apply() stacks them as actual Image layers in
    // that exact bottom-to-top order - "層次渲染" (layered rendering) with the mockup's own art,
    // not a redrawn approximation of it.
    //
    // 2026-08-24 follow-up, real bug report from the enlarged preview ("500/500字體模糊 並且有部分
    // 貼圖重疊") - Frame/Background/DelayedFill still come from the "UI結構分層" diagram column
    // (illustrative composites, fine for those three), but Fill/EnergyFlow/Spark were ALSO being
    // cropped from that same column - which turned out to be composited "the bar at ~80% state"
    // illustrations, each with their OWN baked-in spark + a dark scorch/fade trailing it. Stacking
    // that against this file's OWN separately-positioned EdgeGlow node (cropped from the same
    // region) doubled the spark and left a dark smudge floating on the bar at full HP. The mockup
    // has a SEPARATE "素材需求" section with individually isolated assets for exactly this purpose
    // (3. Fill 血量材質 - a plain solid bar, no spark; 4. Energy 能量紋理 "透明背景" - an isolated
    // lightning bolt on transparent black; 5. Front Glow 前端發光 "透明背景" - an isolated star
    // burst) - Fill/EnergyFlow/Spark now source from THOSE instead. The 500/500 blur was a
    // separate, unrelated fix - see HealthBarPreviewSetup.cs's own comment.
    //
    // Crop rects below are measured directly against HealthBarReferenceMockup.png (1536x1024,
    // a copy of the user's supplied reference image) using standard top-left-origin image
    // coordinates - ToUnityY converts to Texture2D's bottom-left-origin GetPixels coordinates.
    internal static class PlayerHealthBarFxSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string SourceMockupPath = "Assets/_Project/UI/Textures/HealthBarArt/Source/HealthBarReferenceMockup.png";
        private const string ArtFolder = "Assets/_Project/UI/Textures/HealthBarArt/";
        private const string FlowMaterialPath = "Assets/_Project/VFX/Materials/HealthEnergyFlowUI.mat";
        private const string ShaderName = "Live2DAction/UI/HealthEnergyFlow";

        // "UI結構分層" diagram column - fine for these three, no complaints against them.
        private static readonly RectInt FrameSourceRect = new RectInt(679, 509, 610, 65);
        private static readonly RectInt BackgroundSourceRect = new RectInt(679, 436, 610, 58);
        private static readonly RectInt DelayedFillSourceRect = new RectInt(679, 354, 610, 60);
        // "素材需求" section's own isolated assets - see class comment for why these three moved
        // off the diagram column.
        private static readonly RectInt FillSourceRect = new RectInt(20, 806, 280, 28);
        private static readonly RectInt EnergyFlowSourceRect = new RectInt(340, 703, 320, 35);
        // Centered exactly on the star's brightest pixel (measured directly against the mockup)
        // and cropped tight enough to stay clear of the "Front Glow"/"Noise" labels immediately
        // above/below it in the 素材需求 section.
        private static readonly RectInt SparkSourceRect = new RectInt(370, 760, 80, 44);

        // Silhouette layers (00/01/02/03) are opaque BAR SHAPES with their own dark surface
        // tones - measured directly against the mockup (bar body luminance ~0.09-0.145, the flat
        // black canvas around it ~0.055-0.07), so a tight band right in that gap cuts out just
        // the bar's own silhouette without eating into its own dark shading.
        private const float SilhouetteLumLow = 0.075f;
        private const float SilhouetteLumHigh = 0.11f;
        // Glow layers (05 energy flow, the front-glow spark) want a soft falloff instead of a
        // hard cutout - measured background noise in the "素材需求" section runs ~0.06-0.07, so
        // this starts with real margin above that (unlike the first attempt, which started right
        // at the noise ceiling and left a faint but very real non-zero-alpha haze - invisible at
        // the small real HUD size, an obvious dark smudge once blown up 4.5x for the preview).
        private const float GlowLumLow = 0.13f;
        private const float GlowLumHigh = 0.45f;

        private static readonly Color EdgeGlowColor = new Color(1f, 0.95f, 0.85f, 1f);
        private static readonly Color SparkColor = new Color(1f, 0.75f, 0.35f, 1f);
        private const int SparkCount = 6;
        private const float SparkParticleSize = 10f;
        private const float EdgeGlowSize = 22f;

        // Matches every other PlayerCornerHud row's own bar width so the 生命 row doesn't
        // suddenly dwarf 必殺/架勢/飛行 next to it.
        private const float RowWidth = 176f;

        [MenuItem("Tools/Live2DAction/Add Player Health Bar FX")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Exit Play Mode before running this - EditorSceneManager.OpenScene throws mid-Play.");
                return;
            }

            if (!BakeArt())
            {
                return;
            }

            Material flowMaterial = EnsureFlowMaterial();
            if (flowMaterial == null)
            {
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.Find("Player");
            GameObject hudGo = GameObject.Find("PlayerCornerHud");
            if (player == null || hudGo == null)
            {
                Debug.LogError("Player or PlayerCornerHud not found - run 'Polish Player Corner HUD' first.");
                return;
            }

            Health health = player.GetComponent<Health>();
            if (health == null)
            {
                Debug.LogError("Player has no Health component.");
                return;
            }

            Transform panelTransform = hudGo.transform.Find("Panel");
            Transform oldRowTransform = panelTransform != null ? panelTransform.Find("生命Track") : null;
            if (panelTransform == null || oldRowTransform == null)
            {
                Debug.LogError("Panel/生命Track not found under PlayerCornerHud - run 'Polish Player Corner HUD' first.");
                return;
            }

            RectTransform oldRowRect = oldRowTransform.GetComponent<RectTransform>();
            Vector2 rowAnchorMin = oldRowRect.anchorMin;
            Vector2 rowAnchorMax = oldRowRect.anchorMax;
            Vector2 rowPivot = oldRowRect.pivot;
            Vector2 rowAnchoredPosition = oldRowRect.anchoredPosition;

            Transform oldValueTransform = oldRowTransform.Find("Value");
            Text valueText = oldValueTransform != null ? oldValueTransform.GetComponent<Text>() : null;
            if (valueText != null)
            {
                // Reparent out before the old row GameObject (its current parent) gets destroyed
                // below - PlayerCornerHudPolishSetup's own "生命Label" is untouched, only the
                // Track/Fill/Value hierarchy this file owns gets rebuilt.
                valueText.transform.SetParent(panelTransform, false);
            }

            Object.DestroyImmediate(oldRowTransform.gameObject);
            // Cleans up the "生命Frame" sibling a PREVIOUS version of this file (procedural
            // rounded-rect + diamond accents, before this session's "把各階層圖扣下來" rewrite)
            // left as a Panel-level sibling of 生命Track - that concept doesn't exist anymore,
            // Frame is now a child inside the rebuilt row itself.
            RemoveChildIfPresent(panelTransform, "生命Frame");

            var rowGo = new GameObject("生命Track");
            rowGo.transform.SetParent(panelTransform, false);
            RectTransform rowRect = rowGo.AddComponent<RectTransform>();
            rowRect.anchorMin = rowAnchorMin;
            rowRect.anchorMax = rowAnchorMax;
            rowRect.pivot = rowPivot;
            rowRect.anchoredPosition = rowAnchoredPosition;
            rowRect.sizeDelta = new Vector2(RowWidth, RowWidth * BackgroundSourceRect.height / BackgroundSourceRect.width);

            Sprite frameSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath("00_Frame"));
            Sprite backgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath("01_Background"));
            Sprite delayedFillSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath("02_DelayedFill"));
            Sprite fillSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath("03_Fill"));
            Sprite energyFlowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath("05_EnergyFlow"));
            Sprite sparkSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath("Spark"));

            CreateArtLayer(rowGo.transform, "Frame", frameSprite, false);
            CreateArtLayer(rowGo.transform, "Background", backgroundSprite, false);
            Image delayedFillImage = CreateArtLayer(rowGo.transform, "DelayedFill", delayedFillSprite, true);
            Image fillImage = CreateArtLayer(rowGo.transform, "Fill", fillSprite, true);
            Image energyFlowImage = CreateArtLayer(rowGo.transform, "EnergyFlow", energyFlowSprite, true);
            energyFlowImage.material = flowMaterial;

            RectTransform edgeGlowRect = CreateEdgeGlow(rowGo.transform, sparkSprite);
            RectTransform[] sparkRects = CreateSparks(rowGo.transform, sparkSprite);

            if (valueText != null)
            {
                valueText.transform.SetParent(rowGo.transform, false);
                valueText.transform.SetAsLastSibling();
            }

            var fx = rowGo.AddComponent<PlayerHealthBarFx>();
            var so = new SerializedObject(fx);
            so.FindProperty("health").objectReferenceValue = health;
            so.FindProperty("currentFillImage").objectReferenceValue = fillImage;
            so.FindProperty("delayedFillImage").objectReferenceValue = delayedFillImage;
            so.FindProperty("energyFlowImage").objectReferenceValue = energyFlowImage;
            so.FindProperty("edgeGlowRect").objectReferenceValue = edgeGlowRect;
            so.FindProperty("trackRect").objectReferenceValue = rowRect;
            so.FindProperty("valueText").objectReferenceValue = valueText;

            SerializedProperty sparkArray = so.FindProperty("sparkRects");
            sparkArray.arraySize = sparkRects.Length;
            for (int i = 0; i < sparkRects.Length; i++)
            {
                sparkArray.GetArrayElementAtIndex(i).objectReferenceValue = sparkRects[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Rebuilt the 生命 row from the reference mockup's own 6 layer crops (Frame/Background/DelayedFill/Fill/EnergyFlow/EdgeGlow).");
        }

        private static void RemoveChildIfPresent(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null)
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }

        // ---- Art baking ----

        private static bool BakeArt()
        {
            string sourceFullPath = Path.Combine(Application.dataPath, "..", SourceMockupPath);
            if (!File.Exists(sourceFullPath))
            {
                Debug.LogError("Reference mockup not found at " + SourceMockupPath);
                return false;
            }

            byte[] sourceBytes = File.ReadAllBytes(sourceFullPath);
            var sourceTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!sourceTexture.LoadImage(sourceBytes))
            {
                Debug.LogError("Failed to decode " + SourceMockupPath);
                Object.DestroyImmediate(sourceTexture);
                return false;
            }

            bool ok = true;
            ok &= BakeLayer(sourceTexture, "00_Frame", FrameSourceRect, SilhouetteLumLow, SilhouetteLumHigh, false);
            ok &= BakeLayer(sourceTexture, "01_Background", BackgroundSourceRect, SilhouetteLumLow, SilhouetteLumHigh, false);
            ok &= BakeLayer(sourceTexture, "02_DelayedFill", DelayedFillSourceRect, SilhouetteLumLow, SilhouetteLumHigh, false);
            ok &= BakeLayer(sourceTexture, "03_Fill", FillSourceRect, SilhouetteLumLow, SilhouetteLumHigh, false);
            ok &= BakeLayer(sourceTexture, "05_EnergyFlow", EnergyFlowSourceRect, GlowLumLow, GlowLumHigh, true);
            ok &= BakeLayer(sourceTexture, "Spark", SparkSourceRect, GlowLumLow, GlowLumHigh, false);

            Object.DestroyImmediate(sourceTexture);
            return ok;
        }

        // sourceRect is in top-left-origin image coordinates (as measured in any normal image
        // viewer); Texture2D.GetPixels is bottom-left-origin, hence the height-flip below.
        private static bool BakeLayer(Texture2D source, string name, RectInt sourceRect, float lumLow, float lumHigh, bool wrapRepeat)
        {
            int unityY = source.height - sourceRect.y - sourceRect.height;
            if (sourceRect.x < 0 || unityY < 0 ||
                sourceRect.x + sourceRect.width > source.width ||
                unityY + sourceRect.height > source.height)
            {
                Debug.LogError("Crop rect for " + name + " falls outside the source mockup's bounds.");
                return false;
            }

            Color[] pixels = source.GetPixels(sourceRect.x, unityY, sourceRect.width, sourceRect.height);
            for (int i = 0; i < pixels.Length; i++)
            {
                Color c = pixels[i];
                float luminance = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
                float t = Mathf.InverseLerp(lumLow, lumHigh, luminance);
                float alpha = Mathf.SmoothStep(0f, 1f, t);
                pixels[i] = new Color(c.r, c.g, c.b, alpha);
            }

            // Written into a texture that never went through LoadImage - see
            // ChallengeStartTauntSetup.BakeTransparentPng's own comment for the real bug this
            // avoids (a LoadImage-sourced texture can silently drop alpha storage on EncodeToPNG
            // regardless of the RGBA32 format hint).
            var output = new Texture2D(sourceRect.width, sourceRect.height, TextureFormat.RGBA32, false);
            output.SetPixels(pixels);
            output.Apply();
            byte[] png = output.EncodeToPNG();
            Object.DestroyImmediate(output);

            string relativePath = SpritePath(name);
            string fullPath = Path.Combine(Application.dataPath, "..", relativePath);
            string directory = Path.GetDirectoryName(fullPath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllBytes(fullPath, png);

            AssetDatabase.ImportAsset(relativePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(relativePath);
            if (importer == null)
            {
                Debug.LogError("Baked layer not found at " + relativePath + " after import.");
                return false;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            // Only the energy-flow layer is UV-scrolled - Repeat wrap is what makes that loop
            // instead of clamping/going blank past 0-1 (see UVScrollDemo.shader's own comment,
            // this is the same mechanism). Everything else is a static, non-tiling bar image.
            importer.wrapMode = wrapRepeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
            return true;
        }

        private static string SpritePath(string name)
        {
            return ArtFolder + name + ".png";
        }

        // ---- Scene hierarchy ----

        // Every layer shares the exact same RowWidth and a centered anchor, so they all line up
        // horizontally regardless of each crop's own native aspect ratio (they were all cropped
        // from the mockup using the SAME x-range, so this reproduces that alignment exactly).
        private static Image CreateArtLayer(Transform parent, string name, Sprite sprite, bool filled)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;

            float aspect = sprite != null ? sprite.rect.width / sprite.rect.height : 8.8f;
            rect.sizeDelta = new Vector2(RowWidth, RowWidth / aspect);

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

        private static RectTransform CreateEdgeGlow(Transform parent, Sprite sparkSprite)
        {
            var go = new GameObject("EdgeGlow");
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(EdgeGlowSize, EdgeGlowSize);
            rect.anchoredPosition = new Vector2(RowWidth, 0f);

            Image image = go.AddComponent<Image>();
            image.sprite = sparkSprite;
            image.color = EdgeGlowColor;
            image.raycastTarget = false;
            return rect;
        }

        private static RectTransform[] CreateSparks(Transform parent, Sprite sparkSprite)
        {
            var sparks = new RectTransform[SparkCount];
            for (int i = 0; i < SparkCount; i++)
            {
                var go = new GameObject("Spark" + i);
                go.transform.SetParent(parent, false);
                RectTransform rect = go.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 0.5f);
                rect.anchorMax = new Vector2(0f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(SparkParticleSize, SparkParticleSize);

                Image image = go.AddComponent<Image>();
                image.sprite = sparkSprite;
                image.color = SparkColor;
                image.raycastTarget = false;

                go.SetActive(false);
                sparks[i] = rect;
            }

            return sparks;
        }

        private static Material EnsureFlowMaterial()
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError("Shader not found: " + ShaderName + " - HealthEnergyFlowUI.shader may still be compiling.");
                return null;
            }

            Material existing = AssetDatabase.LoadAssetAtPath<Material>(FlowMaterialPath);
            if (existing != null)
            {
                if (existing.shader != shader)
                {
                    existing.shader = shader;
                    EditorUtility.SetDirty(existing);
                }
                return existing;
            }

            var material = new Material(shader);
            string directory = Path.GetDirectoryName(FlowMaterialPath);
            if (directory != null && !AssetDatabase.IsValidFolder(directory))
            {
                AssetDatabase.CreateFolder(Path.GetDirectoryName(directory), Path.GetFileName(directory));
            }
            AssetDatabase.CreateAsset(material, FlowMaterialPath);
            return material;
        }
    }
}
