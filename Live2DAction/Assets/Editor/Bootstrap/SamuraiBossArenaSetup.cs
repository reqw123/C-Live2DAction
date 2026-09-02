using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;
using Live2DAction.Cutscene;

namespace Live2DAction.EditorTools
{
    // 2026-09-01, /grill-with-docs exploration — see Docs/BOSS_INTRO_EXPLORATION.md.
    //
    // Re-runnable builder for the THROWAWAY samurai-boss-intro demo scene. Builds everything from
    // code (project convention - every scene has a builder): arena + lighting, minimal 武士 / Player
    // stand-ins, the 4 Cinemachine cameras, the intro Timeline (Animation + Cinemachine Shot +
    // Signal tracks), the BladeDraw SignalAsset + receiver wiring, a procedurally-synthesized
    // KatanaDraw.wav, and the BossTrigger -> BossIntroManager -> BossSignalReceiver chain.
    //
    // NOT added to Build Settings. The scene, the Timeline assets and KatanaDraw.wav all live under
    // _Project but are only referenced by SamuraiBossArena.unity.
    internal static class SamuraiBossArenaSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/SamuraiBossArena.unity";
        private const string TimelineDir = "Assets/_Project/Timeline";
        private const string TimelinePath = TimelineDir + "/BossIntro.playable";
        private const string SignalPath = TimelineDir + "/BladeDraw.signal";
        private const string DrawWavPath = "Assets/_Project/Audio/Skills/KatanaDraw.wav";
        private const string FloorMatPath = "Assets/_Project/VFX/SamuraiArenaFloor.mat";

        private const string WushiFbx = "Assets/_Project/Characters/Placeholder/Wushi/Wushi.fbx";
        private const string WushiController = "Assets/_Project/Characters/Placeholder/Wushi/Animator/Wushi.controller";
        private const string SwordJudgmentFbx = "Assets/_Project/Characters/Placeholder/Wushi/Animations/Wushi_SwordJudgment.fbx";
        private const string SlashPrefab = "Assets/_Project/VFX/Slash/Attack3SlashEffect.prefab";

        // 武士 stand-in scale (the real GreyboxTest boss is 4x; 2.2x reads imposing but still frames
        // cleanly in the tight cutscene shots).
        private const float BossScale = 2.2f;
        // ready-stance clip: ONLY the lift-to-overhead + the hold (nt 0..0.28 of SwordJudgment) -
        // past that the clip starts its downswing and the body crouches, which is not a "ready
        // stance". Slowed hard for cinematic weight.
        private const float ClipPortion = 0.28f;
        private const float ClipSpeed = 0.4f;

        [MenuItem("Tools/Live2DAction/[Exploration] Build Samurai Boss Arena")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Exit Play Mode first.");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Application.dataPath + "/../" + DrawWavPath));
            if (!AssetDatabase.IsValidFolder(TimelineDir))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Timeline");
            }
            EnsureDrawWav();
            Material floorMat = EnsureFloorMaterial();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---- arena + lighting ----------------------------------------------------------
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Arena";
            floor.transform.localScale = new Vector3(5f, 1f, 5f);
            floor.GetComponent<MeshRenderer>().sharedMaterial = floorMat;

            var lightRoot = new GameObject("Lighting");
            var dirGo = new GameObject("Directional Light");
            dirGo.transform.SetParent(lightRoot.transform);
            dirGo.transform.rotation = Quaternion.Euler(55f, -40f, 0f);
            var dir = dirGo.AddComponent<Light>();
            dir.type = LightType.Directional;
            dir.intensity = 0.25f;
            dir.color = new Color(0.7f, 0.75f, 0.9f);
            dir.shadows = LightShadows.Soft;

