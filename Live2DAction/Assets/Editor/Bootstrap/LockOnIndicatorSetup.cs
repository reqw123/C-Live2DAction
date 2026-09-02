using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Live2DAction.Targeting;
using Live2DAction.UI;

namespace Live2DAction.EditorTools
{
    // 2026-08-23, explicit user request ("玩家在使用滑鼠滾輪鎖定敵人是 必須能視覺上判定成功鎖定到了
    // 誰") - adds ONE world-space lock-on ring indicator to the scene (not one per potential
    // target - see LockOnIndicator's own comment for why a single dynamically-repositioned
    // indicator is the right shape here), wired to Player's TargetLockController.
    //
    // Sprite is procedurally generated (baked pixel math, not hand-authored art) - same
    // "not hand-authored" precedent as ExecutionRing.png/InvulnerabilityRipple.png. Golden-yellow,
    // deliberately distinct from the cyan invulnerability ripple and the red execution-ready ring
    // so a glance can tell all three apart during combat.
    internal static class LockOnIndicatorSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string RingSpritePath = "Assets/_Project/UI/Textures/LockOnRing.png";
        // 2026-08-28, explicit user request ("某些視角角度會讓此圓圈消失") - draw the ring over all
        // scene geometry so a low camera angle / the target's own body between it and the camera
        // can't occlude it. Material baked here from the always-on-top UI shader.
        private const string RingMaterialPath = "Assets/_Project/VFX/Materials/UILockOnRing.mat";
        private const string RingShaderName = "Live2DAction/UIAlwaysOnTop";

        private static readonly Vector2 RingSize = new Vector2(0.35f, 0.35f);

        [MenuItem("Tools/Live2DAction/Add Lock-On Indicator")]
        public static void Apply()
        {
            EnsureRingSprite();
            Material ringMaterial = EnsureRingMaterial();

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("Player GameObject not found in " + ScenePath);
                return;
            }

            TargetLockController lockController = player.GetComponent<TargetLockController>();
            if (lockController == null)
            {
                Debug.LogError("Player has no TargetLockController - cannot wire the lock-on indicator to it.");
                return;
            }

            Sprite ringSprite = AssetDatabase.LoadAssetAtPath<Sprite>(RingSpritePath);
            if (ringSprite == null)
            {
                Debug.LogError("Lock-on ring sprite not found at " + RingSpritePath + " after generation.");
                return;
            }

            LockOnIndicator existing = Object.FindFirstObjectByType<LockOnIndicator>();
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            var canvasGo = new GameObject("LockOnIndicatorCanvas");
            // Not parented under Player - LockOnIndicator's own LateUpdate sets world
            // position/rotation directly every frame from whatever is currently locked, so a
            // parent transform would only add a confusing extra layer of relative offsets for no
            // benefit (same reasoning ExecutionReadyIndicator's chest-bone tracking avoided by
            // reading positionAnchor.position directly rather than relying on parent inheritance).
            canvasGo.transform.localScale = Vector3.one;

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.sizeDelta = RingSize;

            Image ring = CreateCenteredImage(canvasGo.transform, "Ring", ringSprite, RingSize);
            ring.color = new Color(1f, 1f, 1f, 0f); // starts invisible - LateUpdate drives alpha
            if (ringMaterial != null)
            {
                ring.material = ringMaterial; // ZTest Always - never occluded by scene geometry
            }

            LockOnIndicator indicator = canvasGo.AddComponent<LockOnIndicator>();
            var so = new SerializedObject(indicator);
            so.FindProperty("lockOnSource").objectReferenceValue = lockController;
            so.FindProperty("ringImage").objectReferenceValue = ring;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Added lock-on indicator, wired to Player's TargetLockController.");
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
            image.color = Color.white; // sprite already bakes the golden color in
            image.raycastTarget = false;
            // Same tight-sprite-mesh fix as ExecutionIndicatorSetup/InvulnerabilityRippleSetup -
            // avoids the compressed-corner faint-square-outline bug those both hit first.
            image.useSpriteMesh = true;
            return image;
        }

        private static Material EnsureRingMaterial()
        {
            Shader shader = Shader.Find(RingShaderName);
            if (shader == null)
            {
                Debug.LogError("Shader '" + RingShaderName + "' not found - is UIAlwaysOnTop.shader in the project? Ring will use the default UI material and can still be occluded.");
                return null;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(RingMaterialPath);
            if (material == null)
            {
                string dir = Path.GetDirectoryName(RingMaterialPath);
                if (dir != null && !AssetDatabase.IsValidFolder(dir))
                {
                    Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", dir));
                    AssetDatabase.Refresh();
                }
                material = new Material(shader) { name = "UILockOnRing" };
                AssetDatabase.CreateAsset(material, RingMaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }
            return material;
        }

        // Same core+glow Gaussian ring bake as InvulnerabilityRippleSetup.EnsureRingSprite, just a
        // different baked color (golden-yellow, for "target locked" instead of that one's
        // cyan "shielded").
        private static void EnsureRingSprite()
        {
            string fullPath = Path.Combine(Application.dataPath, "..", RingSpritePath);

            const int size = 256;
            const float ringRadius = 0.40f;
            const float coreSigma = 0.05f;
            const float glowSigma = 0.20f;
            var ringColor = new Color(1f, 0.82f, 0.15f);

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            float half = size / 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);

                    float core = Mathf.Exp(-Mathf.Pow(d - ringRadius, 2f) / (2f * coreSigma * coreSigma));
                    float glow = Mathf.Exp(-Mathf.Pow(d - ringRadius, 2f) / (2f * glowSigma * glowSigma)) * 0.7f;
                    float a = Mathf.Clamp01(core + glow);

                    pixels[y * size + x] = new Color(ringColor.r, ringColor.g, ringColor.b, a);
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

            AssetDatabase.ImportAsset(RingSpritePath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(RingSpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = 100f;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }
    }
}
