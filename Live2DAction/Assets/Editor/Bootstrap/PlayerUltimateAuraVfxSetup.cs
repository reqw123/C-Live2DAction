using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.Core;
using Live2DAction.UI;

namespace Live2DAction.EditorTools
{
    // 2026-08-31 (追加77) — baked from 不要出現人物_單純的周邊特效_請重新生成.mp4 as a flame-pillar
    // VFX. Originally wired as UltimateAbility.castVfxPrefab (spawned once at cast).
    //
    // 2026-08-31 (追加79), user correction: it is a READY-STATE aura, not a cast effect. On while
    // UltimateEnergy.IsFull, off otherwise, gone the instant R is pressed. A persistent child that
    // UltimateReadyAura SetActive-toggles. UltimateActivationBurst still handles the cast punch.
    //
    // 2026-08-31 (追加81 續 5), user swapped the source asset: a NEW clip ("...一個孤立的3D遊戲
    // 特效，沒有角色、武..." - a teal/gold spirit-fire pillar with white energy ribbons, spark
    // flecks and concentric ground ripple rings, on clean black). Requirements: match the body
    // size; the CENTRE (where the character stands) only slightly transparent, everything else at
    // full asset opacity; handle the alpha channel; keep the "mimic 3D" billboard treatment.
    //
    // Pipeline is unchanged from 追加81: ONE billboard flipbook layer, full uncropped 16:9 frame,
    // ZTest=Always, premultiplied-alpha blend, two phase-offset particles + a triangle alpha fade
    // to cross-dissipate the loop seam. The clip's own ground rings / licks / sparks are in the
    // atlas - no extra child particle systems.
    //
    // 透明通道 (追加81 續 5 bake): clean opaque H.264 on BLACK. Baked OFFLINE (ffmpeg) into
    // PlayerUltimateAura_Atlas.png - **64 frames, source 30..156 every 2**, each 320x180, tiled
    // 8x8 -> 2560x1440. `delogo` (not a black drawbox - that would punch a hole in the ground
    // rings the watermark sits on) removes the corner ✦. Alpha = a luminance key TIMES a soft
    // central "silhouette dip":
    //   - key: threshold 22 (clean black bg), range /50, gamma 0.80 -> flame solid, faint glow gone.
    //   - dip: a per-cell Gaussian, min ~0.45 at the centre-lower band where the ~1.28 m character
    //     stands (centred 55.6% down the frame, sx 7% W / sy 19% H), easing back to full alpha
    //     outward. So "只有角色中心輪廓稍微透明，其他部分完全還原資產效果".
    // Reprocess (from Source/PlayerUltimateAuraSource.mp4):
    //   ffmpeg -y -i PlayerUltimateAuraSource.mp4 -vf "select='between(n,30,156)*not(mod(n-30\,2))',\
    //     setpts=N/(24*TB),delogo=x=1124:y=572:w=56:h=52,scale=320:180,format=rgba,\
    //     geq=r='r(X,Y)':g='g(X,Y)':b='b(X,Y)':\
    //     a='255*pow(clip((max(max(r(X,Y),g(X,Y)),b(X,Y))-22)/50,0,1),0.80)*\
    //        (1-0.55*exp(-(pow((X-0.5*W)/(0.070*W),2)+pow((Y-0.556*H)/(0.190*H),2))/2))',\
    //     tile=8x8" -frames:v 1 ../PlayerUltimateAura_Atlas.png
    //
    // Re-runnable: rebuilds material + prefab, re-instantiates the player child, re-wires
    // UltimateReadyAura each run.
    internal static class PlayerUltimateAuraVfxSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string Folder = "Assets/_Project/VFX/Skills/PlayerUltimateAura/";
        private const string AtlasPath = Folder + "PlayerUltimateAura_Atlas.png";
        private const string MaterialPath = Folder + "PlayerUltimateAuraFlipbook.mat";
        private const string PrefabPath = Folder + "PlayerUltimateAuraVFX.prefab";
        private const string FlipbookShader = "Live2DAction/VFX/SlashFlipbook";

        // Name of the persistent aura child instantiated under Player (inactive; UltimateReadyAura
        // toggles it).
        private const string AuraChildName = "ReadyFlameAura";

