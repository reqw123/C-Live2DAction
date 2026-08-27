using System.IO;
using UnityEditor;
using UnityEngine;
using Live2DAction.VFX;

namespace Live2DAction.EditorTools
{
    // 2026-08-24, explicit user request ("將我提供的三段攻擊特效影片...製作成可用於 3D 動作遊戲的 2.5D
    // VFX 技能特效") - builds 3 standalone, reusable Slash VFX prefabs from the sprite sheets
    // already baked out of the user's source video (see Assets/_Project/VFX/Slash/Textures/ -
    // extracted+alpha-keyed+packed via an external Python pass, since that bulk per-pixel work
    // over 44 HD frames is far more practical there than in an Editor script; the source video
    // itself is kept at Assets/_Project/VFX/Slash/Source/SlashSourceVideo.mp4 for reference/
    // reprocessing, per "不要直接使用 VideoPlayer 播放 MP4" - it is NEVER played at runtime,
    // purely an archived source asset).
    //
    // Each prefab = one flipbook ParticleSystem (SlashFlipbookURP.shader, Texture Sheet Animation
    // module driving the per-frame UV - no in-shader tiling math needed) + three small child
    // ParticleSystems (spark burst / smoke puff / glow pulse) for "增加立體感,不要只是一張平面動畫"
    // + a SlashVfxController that applies every tunable field and self-destructs when done. A
    // dedicated Trail sub-effect was deliberately left out - the flipbook art itself already
    // depicts the streaking trail motion, and stacking a literal TrailRenderer/Trails module on
    // top of a single-particle flipbook quad reads as a redundant duplicate ghost, not genuine
    // extra depth.
    //
    // Deliberately does NOT touch PlayerCombat/ComboAttackState/EnemyAI or any existing Animator
    // Controller/attack clip - per "避免修改既有戰鬥系統,只建立獨立、模組化且可重複使用的 VFX 系統"
    // this only ever creates new, independent assets. SlashVfxSpawner (a separate new component,
    // also untouched-by-default) is the intended hookup point for Animation Events - see that
    // class's own comment.
    //
    // Render pipeline check (done before writing a single line of shader/asset code): this
    // project has Universal Render Pipeline 17.0.4 installed and NO com.unity.shadergraph package
    // (confirmed via manage_packages.list_packages) - see SlashFlipbookURP.shader's own comment
    // for why a hand-written URP HLSL shader was used instead of the literally-requested Shader
    // Graph (no tool here can author a .shadergraph node file, and hand-typing that JSON blind
    // with no way to open/verify it in the actual graph editor risks a broken/pink asset).
    internal static class SlashVfxSetup
    {
        private const string TextureFolder = "Assets/_Project/VFX/Slash/Textures/";
        private const string MaterialFolder = "Assets/_Project/VFX/Slash/Materials/";
        private const string PrefabFolder = "Assets/_Project/VFX/Slash/Prefabs/";
        private const string ShaderName = "Live2DAction/VFX/SlashFlipbook";
        private const string SoftDotSpritePath = TextureFolder + "SoftDot.png";

        private const float AdditiveSrc = 1f; // UnityEngine.Rendering.BlendMode.One
        private const float AdditiveDst = 1f;
        // 2026-08-24, real bug found via in-Play-Mode screenshot - SlashFlipbookURP.shader's
        // Frag() now premultiplies RGB by alpha (see that shader's own comment for why: Additive
        // blend ignores the alpha channel entirely, so a flat-RGB/alpha-only texture like
        // SoftDot.png rendered as a solid box under Blend One One). Once RGB is premultiplied,
        // the correct "Alpha Blend" formula is One/OneMinusSrcAlpha, NOT the textbook SrcAlpha/
        // OneMinusSrcAlpha - using SrcAlpha here on already-premultiplied RGB would double-apply
        // alpha and darken/wrongly-fade the edges.
        private const float AlphaSrc = 1f; // UnityEngine.Rendering.BlendMode.One (premultiplied)
        private const float AlphaDst = 10f; // UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha

        private struct AttackSpec
        {
            public string Name;
            public int Columns;
            public int Rows;
            public int FrameCount;
            public float Duration;
            public float StartSize;
        }

        // Frame counts/grids match exactly what pack_sheets.py baked (see that script's own
        // ATTACKS dict) - 8/12/14 real frames padded to 4x2/4x3/4x4 grids. Durations are a
        // reasonable first pass for "quick swipe -> bright cross slash -> heavy finisher burst"
        // pacing, adjustable per-instance via SlashVfxController.lifetimeSecondsOverride/
        // playbackSpeed without needing to rebuild the prefab.
        private static readonly AttackSpec[] Specs =
        {
            new AttackSpec { Name = "Attack01", Columns = 4, Rows = 2, FrameCount = 8, Duration = 0.32f, StartSize = 3.0f },
            new AttackSpec { Name = "Attack02", Columns = 4, Rows = 3, FrameCount = 12, Duration = 0.42f, StartSize = 3.5f },
            new AttackSpec { Name = "Attack03", Columns = 4, Rows = 4, FrameCount = 14, Duration = 0.60f, StartSize = 4.2f },
        };

