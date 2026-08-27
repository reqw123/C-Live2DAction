using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Live2DAction.World;

namespace Live2DAction.EditorTools
{
    // 2026-08-20, explicit user request ("本地周圍有一道看不到的牆碰撞體，這是對的，但能不能在任何
    // 角色碰撞此牆時設計一個被阻擋的特效，像是原神玩家不小心走到的未開放地圖邊界一樣") - "本地" is
    // the user's own chosen name for the main ground spawn/rest area (GreyboxSceneBuilder's
    // BoundaryWall_North/South/East/West). The walls themselves are explicitly correct and
    // unchanged - this only adds a "blocked" visual on touch: a world-space icy shimmer at the
    // contact point (BoundaryBlockEffect, fires for ANY character) plus a Genshin-style
    // screen-edge vignette pulse (BoundaryBlockHud, player-only - see that class's own comment
    // for why).
    //
    // Exposes EnsureHud/AddBlockEffectToWall as public statics rather than only a menu-item
    // Apply() so GreyboxSceneBuilder's own CreateBoundaryWalls can call straight into this same
    // logic when rebuilding the scene from scratch - same "keep the live-scene fix and the
    // rebuilder in sync" convention this project already follows for every other one-off
    // bootstrap fix.
    internal static class BoundaryWallBlockEffectSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string VignetteSpritePath = "Assets/_Project/UI/Textures/BoundaryVignette.png";
        private const string RippleMaterialPath = "Assets/_Project/VFX/BoundaryRipple.mat";

        private static readonly string[] WallNames =
        {
            "BoundaryWall_North", "BoundaryWall_South", "BoundaryWall_East", "BoundaryWall_West",
        };

        // Padding added on top of each wall's own solid BoxCollider size for the new detection-
        // only trigger - thin enough that the visual cue reads as "you just touched it", not
        // "the whole room flashes from ten feet away".
        private const float TriggerPadding = 0.6f;

        [MenuItem("Tools/Live2DAction/Add Boundary Wall Block Effect")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            EnsureHud();

            foreach (string name in WallNames)
            {
                GameObject wall = GameObject.Find(name);
                if (wall == null)
                {
                    Debug.LogError(name + " not found in " + ScenePath + " - run Build Greybox Test Scene first.");
                    continue;
                }

                AddBlockEffectToWall(wall);
            }

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Added boundary-block touch effect (world-space ripple + screen vignette) to all four ground map boundary walls.");
        }