        // 2026-08-31 (追加78→79): the "charged / ultimate ready" cue, cut from the SAME source
        // video's AAC track (a rising fire whoosh). 追加79 trimmed off the back half - it fires
        // ONCE when the aura activates (energy hits full), it is not a looping ambience.
        //   ffmpeg -y -ss 1.0 -t 2.3 -i PlayerUltimateAuraSource.mp4 -vn \
        //     -af "afade=t=in:st=0:d=0.08,afade=t=out:st=1.8:d=0.5,loudnorm=I=-15:TP=-1.5:LRA=11" \
        //     -ar 44100 -ac 2 PlayerUltimateAura_Ready.wav
        private const string ReadyAudioPath = "Assets/_Project/Audio/Skills/PlayerUltimateAura_Ready.wav";

        // Grid + real-frame count baked by the ffmpeg pass (8*8 = 64 cells, 64 real frames - the
        // whole grid is used now).
        private const int TilesX = 8;
        private const int TilesY = 8;
        private const int RealFrames = 64;

        // One flipbook cycle replays the 54-frame settled-pillar section in ~2.2 s. Two particles
        // stay alive at once, half a lifetime out of phase, cross-fading so the loop has no seam.
        private const float Lifetime = 2.2f;

        // "體型匹配": SizeHeight 1.7 -> visible flame ~1.2 m, roughly the 1.28 m character's height
        // (tip near the head, not over it). SizeWidth = H * 1280/720 keeps the frame undistorted so
        // the ground rings stay circular; the new clip's rings only fill ~58% of the frame width
        // (~1.75 m across) so the footprint is already modest. The "don't cover the character"
        // part (追加81 續 4) is now the BAKED central alpha dip, not a global _Opacity cut - so
        // _Opacity goes back to 1.0 and the rest of the flame renders at full asset strength.
        private const float SizeHeight = 1.7f;
        private const float SizeWidth = SizeHeight * (1280f / 720f);

        // The whole atlas is the settled roaring pillar now, so loop all of it (0..RealFrames).
        private const float LoopStartFraction = 0f;

        // Local position of the aura child under the Player transform. The new clip's ground rings
        // sit ~78% down the frame = ~0.28 * SizeHeight below the quad centre (~0.48 m at H 1.7),
        // so feet (local Y -0.58) + 0.48 ≈ -0.10 keeps the rings at the character's feet.
        private static readonly Vector3 AuraLocalOffset = new Vector3(0f, -0.1f, 0f);

        [MenuItem("Tools/Live2DAction/Add Player Ultimate Ready Aura VFX (flame, on full energy)")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Exit Play Mode first - this touches the AssetDatabase and the scene.");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath) == null)
            {
                Debug.LogError("PlayerUltimateAuraVfxSetup: atlas not found at " + AtlasPath +
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
            Material mat = CreateOrUpdateMaterial(shader);
            GameObject prefab = CreateOrUpdatePrefab(mat);
            WireReadyAura(prefab);

            AssetDatabase.SaveAssets();
            Debug.Log("PlayerUltimateAuraVfxSetup: built " + PrefabPath +
                      " and wired it as UltimateReadyAura.flameAura (shows while energy is full).");
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
            importer.npotScale = TextureImporterNPOTScale.None;
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
            mat.SetColor("_Color", Color.white);
            mat.SetFloat("_SrcBlend", 1f);   // One (the shader premultiplies rgb by a)
            mat.SetFloat("_DstBlend", 10f);  // OneMinusSrcAlpha
            mat.SetFloat("_Brightness", 1.05f); // slight emissive lift so it engages URP Bloom and reads as fire; the see-through is baked into the atlas centre, not here
            mat.SetFloat("_Opacity", 1f);       // full - the central "silhouette dip" is in the atlas alpha (追加81 續 5)
            mat.SetFloat("_ZTest", 8f);      // Always - the character mesh never clips the aura
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            if (isNew) AssetDatabase.CreateAsset(mat, MaterialPath);
            else EditorUtility.SetDirty(mat);
            return mat;
        }

        private static GameObject CreateOrUpdatePrefab(Material mat)
        {
            bool isNew = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null;
            GameObject root = isNew ? new GameObject("PlayerUltimateAuraVFX") : PrefabUtility.LoadPrefabContents(PrefabPath);

            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            }
            // 追加79: persistent toggled child - no SlashVfxController (that self-destructs).
            SlashVfxControllerCleanup(root);

            // 追加81: a single flipbook billboard - the source clip and nothing else.
            ConfigureFlipbookLayer(EnsureComponent<ParticleSystem>(root), mat);
            ConfigureReadyAudio(root);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            if (isNew) Object.DestroyImmediate(root);
            else PrefabUtility.UnloadPrefabContents(root);
            return saved;
        }

        private static void SlashVfxControllerCleanup(GameObject root)
        {
            var stale = root.GetComponent<Live2DAction.VFX.SlashVfxController>();
            if (stale != null)
            {
                Object.DestroyImmediate(stale);
            }
        }

        private static void ConfigureFlipbookLayer(ParticleSystem ps, Material material)
        {
            ParticleSystem.MainModule main = ps.main;
            main.duration = Lifetime;
            main.loop = true;                    // persistent aura
            main.prewarm = true;                 // full pillar the instant the aura switches on
            main.playOnAwake = true;
            main.startLifetime = Lifetime;
            main.startSpeed = 0f;
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(SizeWidth);
            main.startSizeY = new ParticleSystem.MinMaxCurve(SizeHeight);
            main.startSizeZ = new ParticleSystem.MinMaxCurve(1f);
            main.startRotation = 0f;
            main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.Local; // rides the parented player
            main.maxParticles = 4;
            main.stopAction = ParticleSystemStopAction.None;

            // ~2 particles alive at once, offset in phase, so the flipbook loop has no restart pop.
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 2f / Lifetime;
            emission.SetBursts(new ParticleSystem.Burst[0]);

            var shapeOff = ps.shape; shapeOff.enabled = false;

            ParticleSystem.TextureSheetAnimationModule sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = TilesX;
            sheet.numTilesY = TilesY;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.cycleCount = 1;
            float endFraction = RealFrames / (float)(TilesX * TilesY);
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, LoopStartFraction, 1f, endFraction));