            var spotGo = new GameObject("Boss Spot Light");
            spotGo.transform.SetParent(lightRoot.transform);
            spotGo.transform.SetPositionAndRotation(new Vector3(0f, 9f * BossScale / 3f + 3f, 0f), Quaternion.Euler(90f, 0f, 0f));
            var spot = spotGo.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.range = 30f;
            spot.spotAngle = 55f;
            spot.intensity = 40f;
            spot.color = new Color(1f, 0.96f, 0.85f);
            spot.shadows = LightShadows.Soft;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.06f, 0.07f, 0.10f);

            // ---- 武士 stand-in ------------------------------------------------------------
            var wushiSrc = AssetDatabase.LoadAssetAtPath<GameObject>(WushiFbx);
            var wushi = (GameObject)PrefabUtility.InstantiatePrefab(wushiSrc);
            wushi.name = "武士";
            wushi.transform.position = Vector3.zero;
            wushi.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // face -Z (toward the player)
            wushi.transform.localScale = Vector3.one * BossScale;

            var wushiAnim = wushi.GetComponentInChildren<Animator>();
            if (wushiAnim == null)
            {
                wushiAnim = wushi.AddComponent<Animator>();
            }
            wushiAnim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(WushiController);
            wushiAnim.applyRootMotion = false;

            // Plant the feet on the floor + fix the Meshy model's degenerate SkinnedMeshRenderer
            // bounds. Those broken bounds (localBounds extents ~44 units, centred well below the
            // feet) frustum-cull the boss away the moment the cutscene camera isn't pointed at
            // that phantom centre - it vanished from every shot. updateWhenOffscreen recomputes
            // the real bounds from the skinned verts each frame (same fix GreyboxTest's own boss
            // and the campus FBX use).
            foreach (var wushiSmr in wushi.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                wushiSmr.updateWhenOffscreen = true;
                var baked = new Mesh();
                wushiSmr.BakeMesh(baked, true);
                float lowest = float.MaxValue;
                foreach (var v in baked.vertices)
                {
                    lowest = Mathf.Min(lowest, wushiSmr.transform.TransformPoint(v).y);
                }
                UnityEngine.Object.DestroyImmediate(baked);
                if (lowest < float.MaxValue)
                {
                    wushi.transform.position += new Vector3(0f, -lowest, 0f);
                }
            }

            var impulse = wushi.AddComponent<CinemachineImpulseSource>();
            impulse.DefaultVelocity = new Vector3(0f, -0.4f, 0.15f);

            // blade-flash VFX: an instance of the existing enemy-slash particle, parked on the boss.
            var slashSrc = AssetDatabase.LoadAssetAtPath<GameObject>(SlashPrefab);
            GameObject slashGo = slashSrc != null ? (GameObject)PrefabUtility.InstantiatePrefab(slashSrc) : new GameObject("BladeDrawVFX");
            slashGo.name = "BladeDrawVFX";
            slashGo.transform.SetParent(wushi.transform, false);
            slashGo.transform.localPosition = new Vector3(0f, 1.4f, 0.2f);
            slashGo.transform.localRotation = Quaternion.identity;
            var bladeVfx = slashGo.GetComponentInChildren<ParticleSystem>();
            if (bladeVfx != null)
            {
                var main = bladeVfx.main;
                main.playOnAwake = false;
                bladeVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            var drawSfx = wushi.AddComponent<AudioSource>();
            drawSfx.playOnAwake = false;
            drawSfx.spatialBlend = 0f;
            drawSfx.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(DrawWavPath);

            var signalReceiverScript = wushi.AddComponent<BossSignalReceiver>();
            signalReceiverScript.EditorConfigure(bladeVfx, drawSfx, impulse);

            // ---- Player stand-in --------------------------------------------------------
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.tag = "Player";
            UnityEngine.Object.DestroyImmediate(player.GetComponent<Collider>());
            player.transform.position = new Vector3(0f, 1f, -9f);
            var pcc = player.AddComponent<CharacterController>();
            pcc.height = 2f; pcc.radius = 0.4f; pcc.center = new Vector3(0f, 0f, 0f);
            var demoPlayer = player.AddComponent<DemoPlayerController>();
            player.GetComponent<MeshRenderer>().sharedMaterial = MakeColorMat("PlayerStandin", new Color(0.3f, 0.55f, 0.9f));

            // ---- stub AI / UI --------------------------------------------------------------
            var demoBossAiGo = new GameObject("DemoBossAI");
            demoBossAiGo.transform.position = wushi.transform.position;
            var demoBossAi = demoBossAiGo.AddComponent<DemoBossAI>();

            var bossHpBar = MakeCanvas("DemoBossHealthBar", new Vector2(0.5f, 0.92f), new Vector2(600f, 26f), new Color(0.7f, 0.1f, 0.1f, 0.9f), "武士");
            var playerUi = MakeCanvas("PlayerUI", new Vector2(0.12f, 0.10f), new Vector2(260f, 60f), new Color(0.15f, 0.6f, 1f, 0.85f), "PLAYER UI");

            // ---- cameras --------------------------------------------------------------------
            var camGo = new GameObject("CutsceneCamera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            camGo.AddComponent<AudioListener>();
            var brain = camGo.AddComponent<CinemachineBrain>();
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            camGo.AddComponent<CinemachineImpulseListener>();
            camGo.transform.SetPositionAndRotation(new Vector3(0f, 3f, -13f), Quaternion.Euler(8f, 0f, 0f));

            // 武士 stands at origin facing -Z (toward the player at z=-9). At BossScale 2.2, feet
            // planted: mesh centre ~y1.15, head ~y2.3. These 4 shots are a FIRST PASS - the boss is
            // framed and visible in each, but the cinematic composition is best fine-tuned by
            // dragging the vcams in the Scene view (this was always flagged as tuning, not design).
            var vBack = MakeVcam("CM_Vcam_Back", new Vector3(0.35f, 1.85f, 3.10f), Quaternion.Euler(2f, 186f, 0f), 48f);
            var vFace = MakeVcam("CM_Vcam_Face", new Vector3(-0.40f, 1.55f, -3.00f), Quaternion.Euler(-6f, 6f, 0f), 44f);
            var vAction = MakeVcam("CM_Vcam_Action", new Vector3(-6.00f, 2.30f, -4.60f), Quaternion.Euler(9f, 76f, 0f), 50f);
            var vGameplay = MakeVcam("CM_Vcam_Gameplay", new Vector3(0f, 2.10f, -11.50f), Quaternion.Euler(6f, 0f, 0f), 52f);
            vGameplay.Priority = 5;
            vBack.Priority = 10; vFace.Priority = 10; vAction.Priority = 10;

            // ---- Timeline -----------------------------------------------------------------
            var managerGo = new GameObject("BossIntroManagerObject");
            var director = managerGo.AddComponent<PlayableDirector>();
            var manager = managerGo.AddComponent<BossIntroManager>();

            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(timeline, TimelinePath);

            var signal = ScriptableObject.CreateInstance<SignalAsset>();
            AssetDatabase.CreateAsset(signal, SignalPath);

            var sjSrc = AssetDatabase.LoadAssetAtPath<AnimationClip>(SwordJudgmentFbx);
            // Wushi_SwordJudgment carries full root motion (lockRootPos* all false) and NO Timeline
            // trackOffset mode fully suppresses it - the boss walked +X/+Z and sank into the floor
            // over the cutscene. Bake a root-motion-stripped copy just for the intro (the shared
            // FBX / the GreyboxTest fight are untouched). Keeps the boss planted on its mark.
            var sjClip = EnsureInPlaceClip(sjSrc);
            double animDuration = sjClip != null ? (sjClip.length * ClipPortion) / ClipSpeed : 2.3;

            var animTrack = timeline.CreateTrack<AnimationTrack>(null, "武士 起手式");
            animTrack.trackOffset = TrackOffset.ApplySceneOffsets;
            if (sjClip != null)
            {
                var tClip = animTrack.CreateClip(sjClip);
                tClip.start = 0.0;
                tClip.timeScale = ClipSpeed;
                tClip.duration = animDuration;
            }
            director.SetGenericBinding(animTrack, wushiAnim);

            var cmTrack = timeline.CreateTrack<CinemachineTrack>(null, "Cinemachine");
            director.SetGenericBinding(cmTrack, brain);
            // Hard cuts between the three shots - punchy boss-intro style (Sekiro / Nioh), and it
            // makes the framing deterministic (no multi-second blend easing the camera in).
            // Back ~40% of the raise, cut to Face for the apex + hold, cut wide for the stand-off.
            double d0 = animDuration * 0.42;
            double d1 = animDuration * 0.40;
            double d2 = 1.8;
            AddShot(cmTrack, director, vBack, 0.0, d0, 0.0f);
            AddShot(cmTrack, director, vFace, d0, d1, 0.0f);
            AddShot(cmTrack, director, vAction, d0 + d1, d2, 0.0f);

            var sigTrack = timeline.CreateTrack<SignalTrack>(null, "Signals");
            // ~ blade fully raised - land it just after the cut to the Face shot.
            double apex = d0 + d1 * 0.35;
            var emitter = sigTrack.CreateMarker<SignalEmitter>(apex);
            emitter.asset = signal;
            emitter.retroactive = false;
            emitter.emitOnce = true;

            var receiver = wushi.AddComponent<SignalReceiver>();
            var reaction = new UnityEvent();
            UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(reaction, signalReceiverScript.OnBladeDrawSignal);
            receiver.AddReaction(signal, reaction);
            director.SetGenericBinding(sigTrack, receiver);

            director.playableAsset = timeline;
            director.playOnAwake = false;
            director.extrapolationMode = DirectorWrapMode.None;

            // ---- trigger + manager wiring -----------------------------------------------
            var triggerGo = new GameObject("BossRoomTrigger");
            triggerGo.transform.position = new Vector3(0f, 1f, -6f);
            var box = triggerGo.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(10f, 3f, 1.5f);
            var trigger = triggerGo.AddComponent<BossTrigger>();

            manager.EditorConfigure(
                player,
                new Behaviour[] { demoPlayer },
                new GameObject[] { playerUi },
                demoBossAi,
                bossHpBar,
                director,
                vGameplay.gameObject);

            var trigSo = new SerializedObject(trigger);
            trigSo.FindProperty("introManager").objectReferenceValue = manager;
            trigSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- save (NOT to build settings) -------------------------------------------
            EditorUtility.SetDirty(timeline);
            EditorUtility.SetDirty(signal);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log("[SamuraiBossArenaSetup] Built " + ScenePath + " (exploration - NOT in Build Settings).\n" +
                      "Timeline: " + TimelinePath + "  Signal: " + SignalPath + "  Draw SFX: " + DrawWavPath + "\n" +
                      "Play the scene and walk (WASD) into BossRoomTrigger.");
        }

        // --------------------------------------------------------------------------- helpers

        private const string InPlaceClipPath = TimelineDir + "/Wushi_SwordJudgment_InPlace.anim";

        // A copy of a Humanoid clip with only its HORIZONTAL root motion removed (RootT.x/RootT.z
        // + root yaw), so Timeline plays it planted on the mark. RootT.y is KEPT - a Humanoid clip
        // needs it for the body's height above the ground; stripping it collapses the pose. Rebuilt
        // every run.
        private static AnimationClip EnsureInPlaceClip(AnimationClip src)
        {
            if (src == null) return null;
            var dst = UnityEngine.Object.Instantiate(src);
            dst.name = Path.GetFileNameWithoutExtension(InPlaceClipPath);
            foreach (var b in UnityEditor.AnimationUtility.GetCurveBindings(dst))
            {
                if (b.path != string.Empty) continue;
                string p = b.propertyName;
                if (p == "RootT.x" || p == "RootT.z" || p == "MotionT.x" || p == "MotionT.z"
                    || p.StartsWith("RootQ.") || p.StartsWith("MotionQ."))
                {
                    UnityEditor.AnimationUtility.SetEditorCurve(dst, b, null);
                }
            }
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(InPlaceClipPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(dst, existing);
                UnityEngine.Object.DestroyImmediate(dst);
                return existing;
            }
            AssetDatabase.CreateAsset(dst, InPlaceClipPath);
            return dst;
        }

        private static void AddShot(CinemachineTrack track, PlayableDirector dir, CinemachineCamera vcam,
            double start, double duration, float blendIn)
        {
            var clip = track.CreateClip<CinemachineShot>();
            clip.start = start;
            clip.duration = duration;
            clip.blendInDuration = blendIn;
            var shot = (CinemachineShot)clip.asset;
            string exposed = "vcam_" + Guid.NewGuid().ToString("N");
            shot.VirtualCamera.exposedName = exposed;
            dir.SetReferenceValue(exposed, vcam);
        }

        private static CinemachineCamera MakeVcam(string name, Vector3 pos, Quaternion rot, float fov)
        {
            var go = new GameObject(name);
            go.transform.SetPositionAndRotation(pos, rot);
            var vcam = go.AddComponent<CinemachineCamera>();
            var lens = vcam.Lens;
            lens.FieldOfView = fov;
            vcam.Lens = lens;
            return vcam;
        }

        private static Material MakeColorMat(string name, Color c)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(sh) { name = name };
            m.SetColor("_BaseColor", c);
            return m;
        }

        private static Material EnsureFloorMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(FloorMatPath);
            if (existing != null) return existing;
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(sh) { name = "SamuraiArenaFloor" };
            m.SetColor("_BaseColor", new Color(0.05f, 0.05f, 0.06f));
            m.SetFloat("_Smoothness", 0.85f);
            m.SetFloat("_Metallic", 0.6f);
            AssetDatabase.CreateAsset(m, FloorMatPath);
            return m;
        }

        private static GameObject MakeCanvas(string name, Vector2 anchor, Vector2 size, Color color, string label)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            go.AddComponent<UnityEngine.UI.CanvasScaler>().uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            go.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            var panelGo = new GameObject("Panel", typeof(RectTransform));
            panelGo.transform.SetParent(go.transform, false);
            var rt = (RectTransform)panelGo.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
            var img = panelGo.AddComponent<UnityEngine.UI.Image>();
            img.color = color;

            var textGo = new GameObject("Label", typeof(RectTransform));
            textGo.transform.SetParent(panelGo.transform, false);
            var trt = (RectTransform)textGo.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
            var txt = textGo.AddComponent<UnityEngine.UI.Text>();
            txt.text = label;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.font = (Font)Resources.GetBuiltinResource(typeof(Font), "LegacyRuntime.ttf");
            txt.color = Color.white;
            return go;
        }

        // Procedurally-synthesized metallic "shing" placeholder (same hand-rolled WAV approach as
        // GunshotSfxSetup): a bright band-swept ring modulated by fast-decaying noise. Swap the file
        // for a real 拔刀 recording any time - the AudioSource just points at DrawWavPath.
        private static void EnsureDrawWav()
        {
            string full = Path.GetFullPath(Application.dataPath + "/../" + DrawWavPath);
            if (File.Exists(full)) return;

            const int sr = 44100;
            const float dur = 0.5f;
            int n = Mathf.RoundToInt(sr * dur);
            var s = new float[n];
            var rng = new System.Random(4242);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                // pitch sweeps DOWN from ~5kHz to ~1.6kHz over the first 0.18s (the ring after the draw)
                float k = Mathf.Clamp01(t / 0.18f);
                float freq = Mathf.Lerp(5000f, 1600f, k);
                float ring = Mathf.Sin(2f * Mathf.PI * freq * t);
                float ringEnv = Mathf.Exp(-t / 0.16f);
                // sharp metallic scrape transient at the very front
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float scrape = noise * Mathf.Exp(-t / 0.012f);
                // a shimmering high partial
                float shimmer = Mathf.Sin(2f * Mathf.PI * (freq * 2.02f) * t) * ringEnv * 0.35f;
                s[i] = ring * ringEnv * 0.7f + scrape * 0.6f + shimmer;
            }
            float peak = 0f;
            foreach (var v in s) peak = Mathf.Max(peak, Mathf.Abs(v));
            if (peak > 0f) for (int i = 0; i < n; i++) s[i] = s[i] / peak * 0.9f;

            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllBytes(full, EncodeWav16(s, sr));
            AssetDatabase.ImportAsset(DrawWavPath);
        }

        private static byte[] EncodeWav16(float[] samples, int sr)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            int dataSize = samples.Length * 2;
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + dataSize);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            w.Write(16); w.Write((short)1); w.Write((short)1);
            w.Write(sr); w.Write(sr * 2); w.Write((short)2); w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            w.Write(dataSize);
            foreach (var f in samples)
                w.Write((short)Mathf.Clamp(f * short.MaxValue, short.MinValue, short.MaxValue));
            return ms.ToArray();
        }
    }
}
