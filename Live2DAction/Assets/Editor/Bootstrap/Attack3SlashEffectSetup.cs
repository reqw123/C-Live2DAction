using UnityEditor;
using UnityEngine;
using Live2DAction.Combat;

namespace Live2DAction.EditorTools
{
    // Builds a flipbook slash VFX from a user-provided crescent-slash sprite sheet
    // (2026-08-13, explicit user request - "attack3 專用特效"; AI-generated, user's own, no
    // license concern per Docs/ASSET_LICENSES.md discipline for external art in this project)
    // and wires it as LightAttack3's HitEffectOverride, so only Attack3's landed hits spawn
    // this instead of PlayerCombat's shared spark prefab (see AttackData.HitEffectOverride
    // and PlayerCombat.ResolveActiveHit for the override mechanism itself).
    //
    // 2026-08-13 revision history: original 5-frame single-row PNG sheet
    // (T_SlashCrescent_Sheet.png, still in the project unused) -> a denser 6x4=24-frame JPG
    // sheet (T_SlashCrescent_Grid6x4.jpg, also kept unused) -> a 6x3=18-cell atlas
    // Attack3SlashFrameAtlasBuilder composited from 17 separate trimmed-frame PNGs (kept as
    // T_SlashCrescent_Frames17.png, unused - see that class for why THAT source needed
    // compositing first instead of being usable directly) -> now an "X" cross-slash effect
    // that ships as a proper pre-built 8x8=64-cell grid (T_SlashCrescent_XSlash8x8.png, only
    // 62 of the 64 cells have real content - the source tool's own index.json confirms this
    // and the trailing 2 cells are visibly solid black in the sheet itself; no special
    // handling needed for them, see ConfigureTextureImport's comment) - no compositing step
    // needed this time, so Apply() below skips Attack3SlashFrameAtlasBuilder entirely. This
    // tool genuinely re-runs its own prefab/material update path rather than skip-if-exists,
    // so pointing SourceTexturePath/TilesX/TilesY at yet another source later just needs
    // those constants changed and a re-run, no manual Editor surgery on the existing prefab
    // required.
    internal static class Attack3SlashEffectSetup
    {
        private const string VfxFolder = "Assets/_Project/VFX/Slash";
        // 2026-08-13: points at the background-cleaned derivative, not the raw source PNG -
        // see Attack3SlashBackgroundCleaner for why the raw sheet's "empty" areas are an
        // opaque grey, not transparent/black, and washed the scene out under Additive blend
        // ("背景是偏白色的畫布" bug report) until this ran.
        private const string SourceTexturePath = Attack3SlashBackgroundCleaner.CleanedPath;
        private const string MaterialPath = VfxFolder + "/SlashCrescent.mat";
        private const string PrefabPath = VfxFolder + "/Attack3SlashEffect.prefab";
        private const string LightAttack3Path = "Assets/_Project/Settings/Combat/LightAttack3.asset";

        // Straight from the source sheet's own index.json ("frame_size": 1280x720,
        // "sheet_size": 10240x5760 -> 10240/1280=8, 5760/720=8) - a real pre-built grid this
        // time, not something this tool composited itself.
        private const int TilesX = 8;
        private const int TilesY = 8;

        // Cell aspect ratio is 1280:720 = 16:9 (unlike the previous sources' roughly-square
        // crescents) - a uniform startSize would squash this wide "X" shape into a square.
        // Height picked to roughly match the previous crescents' vertical reach; width
        // derived from it via the sheet's own real aspect ratio rather than guessed
        // separately, so the two can't drift out of proportion with each other.
        private const float FrameAspect = 1280f / 720f;
        private const float SizeHeight = 1.0f;
        private const float SizeWidth = SizeHeight * FrameAspect;

        // 62 real frames (of 64 grid cells) at ~40fps (matching prior sheets' pacing) would be
        // ~1.55s - LightAttack3's own mechanical duration is only ~0.6s (37 frames / 60fps,
        // see LightAttack3.asset). Same "compress to the attack's own timing rather than the
        // source clip's original real-time pacing" call as the previous two sheets.
        private const float Lifetime = 0.6f;