            // TRIANGLE fade (0 -> 1 at mid-life -> 0). With emission 2/Lifetime the two live
            // particles are exactly half a lifetime out of phase, so their alphas sum to ~1.0 at
            // every instant - a seamless loop with no double-brightness.
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sharedMaterial = material;
            renderer.sortMode = ParticleSystemSortMode.None;
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            T c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        // 3D one-shot cue on the aura root. playOnAwake means it fires each time the aura is
        // SetActive(true) - i.e. every time the energy bar reaches full. loop=false: it is a
        // "charged!" stinger, not an ambience. Null-safe.
        private static void ConfigureReadyAudio(GameObject root)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ReadyAudioPath);
            if (clip == null)
            {
                Debug.LogWarning("PlayerUltimateAuraVfxSetup: ready audio not found at " + ReadyAudioPath +
                                 " - aura will be silent. Run the ffmpeg extract in this file's header.");
                return;
            }

            AudioSource source = EnsureComponent<AudioSource>(root);
            source.clip = clip;
            source.playOnAwake = true;
            source.loop = false;
            source.spatialBlend = 1f;
            source.volume = 0.8f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 4f;
            source.maxDistance = 50f;
        }

        private static void WireReadyAura(GameObject prefab)
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject player = GameObject.Find("Player");
            if (player == null) { Debug.LogError("PlayerUltimateAuraVfxSetup: no Player in " + ScenePath); return; }

            UltimateEnergy energy = player.GetComponent<UltimateEnergy>();
            if (energy == null) { Debug.LogError("PlayerUltimateAuraVfxSetup: Player has no UltimateEnergy."); return; }

            // (Re)create the persistent aura child, inactive - UltimateReadyAura toggles it.
            Transform existing = player.transform.Find(AuraChildName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }
            GameObject auraInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, player.transform);
            auraInstance.name = AuraChildName;
            auraInstance.transform.localPosition = AuraLocalOffset;
            auraInstance.transform.localRotation = Quaternion.identity;
            auraInstance.SetActive(false);

            UltimateReadyAura ready = player.GetComponent<UltimateReadyAura>();
            if (ready == null)
            {
                ready = player.AddComponent<UltimateReadyAura>();
            }
            var so = new SerializedObject(ready);
            so.FindProperty("energy").objectReferenceValue = energy;
            SerializedProperty flameProp = so.FindProperty("flameAura");
            if (flameProp != null)
            {
                flameProp.objectReferenceValue = auraInstance;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
