using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Live2DAction.CameraSystem;
using Live2DAction.Characters;
using Live2DAction.Combat;
using Live2DAction.Core;
using Live2DAction.UI;
using Live2DAction.VFX;

namespace Live2DAction.EditorTools
{
    // 2026-08-31, user request ("讓 cat 能量滿格時可以施放 '幫我生成一個黑暗劍氣風格的版本.mp4' 這個
    // 技能特效 要處理透明通道和仿3d問題"). Same video-to-flipbook pipeline as
    // PlayerUltimateAuraVfxSetup (the player's R cast), for the cat's own ultimate:
    //
    // 透明通道: the source clip is opaque H.264 - NOT on pure black, on a baked-in grey
    // transparency checkerboard (AI-VFX-generator convention, same as 不要有人形.mp4). Baked
    // OFFLINE (ffmpeg) into DarkSwordQi_Atlas.png.
    //
    // 2026-08-31 (追加81), user "掉漆很嚴重 是因為透明通道...": the 追加75/80 bake keyed alpha from
    // LUMINANCE with threshold 28. That is far too low for this clip's checkerboard (its light
    // squares run to ~luma 74), so the checker + the H.264 block noise in the dark areas leaked
    // through as a grubby grey semi-transparent haze - catastrophic over a bright scene. Re-baked
    // with a CHROMA key instead: the checker is perfectly grey (chroma == 0) while the effect is
    // crimson/violet (chroma 40-200), so `alpha ~ max(chroma_term, bright_luma_term)` drops the
    // checker completely and keeps the colour. The 追加75 `crop=720:720:400:0` centre-crop is also
    // gone (it sliced the outer half of the wide vortex rings off) - now the full 1280x720 frame,
    // a drawbox paints out the corner watermark. 8x8 grid of 320x180 cells (2560x1440), source
    // frames 21..210 every 3 = 64 frames (full arc: rune-sword forms -> crimson ribbon vortex ->
    // violet blast -> settle). Reprocess (from Source/DarkSwordQiSource.mp4):
    //   MX = max(max(r,g),b) ; MN = min(min(r,g),b)   (per-pixel, in the geq expr below)
    //   ffmpeg -y -i DarkSwordQiSource.mp4 -vf "select='between(n,21,210)*not(mod(n-21\,3))',\
    //     setpts=N/(24*TB),drawbox=x=1108:y=542:w=156:h=128:color=black@1:t=fill,\
    //     scale=320:180,tile=8x8,format=rgba,geq=r='r(X,Y)':g='g(X,Y)':b='b(X,Y)':\
    //     a='255*pow(clip(max((MX-MN-10)/70,(MX-92)/120),0,1),0.9)'" \
    //     -frames:v 1 ../DarkSwordQi_Atlas.png
    // (chroma term (MX-MN-10)/70 keys the coloured effect - checker chroma is 0 so it vanishes;
    //  the second term (MX-92)/120 keeps near-white spark cores, and 92 > the checker's 74 ceiling
    //  so it stays clean. Downside: the grey dissipation smoke at the very end is low-chroma and
    //  keys weak - acceptable, it was never the hero beat.)
    // The .mp4 under Source/ is an archived reference only, never played at runtime.
    //
    // 仿3d: a billboard flipbook quad (SlashFlipbookURP.shader, premultiplied-alpha blend so the
    // bright crimson/violet cores don't blow out) plus small 3D child particle systems - a dark
    // crimson spark burst and a violet volume pulse - so it reads with depth, not as a flat card.
    //
    // Wire() (also called from CatCharacterSetup's end) does the cat side: a dedicated skill-energy
    // meter (Cat/SkillEnergy, 100 / 5-per-1s = 20s), CatUltimateAbility, the CatDarkQi AttackData
    // for its AOE, re-points the CatCornerHud 能量 bar at the skill meter, and adds
    // CatUltimateAbility to CameraPossessionSwitcher.catControl (so R only casts while you're the cat).
    internal static class CatDarkQiVfxSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string Folder = "Assets/_Project/VFX/Skills/DarkSwordQi/";
        private const string AtlasPath = Folder + "DarkSwordQi_Atlas.png";
        private const string MaterialPath = Folder + "DarkSwordQiFlipbook.mat";
        private const string PrefabPath = Folder + "CatDarkQiSkillVFX.prefab";
        private const string FlipbookShader = "Live2DAction/VFX/SlashFlipbook";
        private const string SoftDotAdditiveMat = "Assets/_Project/VFX/Slash/Materials/SoftDotAdditiveMat.mat";
        private const string AttackAssetPath = "Assets/_Project/Settings/Combat/Cat/CatDarkQi.asset";

