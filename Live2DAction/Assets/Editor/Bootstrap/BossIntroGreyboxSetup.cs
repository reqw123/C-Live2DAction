using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.Events;
using Unity.Cinemachine;
using Live2DAction.Cutscene;
using Live2DAction.AI.Boss;

namespace Live2DAction.EditorTools
{
    // 追加92, 2026-09-01 — see Docs/BOSS_INTRO_EXPLORATION.md §7 ("轉正 checklist").
    //
    // Wires the boss-intro cutscene (built as a throwaway demo in 追加91) into the REAL
    // GreyboxTest.unity so walking up to 武士 plays the sword-raise演出 and then drops you
    // straight into the fight.
    //
    // Re-runnable and idempotent: every object/component it adds is torn down and rebuilt each run,
    // so tweak the constants and re-run. Operates on the CURRENTLY OPEN scene and refuses to run
    // unless that scene is GreyboxTest.
    //
    // What it builds in the scene:
    //   BossRoomTrigger            - BoxCollider trigger on the approach path (z≈4, OUTSIDE the
    //                                boss's alertRange=6 so the cutscene beats the auto-wake)
    //   BossIntroCutsceneRig        - own Camera + CinemachineBrain + 3 CinemachineCameras
    //                                (starts inactive; BossIntroManager toggles it)
    //   BossIntroManagerObject      - PlayableDirector + BossIntroManager
    //   + on 武士: BladeDrawVFX child, draw AudioSource, CinemachineImpulseSource,
    //     BossSignalReceiver, a Timeline SignalReceiver
    //   + a new Timeline: Assets/_Project/Timeline/BossIntro_Greybox.playable
    //
    // Reuses the demo's assets: Wushi_SwordJudgment_InPlace.anim / BladeDraw.signal / KatanaDraw.wav
    // / Attack3SlashEffect.prefab.
    internal static class BossIntroGreyboxSetup
    {
        private const string ExpectedScene = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string TimelinePath = "Assets/_Project/Timeline/BossIntro_Greybox.playable";
        private const string InPlaceClipPath = "Assets/_Project/Timeline/Wushi_SwordJudgment_InPlace.anim";
        private const string SignalPath = "Assets/_Project/Timeline/BladeDraw.signal";
        private const string DrawWavPath = "Assets/_Project/Audio/Skills/KatanaDraw.wav";
        private const string SlashPrefab = "Assets/_Project/VFX/Slash/Attack3SlashEffect.prefab";

        // Trigger sits closer to spawn than the boss's alertRange (6m). 武士 is at z=11, so z=4 is
        // 7m out - the cutscene fires one step before the boss would wake on its own.
        private static readonly Vector3 TriggerPos = new Vector3(0f, 1.6f, 4f);
        private static readonly Vector3 TriggerSize = new Vector3(30f, 4f, 1.0f);

        // 2026-09-01, user request - camera does a 2.5s close 360° orbit of the boss, then settles
        // front-on and the boss plays a full swing.
        private const float OrbitSeconds = 2.5f;
        private const float OrbitRadius = 4.6f;
        private const float OrbitHeight = 3.1f;
        private const float OrbitStartAngle = 200f;
        // The swing clip (Wushi_SwordJudgment_InPlace): full raise + downcut, slowed a touch, starting
        // just before the orbit ends so it launches as the camera settles.
        private const float SwingSpeed = 0.8f;
        private const float SwingStart = 2.3f;
        private const float SwingPortion = 0.40f; // clip nt 0..0.40 = ready -> downcut, skip the long tail

