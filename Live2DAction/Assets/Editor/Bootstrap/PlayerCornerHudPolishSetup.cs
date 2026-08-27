using System.IO;
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
    // 2026-08-23, explicit user request ("玩家狀態條...很醜") - the original PlayerCornerHud
    // (2026-08-23 earlier this session) was 4 bare solid-color Image rectangles with no frame,
    // no labels, no visual hierarchy - functional but plain. Rebuilds it with a dark rounded panel
    // behind all 4 rows, a rounded track under each fill (same procedural rounded-rect sprite,
    // 9-sliced so it scales cleanly), and a short label per row so each bar reads at a glance
    // without needing to memorize "red=health, blue=ultimate" by color alone.
    //
    // Fill bars themselves stay square-cornered Image.Type=Filled (same as before) - Unity's
    // Image can't be both Sliced (for rounded corners) AND Filled (for fillAmount clipping) at
    // once, and adding a mask+separate-fill-object just to round the fill's own corners wasn't
    // worth the complexity for a moving bar whose ends are mostly hidden inside the rounded
    // track anyway. PlayerCornerHud.cs itself is untouched - still just polls CurrentX/MaxX into
    // fillAmount every frame, same fields, just re-wired to the new Image objects.
    internal static class PlayerCornerHudPolishSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string RoundedRectSpritePath = "Assets/_Project/UI/Textures/HudRoundedRect.png";

        private const float PanelWidth = 260f;
        private const float PanelHeight = 156f;
        private const float RowHeight = 26f;
        private const float RowSpacing = 34f;
        private const float LabelWidth = 44f;
        private const float BarWidth = 176f;
        private const float BarHeight = 20f;
        private const float PanelPadding = 12f;

        private static readonly Color PanelColor = new Color(0.05f, 0.06f, 0.09f, 0.72f);
        private static readonly Color TrackColor = new Color(1f, 1f, 1f, 0.14f);

        private struct RowSpec
        {
            public string Label;
            public Color FillColor;

            public RowSpec(string label, Color fillColor)
            {
                Label = label;
                FillColor = fillColor;
            }
        }

        [MenuItem("Tools/Live2DAction/Polish Player Corner HUD")]
        public static void Apply()
        {
            EnsureRoundedRectSprite();

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("Player GameObject not found in " + ScenePath);
                return;
            }

            Health health = player.GetComponent<Health>();
            UltimateEnergy[] energies = player.GetComponents<UltimateEnergy>();
            UltimateEnergy ultimateEnergy = null;
            UltimateEnergy flightEnergy = null;
            foreach (UltimateEnergy e in energies)
            {
                if (e.MaxEnergy < 200f)
                {
                    ultimateEnergy = e;
                }
                else
                {
                    flightEnergy = e;
                }
            }
            StancePoise stance = player.GetComponent<StancePoise>();

            Sprite roundedRect = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedRectSpritePath);
            if (roundedRect == null)
            {
                Debug.LogError("Rounded-rect sprite not found at " + RoundedRectSpritePath + " after generation.");
                return;
            }

            GameObject existing = GameObject.Find("PlayerCornerHud");
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            var hudGo = new GameObject("PlayerCornerHud");
            Canvas canvas = hudGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = hudGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            hudGo.AddComponent<GraphicRaycaster>();

            GameObject panelGo = new GameObject("Panel");
            panelGo.transform.SetParent(hudGo.transform, false);
            RectTransform panelRect = panelGo.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(1f, 1f);
            panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            panelRect.anchoredPosition = new Vector2(-16f, -16f);
            Image panelImage = panelGo.AddComponent<Image>();
            panelImage.sprite = roundedRect;
            panelImage.type = Image.Type.Sliced;
            panelImage.color = PanelColor;
            panelImage.raycastTarget = false;

            RowSpec[] rows =
            {
                new RowSpec("生命", new Color(0.86f, 0.16f, 0.16f)),
                new RowSpec("必殺", new Color(0.25f, 0.55f, 0.95f)),
                new RowSpec("架勢", new Color(0.95f, 0.62f, 0.12f)),
                new RowSpec("飛行", new Color(0.25f, 0.85f, 0.85f)),
            };

            var fills = new Image[4];
            var texts = new Text[4];
            for (int i = 0; i < rows.Length; i++)
            {
                float rowY = -PanelPadding - RowHeight * 0.5f - RowSpacing * i;
                fills[i] = BuildRow(panelGo.transform, rows[i], rowY, roundedRect, out texts[i]);
            }

            var hud = hudGo.AddComponent<PlayerCornerHud>();
            var so = new SerializedObject(hud);
            so.FindProperty("health").objectReferenceValue = health;
            so.FindProperty("ultimateEnergy").objectReferenceValue = ultimateEnergy;
            so.FindProperty("stance").objectReferenceValue = stance;
            so.FindProperty("flightEnergy").objectReferenceValue = flightEnergy;
            so.FindProperty("healthFill").objectReferenceValue = fills[0];
            so.FindProperty("ultimateEnergyFill").objectReferenceValue = fills[1];
            so.FindProperty("stanceFill").objectReferenceValue = fills[2];
            so.FindProperty("flightEnergyFill").objectReferenceValue = fills[3];
            so.FindProperty("healthText").objectReferenceValue = texts[0];
            so.FindProperty("ultimateEnergyText").objectReferenceValue = texts[1];
            so.FindProperty("stanceText").objectReferenceValue = texts[2];
            so.FindProperty("flightEnergyText").objectReferenceValue = texts[3];
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Rebuilt PlayerCornerHud with rounded panel + labels.");
        }

        private static Image BuildRow(Transform panel, RowSpec spec, float rowY, Sprite roundedRect, out Text valueText)
        {
            var labelGo = new GameObject(spec.Label + "Label");
            labelGo.transform.SetParent(panel, false);
            RectTransform labelRect = labelGo.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(0f, 1f);
            labelRect.pivot = new Vector2(0f, 0.5f);
            labelRect.sizeDelta = new Vector2(LabelWidth, RowHeight);
            labelRect.anchoredPosition = new Vector2(PanelPadding, rowY);
            Text label = labelGo.AddComponent<Text>();
            label.text = spec.Label;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 18;
            label.color = new Color(0.92f, 0.92f, 0.95f);
            label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false;

            var trackGo = new GameObject(spec.Label + "Track");
            trackGo.transform.SetParent(panel, false);
            RectTransform trackRect = trackGo.AddComponent<RectTransform>();
            trackRect.anchorMin = new Vector2(0f, 1f);
            trackRect.anchorMax = new Vector2(0f, 1f);
            trackRect.pivot = new Vector2(0f, 0.5f);
            trackRect.sizeDelta = new Vector2(BarWidth, BarHeight);
            trackRect.anchoredPosition = new Vector2(PanelPadding + LabelWidth, rowY);
            Image track = trackGo.AddComponent<Image>();
            track.sprite = roundedRect;
            track.type = Image.Type.Sliced;
            track.color = TrackColor;
            track.raycastTarget = false;

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(trackGo.transform, false);
            RectTransform fillRect = fillGo.AddComponent<RectTransform>();
            // Inset slightly inside the track so the rounded track edge always frames the fill,
            // even at fillAmount=1 - same "track is the frame, fill floats inside it" idea a
            // polished health bar uses instead of the fill exactly matching the track's bounds.
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);
            Image fill = fillGo.AddComponent<Image>();
            fill.color = spec.FillColor;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            fill.raycastTarget = false;
            // 2026-08-23, real playtested bug ("數字是有變的 但ui貼紙沒變") - same root cause
            // HealthBarSetup.CreateStretchedImage's own comment already documents from a
            // 2026-08-12 report: Image.Type.Filled needs actual sprite UV/geometry data to build
            // a partial-fill mesh - with sprite == null it silently always renders the full rect
            // no matter what fillAmount is set to, even though fillAmount itself reads back
            // correctly as a plain property (exactly why the "does the number change" diagnostic
            // passed while the bar visually didn't). Same fix: Unity's own built-in default UI
            // sprite, no custom art asset needed.
            fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            // Numeric "current/max" overlay, centered on the track itself (a child of the track,
            // not the fill, so it stays put and readable regardless of how far the fill shrinks) -
            // see PlayerCornerHud.healthText's own comment for why this exists.
            var valueTextGo = new GameObject("Value");
            valueTextGo.transform.SetParent(trackGo.transform, false);
            RectTransform valueRect = valueTextGo.AddComponent<RectTransform>();
            valueRect.anchorMin = Vector2.zero;
            valueRect.anchorMax = Vector2.one;
            valueRect.offsetMin = Vector2.zero;
            valueRect.offsetMax = Vector2.zero;
            valueText = valueTextGo.AddComponent<Text>();
            valueText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            valueText.fontSize = 13;
            valueText.color = Color.white;
            valueText.alignment = TextAnchor.MiddleCenter;
            valueText.raycastTarget = false;
            valueText.horizontalOverflow = HorizontalWrapMode.Overflow;
            Shadow textOutline = valueTextGo.AddComponent<Shadow>();
            textOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            textOutline.effectDistance = new Vector2(1f, -1f);

            return fill;
        }

        // Procedural rounded-rect with a signed-distance-field edge (crisp anti-aliased border,
        // not a hard pixel cutoff) - same "baked alpha, tinted at runtime via Image.color"
        // convention as InvulnerabilityRippleSetup/ExecutionIndicatorSetup's own ring sprites.
        // 9-sliced (border matches the corner radius) so ANY of the three uses above (wide panel,
        // wide track, narrow label backdrop) stretches without visibly distorting the corners.
        private static void EnsureRoundedRectSprite()
        {
            string fullPath = Path.Combine(Application.dataPath, "..", RoundedRectSpritePath);

            const int size = 64;
            const float radius = 16f;
            float half = size / 2f;

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f - half;
                    float py = y + 0.5f - half;
                    float qx = Mathf.Abs(px) - (half - radius);
                    float qy = Mathf.Abs(py) - (half - radius);
                    float outsideX = Mathf.Max(qx, 0f);
                    float outsideY = Mathf.Max(qy, 0f);
                    float distance = Mathf.Sqrt(outsideX * outsideX + outsideY * outsideY) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                    float alpha = Mathf.Clamp01(0.5f - distance);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            byte[] png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);

            string directory = Path.GetDirectoryName(fullPath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllBytes(fullPath, png);

            AssetDatabase.ImportAsset(RoundedRectSpritePath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(RoundedRectSpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = 100f;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();

            // Border matches the corner radius exactly - the flat center 1px strip between the
            // two radius bands is what gets stretched, so scaling a 260px-wide panel from this
            // 64px source doesn't squash or bulge the rounded corners themselves.
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedRectSpritePath);
            var spriteImporter = (TextureImporter)AssetImporter.GetAtPath(RoundedRectSpritePath);
            spriteImporter.spriteBorder = new Vector4(radius, radius, radius, radius);
            EditorUtility.SetDirty(spriteImporter);
            spriteImporter.SaveAndReimport();
        }
    }
}
