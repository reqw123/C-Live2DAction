using UnityEditor;
using UnityEngine;

namespace Live2DAction.EditorTools
{
    // 2026-09-05, explicit user request: "長矛型光彈的特效，能採用 'C:\Users\homec\Downloads\
    // 長矛型光彈-3d.mp4' 這個影片當特效嗎" - asked via AskUserQuestion whether the video-baked
    // flipbook should REPLACE the existing 3D CrimsonVoidSpearProjectile model or overlay it; user
    // picked "疊加在現有3D模型上" (overlay) - 3D model/trail/point-light stay exactly as-is
    // (YuanpeiAttacks.SpearVolley isn't touched at all), this only ADDS a flipbook child onto the
    // SAME prefab (Assets/_Project/VFX/Boss/CrimsonVoidSpearProjectile.prefab) that SpearVolley
    // already Instantiate()s, so every future shot carries it automatically with zero code changes
    // to the attack logic.
    //
    // 透明通道: source clip is opaque H.264 on a near-black background (sampled via PIL: ~RGB
    // 20-23 at the corners, up to ~36-42 near a faint centre-top vignette - NOT the grey-checker
    // convention SwordOrbitSource.mp4 used), so the luma-key threshold is pushed down to 55
    // (vs SwordOrbit's 60 for a lighter checker-square floor) with range 90 + gamma 1.0 - verified
    // via direct pixel sampling on the baked atlas: background corners read alpha=0, the bright
    // white arrow-tip core reads alpha=255, mid-glow trail reads partial alpha (~70-120). Baked
    // OFFLINE (ffmpeg) into SpearFlipbook_Atlas.png: 48 frames (source frames 144-191 of a 24fps/
    // 10s clip - the "clean flying arrow + purple/crimson trailing energy" segment, picked out of
    // the full arc form-in -> flash-transition -> flying -> dissolve -> fade), 8x6 grid of 240x135
    // cells (1920x810, matches the source's exact 16:9 so no stretch). A drawbox paints out a small
    // 4-pointed star/sparkle watermark sitting in the bottom-right corner of every frame (full-
    // frame coords ~1110-1270 x / ~555-715 y out of 1280x720) with the sampled background colour
    // before the alpha key runs, so it disappears into "background" instead of showing as a fixed
    // opaque icon burned into every tile. Reprocess (from the original Downloads copy):
    //   ffmpeg -y -i "長矛型光彈-3d.mp4" -vf "select='between(n,144,191)',setpts=N/(24*TB),\
    //     drawbox=x=1110:y=555:w=160:h=160:color=0x151515:t=fill,scale=240:135,tile=8x6,\
    //     format=rgba,geq=r='r(X,Y)':g='g(X,Y)':b='b(X,Y)':\
    //     a='255*pow(clip((max(max(r(X,Y),g(X,Y)),b(X,Y))-55)/90,0,1),1.0)'" \
    //     -update 1 -frames:v 1 SpearFlipbook_Atlas.png
    //
    // Orientation: unlike SwordOrbit/PlayerUltimateAura (both Billboard, always face-camera), this
    // has to fly WITH the projectile - SpearVolley spawns the prefab via
    // Instantiate(prefab, origin, Quaternion.LookRotation(dir, Vector3.up)), so the prefab root's
    // own +Z is already the flight direction. Mesh render mode (not Billboard) + alignment=LOCAL
    // (not World - World silently ignores this transform's rotation entirely, see the 續(2026-09-05)
    // comment on BakeOntoPrefab's rotation constant for how that was found and fixed) + a fixed
    // child local rotation (Euler(0,90,0), also empirically swept and verified - see the same
    // comment) so the card reads flat-on with the tip leading in the parent's flight direction.
    // Cull is Off on SlashFlipbookURP, so which physical face ends up toward the camera doesn't
    // matter for transparency, but Local alignment still needs the RIGHT rotation to face flat
    // (an edge-on card is invisible) and to point the tip the correct way (not backward).
    //
    // Loops instead of self-destructing: a shot's actual flight time is speed-dependent (fast
    // enough shots hit well before the 2s source clip would even finish once), so this is NOT run
    // through SlashVfxController (which schedules a one-shot Destroy(gameObject, duration) - fine
    // for a fixed-length cast effect, wrong here since it would cut the trailing visual short on
    // any shot that outlives one clip length, or leave a dead lingering child on a shot that
    // doesn't). Instead the ParticleSystem loops forever (main.loop=true, a single particle re-
    // emitted every CycleSeconds via a burst at t=0, which Unity re-fires every loop) and rides
    // along for exactly as long as YuanpeiProjectile keeps the whole GameObject alive - it dies
    // with its parent, nothing here schedules its own Destroy.
    internal static class SpearFlipbookVfxSetup
    {
        private const string PrefabPath = "Assets/_Project/VFX/Boss/CrimsonVoidSpearProjectile.prefab";
        private const string Folder = "Assets/_Project/VFX/Boss/SpearVolley/";
        private const string AtlasPath = Folder + "SpearFlipbook_Atlas.png";
        private const string MaterialPath = Folder + "SpearFlipbookMat.mat";
        private const string FlipbookShader = "Live2DAction/VFX/SlashFlipbook";
        private const string ChildName = "SpearFlipbookVFX";

