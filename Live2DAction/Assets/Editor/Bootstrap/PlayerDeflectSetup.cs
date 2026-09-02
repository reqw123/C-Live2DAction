using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.Combat;
using Live2DAction.Combat.Boss;

namespace Live2DAction.EditorTools
{
    // 2026-09-01, user request - Sekiro-style deflect, Phase 1b. Wires the blade-clash path into
    // GreyboxTest:
    //   - Player: PlayerHurtbox -> "PlayerHurtbox" layer; new GuardVolume child (PlayerGuardVolume,
    //     "PlayerGuardWeapon" layer) anchored at the sword hand + extended toward the facing;
    //     PlayerGuard tuned to spec
    //     (guardArcDegrees 120, parryWindowDuration 0.12); PlayerGuardAnimatorLink + NewAnimator
    //     "IsGuarding" bool.
    //   - Boss: BladeHitbox -> "BossWeapon" layer.
    // Re-runnable. Nothing here changes damage numbers - just the routing + layers.
    internal static class PlayerDeflectSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string ClashClipPath = "Assets/_Project/Audio/Combat/KatanaClash.mp3";
        private const string GuardClipPath = "Assets/_Project/Characters/Placeholder/CombatAnimations/Meshy/Guard.fbx";
        // The sword-hand weapon mount - the guard volume anchors its near end here (follows the arm)
        // and extends toward the player's facing.
        private const string HandAnchorPath =
            "Visual/player_004_lacrimosa_skin_LOD1_Skeleton/root/Bip001/Bip001-Pelvis/Bip001-Spine/Bip001-Spine1/Bip001-Spine2/Bip001-R-Clavicle/Bip001-R-UpperArm/Bip001-R-Forearm/Bip001-R-Hand/Bip001-Prop1/Rhand_Weapon2/WolfsGravestone";

