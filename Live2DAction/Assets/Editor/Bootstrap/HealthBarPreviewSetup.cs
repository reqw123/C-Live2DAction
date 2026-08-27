using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Live2DAction.EditorTools
{
    // 2026-08-24, explicit user request ("接下來你把這個血量ui 用放大版呈現在畫面中間 ,單純視覺測試
    // 之後會刪除") - throwaway visual-inspection tool, NOT part of the real HUD. Clones the
    // already-built PlayerCornerHud/Panel/生命Track row (all 6 baked art layers + the live
    // PlayerHealthBarFx component, wired exactly as PlayerHealthBarFxSetup left it) under its own
    // screen-centered canvas and scales the clone up - Instantiate carries over every sprite/
    // material reference and internal cross-reference (sparkRects etc) automatically, so this is
    // guaranteed to look pixel-identical to the real corner HUD, just bigger. The clone's `health`
    // field still points at the real Player, so it mirrors live HP changes same as the corner one.
    //
    // Everything this creates lives under one single "HealthBarPreview_TEMP" root - delete that
    // GameObject (or re-run RemovePreview) when done, nothing else in the scene is touched.
    internal static class HealthBarPreviewSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string RootName = "HealthBarPreview_TEMP";
        private const float PreviewScale = 4.5f;

        [MenuItem("Tools/Live2DAction/Add Health Bar Preview (Temp)")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Exit Play Mode before running this - EditorSceneManager.OpenScene throws mid-Play.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject hudGo = GameObject.Find("PlayerCornerHud");
            Transform sourceRow = hudGo != null ? hudGo.transform.Find("Panel/生命Track") : null;
            if (sourceRow == null)
            {
                Debug.LogError("PlayerCornerHud/Panel/生命Track not found - run 'Add Player Health Bar FX' first.");
                return;
            }

            RemovePreviewInternal();

            var canvasGo = new GameObject(RootName);
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Draws above the corner HUD's own canvas so the enlarged preview is never hidden
            // behind it.
            canvas.overrideSorting = true;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();

            GameObject clone = Object.Instantiate(sourceRow.gameObject, canvasGo.transform);
            clone.name = "生命Track_Preview";
            RectTransform cloneRect = clone.GetComponent<RectTransform>();
            cloneRect.anchorMin = new Vector2(0.5f, 0.5f);
            cloneRect.anchorMax = new Vector2(0.5f, 0.5f);
            cloneRect.pivot = new Vector2(0.5f, 0.5f);
            cloneRect.anchoredPosition = Vector2.zero;
            Vector2 unscaledRowSize = cloneRect.sizeDelta;
            cloneRect.localScale = Vector3.one * PreviewScale;

            // 2026-08-24, real bug report ("500/500 字體是模糊的") - Text rasterizes its glyph
            // atlas from its OWN fontSize/rect size, then whatever bitmap that produces gets
            // stretched by any ANCESTOR transform.localScale - since the whole row above is
            // scaled PreviewScale x, the tiny fontSize=13 glyphs baked for the real HUD were
            // being blown up post-rasterization instead of rasterized crisp in the first place.
            // Fixed by detaching Value from the scaled hierarchy entirely (reparented straight
            // under the unscaled canvas) and resizing/refonting it directly in absolute canvas
            // units instead, so it rasterizes crisp at its actual on-screen size.
            Transform valueTransform = clone.transform.Find("Value");
            if (valueTransform != null)
            {
                Text valueText = valueTransform.GetComponent<Text>();
                valueTransform.SetParent(canvasGo.transform, false);
                RectTransform valueRect = valueTransform.GetComponent<RectTransform>();
                valueRect.anchorMin = new Vector2(0.5f, 0.5f);
                valueRect.anchorMax = new Vector2(0.5f, 0.5f);
                valueRect.pivot = new Vector2(0.5f, 0.5f);
                valueRect.anchoredPosition = Vector2.zero;
                valueRect.sizeDelta = unscaledRowSize * PreviewScale;
                valueRect.localScale = Vector3.one;
                if (valueText != null)
                {
                    valueText.fontSize = Mathf.RoundToInt(valueText.fontSize * PreviewScale);
                }
                valueTransform.SetAsLastSibling();
            }

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Added " + RootName + " - centered, " + PreviewScale + "x scale. Run 'Remove Health Bar Preview' when done.");
        }

        [MenuItem("Tools/Live2DAction/Remove Health Bar Preview (Temp)")]
        public static void RemovePreview()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Exit Play Mode before running this - EditorSceneManager.OpenScene throws mid-Play.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            bool removed = RemovePreviewInternal();
            if (removed)
            {
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                Debug.Log("Removed " + RootName + ".");
            }
            else
            {
                Debug.Log(RootName + " not present - nothing to remove.");
            }
        }

        private static bool RemovePreviewInternal()
        {
            GameObject existing = GameObject.Find(RootName);
            if (existing == null)
            {
                return false;
            }

            Object.DestroyImmediate(existing);
            return true;
        }
    }
}
