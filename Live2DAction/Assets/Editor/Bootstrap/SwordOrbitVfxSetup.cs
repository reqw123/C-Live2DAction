using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.Combat;
using Live2DAction.VFX;

namespace Live2DAction.EditorTools
{
    // 2026-08-30 (追加69), user: use `不要有人形.mp4` as the player's R ultimate CAST effect - a
    // swirling blue+orange energy orbiting a sword. 追加77 replaced it with the PlayerUltimateAura
    // flame, and 追加79 turned that flame into a persistent READY-STATE aura (UltimateReadyAura),
    // leaving the R cast with no VFX of its own but the UltimateActivationBurst shockwave.
    //
    // 2026-08-31 (追加81), user: "player 施展 r 技能原來的特效不見了 就是一把劍的旋轉砍擊(我不是說
    // 大劍)" - restore this. It is a CAST effect (spawned the instant R fires), separate from the
    // ready-state flame aura: the flame says "ultimate is charged", this plays when you actually
    // use it. Rebuilt from the same `不要有人形.mp4` (re-archived at Source/SwordOrbitSource.mp4).
    //
    // 透明通道: the source clip is opaque H.264 - NOT on pure black, on a baked-in grey
    // transparency checkerboard (AI-VFX-generator convention). Baked OFFLINE (ffmpeg) into
    // SwordOrbit_Atlas.png: 49 frames (source 33..177 every 3 - form -> sword+orbiting rings ->
    // dust), 8x7 grid of 320x180 cells (2560x1260). The luminance key threshold is pushed to 60
    // (higher than a black-background clip needs) to zero the light checker squares, which sit at
    // ~luma 55-70; range /165 + gamma 1.4 keeps the blue/orange ribbons and the ghost-sword.
    // A drawbox paints out the corner watermark. Reprocess (from Source/SwordOrbitSource.mp4):
    //   ffmpeg -y -i SwordOrbitSource.mp4 -vf "select='between(n,33,177)*not(mod(n-33\,3))',\
    //     setpts=N/(24*TB),drawbox=x=1148:y=548:w=132:h=132:color=black@1:t=fill,\
    //     scale=320:180,tile=8x7,format=rgba,\
    //     geq=r='r(X,Y)':g='g(X,Y)':b='b(X,Y)':\
    //     a='255*pow(clip((max(max(r(X,Y),g(X,Y)),b(X,Y))-60)/165,0,1),1.4)'" \
    //     -frames:v 1 ../SwordOrbit_Atlas.png
    //
    // 仿3d (fake-3D / 2.5D): a billboard flipbook quad (SlashFlipbookURP.shader, premultiplied-
    // alpha blend so the white-hot ribbon cores don't blow out) + two small 3D child particle
    // systems (a spark burst and a centre glow pulse) so the swirl reads with volume, not as a
    // flat card. SlashVfxController on the root self-destructs it once it (and any audio) finish.
    //
    // Re-runnable: rebuilds material + prefab and re-wires UltimateAbility.castVfxPrefab each run.
    internal static class SwordOrbitVfxSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string Folder = "Assets/_Project/VFX/Skills/SwordOrbit/";
        private const string AtlasPath = Folder + "SwordOrbit_Atlas.png";
        private const string MaterialPath = Folder + "SwordOrbitFlipbook.mat";
        private const string PrefabPath = Folder + "SwordOrbitSkillVFX.prefab";
        private const string FlipbookShader = "Live2DAction/VFX/SlashFlipbook";
        private const string SoftDotAdditiveMat = "Assets/_Project/VFX/Slash/Materials/SoftDotAdditiveMat.mat";

        // Grid + real-frame count baked by the ffmpeg pass (8*7 = 56 cells, 49 real frames).
        private const int TilesX = 8;
        private const int TilesY = 7;
        private const int RealFrames = 49;

        // The whole arc (form -> sword + orbiting rings -> dust) played once over ~1.6 s.
        private const float Lifetime = 1.6f;

        // Source cell is 16:9. The energy orbit is roughly as wide as it is tall; sized a touch
        // taller than the player so the rings sweep around the torso as the weapon spins up.
        private const float SizeHeight = 3.0f;
        private const float SizeWidth = SizeHeight * (320f / 180f);

        // Where the swirl centres on the player - mid-torso (player root is ~1.08 world, so local
        // +0.4 ≈ chest), where the weapon spins in place before it flies. UltimateAbility spawns
        // the prefab here, parented to the player.
        private static readonly Vector3 CastLocalOffset = new Vector3(0f, 0.4f, 0f);

        [MenuItem("Tools/Live2DAction/Add Sword Orbit Skill VFX (R ultimate cast)")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Exit Play Mode first - this touches the AssetDatabase and the scene.");
                return;
            }

            AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath) == null)
            {
                Debug.LogError("SwordOrbitVfxSetup: atlas not found at " + AtlasPath +
                               " - the ffmpeg bake step must run first (recipe in this file's header).");
                return;
            }

            Shader shader = Shader.Find(FlipbookShader);
            if (shader == null)
            {
                Debug.LogError("Shader not found: " + FlipbookShader + " (still compiling?).");
                return;
            }

            ConfigureAtlasImport();
            Material material = CreateOrUpdateMaterial(shader);
            GameObject prefab = CreateOrUpdatePrefab(material);

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Wire(prefab);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("SwordOrbitVfxSetup: built " + PrefabPath +
                      " and wired it as UltimateAbility.castVfxPrefab (plays when R fires).");
        }

        private static void ConfigureAtlasImport()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
            if (importer == null) return;

            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None; // keep exact 2560x1260
            importer.maxTextureSize = 4096;
            importer.SaveAndReimport();
        }

        private static Material CreateOrUpdateMaterial(Shader shader)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            bool isNew = mat == null;
            if (isNew) mat = new Material(shader);
            else if (mat.shader != shader) mat.shader = shader;

            mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath));
            mat.SetColor("_Color", Color.white); // atlas already carries the blue/orange
            mat.SetFloat("_SrcBlend", 1f);   // One (the shader premultiplies rgb by a)
            mat.SetFloat("_DstBlend", 10f);  // OneMinusSrcAlpha
            mat.SetFloat("_Brightness", 1.7f); // ribbon cores blow past 1.0 to catch URP Bloom
            mat.SetFloat("_Opacity", 1f);
            mat.SetFloat("_ZTest", 4f);       // normal depth - a cast effect, not a body-wrap aura
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            if (isNew) AssetDatabase.CreateAsset(mat, MaterialPath);
            else EditorUtility.SetDirty(mat);
            return mat;
        }

        private static GameObject CreateOrUpdatePrefab(Material material)
        {
            bool isNew = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null;
            GameObject root = isNew ? new GameObject("SwordOrbitSkillVFX") : PrefabUtility.LoadPrefabContents(PrefabPath);

            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            }

            ConfigureMainFlipbook(EnsureComponent<ParticleSystem>(root), material);
            BuildSparkBurst(root.transform);
            BuildGlowPulse(root.transform);

            var controller = EnsureComponent<SlashVfxController>(root);
            var so = new SerializedObject(controller);
            so.FindProperty("playbackSpeed").floatValue = 1f;
            so.FindProperty("sizeMultiplier").floatValue = 1f;
            so.FindProperty("lifetimeSecondsOverride").floatValue = 0f;
            so.FindProperty("opacity").floatValue = 1f;
            so.FindProperty("brightness").floatValue = 1.7f;
            so.FindProperty("billboardToCamera").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            if (isNew) Object.DestroyImmediate(root);
            else PrefabUtility.UnloadPrefabContents(root);
            return saved;
        }

        private static void ConfigureMainFlipbook(ParticleSystem ps, Material material)
        {
            ParticleSystem.MainModule main = ps.main;
            main.duration = Lifetime;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = Lifetime;
            main.startSpeed = 0f;
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(SizeWidth);
            main.startSizeY = new ParticleSystem.MinMaxCurve(SizeHeight);
            main.startSizeZ = new ParticleSystem.MinMaxCurve(1f);
            main.startRotation = 0f;
            main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 1;
            main.stopAction = ParticleSystemStopAction.None;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

            var shapeOff = ps.shape; shapeOff.enabled = false;

            ParticleSystem.TextureSheetAnimationModule sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = TilesX;
            sheet.numTilesY = TilesY;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.cycleCount = 1;
            float endFraction = RealFrames / (float)(TilesX * TilesY);
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, endFraction));

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sharedMaterial = material;
            renderer.sortMode = ParticleSystemSortMode.None;
        }

        // 仿3d - a burst of blue-white sparks at the cast moment.
        private static void BuildSparkBurst(Transform parent)
        {
            var go = new GameObject("Sparks");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = ps.main;
            main.duration = Lifetime;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.11f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.5f, 0.75f, 1f), new Color(1f, 0.7f, 0.35f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;
            main.gravityModifier = 0.15f;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0.0f, 30),
                new ParticleSystem.Burst(0.28f, 16),
            });

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.4f;

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(FadeOut());

            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(SoftDotAdditiveMat);
        }

        // 仿3d - a pale volume pulse at the centre so the swirl reads as orbiting a core.
        private static void BuildGlowPulse(Transform parent)
        {
            var go = new GameObject("GlowPulse");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = ps.main;
            main.duration = Lifetime;
            main.loop = false;
            main.startLifetime = 0.6f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.0f);
            main.startColor = new Color(0.7f, 0.85f, 1f, 0.5f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 2;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1), new ParticleSystem.Burst(0.24f, 1) });
            var shapeOff = ps.shape; shapeOff.enabled = false;

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.35f));

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(FadeInOut());

            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(SoftDotAdditiveMat);
        }

        private static Gradient FadeOut()
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        private static Gradient FadeInOut()
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            T c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        private static void Wire(GameObject prefab)
        {
            GameObject player = GameObject.Find("Player");
            if (player == null) { Debug.LogError("SwordOrbitVfxSetup: no Player in " + ScenePath); return; }

            var ability = player.GetComponent<UltimateAbility>();
            if (ability == null) { Debug.LogError("SwordOrbitVfxSetup: Player has no UltimateAbility - run 'Add Ultimate Ability' first."); return; }

            var so = new SerializedObject(ability);
            SerializedProperty prefabProp = so.FindProperty("castVfxPrefab");
            SerializedProperty offsetProp = so.FindProperty("castVfxLocalOffset");
            if (prefabProp == null)
            {
                Debug.LogError("SwordOrbitVfxSetup: UltimateAbility has no 'castVfxPrefab' field - is the script up to date?");
                return;
            }
            prefabProp.objectReferenceValue = prefab;
            if (offsetProp != null) offsetProp.vector3Value = CastLocalOffset;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ability);
        }
    }
}