        // 2026-08-31 (追加78): the cast sound, cut from the SAME source video the flipbook was
        // baked from (it ships with an AAC track - a dark-energy windup that snaps into a slam).
        // Trimmed + faded + loudness-normalised offline with ffmpeg (INPUT seek - output seek on
        // this file produced digital silence):
        //   ffmpeg -y -ss 3.25 -t 2.9 -i DarkSwordQiSource.mp4 -vn \
        //     -af "afade=t=in:st=0:d=0.05,afade=t=out:st=2.4:d=0.5,loudnorm=I=-15:TP=-1.5:LRA=11" \
        //     -ar 44100 -ac 2 CatDarkQi_Cast.wav
        private const string CastAudioPath = "Assets/_Project/Audio/Skills/CatDarkQi_Cast.wav";

        // Grid + real-frame count baked by the ffmpeg pass (8*8 = 64 cells, all 64 used). Full arc
        // (rune-sword forms -> crimson ribbon vortex -> violet blast -> settle), source frames
        // 21..210 every 3. Recipe (incl. the 追加81 chroma key) is in this file's header.
        private const int TilesX = 8;
        private const int TilesY = 8;
        private const int RealFrames = 64;

        // Full arc replayed once over ~2.6s. The prefab's SlashVfxController destroys it once BOTH
        // this and the cast audio (2.9s) have finished.
        private const float Lifetime = 2.6f;

        // 追加81: cell is now the full 16:9 frame (320x180), not a 720 square crop. Keep the frame
        // undistorted so the wide vortex stays elliptical, not squashed to a circle.
        private const float SizeHeight = 3.2f;
        private const float SizeWidth = SizeHeight * (320f / 180f);