        [MenuItem("Tools/Live2DAction/Add Attack3 Slash Effect")]
        public static void Apply()
        {
            Attack3SlashBackgroundCleaner.Clean();
            ConfigureTextureImport();
            GameObject prefab = CreateOrUpdatePrefab();
            WireToLightAttack3(prefab);

            AssetDatabase.SaveAssets();
            Debug.Log("Wired Attack3 slash VFX (" + SourceTexturePath + ") into LightAttack3's HitEffectOverride.");
        }

        // Mipmaps and wrap-repeat both bleed neighboring frames into each other at the edges
        // of a flipbook cell (mipmapping blurs across the whole sheet as it downsamples;
        // Repeat wrap can sample the next tile over at a UV seam) - standard flipbook-texture
        // settings turn both off. Alpha Is Transparency cleans up edge blending on this
        // source's real alpha channel (confirmed via `file` - genuine 8-bit RGBA, same as the
        // 17-frame source, unlike the alpha-less JPG sheet in between). The trailing 2 unused
        // grid cells (62 real frames in an 8x8=64 grid - see the source's own index.json)
        // don't need any special masking: they're plain solid-black cells in the sheet
        // itself, and black already renders as invisible under this material's Additive
        // blend (see CreateOrUpdateMaterial) regardless of whether playback ever reaches
        // them.
        //
        // maxTextureSize is set explicitly (not left on the importer's own default) because
        // the source sheet is unusually large for this kind of VFX texture - 10240x5760,
        // 27MB on disk - large enough that leaving the default unspecified would depend on
        // whatever this project's default happens to be rather than a deliberate choice.
        // 4096 keeps each 1280x720 cell readably sharp (~512x288 after the resize) without
        // shipping the full 27MB texture into the game.
        private static void ConfigureTextureImport()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(SourceTexturePath);
            if (importer == null)
            {
                Debug.LogError("Could not find texture importer at " + SourceTexturePath);
                return;
            }

            bool changed = false;
            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                changed = true;
            }

