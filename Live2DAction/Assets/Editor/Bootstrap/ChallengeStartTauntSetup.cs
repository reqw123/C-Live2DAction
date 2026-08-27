using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Live2DAction.World;

namespace Live2DAction.EditorTools
{
    // 2026-08-23, explicit user request ("當上升氣流機關啟用時 此圖片的文字ui就要出現 處理方式與
    // "想要起飛嗎" 一致") - see ChallengeStartTaunt's own class comment for why this is a
    // screen-space fade rather than living on TimeTrialStartMechanism's world-space PromptCanvas.
    //
    // 2026-08-23 follow-up, explicit user request ("換成這張 注意透明通道 還有實際匹配的ui大小") -
    // replaced the source art with a moody dark-smoke "還是做不到嗎?" banner and two fixes:
    // (1) the source is a plain JPG (no alpha channel possible) with a near-black background meant
    // to dissolve into the 3D scene, same intent as "想要起飛嗎"'s own PNG (confirmed that one has
    // real alpha - alphaIsTransparency=true, alphaSource=FromInput). A JPG can't carry that data on
    // its own, so this bakes a luminance-keyed alpha channel (dark background -> transparent,
    // bright text/ornament -> opaque, smooth falloff between via Mathf.SmoothStep so the misty
    // glow fades naturally instead of a hard cutout) and writes a real PNG with that alpha - see
    // BakeTransparentPng below.
    // (2) "實際匹配的ui大小" - the banner's RectTransform is now sized directly from the baked
    // PNG's own pixel aspect ratio (TauntTargetWidth held constant, height derived), instead of a
    // guessed 900x500 box that could letterbox/pillarbox against Image.preserveAspect.
    internal static class ChallengeStartTauntSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string SourceImagePath = "Assets/_Project/UI/TimeTrialFailTauntTextSource.jpg";
        private const string BakedImagePath = "Assets/_Project/UI/TimeTrialFailTauntText.png";

        // Background pixels at or below this luminance are fully transparent; at or above the
        // second value are fully opaque. Tuned against this specific source image's own near-black
        // corners vs. its brighter misty center/text - not a generic constant.
        private const float AlphaLuminanceLow = 0.05f;
        private const float AlphaLuminanceHigh = 0.35f;

        // Held constant; height is derived from the baked PNG's own aspect ratio at Apply() time
        // so the banner is never stretched/letterboxed against whatever the source image's real
        // proportions are.
        private const float TauntTargetWidth = 900f;

        [MenuItem("Tools/Live2DAction/Add Challenge Start Taunt")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject root = GameObject.Find("SkyIslandTimeTrial");
            if (root == null)
            {
                Debug.LogError("SkyIslandTimeTrial not found - run 'Add Sky Island Time Trial Course' first.");
                return;
            }

            TimeTrialController controller = null;
            foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>())
            {
                if (mb is TimeTrialController tc)
                {
                    controller = tc;
                    break;
                }
            }
            if (controller == null)
            {
                Debug.LogError("TimeTrialController not found under SkyIslandTimeTrial.");
                return;
            }

            Sprite tauntSprite = EnsureTauntSprite(out float aspectWidthOverHeight);
            if (tauntSprite == null)
            {
                Debug.LogError("EnsureTauntSprite returned null - aborting before touching the scene.");
                return;
            }

            GameObject existing = GameObject.Find("ChallengeStartTauntCanvas");
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            var canvasGo = new GameObject("ChallengeStartTauntCanvas");
            canvasGo.transform.SetParent(root.transform, false);

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();

            CanvasGroup canvasGroup = canvasGo.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            var imageGo = new GameObject("TauntImage");
            imageGo.transform.SetParent(canvasGo.transform, false);
            RectTransform imageRect = imageGo.AddComponent<RectTransform>();
            imageRect.anchorMin = new Vector2(0.5f, 0.5f);
            imageRect.anchorMax = new Vector2(0.5f, 0.5f);
            imageRect.pivot = new Vector2(0.5f, 0.5f);
            // Width held constant, height derived from the source's own aspect ratio - see
            // TauntTargetWidth's own comment for why this replaced a guessed fixed box.
            imageRect.sizeDelta = new Vector2(TauntTargetWidth, TauntTargetWidth / aspectWidthOverHeight);
            // 2026-08-24, explicit user request ("並且是顯示在畫面正中央") - was offset (0,80) from
            // an earlier pass; now this fires on FAIL (see ChallengeStartTaunt's own comment) the
            // player has stopped flying and is looking at the HUD, so dead center reads better
            // than the old above-center offset that suited a mid-flight "go!" flourish.
            imageRect.anchoredPosition = Vector2.zero;