        [MenuItem("Tools/Live2DAction/Add Cat Dark Sword-Qi Skill (R at full energy)")]
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
                Debug.LogError("CatDarkQiVfxSetup: atlas not found at " + AtlasPath + " - the ffmpeg bake step must run first.");
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
            EnsureAttackData();

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Wire(prefab);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("CatDarkQiVfxSetup: built " + PrefabPath + " and wired the cat's ultimate. " +
                      "Possess the cat (C), fill its 能量 bar, press R.");
        }

        // Cat-side wiring only - safe to call from CatCharacterSetup after it rebuilds the Cat.
        // No-op for the VFX prefab reference if it hasn't been baked yet (run the menu once).
        public static void Wire()
        {
            Wire(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        }

        private static void Wire(GameObject prefab)
        {
            GameObject cat = FindRoot("Cat");
            if (cat == null)
            {
                return;
            }

            var ability = cat.GetComponent<CatUltimateAbility>();
            if (ability == null)
            {
                ability = cat.AddComponent<CatUltimateAbility>();
                ability.enabled = false; // catControl enables it
            }

            Transform skillEnergyT = cat.transform.Find("SkillEnergy");
            UltimateEnergy skillEnergy = skillEnergyT != null ? skillEnergyT.GetComponent<UltimateEnergy>() : null;
            if (skillEnergy == null)
            {
                var go = new GameObject("SkillEnergy");
                go.transform.SetParent(cat.transform, false);
                skillEnergy = go.AddComponent<UltimateEnergy>();
                var seSo = new SerializedObject(skillEnergy);
                seSo.FindProperty("maxEnergy").floatValue = 100f;
                seSo.FindProperty("regenAmount").floatValue = 5f;
                seSo.FindProperty("regenIntervalSeconds").floatValue = 1f;
                seSo.FindProperty("regenIdleDelaySeconds").floatValue = 0f;
                seSo.ApplyModifiedPropertiesWithoutUndo();
            }

            AttackData attack = AssetDatabase.LoadAssetAtPath<AttackData>(AttackAssetPath) ?? EnsureAttackData();

            var so = new SerializedObject(ability);
            SetRef(so, "inputSource", cat.GetComponent<Live2DAction.Input.PlayerInputProvider>());
            SetRef(so, "energy", skillEnergy);
            SetRef(so, "attack", attack);
            SetRef(so, "stance", cat.GetComponent<StancePoise>());
            SetRef(so, "health", cat.GetComponent<Health>());
            if (prefab != null)
            {
                SetRef(so, "castVfxPrefab", prefab);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ability);

            RepointCatEnergyBar(skillEnergy);
            AddToCatControl(ability);

            Debug.Log("CatDarkQiVfxSetup.Wire: cat ultimate wired" + (prefab == null ? " (no VFX prefab yet - run the menu)" : "") + ".");
        }

        // ---- VFX asset pipeline (mirrors SwordOrbitVfxSetup) ----

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
            importer.npotScale = TextureImporterNPOTScale.None; // keep exact 2560x1440
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
            mat.SetColor("_Color", Color.white); // atlas already carries the crimson/violet
            mat.SetFloat("_SrcBlend", 1f);   // One (premultiplied)
            mat.SetFloat("_DstBlend", 10f);  // OneMinusSrcAlpha
            mat.SetFloat("_Brightness", 2.0f); // 追加81: this is a DARK asset - lift it so it still reads (and catches Bloom) outside a night scene
            mat.SetFloat("_Opacity", 1f);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            if (isNew) AssetDatabase.CreateAsset(mat, MaterialPath);
            else EditorUtility.SetDirty(mat);
            return mat;
        }

        private static GameObject CreateOrUpdatePrefab(Material material)
        {
            bool isNew = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null;
            GameObject root = isNew ? new GameObject("CatDarkQiSkillVFX") : PrefabUtility.LoadPrefabContents(PrefabPath);

            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            }

            ConfigureMainFlipbook(EnsureComponent<ParticleSystem>(root), material);
            BuildSparkBurst(root.transform);
            BuildGlowPulse(root.transform);
            ConfigureCastAudio(root);

            var controller = EnsureComponent<SlashVfxController>(root);
            var so = new SerializedObject(controller);
            so.FindProperty("playbackSpeed").floatValue = 1f;
            so.FindProperty("sizeMultiplier").floatValue = 1f;
            so.FindProperty("lifetimeSecondsOverride").floatValue = 0f;
            so.FindProperty("opacity").floatValue = 1f;
            so.FindProperty("brightness").floatValue = 2.0f;
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
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(1f, 0.82f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sharedMaterial = material;
            renderer.sortMode = ParticleSystemSortMode.None;
        }

        // 仿3d - a burst of dark crimson embers at the cast moment.
        private static void BuildSparkBurst(Transform parent)
        {
            var go = new GameObject("Embers");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = ps.main;
            main.duration = Lifetime;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.65f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.1f, 0.18f), new Color(0.42f, 0.12f, 0.6f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 70;
            main.gravityModifier = 0.2f;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0.0f, 28),
                new ParticleSystem.Burst(0.32f, 18),
            });

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.45f;

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(FadeOut());

            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(SoftDotAdditiveMat);
        }

        // 仿3d - a dark violet volume pulse in the centre so the swirl reads around a core.
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
            main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.1f);
            main.startColor = new Color(0.45f, 0.15f, 0.6f, 0.5f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 2;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1), new ParticleSystem.Burst(0.26f, 1) });
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

        // ---- cat wiring helpers ----

        // 2026-08-31 (追加80): this used to overwrite EVERY field (incl. damage) on every menu
        // run, which silently reverted the user's tuned values twice. Now structural fields
        // (attackId / geometry / frame data) are re-synced always, but the BALANCE numbers
        // (damage, knockback) are only written when the asset is first created - re-runs leave a
        // hand-tuned asset alone (rule 7 + the 手動調校值 rule). `damage` seeds at 50: the cat R
        // is a 10-hit barrage (CatUltimateAbility.hitCount), 10 * 50 = 500 total.
        private static AttackData EnsureAttackData()
        {
            AttackData a = AssetDatabase.LoadAssetAtPath<AttackData>(AttackAssetPath);
            bool isNew = a == null;
            if (isNew) a = ScriptableObject.CreateInstance<AttackData>();

            var so = new SerializedObject(a);
            so.FindProperty("attackId").stringValue = "CatDarkQi";
            so.FindProperty("range").floatValue = 2.6f;   // Range + Radius = 3.2 OverlapSphere reach
            so.FindProperty("radius").floatValue = 0.6f;
            if (isNew)
            {
                so.FindProperty("damage").floatValue = 50f; // per hit; barrage of 10 -> 500 total
                SetFloatIfPresent(so, "knockbackForce", 2.5f); // small per hit - 10 of them, don't launch to orbit
                SetBoolIfPresent(so, "knockbackLaunches", false);
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            if (isNew) AssetDatabase.CreateAsset(a, AttackAssetPath);
            else EditorUtility.SetDirty(a);
            return a;
        }

        private static void RepointCatEnergyBar(UltimateEnergy skillEnergy)
        {
            GameObject hud = GameObject.Find("CatCornerHud");
            if (hud == null)
            {
                return; // CatBarsWiring hasn't run yet
            }
            Transform panel = hud.transform.Find("Panel");
            Transform track = panel != null ? panel.Find("必殺Track") : null;
            if (track == null)
            {
                return;
            }
            foreach (MonoBehaviour mb in track.GetComponents<MonoBehaviour>())
            {
                if (mb == null || mb is Image) continue;
                var so = new SerializedObject(mb);
                SerializedProperty p = so.FindProperty("energy");
                if (p != null && p.propertyType == SerializedPropertyType.ObjectReference)
                {
                    p.objectReferenceValue = skillEnergy;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(mb);
                }
            }
        }

        private static void AddToCatControl(CatUltimateAbility ability)
        {
            CameraPossessionSwitcher switcher = null;
            foreach (var s in Object.FindObjectsByType<CameraPossessionSwitcher>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                switcher = s;
            }
            if (switcher == null)
            {
                return;
            }
            var so = new SerializedObject(switcher);
            SerializedProperty arr = so.FindProperty("catControl");
            for (int i = 0; i < arr.arraySize; i++)
            {
                if (arr.GetArrayElementAtIndex(i).objectReferenceValue == ability)
                {
                    return; // already listed
                }
            }
            arr.arraySize++;
            arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = ability;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(switcher);
        }

        // ---- small utils ----

        private static void SetRef(SerializedObject so, string prop, Object value)
        {
            SerializedProperty p = so.FindProperty(prop);
            if (p != null && p.propertyType == SerializedPropertyType.ObjectReference)
            {
                p.objectReferenceValue = value;
            }
        }

        private static void SetFloatIfPresent(SerializedObject so, string prop, float value)
        {
            SerializedProperty p = so.FindProperty(prop);
            if (p != null && p.propertyType == SerializedPropertyType.Float) p.floatValue = value;
        }

        private static void SetBoolIfPresent(SerializedObject so, string prop, bool value)
        {
            SerializedProperty p = so.FindProperty(prop);
            if (p != null && p.propertyType == SerializedPropertyType.Boolean) p.boolValue = value;
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            T c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        // 3D one-shot cast cue on the VFX root - plays the instant the prefab is instantiated
        // (cat R fires). Null-safe: missing .wav just leaves it silent. SlashVfxController keeps
        // the GameObject alive until the clip finishes.
        private static void ConfigureCastAudio(GameObject root)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(CastAudioPath);
            if (clip == null)
            {
                Debug.LogWarning("CatDarkQiVfxSetup: cast audio not found at " + CastAudioPath +
                                 " - VFX will be silent. Run the ffmpeg extract in this file's header.");
                return;
            }

            AudioSource source = EnsureComponent<AudioSource>(root);
            source.clip = clip;
            source.playOnAwake = true;
            source.loop = false;
            source.spatialBlend = 1f;
            source.volume = 0.85f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 4f;
            source.maxDistance = 50f;
        }

        private static GameObject FindRoot(string name)
        {
            foreach (GameObject g in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (g != null && g.name == name && g.transform.parent == null)
                {
                    return g;
                }
            }
            return null;
        }
    }
}