        [MenuItem("Tools/Live2DAction/[Boss Intro] Wire Into GreyboxTest")]
        public static void Wire()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[BossIntroGreyboxSetup] Exit Play Mode first.");
                return;
            }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ExpectedScene)
            {
                Debug.LogError($"[BossIntroGreyboxSetup] Open {ExpectedScene} first (active scene is '{scene.path}').");
                return;
            }

            // ---- locate the existing scene objects -------------------------------------------
            var wushi = GameObject.Find("武士");
            var player = GameObject.Find("Player");
            var mainCam = GameObject.Find("Main Camera");
            if (wushi == null || player == null || mainCam == null)
            {
                Debug.LogError("[BossIntroGreyboxSetup] Could not find 武士 / Player / Main Camera in the scene.");
                return;
            }
            var boss = wushi.GetComponent<BossStateMachine>();
            var bossAnim = wushi.GetComponentInChildren<Animator>();
            var cornerHud = GameObject.Find("PlayerCornerHud");

            var controls = CollectPlayerControls(player, mainCam);

            // ---- tear down a previous run ----------------------------------------------------
            DestroyIfExists("BossRoomTrigger");
            DestroyIfExists("BossIntroManagerObject");
            DestroyIfExists("BossIntroCutsceneRig");
            StripBossAdditions(wushi);

            // Meshy models ship degenerate SkinnedMeshRenderer bounds - a cutscene camera not aimed
            // at that phantom centre frustum-culls the boss out of the shot. Recompute per frame.
            foreach (var smr in wushi.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                smr.updateWhenOffscreen = true;
            }

            // ---- blade-flash VFX + draw SFX + impulse on 武士 --------------------------------
            var slashSrc = AssetDatabase.LoadAssetAtPath<GameObject>(SlashPrefab);
            GameObject slashGo = slashSrc != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(slashSrc)
                : new GameObject("BladeDrawVFX");
            slashGo.name = "BladeDrawVFX";
            slashGo.transform.SetParent(wushi.transform, false);
            slashGo.transform.localPosition = new Vector3(0f, 0.75f, 0.12f); // 武士 is scale 4 - local units are /4
            slashGo.transform.localRotation = Quaternion.identity;
            slashGo.transform.localScale = Vector3.one * 0.5f;
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

            var impulse = wushi.AddComponent<CinemachineImpulseSource>();
            impulse.DefaultVelocity = new Vector3(0f, -0.4f, 0.15f);

            var signalReceiverScript = wushi.AddComponent<BossSignalReceiver>();
            signalReceiverScript.EditorConfigure(bladeVfx, drawSfx, impulse);

            // ---- cutscene camera rig (starts inactive) --------------------------------------
            var rig = new GameObject("BossIntroCutsceneRig");
            var camGo = new GameObject("IntroCam");
            camGo.transform.SetParent(rig.transform);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.depth = 20f;
            camGo.AddComponent<AudioListener>();
            var brain = camGo.AddComponent<CinemachineBrain>();
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            camGo.AddComponent<CinemachineImpulseListener>();

            // 武士 at (0,0.6,11) facing -Z. Baked SkinnedMeshRenderer measure at scale 4: feet y≈0.6,
            // head y≈4.6 (~4m tall, chest ≈y2.6). 2026-09-01: single vcam whose transform is driven
            // by IntroOrbitCamera - a close 360° orbit for OrbitSeconds, then a settle to a front-on
            // shot it holds while the boss swings. Front pose = the old CM_Vcam_Face position.
            var vIntro = MakeVcam(rig, "CM_Vcam_Intro", new Vector3(-1.6f, 3.25f, 4.1f), Quaternion.Euler(5.6f, 13.1f, 0f), 44f);
            vIntro.Priority = 10;
            var orbit = vIntro.gameObject.AddComponent<Live2DAction.Cutscene.IntroOrbitCamera>();
            orbit.EditorConfigure(
                wushi.transform, OrbitSeconds, OrbitRadius, OrbitHeight, OrbitStartAngle,
                new Vector3(-1.6f, 3.25f, 4.1f),   // final front position (old Face shot)
                new Vector3(0f, 2.3f, 0f));         // look at ~chest

            rig.SetActive(false);

            // ---- Timeline ------------------------------------------------------------------
            var managerGo = new GameObject("BossIntroManagerObject");
            var director = managerGo.AddComponent<PlayableDirector>();
            var manager = managerGo.AddComponent<BossIntroManager>();

            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(timeline, TimelinePath);

            var signal = AssetDatabase.LoadAssetAtPath<SignalAsset>(SignalPath);
            var sjClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(InPlaceClipPath);

            // 2.5s orbit (boss idle) -> boss swings, front-on. Swing clip starts just before the
            // orbit ends and plays its raise+downcut portion, slowed.
            double swingDuration = sjClip != null ? (sjClip.length * SwingPortion) / SwingSpeed : 2.0;
            double totalDuration = SwingStart + swingDuration + 0.4; // small hold after the cut lands

            var animTrack = timeline.CreateTrack<AnimationTrack>(null, "武士 揮刀");
            animTrack.trackOffset = TrackOffset.ApplySceneOffsets;
            if (sjClip != null)
            {
                var tClip = animTrack.CreateClip(sjClip);
                tClip.start = SwingStart;
                tClip.timeScale = SwingSpeed;
                tClip.duration = swingDuration;
            }
            director.SetGenericBinding(animTrack, bossAnim);

            var cmTrack = timeline.CreateTrack<CinemachineTrack>(null, "Cinemachine");
            director.SetGenericBinding(cmTrack, brain);
            // One shot for the whole cutscene - IntroOrbitCamera does the actual movement.
            AddShot(cmTrack, director, vIntro, 0.0, totalDuration);

            SignalReceiver sigReceiver = null;
            if (signal != null)
            {
                var sigTrack = timeline.CreateTrack<SignalTrack>(null, "Signals");
                // Blade flash at the downcut - ~65% through the swing clip's played portion.
                double apex = SwingStart + swingDuration * 0.62;
                var emitter = sigTrack.CreateMarker<SignalEmitter>(apex);
                emitter.asset = signal;
                emitter.retroactive = false;
                emitter.emitOnce = true;

                sigReceiver = wushi.AddComponent<SignalReceiver>();
                var reaction = new UnityEvent();
                UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(reaction, signalReceiverScript.OnBladeDrawSignal);
                sigReceiver.AddReaction(signal, reaction);
                director.SetGenericBinding(sigTrack, sigReceiver);
            }

            director.playableAsset = timeline;
            director.playOnAwake = false;
            director.extrapolationMode = DirectorWrapMode.None;

            // ---- BossIntroManager wiring --------------------------------------------------
            manager.EditorConfigure(
                player,
                controls,
                cornerHud != null ? new[] { cornerHud } : new GameObject[0],
                boss,
                null,          // bossHealthBar: WushiBossHudVisibility already gates the HUD by state
                director,
                mainCam);      // gameplayCamera: SetActive-toggled

            var mgrSo = new SerializedObject(manager);
            mgrSo.FindProperty("cutsceneCameraRoot").objectReferenceValue = rig;
            mgrSo.ApplyModifiedPropertiesWithoutUndo();
            WirePersistentForceEngage(manager, boss);

            // ---- trigger -----------------------------------------------------------------
            var triggerGo = new GameObject("BossRoomTrigger");
            triggerGo.transform.position = TriggerPos;
            var box = triggerGo.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = TriggerSize;
            var trigger = triggerGo.AddComponent<BossTrigger>();
            trigger.EditorConfigure(manager, player.transform);

            // ---- save ------------------------------------------------------------------
            EditorUtility.SetDirty(timeline);
            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(wushi);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[BossIntroGreyboxSetup] Wired the boss intro into GreyboxTest.\n" +
                      $"Timeline: {TimelinePath}\n" +
                      "Play, walk north into BossRoomTrigger (z≈4). Cutscene plays, then 武士 engages.");
        }

        // --------------------------------------------------------------------------- helpers

        // onIntroComplete -> BossStateMachine.ForceEngage as a serialized persistent listener, so
        // it fires in a built player / a fresh editor session with no code involved.
        private static void WirePersistentForceEngage(BossIntroManager manager, BossStateMachine boss)
        {
            var field = typeof(BossIntroManager).GetField("onIntroComplete",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var unityEvent = field.GetValue(manager) as UnityEvent;
            if (unityEvent == null)
            {
                unityEvent = new UnityEvent();
                field.SetValue(manager, unityEvent);
            }

            // Drop any stale persistent calls from a previous run, then add the one we want.
            var so = new SerializedObject(manager);
            so.FindProperty("onIntroComplete.m_PersistentCalls.m_Calls").ClearArray();
            so.ApplyModifiedPropertiesWithoutUndo();

            UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(unityEvent,
                (UnityAction)boss.ForceEngage);
            EditorUtility.SetDirty(manager);
        }

        private static Behaviour[] CollectPlayerControls(GameObject player, GameObject mainCam)
        {
            var list = new System.Collections.Generic.List<Behaviour>();
            AddByType(list, player, "Live2DAction.Input.PlayerInputProvider");
            AddByType(list, player, "Live2DAction.Characters.CharacterMovement");
            AddByType(list, player, "Live2DAction.Combat.PlayerCombat");
            AddByType(list, player, "Live2DAction.Targeting.TargetLockController");
            AddByType(list, player, "Live2DAction.Combat.UltimateAbility");
            AddByType(list, player, "Live2DAction.Combat.PlayerGuard");
            AddByType(list, player, "Live2DAction.Combat.ExecutionAbility");
            AddByType(list, mainCam, "Live2DAction.CameraSystem.ThirdPersonCameraController");
            AddSceneObjectComponent(list, "CameraPossession", "Live2DAction.CameraSystem.CameraPossessionSwitcher");
            AddSceneObjectComponent(list, "ViewDirector", "Live2DAction.CameraSystem.ViewFocusDirector");
            AddSceneObjectComponent(list, "BugSpectator", "Live2DAction.CameraSystem.SpectatorCameraToggle");
            return list.ToArray();
        }

        private static void AddByType(System.Collections.Generic.List<Behaviour> list, GameObject go, string typeName)
        {
            var t = FindRuntimeType(typeName);
            if (t == null || go == null) return;
            var c = go.GetComponent(t) as Behaviour;
            if (c != null && !list.Contains(c)) list.Add(c);
        }

        private static void AddSceneObjectComponent(System.Collections.Generic.List<Behaviour> list, string goName, string typeName)
        {
            var go = GameObject.Find(goName);
            if (go != null) AddByType(list, go, typeName);
        }

        private static Type FindRuntimeType(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType(fullName))
                .FirstOrDefault(t => t != null);
        }

        private static void DestroyIfExists(string name)
        {
            var go = GameObject.Find(name);
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }

        // Removes ONLY what a previous run of this tool added - so re-running is clean but a
        // hand-added AudioSource / component on 武士 is left alone.
        private static void StripBossAdditions(GameObject wushi)
        {
            var old = wushi.transform.Find("BladeDrawVFX");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            foreach (var c in wushi.GetComponents<BossSignalReceiver>()) UnityEngine.Object.DestroyImmediate(c);
            foreach (var c in wushi.GetComponents<SignalReceiver>()) UnityEngine.Object.DestroyImmediate(c);
            foreach (var c in wushi.GetComponents<CinemachineImpulseSource>()) UnityEngine.Object.DestroyImmediate(c);
            var drawClip = AssetDatabase.LoadAssetAtPath<AudioClip>(DrawWavPath);
            foreach (var a in wushi.GetComponents<AudioSource>())
            {
                if (a.clip == drawClip) UnityEngine.Object.DestroyImmediate(a);
            }
        }

        private static void AddShot(CinemachineTrack track, PlayableDirector dir, CinemachineCamera vcam,
            double start, double duration)
        {
            var clip = track.CreateClip<CinemachineShot>();
            clip.start = start;
            clip.duration = duration;
            clip.blendInDuration = 0.0;
            var shot = (CinemachineShot)clip.asset;
            string exposed = "vcam_" + Guid.NewGuid().ToString("N");
            shot.VirtualCamera.exposedName = exposed;
            dir.SetReferenceValue(exposed, vcam);
        }

        private static CinemachineCamera MakeVcam(GameObject parent, string name, Vector3 pos, Quaternion rot, float fov)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform);
            go.transform.SetPositionAndRotation(pos, rot);
            var vcam = go.AddComponent<CinemachineCamera>();
            var lens = vcam.Lens;
            lens.FieldOfView = fov;
            vcam.Lens = lens;
            return vcam;
        }
    }
}