            Image image = imageGo.AddComponent<Image>();
            image.sprite = tauntSprite;
            image.preserveAspect = true;
            image.raycastTarget = false;

            ChallengeStartTaunt taunt = canvasGo.AddComponent<ChallengeStartTaunt>();
            var so = new SerializedObject(taunt);
            so.FindProperty("controller").objectReferenceValue = controller;
            so.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"Added challenge-start taunt banner ({imageRect.sizeDelta.x}x{imageRect.sizeDelta.y}), wired to TimeTrialController.");
        }

        private static Sprite EnsureTauntSprite(out float aspectWidthOverHeight)
        {
            aspectWidthOverHeight = 16f / 9f;

            if (!BakeTransparentPng(out int width, out int height))
            {
                return null;
            }
            aspectWidthOverHeight = (float)width / height;

            AssetDatabase.ImportAsset(BakedImagePath, ImportAssetOptions.ForceSynchronousImport);

            var importer = AssetImporter.GetAtPath(BakedImagePath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError("Baked taunt image not found at " + BakedImagePath);
                return null;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BakedImagePath);
            if (sprite == null)
            {
                Debug.LogError("Sprite failed to load at " + BakedImagePath + " even after reimport.");
            }
            return sprite;
        }

        // Reads the raw JPG source directly off disk (LoadImage on an in-memory Texture2D is
        // always readable regardless of the asset's own import settings, so this doesn't need to
        // toggle isReadable on SourceImagePath first) and bakes a luminance-keyed alpha channel -
        // see AlphaLuminanceLow/High's own comment for the actual thresholds.
        private static bool BakeTransparentPng(out int width, out int height)
        {
            width = 0;
            height = 0;

            string sourceFullPath = Path.Combine(Application.dataPath, "..", SourceImagePath);
            if (!File.Exists(sourceFullPath))
            {
                Debug.LogError("Taunt source image not found at " + SourceImagePath);
                return false;
            }

            byte[] sourceBytes = File.ReadAllBytes(sourceFullPath);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(sourceBytes))
            {
                Debug.LogError("Failed to decode taunt source image at " + SourceImagePath);
                Object.DestroyImmediate(texture);
                return false;
            }

            // 2026-08-23, real bug found via direct pixel inspection after the first bake -
            // LoadImage on a JPG source (no alpha channel in the file) silently settles the
            // texture into a storage format with no real alpha channel regardless of the RGBA32
            // hint passed to the constructor above - SetPixels/Apply on THAT texture accepted the
            // per-pixel alpha values below without error, but EncodeToPNG then wrote alpha=1
            // everywhere (confirmed: GetPixel on the baked output showed RGBA(...,1.000) at every
            // sample, including the near-black corners that should have been near-transparent).
            // Fixed by writing the computed pixels into a SEPARATE, freshly-constructed RGBA32
            // texture that never went through LoadImage, so it has no ambiguity about its own
            // alpha storage.
            Color[] sourcePixels = texture.GetPixels();
            width = texture.width;
            height = texture.height;
            Object.DestroyImmediate(texture);

            var outputTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var outputPixels = new Color[sourcePixels.Length];
            for (int i = 0; i < sourcePixels.Length; i++)
            {
                Color c = sourcePixels[i];
                float luminance = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
                float t = Mathf.InverseLerp(AlphaLuminanceLow, AlphaLuminanceHigh, luminance);
                float alpha = Mathf.SmoothStep(0f, 1f, t);
                outputPixels[i] = new Color(c.r, c.g, c.b, alpha);
            }
            outputTexture.SetPixels(outputPixels);
            outputTexture.Apply();

            byte[] pngBytes = outputTexture.EncodeToPNG();
            Object.DestroyImmediate(outputTexture);

            string bakedFullPath = Path.Combine(Application.dataPath, "..", BakedImagePath);
            string directory = Path.GetDirectoryName(bakedFullPath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllBytes(bakedFullPath, pngBytes);

            AssetDatabase.ImportAsset(BakedImagePath, ImportAssetOptions.ForceSynchronousImport);

            return true;
        }
    }
}