        // Grid + real-frame count baked by the ffmpeg pass (8*6 = 48 cells, 48 real frames - an
        // exact fit, so the sheet's frameOverTime curve runs the full 0..1 range with no dead tiles).
        private const int TilesX = 8;
        private const int TilesY = 6;
        private const int RealFrames = 48;

        // How long one loop of the 48-frame sheet takes - a fast pulsing/writhing energy loop,
        // not a real-time replay of the source clip's original 2s pacing (which would read as
        // sluggish on a fast-flying spear).
        private const float CycleSeconds = 0.6f;

        // 2026-09-06, user: "影片特效應該要調整跟3d模型對應的大小" - the card was a fixed 2.2 long
        // (~1.8x the model's own ~1.2 length / 1.25 collider height), which read as an oversized
        // detached energy cloud rather than an overlay ON the spear. Now MEASURED from the model's
        // renderer bounds at bake time (see BakeOntoPrefab) and sized to it: length = model's longest
        // axis x CardLengthVsModel (a slight overhang so the trailing energy still reads past the
        // tip), height derived from that keeping the source video's 240:135 aspect.
        // ~1.4x: the arrow ART only fills ~70% of the source cell (padding + trailing energy past
        // it), so at 1.4x the card the VISIBLE energy reads about the same length as the model, with
        // a little overhang past the tip and a trail behind - "corresponds to the model" without the
        // old 1.8x detached-cloud look. Nudge here if it wants more/less presence.
        private const float CardLengthVsModel = 1.4f;
        private const float FallbackModelLength = 1.2f;   // if the renderer bounds can't be read

        [MenuItem("Tools/Live2DAction/Add Spear Flipbook VFX (SpearVolley overlay)")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Exit Play Mode first - this edits a prefab asset on disk.");
                return;
            }

            AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath) == null)
            {
                Debug.LogError("SpearFlipbookVfxSetup: atlas not found at " + AtlasPath +
                               " - the ffmpeg bake step must run first (recipe in this file's header).");
                return;
            }

            Shader shader = Shader.Find(FlipbookShader);
            if (shader == null)
            {
                Debug.LogError("Shader not found: " + FlipbookShader + " (still compiling?).");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                Debug.LogError("SpearFlipbookVfxSetup: target prefab not found at " + PrefabPath);
                return;
            }

            ConfigureAtlasImport();
            Material material = CreateOrUpdateMaterial(shader);
            BakeOntoPrefab(material);

            AssetDatabase.SaveAssets();
            Debug.Log("SpearFlipbookVfxSetup: added/updated '" + ChildName + "' on " + PrefabPath +
                       " - every SpearVolley shot now carries the video-baked flipbook alongside " +
                       "the existing 3D model/trail/glow (unchanged).");
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
            importer.npotScale = TextureImporterNPOTScale.None; // keep exact 1920x810
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
            mat.SetColor("_Color", Color.white); // atlas already carries the crimson/void-purple
            mat.SetFloat("_SrcBlend", 1f);   // One (the shader premultiplies rgb by a)
            mat.SetFloat("_DstBlend", 10f);  // OneMinusSrcAlpha
            mat.SetFloat("_Brightness", 2.0f); // the arrow's white-hot core should blow past 1.0 for Bloom
            mat.SetFloat("_Opacity", 1f);
            mat.SetFloat("_ZTest", 4f); // normal depth test - flies through the world like the model does

            if (isNew) AssetDatabase.CreateAsset(mat, MaterialPath);
            else EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void BakeOntoPrefab(Material material)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

            Transform existing = root.transform.Find(ChildName);
            GameObject child = existing != null ? existing.gameObject : new GameObject(ChildName);
            if (existing == null) child.transform.SetParent(root.transform, false);

            // 2026-09-05, user: "影片特效的左右方向顛倒了" - the first pass (renderer.alignment =
            // World) LOOKED plausible in an isolated non-Play-mode screenshot because that test never
            // actually forced the ParticleSystem to simulate a frame (ParticleSystems don't tick
            // outside Play Mode without an explicit Simulate() call), so what that screenshot showed
            // was only the 3D model's own silhouette - the flipbook itself was never actually visible
            // in that check at all. Re-verified properly this time (ps.Simulate(t,true,true,true) to
            // force a real frame, Model child temporarily disabled so ONLY the flipbook shows) and
            // found TWO real bugs stacked together:
            //   1. `ParticleSystemRenderSpace.World` for a Mesh-mode renderer does NOT respect this
            //      transform's rotation at all - swept the child's local Y rotation through all 8
            //      cardinal angles (0/45/.../315) with World alignment and every single one rendered
            //      the IDENTICAL image (tip pointing backward, opposite the flight direction) -
            //      confirming the rotation value was never the actual bug, the alignment MODE was.
            //   2. Switching to `ParticleSystemRenderSpace.Local` (respects this transform's own
            //      rotation, which is what a child riding along on a moving/rotating parent actually
            //      needs) and re-sweeping the same 8 angles found 0/180 edge-on (invisible - the
            //      card's plane is exactly in the camera's line of sight), 45/90/135 pointing the tip
            //      the CORRECT way (matching the flight direction), 225/270/315 pointing it backward
            //      - with 90 being the flattest/most face-on (45 and 135 are visibly foreshortened).
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            child.transform.localScale = Vector3.one;

            // Measure the actual 3D model so the card tracks it (2026-09-06 user request) instead of
            // being a fixed guess. The model renderer is a sibling of this child under `root`; exclude
            // any renderer on the flipbook child itself.
            float modelLength = FallbackModelLength;
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (r == null || r.transform.IsChildOf(child.transform) || r.transform == child.transform) continue;
                Vector3 s = r.bounds.size;
                float longest = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
                if (longest > 0.0001f) { modelLength = longest; break; }
            }
            float cardLength = modelLength * CardLengthVsModel;
            float cardHeight = cardLength * (135f / 240f);

            var ps = child.GetComponent<ParticleSystem>();
            if (ps == null) ps = child.AddComponent<ParticleSystem>();
            ConfigureFlipbook(ps, material, cardLength, cardHeight);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log("SpearFlipbookVfxSetup: model length measured ~" + modelLength.ToString("0.00") +
                      " -> card " + cardLength.ToString("0.00") + " x " + cardHeight.ToString("0.00"));
        }

        private static void ConfigureFlipbook(ParticleSystem ps, Material material, float cardLength, float cardHeight)
        {
            ParticleSystem.MainModule main = ps.main;
            main.duration = CycleSeconds;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = CycleSeconds;
            main.startSpeed = 0f;
            main.startSize3D = true;
            // Card's local X/Y are its own flat plane before the child's -90Y rotation is applied -
            // X is the length axis (maps to parent forward), Y is height (maps to parent up), Z is
            // negligible thickness.
            main.startSizeX = new ParticleSystem.MinMaxCurve(cardLength);
            main.startSizeY = new ParticleSystem.MinMaxCurve(cardHeight);
            main.startSizeZ = new ParticleSystem.MinMaxCurve(1f);
            main.startRotation = 0f;
            main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 1;
            main.stopAction = ParticleSystemStopAction.None;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            // Burst at t=0 only - Unity re-fires bursts specified at loop-relative time 0 every time
            // a looping system restarts a cycle, so this alone gives a continuous re-emitted loop
            // with no extra scripting.
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

            var shapeOff = ps.shape; shapeOff.enabled = false;

            ParticleSystem.TextureSheetAnimationModule sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = TilesX;
            sheet.numTilesY = TilesY;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.cycleCount = 1;
            float endFraction = RealFrames / (float)(TilesX * TilesY); // = 1.0, exact fit
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, endFraction));

            // No fade-in/out gradient (unlike SwordOrbit's one-shot cast effect) - this loops
            // continuously for the whole flight, so a lifetime-based fade would just flicker every
            // cycle. The source clip's own frames already dissolve into nothing at the tail.
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = false;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = GetQuadMesh();
            // Local (NOT World - see the big comment on the -90/90 rotation choice above) - this has
            // to follow the transform's own rotation since it's a child riding a moving/rotating
            // parent, not a fixed-orientation stamp.
            renderer.alignment = ParticleSystemRenderSpace.Local;
            renderer.sharedMaterial = material;
            renderer.sortMode = ParticleSystemSortMode.None;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // See Attack3SlashEffectSetup's own note: AssetDatabase.GetBuiltinExtraResource<Mesh>
        // ("Quad.fbx") can silently return null in this Unity version/context - CreatePrimitive is
        // a more reliable route to the same built-in quad mesh.
        private static Mesh GetQuadMesh()
        {
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temp);
            return mesh;
        }
    }
}