        [MenuItem("Tools/Live2DAction/Add Slash VFX Prefabs")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Exit Play Mode before running this - prefab creation touches the AssetDatabase.");
                return;
            }

            EnsureFolder(MaterialFolder);
            EnsureFolder(PrefabFolder);

            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError("Shader not found: " + ShaderName + " - SlashFlipbookURP.shader may still be compiling.");
                return;
            }

            ConfigureSpriteSheetImports();
            EnsureSoftDotSprite();

            Material additiveDotMaterial = EnsureSimpleMaterial("SoftDotAdditive", shader, SoftDotSpritePath, AdditiveSrc, AdditiveDst);
            Material alphaDotMaterial = EnsureSimpleMaterial("SoftDotAlpha", shader, SoftDotSpritePath, AlphaSrc, AlphaDst);
            Mesh quadMesh = GetPrimitiveQuadMesh();

            foreach (AttackSpec spec in Specs)
            {
                string texturePath = TextureFolder + spec.Name + "_SpriteSheet.png";
                Material flowMaterial = EnsureFlipbookMaterial(spec.Name, shader, texturePath);
                GameObject prefabRoot = BuildAttackGameObject(spec, flowMaterial, additiveDotMaterial, alphaDotMaterial, quadMesh);

                string prefabPath = PrefabFolder + spec.Name + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath, out bool success);
                if (!success)
                {
                    Debug.LogError("Failed to save prefab at " + prefabPath);
                }
                Object.DestroyImmediate(prefabRoot);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Built Attack01/02/03 Slash VFX prefabs under " + PrefabFolder);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path.TrimEnd('/')))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path.TrimEnd('/'))?.Replace('\\', '/');
            string leaf = Path.GetFileName(path.TrimEnd('/'));
            if (parent != null && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent + "/");
            }
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static void ConfigureSpriteSheetImports()
        {
            foreach (AttackSpec spec in Specs)
            {
                string path = TextureFolder + spec.Name + "_SpriteSheet.png";
                ConfigureImport(path);
            }
        }

        private static void ConfigureImport(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
            {
                Debug.LogError("Texture not found at " + path);
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            // Clamp, not Repeat/Wrap - Texture Sheet Animation reads one exact tile rect per
            // frame; any wrap bleed at a tile's edge would smear the NEXT frame's pixels in.
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        // A single small soft-edged white dot (Gaussian falloff, same baked-alpha convention as
        // every other procedural UI/VFX sprite in this project - CheckpointGate's ring, the
        // health bar's rounded-rect) - reused, tinted per-particle via Start Color, for the
        // spark/smoke/glow child systems. Only generated once; re-running this tool later won't
        // clobber any hand-tweaks made to the file.
        private static void EnsureSoftDotSprite()
        {
            string fullPath = Path.Combine(Application.dataPath, "..", SoftDotSpritePath);
            if (File.Exists(fullPath))
            {
                ConfigureImport(SoftDotSpritePath);
                return;
            }

            const int size = 128;
            const float sigma = 0.24f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            float half = size / 2f;

            // 2026-08-24, real bug found via in-Play-Mode screenshot ("受傷回饋") - a pure
            // Gaussian never mathematically reaches exactly 0, so at large particle scale
            // (Smoke/Glow) the texture's true corner pixels (alpha ~0.01-0.03) composited as a
            // faint but visible rectangular quad edge against a flat-colored wall background.
            // Remapped through InverseLerp+clamp so alpha hard-zeroes well before the texture's
            // own edge instead of trailing off asymptotically.
            const float zeroAt = 0.05f;
            const float fullAt = 0.55f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float d2 = dx * dx + dy * dy;
                    float gaussian = Mathf.Exp(-d2 / (2f * sigma * sigma));
                    float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(zeroAt, fullAt, gaussian));
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

            ConfigureImport(SoftDotSpritePath);
        }

        private static Material EnsureFlipbookMaterial(string attackName, Shader shader, string texturePath)
        {
            string path = MaterialFolder + attackName + "Mat.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            mat.SetTexture("_MainTex", tex);
            // Energy-slash look defaults to Additive - see class comment for the runtime
            // SrcBlend/DstBlend mechanism (no ShaderGUI, so these are plain floats).
            mat.SetFloat("_SrcBlend", AdditiveSrc);
            mat.SetFloat("_DstBlend", AdditiveDst);
            mat.SetFloat("_Brightness", 1.5f);
            mat.SetFloat("_Opacity", 1f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material EnsureSimpleMaterial(string name, Shader shader, string texturePath, float srcBlend, float dstBlend)
        {
            string path = MaterialFolder + name + "Mat.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            mat.SetTexture("_MainTex", tex);
            mat.SetFloat("_SrcBlend", srcBlend);
            mat.SetFloat("_DstBlend", dstBlend);
            mat.SetFloat("_Brightness", 1.5f);
            mat.SetFloat("_Opacity", 1f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // Extracted from Unity's own built-in Quad primitive (confirmed: normal=(0,0,-1), 1x1,
        // centered) rather than a hardcoded "Quad.fbx" built-in-resource path string, so this
        // doesn't depend on that exact resource name staying stable - the primitive itself is
        // guaranteed to exist in any Unity version. Facing -Z locally is what makes
        // SlashVfxSpawner's Quaternion.LookRotation(origin.forward, origin.up) place the quad's
        // visible face back toward a camera positioned behind the character looking along
        // +forward (the standard third-person arrangement this project's own
        // ThirdPersonCameraController uses) - see SlashVfxSpawner's own comment.
        private static Mesh GetPrimitiveQuadMesh()
        {
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temp);
            return mesh;
        }

        private static GameObject BuildAttackGameObject(AttackSpec spec, Material flowMaterial, Material additiveDotMaterial, Material alphaDotMaterial, Mesh quadMesh)
        {
            var root = new GameObject(spec.Name + "_SlashVfx");

            ParticleSystem mainSystem = root.AddComponent<ParticleSystem>();
            ConfigureMainFlipbookSystem(mainSystem, spec, flowMaterial, quadMesh);

            BuildSparkBurst(root.transform, spec, additiveDotMaterial);
            BuildSmokePuff(root.transform, spec, alphaDotMaterial);
            BuildGlowPulse(root.transform, spec, additiveDotMaterial);

            SlashVfxController controller = root.AddComponent<SlashVfxController>();
            var so = new SerializedObject(controller);
            so.FindProperty("playbackSpeed").floatValue = 1f;
            so.FindProperty("sizeMultiplier").floatValue = 1f;
            so.FindProperty("lifetimeSecondsOverride").floatValue = 0f;
            so.FindProperty("opacity").floatValue = 1f;
            so.FindProperty("brightness").floatValue = 1.5f;
            so.FindProperty("billboardToCamera").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        private static void ConfigureMainFlipbookSystem(ParticleSystem system, AttackSpec spec, Material material, Mesh quadMesh)
        {
            ParticleSystem.MainModule main = system.main;
            main.duration = spec.Duration;
            main.loop = false;
            main.startLifetime = spec.Duration;
            main.startSpeed = 0f;
            main.startSize = spec.StartSize;
            main.startRotation = 0f;
            main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 1;
            main.playOnAwake = true;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            ParticleSystem.TextureSheetAnimationModule sheet = system.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = spec.Columns;
            sheet.numTilesY = spec.Rows;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.cycleCount = 1;
            // Stops exactly at the last REAL frame instead of continuing into the grid's blank
            // padding cells (Attack03 is 14 real frames in a 4x4=16-cell grid) - see
            // pack_sheets.py's own ATTACKS dict for where these frame counts come from.
            float endFraction = spec.FrameCount / (float)(spec.Columns * spec.Rows);
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, endFraction));

            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = quadMesh;
            renderer.sharedMaterial = material;
            renderer.alignment = ParticleSystemRenderSpace.Local;
        }

        // "增加立體感" - a brief radial burst of small bright dots at the moment the slash
        // appears, on top of the flat flipbook art.
        private static void BuildSparkBurst(Transform parent, AttackSpec spec, Material material)
        {
            var go = new GameObject("Sparks");
            go.transform.SetParent(parent, false);
            ParticleSystem system = go.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = system.main;
            main.duration = spec.Duration;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, spec.StartSize * 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.3f), new Color(1f, 0.4f, 0.1f));
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 32;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = spec.StartSize * 0.12f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(FadeOutGradient());

            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
        }

        // A few soft, slow-growing translucent puffs so the impact reads with a little volume/
        // smoke instead of being purely a flat glowing line.
        private static void BuildSmokePuff(Transform parent, AttackSpec spec, Material material)
        {
            var go = new GameObject("Smoke");
            go.transform.SetParent(parent, false);
            ParticleSystem system = go.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = system.main;
            main.duration = spec.Duration;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(spec.Duration * 1.3f, spec.Duration * 1.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(spec.StartSize * 0.3f, spec.StartSize * 0.5f);
            main.startColor = new Color(0.6f, 0.15f, 0.1f, 0.35f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 12;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 6) });

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = spec.StartSize * 0.15f;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(FadeOutGradient());

            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
        }

        // A single large soft additive pulse behind the flipbook art, so the whole slash reads
        // as genuinely emitting light rather than being a flat cutout pasted in space.
        private static void BuildGlowPulse(Transform parent, AttackSpec spec, Material material)
        {
            var go = new GameObject("Glow");
            go.transform.SetParent(parent, false);
            ParticleSystem system = go.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = system.main;
            main.duration = spec.Duration;
            main.loop = false;
            main.startLifetime = spec.Duration * 0.7f;
            main.startSpeed = 0f;
            main.startSize = spec.StartSize * 0.85f;
            main.startColor = new Color(1f, 0.35f, 0.15f, 0.5f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 1;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            var alphaCurve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.15f, 1f),
                new Keyframe(1f, 0f));
            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
        }

        private static Gradient FadeOutGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }
    }
}