        // Idempotent - safe to call every time the scene is rebuilt (GreyboxSceneBuilder) or the
        // menu item is re-run by hand, without piling up duplicate HUD Canvases.
        public static void EnsureHud()
        {
            if (BoundaryBlockHud.Instance != null || GameObject.Find("BoundaryBlockHud") != null)
            {
                return;
            }

            Sprite vignette = EnsureVignetteSprite();

            var canvasGo = new GameObject("BoundaryBlockHud");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();

            var imageGo = new GameObject("Vignette");
            imageGo.transform.SetParent(canvasGo.transform, false);
            RectTransform rect = imageGo.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = imageGo.AddComponent<Image>();
            image.sprite = vignette;
            image.type = Image.Type.Simple;
            image.raycastTarget = false;
            image.color = new Color(0.65f, 0.9f, 1f, 0f); // starts fully transparent - BoundaryBlockHud drives alpha

            BoundaryBlockHud hud = canvasGo.AddComponent<BoundaryBlockHud>();
            var so = new SerializedObject(hud);
            so.FindProperty("vignetteImage").objectReferenceValue = image;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Adds the padded detection trigger + world-space ripple ParticleSystem +
        // BoundaryBlockEffect to one already-existing solid boundary wall. Safe to call twice
        // (skips if already wired) so re-running the menu item or rebuilding the scene never
        // stacks duplicate components on the same wall.
        public static void AddBlockEffectToWall(GameObject wall)
        {
            if (wall.GetComponent<BoundaryBlockEffect>() != null)
            {
                return;
            }

            BoxCollider solid = wall.GetComponent<BoxCollider>();
            if (solid == null)
            {
                Debug.LogError(wall.name + " has no BoxCollider to pad a detection trigger from.");
                return;
            }

            BoxCollider trigger = wall.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = solid.center;
            trigger.size = solid.size + Vector3.one * (TriggerPadding * 2f);

            // 2026-08-22, real playtested bug ("我在圍牆內自由行走穿梭後 牆體會變形") - the
            // ParticleSystem used to live directly on the wall's own GameObject, so
            // BoundaryBlockEffect repositioning/rotating "the ripple's transform" every touch was
            // actually moving the wall itself (mesh + BoxColliders, all on that same Transform).
            // A dedicated child GameObject gives the ripple its own Transform to be shoved around
            // without ever touching the wall's.
            var rippleGo = new GameObject("RippleEmitter");
            rippleGo.transform.SetParent(wall.transform, false);
            ParticleSystem ps = rippleGo.AddComponent<ParticleSystem>();
            ConfigureRipple(ps);

            BoundaryBlockEffect effect = wall.AddComponent<BoundaryBlockEffect>();
            var so = new SerializedObject(effect);
            so.FindProperty("ripple").objectReferenceValue = ps;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Icy blue-white shimmer burst, world-space so it stays put at the contact point
        // BoundaryBlockEffect repositions it to each time rather than following the wall's own
        // (arbitrary, off-surface) pivot - same "burst particle system, no external art asset"
        // convention as HitEffectSetup, recolored to match this project's existing
        // magic/portal palette (Portal's own particle startColor is this exact blue-white).
        private static void ConfigureRipple(ParticleSystem ps)
        {
            ParticleSystem.MainModule main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = 0.4f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
            main.startColor = new Color(0.6f, 0.85f, 1f, 0.9f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 20, 26) });

            // A flat circle facing the emitter's own forward (BoundaryBlockEffect points that
            // outward, away from the map center, before each Play()) reads as a barrier surface
            // pushing back at you, rather than an omnidirectional puff.
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.5f;
            shape.radiusThickness = 1f;
            shape.rotation = new Vector3(-90f, 0f, 0f); // Circle shape's own normal is local +Y by default - tip it to face local +Z (forward) instead

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;

            ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = EnsureRippleMaterial();
        }

        private static Material EnsureRippleMaterial()
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(RippleMaterialPath);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                Debug.LogError("Could not find Universal Render Pipeline/Particles/Unlit shader.");
                return null;
            }

            var material = new Material(shader);
            material.SetFloat("_Surface", 1f); // Transparent
            material.SetFloat("_Blend", 1f); // Additive
            material.SetColor("_BaseColor", Color.white);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            EnsureFolder("Assets/_Project/VFX");
            AssetDatabase.CreateAsset(material, RippleMaterialPath);
            return material;
        }

        // Baked once, same "Gaussian-ish falloff written straight into a Texture2D" idiom as
        // SkyIslandTimeTrialSetup's own EnsureRingSprite, just inverted (bright at the screen's
        // edges/corners, fully transparent in the middle) so it reads as a boundary vignette
        // rather than a target ring.
        private static Sprite EnsureVignetteSprite()
        {
            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(VignetteSpritePath);
            if (existing != null)
            {
                return existing;
            }

            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            float half = size / 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - half) / half;
                    float dy = Mathf.Abs(y + 0.5f - half) / half;
                    float edgeDistance = Mathf.Max(dx, dy); // 0 at center, 1 at the nearest edge
                    float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, edgeDistance));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            byte[] png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);

            string fullPath = Path.Combine(Application.dataPath, "..", VignetteSpritePath);
            EnsureFolder(Path.GetDirectoryName(VignetteSpritePath).Replace('\\', '/'));
            File.WriteAllBytes(fullPath, png);
            AssetDatabase.ImportAsset(VignetteSpritePath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(VignetteSpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(VignetteSpritePath);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folderName = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