            if (importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                changed = true;
            }

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                changed = true;
            }

            if (importer.maxTextureSize != 4096)
            {
                importer.maxTextureSize = 4096;
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
            }
        }

        // Edits the persisted prefab asset directly via LoadPrefabContents/SaveAsPrefabAsset
        // (rather than the previous create-once-then-skip-if-exists version) so re-running
        // this tool after swapping which sheet SourceTexturePath points at actually takes -
        // 2026-08-13, this is exactly what just happened when the user tried a second sheet.
        private static GameObject CreateOrUpdatePrefab()
        {
            bool isNew = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null;
            GameObject root = isNew ? new GameObject("Attack3SlashEffect") : PrefabUtility.LoadPrefabContents(PrefabPath);

            ParticleSystem ps = root.GetComponent<ParticleSystem>();
            if (ps == null)
            {
                ps = root.AddComponent<ParticleSystem>();
            }

            ParticleSystem.MainModule main = ps.main;
            main.duration = Lifetime;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = Lifetime;
            main.startSpeed = 0f;
            // Non-uniform (startSize3D) instead of the single-float startSize the earlier
            // square-ish crescents used - see SizeWidth/SizeHeight's own comment for why a
            // 16:9 source needs this. Z left at 1 - irrelevant for a flat quad.
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(SizeWidth);
            main.startSizeY = new ParticleSystem.MinMaxCurve(SizeHeight);
            main.startSizeZ = new ParticleSystem.MinMaxCurve(1f);
            main.startColor = Color.white; // sheet is already colored - keep tint neutral
            // No random roll (was 0-2*PI) - 2026-08-13 real user report ("通常視角會從側面看
            // 才會有劍氣掃過去的畫面"): a random spin per hit is fine for an always-camera-
            // facing Billboard, but now that the quad is oriented to the attacker instead (see
            // renderer.alignment below), a random roll would make the crescent's "up" land
            // sideways/upside-down half the time instead of consistently reading as a
            // horizontal sweep.
            main.startRotation3D = false;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 4;
            main.stopAction = ParticleSystemStopAction.Destroy;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = false; // single particle spawns exactly at the emitter's position

            ParticleSystem.TextureSheetAnimationModule tsa = ps.textureSheetAnimation;
            tsa.enabled = true;
            tsa.mode = ParticleSystemAnimationMode.Grid;
            tsa.numTilesX = TilesX;
            tsa.numTilesY = TilesY;
            tsa.animation = ParticleSystemAnimationType.WholeSheet;
            tsa.cycleCount = 1;
            tsa.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));

            // 2026-08-13 real user report ("現在是正面呈現在角色面前，但通常視角會從側面看，
            // 才會有劍氣掃過去的畫面") - Billboard always turns to face the camera no matter
            // where it's standing, so from any angle (including side-on/strafing views from
            // TargetLockController) the slash always reads as a flat sticker held up in front
            // of the lens instead of a card standing in the world that the camera can see
            // from the side. Mesh + World alignment fixes the orientation to whatever rotation
            // Instantiate() is given (now the attacker's own rotation - see
            // PlayerCombat.ResolveActiveHit) instead of tracking the camera, so it holds still
            // in world space like a real sword-swing trail and shows correct depth/foreshortening
            // as the camera moves around it.
            ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = GetQuadMesh();
            renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.sharedMaterial = CreateOrUpdateMaterial();

            if (isNew)
            {
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Object.DestroyImmediate(root);
            }
            else
            {
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                PrefabUtility.UnloadPrefabContents(root);
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        // 2026-08-13 real bug report ("完全沒出現特效") - AssetDatabase.GetBuiltinExtraResource
        // <Mesh>("Quad.fbx"), used here originally, silently returns null in this Unity
        // version/context (confirmed via a diagnostic script - no exception, no console
        // error, just null), which left renderer.mesh unset while renderMode stayed on Mesh -
        // a ParticleSystemRenderer in Mesh mode with no mesh assigned renders nothing at all,
        // hence zero visible effect rather than a wrong-looking one. CreatePrimitive(Quad)'s
        // mesh is a completely different, more reliable route to the same built-in quad (a
        // real GameObject primitive, not a named-resource lookup) - instantiate-then-discard
        // just to read its MeshFilter.sharedMesh, which is safe to keep referencing after the
        // GameObject is destroyed since the mesh itself is a shared built-in asset, not owned
        // by that instance.
        private static Mesh GetQuadMesh()
        {
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temp);
            return mesh;
        }

        // 2026-08-13 real bug report ("動畫又看不見了") - Universal Render Pipeline/Particles/
        // Unlit's _SrcBlend/_DstBlend, force-set to One/One via script to get true Additive
        // (see the git history on this method for that whole saga), turned out not to STAY
        // set: that shader's custom ShaderGUI recalculates the real blend-state properties
        // from the _Blend dropdown enum every time the material gets revalidated (confirmed:
        // simply reopening the Editor was enough to silently flip them back to that dropdown's
        // real "Additive" mapping, SrcAlpha/One, not One/One) - there was no way found to make
        // a script-forced override survive that. Live2DAction/VFX/AdditiveUnlit (this
        // project's own shader, see its own header comment) sidesteps the whole problem: its
        // `Blend One One` is hardcoded directly in the pass, not a runtime property pair any
        // GUI logic can "helpfully" overwrite - so this material now only ever sets the two
        // properties that shader actually declares (_BaseMap, _BaseColor), nothing else to
        // fight.
        private static Material CreateOrUpdateMaterial()
        {
            Shader shader = Shader.Find("Live2DAction/VFX/AdditiveUnlit");
            if (shader == null)
            {
                Debug.LogError("Could not find Live2DAction/VFX/AdditiveUnlit shader.");
                return AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SourceTexturePath);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            bool isNew = material == null;
            if (isNew)
            {
                material = new Material(shader);
            }
            else if (material.shader != shader)
            {
                material.shader = shader; // migrate materials created before this shader existed
            }

            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", texture);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            if (isNew)
            {
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else
            {
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        private static void WireToLightAttack3(GameObject prefab)
        {
            var attackData = AssetDatabase.LoadAssetAtPath<AttackData>(LightAttack3Path);
            if (attackData == null)
            {
                Debug.LogError("Could not load AttackData at " + LightAttack3Path);
                return;
            }

            var so = new SerializedObject(attackData);
            so.FindProperty("hitEffectOverride").objectReferenceValue = prefab;
            // 2026-08-13 explicit user request ("打空氣時也有特效出來") - see
            // AttackData.AlwaysSpawnHitEffect's own comment for why this is scoped to
            // LightAttack3 specifically rather than a PlayerCombat-wide change.
            so.FindProperty("alwaysSpawnHitEffect").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