        [MenuItem("Tools/Live2DAction/Wire Sekiro Deflect Into GreyboxTest")]
        public static void Wire()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Exit Play Mode first - this touches the scene + an AnimatorController.");
                return;
            }

            int guardWeaponLayer = LayerMask.NameToLayer("PlayerGuardWeapon");
            int hurtboxLayer = LayerMask.NameToLayer("PlayerHurtbox");
            int bossWeaponLayer = LayerMask.NameToLayer("BossWeapon");
            if (guardWeaponLayer < 0 || hurtboxLayer < 0 || bossWeaponLayer < 0)
            {
                Debug.LogError("PlayerDeflectSetup: layers PlayerGuardWeapon / PlayerHurtbox / BossWeapon missing from TagManager.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject player = GameObject.Find("Player");
            GameObject wushi = GameObject.Find("武士");
            if (player == null || wushi == null)
            {
                Debug.LogError("PlayerDeflectSetup: Player / 武士 not found in " + ScenePath);
                return;
            }

            var guard = player.GetComponent<PlayerGuard>();
            if (guard == null)
            {
                Debug.LogError("PlayerDeflectSetup: Player has no PlayerGuard - run 'Add Player Katana Guard' first.");
                return;
            }

            // ---- Player hurtbox layer ----------------------------------------------------
            Transform hurtbox = player.transform.Find("PlayerHurtbox");
            if (hurtbox != null)
            {
                hurtbox.gameObject.layer = hurtboxLayer;
            }
            else
            {
                Debug.LogWarning("PlayerDeflectSetup: no PlayerHurtbox child - body hits will land on the Player root Health only.");
            }

            // ---- Boss blade hitbox layer ----------------------------------------------------
            foreach (var hb in wushi.GetComponentsInChildren<BossHitbox>(true))
            {
                if (hb.name == "BladeHitbox")
                {
                    hb.gameObject.layer = bossWeaponLayer;
                }
            }

            // ---- Player guard volume ----------------------------------------------------
            Transform handAnchor = player.transform.Find(HandAnchorPath);
            if (handAnchor == null)
            {
                Debug.LogError("PlayerDeflectSetup: could not find the katana hand mount at " + HandAnchorPath);
                return;
            }

            Transform existing = player.transform.Find("GuardVolume");
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }
            var volGo = new GameObject("GuardVolume");
            volGo.transform.SetParent(player.transform, false);
            volGo.transform.localScale = Vector3.one; // stays unscaled - the katana bone is ~80x
            volGo.layer = guardWeaponLayer; // the dedicated GuardWeapon layer
            var capsule = volGo.AddComponent<CapsuleCollider>();
            capsule.isTrigger = true;
            var vol = volGo.AddComponent<PlayerGuardVolume>();
            // Guard coverage capsule (GuardWeapon layer) - a generous volume in front of + above
            // the player because the 4x boss's blade attacks connect marginally even against the
            // body. It STILL rotates the visible katana forward-up while guarding (rotateWeapon).
            vol.EditorConfigure(guard, handAnchor, 0.45f);

            // ---- PlayerGuard tuning to spec ----------------------------------------------
            var gso = new SerializedObject(guard);
            SetFloat(gso, "guardArcDegrees", 120f);
            SetFloat(gso, "parryWindowDuration", 0.2f);     // Sekiro ~12 frames @60; anti-mash scales it down
            SetFloat(gso, "tapGuardWindowSeconds", 0.55f);  // mistimed tap still soft-blocks (posture cost)
            SetFloat(gso, "clashCooldownSeconds", 0.06f);
            SetFloat(gso, "mashResetSeconds", 0.35f);
            SetFloat(gso, "mashShrinkPerTap", 0.4f);
            SetFloat(gso, "mashRecoverPerSecond", 1.2f);
            SetFloat(gso, "guardPoiseMultiplier", 1f);      // spec item 6: guard costs the attack's own poise x this
            // Camera shake: none on a plain guard (jitter), a small kick on a parry (user: "攝影機視角有點晃").
            SetFloat(gso, "guardShakeAmplitude", 0f);
            SetFloat(gso, "parryShakeAmplitude", 0.06f);
            SetFloat(gso, "parryShakeSeconds", 0.16f);
            SetBool(gso, "useProceduralPose", false); // Phase 3: the Guard clip drives the pose now
            gso.ApplyModifiedPropertiesWithoutUndo();

            // ---- Animator: IsGuarding bool + GuardParry trigger + Guard / GuardParry states ----
            var animator = player.GetComponentInChildren<Animator>();
            if (animator != null && animator.runtimeAnimatorController is AnimatorController ac)
            {
                EnsureParam(ac, "IsGuarding", AnimatorControllerParameterType.Bool);
                EnsureParam(ac, "GuardParry", AnimatorControllerParameterType.Trigger);

                AnimationClip guardClip = null;   // full raise->settle clip (for the parry flash)
                AnimationClip guardHoldClip = null; // short looping "hands up" (for the held guard)
                foreach (var a in AssetDatabase.LoadAllAssetsAtPath(GuardClipPath))
                {
                    var cc = a as AnimationClip;
                    if (cc == null || cc.name.StartsWith("__preview")) continue;
                    if (cc.name == "GuardHold") guardHoldClip = cc;
                    else if (cc.name == "Guard") guardClip = cc;
                }
                if (guardClip != null)
                {
                    WireGuardStates(ac, guardClip, guardHoldClip != null ? guardHoldClip : guardClip);
                }
                else
                {
                    Debug.LogWarning("PlayerDeflectSetup: Guard.fbx clip not found at " + GuardClipPath + " - Animator guard states skipped (procedural pose stays).");
                    SetBool(new SerializedObject(guard), "useProceduralPose", true);
                }

                var link = player.GetComponent<PlayerGuardAnimatorLink>();
                if (link == null)
                {
                    link = player.AddComponent<PlayerGuardAnimatorLink>();
                }
                link.EditorConfigure(guard, animator);
            }

            // ---- Phase 2: clash feedback (sparks + SFX at the contact point) ---------------
            Transform oldSfx = player.transform.Find("GuardClashSfx"); // retired PlayerGuardClashSfx
            if (oldSfx != null)
            {
                Object.DestroyImmediate(oldSfx.gameObject);
            }
            Transform oldFeedback = player.transform.Find("ClashFeedback");
            if (oldFeedback != null)
            {
                Object.DestroyImmediate(oldFeedback.gameObject);
            }

            var fbGo = new GameObject("ClashFeedback");
            fbGo.transform.SetParent(player.transform, false);
            var src = fbGo.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 1f;
            src.dopplerLevel = 0f;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = 3f;
            src.maxDistance = 45f;

            ParticleSystem guardPs = MakeSparkPs(fbGo.transform, "GuardSparks",
                new Color(1f, 0.92f, 0.55f), burstMin: 8, burstMax: 12, speed: 3.5f, size: 0.09f, life: 0.18f);
            ParticleSystem parryPs = MakeSparkPs(fbGo.transform, "ParrySparks",
                new Color(1f, 1f, 0.95f), burstMin: 22, burstMax: 30, speed: 6f, size: 0.13f, life: 0.28f);

            AudioClip clashClip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClashClipPath);
            var feedback = fbGo.AddComponent<PlayerClashFeedback>();
            feedback.EditorConfigure(guard, guardPs, parryPs, src, clashClip, clashClip);

            // ---- Phase 2: debug overlay --------------------------------------------------
            var dbg = player.GetComponent<SekiroDeflectDebug>();
            if (dbg == null)
            {
                dbg = player.AddComponent<SekiroDeflectDebug>();
            }
            dbg.EditorConfigure(guard, vol, wushi.transform);

            EditorUtility.SetDirty(guard);
            EditorUtility.SetDirty(feedback);
            EditorUtility.SetDirty(dbg);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("PlayerDeflectSetup: deflect wired. GuardVolume = a guard-coverage capsule on " +
                      "the GuardWeapon layer (generous - the 4x boss's blade attacks connect " +
                      "marginally even vs the body); while guarding it rotates the visible katana " +
                      "forward-up. Blade + kicks parryable. PlayerHurtbox->PlayerHurtbox, " +
                      "BladeHitbox->BossWeapon. ClashFeedback + SekiroDeflectDebug (F9). " +
                      "Guard/GuardParry Animator states from Guard.fbx.");
        }

        // A one-shot world-space spark burst, same procedural approach as HitEffectSetup.
        private static ParticleSystem MakeSparkPs(Transform parent, string name, Color color,
            int burstMin, int burstMax, float speed, float size, float life)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 0.4f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = life;
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;
            main.gravityModifier = 0.3f;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burstMin, (short)burstMax) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.04f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 1f); // additive
                mat.SetColor("_BaseColor", Color.white);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                renderer.sharedMaterial = mat;
            }
            return ps;
        }

        private static void SetFloat(SerializedObject so, string prop, float value)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.floatValue = value;
        }

        private static void SetBool(SerializedObject so, string prop, bool value)
        {
            var p = so.FindProperty(prop);
            if (p != null) { p.boolValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        private static void EnsureParam(AnimatorController ac, string name, AnimatorControllerParameterType type)
        {
            foreach (var p in ac.parameters)
            {
                if (p.name == name) return;
            }
            ac.AddParameter(name, type);
        }

        // Phase 3: adds a Player-only Guard + GuardParry state pair to the shared NewAnimator,
        // driven from PlayerGuardAnimatorLink (IsGuarding bool / GuardParry trigger). 中立者1 /
        // 守望者 also use this controller but never set those params, so they're unaffected.
        //
        // The Guard.fbx clip is 2.0s (neutral -> raise -> peak hold -> slowly lower). A deflect is
        // instant and a held guard should keep the blade UP, so:
        //   Guard      : a SHORT LOOPING sub-clip (GuardHold, frames 7-15 = the raised two-hand
        //                hold). Held while IsGuarding, no exit time. Hands stay up until release
        //                (user: "一直按著沒鬆開就把手舉著不用放下來").
        //   GuardParry : the FULL clip, speed 2.4 (the raise happens in ~0.14s ≈ the parry window),
        //                one-shot, exits back to Locomotion at 35% so it doesn't linger.
        private static void WireGuardStates(AnimatorController ac, AnimationClip guardClip, AnimationClip guardHoldClip)
        {
            var sm = ac.layers[0].stateMachine;

            AnimatorState guard = FindOrAddState(sm, "Guard", guardHoldClip);
            guard.speed = 1f;
            guard.cycleOffset = 0f;
            guard.motion = guardHoldClip;

            AnimatorState parry = FindOrAddState(sm, "GuardParry", guardClip);
            parry.speed = 2.4f;
            parry.cycleOffset = 0f;
            parry.motion = guardClip;

            // Clear any prior transitions we made so re-runs don't stack duplicates.
            RemoveAnyStateTransitionsTo(sm, guard);
            RemoveAnyStateTransitionsTo(sm, parry);
            guard.transitions = new AnimatorStateTransition[0];
            parry.transitions = new AnimatorStateTransition[0];

            AnimatorState locomotion = null;
            foreach (var s in sm.states)
            {
                if (s.state.name == "Locomotion") { locomotion = s.state; break; }
            }

            // AnyState -> Guard while IsGuarding is true (don't self-interrupt). 2026-09-01 (spec
            // item 2 §3.4): 0.14s -> 0.05s so the blade is up almost immediately - a 0.14s blend
            // left most of the 0.2s parry window still mid-raise, which reads as "I guarded but it
            // didn't deflect".
            var toGuard = sm.AddAnyStateTransition(guard);
            toGuard.hasExitTime = false;
            toGuard.duration = 0.05f;
            toGuard.canTransitionToSelf = false;
            toGuard.AddCondition(AnimatorConditionMode.If, 0f, "IsGuarding");

            // AnyState -> GuardParry on the trigger (quick, beats Guard).
            var toParry = sm.AddAnyStateTransition(parry);
            toParry.hasExitTime = false;
            toParry.duration = 0.03f;
            toParry.canTransitionToSelf = true;
            toParry.AddCondition(AnimatorConditionMode.If, 0f, "GuardParry");

            // Guard -> Locomotion when the button is released.
            if (locomotion != null)
            {
                var guardOut = guard.AddTransition(locomotion);
                guardOut.hasExitTime = false;
                guardOut.duration = 0.15f;
                guardOut.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsGuarding");

                // GuardParry -> Locomotion after the flash (AnyState->Guard re-catches it if still guarding).
                var parryOut = parry.AddTransition(locomotion);
                parryOut.hasExitTime = true;
                parryOut.exitTime = 0.35f;
                parryOut.duration = 0.12f;
            }

            EditorUtility.SetDirty(ac);
        }

        private static AnimatorState FindOrAddState(AnimatorStateMachine sm, string name, Motion motion)
        {
            foreach (var s in sm.states)
            {
                if (s.state.name == name) return s.state;
            }
            return sm.AddState(name);
        }

        private static void RemoveAnyStateTransitionsTo(AnimatorStateMachine sm, AnimatorState dest)
        {
            foreach (var t in sm.anyStateTransitions)
            {
                if (t.destinationState == dest)
                {
                    sm.RemoveAnyStateTransition(t);
                }
            }
        }
    }
}
