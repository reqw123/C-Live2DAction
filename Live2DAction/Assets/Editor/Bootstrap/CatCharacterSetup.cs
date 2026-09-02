using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.CameraSystem;
using Live2DAction.Characters;
using Live2DAction.Combat;
using Live2DAction.Core;
using Live2DAction.Input;
using Live2DAction.Targeting;

namespace Live2DAction.EditorTools
{
    // 2026-08-28, explicit user request ("導入這個模型，這是一隻貓，並請向玩家一樣提供他攝影機視角並且
    // 可將視角切換到他身上，注意他視線較低，與先前攝影機風格不同").
    //
    // Adds a "Cat" character to GreyboxTest at the 本地 spawn area, with:
    //   - a CharacterController + the SAME CharacterMovement the player uses (rule 8: player and AI
    //     share one input interface - the cat is player-controlled when possessed) + its own
    //     PlayerInputProvider. No dodge/stance/health/flight/lock-on wired - a bare movement rig.
    //     The .glb has NO animation clips (Meshy auto-rig, bind pose only), so the cat slides while
    //     moving - accepted placeholder behaviour, see Docs/KNOWN_ISSUES.md.
    //   - a dedicated "CatCamera": a copy of Main Camera's whole rig (Camera + URP data +
    //     ThirdPersonCameraController), retargeted to the cat and RE-TUNED for its low eyeline -
    //     lower targetOffset, shorter distance, a downward initialPitch and a pitch range that
    //     mostly looks DOWN at the ground creature. Distinct from the player's near-level
    //     over-the-shoulder view ("與先前攝影機風格不同").
    //   - a "CameraPossession" object with CameraPossessionSwitcher: C key (+ code API) swaps which
    //     character you see and control. Mirrors VehicleEntrySystem's SetActive camera swap.
    //
    // Re-runnable: destroys and rebuilds Cat / CatCamera / CameraPossession each time; the player
    // rig and everything else in the scene is untouched.
    internal static class CatCharacterSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string CatModelPath = "Assets/_Project/Characters/Cat/Cat.glb";

        // The raw .glb mesh is ~1.0 wide x 1.7 tall x 3.7 long (measured via BakeMesh - the
        // SkinnedMeshRenderer's own serialized bounds are degenerate, a known glTF import hazard in
        // this project, see the Torii notes in KNOWN_ISSUES; updateWhenOffscreen=true below is the
        // fix). 0.45 brings it to ~0.77 tall / eyeline ~0.45 - clearly a low creature next to the
        // player's ~1.08 eyeline, which is the whole reason the camera style differs.
        private const float CatScale = 0.45f;
        private const float CatControllerHeight = 0.76f;   // ~1.7 * CatScale
        private const float CatControllerRadius = 0.2f;
        private const float CatControllerSkin = 0.02f;

        // A clear spot on the 30x30 Ground, ~2m ahead (+Z, the Main Camera's initialYaw 0 facing) of
        // the player's spawn at (-2.5, _, 0) so the cat is on screen the moment Play starts.
        private static readonly Vector2 CatSpawnXz = new Vector2(-2.5f, 2.0f);

        private const float CatMoveSpeed = 3f;

        // 2026-08-29, explicit user request ("讓貓就有飛行和衝刺功能 參考player"). Flight, flight-boost
        // and ground-dash are ALL already implemented (null-safe, opt-in via serialized fields) in
        // the SAME CharacterMovement the cat already uses - the cat's original 2026-08-28 build
        // deliberately left them unwired ("dodgeData / ... / flightEnergy all left null"). This
        // wires the two missing pieces the player has, exactly as FlightSetup + FixDodgeSetup do
        // for the player: a flight-energy pool (UltimateEnergy, reused generically) + a DodgeData
        // asset. Input keys are already shared (same PlayerInputProvider): hold Ctrl to fly, hold
        // Shift to descend, hold Q to boost, tap Shift to dash. Possession already gates all of it -
        // CameraPossessionSwitcher enables/disables the cat's CharacterMovement.
        //
        // Numbers mirror the player's GreyboxTest-tuned values EXCEPT flight horizontal/vertical
        // speed, scaled down for a 0.45-scale creature whose ground speed is already 3 - eyeballed
        // starting points like the camera block above, hand-tune in Play.
        private const float CatFlightMaxEnergy = 500f;       // player: 500
        private const float CatFlightRegenAmount = 30f;      // player: 30
        private const float CatFlightRegenInterval = 1f;     // player: 1
        private const float CatFlightRegenIdleDelay = 3f;    // player: 3 (flight-energy instance only)
        private const float CatFlightMoveSpeed = 7f;         // player: 9
        private const float CatFlightAscendSpeed = 5f;       // player: 6
        private const float CatFlightDescendSpeed = 5f;      // player: 6
        // 2026-08-29, user report ("貓咪視角時 空中沒法衝刺"): at the old 1.6 the dash speed
        // (1.6 / 0.2s = 8) barely beat the cat's flight cruise (flightMoveSpeed 7 = 1.14x) so an
        // air-dash read as no dash at all - the player's 3 / 0.2s = 15 vs flight 9 is 1.67x. Matched
        // to the player's 3 so the dash clearly overtakes cruise speed in the air and on the ground.
        private const float CatDodgeDistance = 3f;           // player: 3
        private const int CatDodgeDurationFrames = 12;       // player: 12 (frame timing unchanged)
        private const int CatDodgeInvulnerabilityFrames = 12;
        private const int CatDodgeCooldownFrames = 20;
        private const string CatDodgeDataPath = "Assets/_Project/Settings/Movement/Cat/CatDodgeData.asset";

        // 2026-08-29, cat combat design (Docs/CAT_COMBAT_DESIGN.md, slices 2-1..2-7). The cat's
        // whole melee stack, wired by WireCombat() below. All frame data / damage / knockback is
        // in AttackData assets under CombatFolder (rule 7); the numbers here are eyeballed
        // starting points scaled for a 0.45-scale ~3-ground-speed creature - hand-tune in Play,
        // re-run the menu to re-apply (assets are re-written every run, like CatDodgeData).
        private const string CombatFolder = "Assets/_Project/Settings/Combat/Cat";
        private const float CatMaxHealth = 200f;
        // 2026-08-31, user request ("為貓咪補上...架式條...接真機制") - the cat now carries the same
        // Souls-like poise bar the player/enemy do (StancePoise is drop-in, accumulates from any
        // incoming hit via Health.Damaged). Lighter than the player's 60 - a smaller creature
        // breaks sooner; the cat has no stagger clip (Meshy rig, no animation) so the stagger is a
        // ~4s no-act window, kept short on purpose.
        private const float CatMaxStance = 50f;
        private const float CatStaggerSeconds = 4f;
        // 2026-08-31 - the cat's ultimate ("dark sword-qi") skill-energy meter. Separate from the
        // flight-energy instance; 100 at 5/1s = 20s to full, same cadence as the player's ultimate.
        private const float CatSkillEnergyMax = 100f;
        private const float CatAttackOriginForward = 0.38f;  // mouth/paw reach ahead of the cat's centre
        private const float CatAttackOriginUp = 0.10f;
        // swipe1 / swipe2 / swipe3 : damage, startup, active, recovery, comboWindow frames, range, radius
        private static readonly float[] Swipe1 = { 6f, 5f, 3f, 12f, 9f, 1.1f, 0.55f };
        private static readonly float[] Swipe2 = { 7f, 6f, 3f, 13f, 9f, 1.1f, 0.55f };
        private static readonly float[] Swipe3 = { 12f, 9f, 4f, 20f, 0f, 1.3f, 0.7f };
        // heavy : damage, startup, active, recovery, comboWindow, range, radius, knockbackForce
        private static readonly float[] Heavy = { 22f, 16f, 5f, 26f, 0f, 1.5f, 0.85f, 6f };
        // pounce : damage, startup, active, recovery, comboWindow, range, radius, knockbackForce
        private static readonly float[] Pounce = { 16f, 4f, 4f, 18f, 0f, 1.2f, 0.8f, 7f };

        // Low-eyeline camera tuning (see class comment). Player camera for reference:
        // distance 2, targetOffset (0.5, 0.5, 0), minPitch -40, maxPitch 70, initialPitch 0.
        // These were eyeballed against a Play-mode render of the cat at CatScale 0.45 - the user
        // hand-tunes camera feel in this project (see the ThirdPersonCameraController field comments),
        // so treat them as a starting point, not a final answer.
        private const float CatCamDistance = 1.9f;
        private static readonly Vector3 CatCamTargetOffset = new Vector3(0f, 0.5f, 0f);
        private const float CatCamInitialPitch = 11f;   // start looking gently DOWN at the cat
        private const float CatCamMinPitch = -12f;       // barely look up - you're watching a ground creature
        private const float CatCamMaxPitch = 70f;

        [MenuItem("Tools/Live2DAction/Add Cat Character + Camera")]
        public static void Apply()
        {
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CatModelPath);
            if (modelAsset == null)
            {
                Debug.LogError("Cat model not found at " + CatModelPath + " - is the .glb imported?");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("Player GameObject not found in " + ScenePath);
                return;
            }
            Behaviour[] playerControl = CollectPlayerControl(player);

            GameObject mainCamera = GameObject.Find("Main Camera");
            if (mainCamera == null)
            {
                Debug.LogError("'Main Camera' not found - cannot clone the camera rig for the cat.");
                return;
            }

            GameObject ground = GameObject.Find("Ground");
            float groundTopY = ground != null && ground.GetComponent<Collider>() != null
                ? ground.GetComponent<Collider>().bounds.max.y
                : 0.5f;

            // Drop the old Cat's RespawnController before BuildCat destroys the Cat under it.
            GameObject existingCat = GameObject.Find("Cat");
            if (existingCat != null)
            {
                RespawnWiring.RemoveRespawnController(existingCat);
            }

            GameObject cat = BuildCat(modelAsset, groundTopY);
            GameObject catCamera = BuildCatCamera(mainCamera, cat.transform);

            // The cat's movement reads the CAT camera's orbital yaw (never its own facing - see
            // ICameraYawSource), same wiring the player has to Main Camera's controller.
            var catMovement = cat.GetComponent<CharacterMovement>();
            var catMovementSo = new SerializedObject(catMovement);
            catMovementSo.FindProperty("cameraYawSource").objectReferenceValue =
                catCamera.GetComponent<ThirdPersonCameraController>();
            catMovementSo.ApplyModifiedPropertiesWithoutUndo();
            // Starts disabled - the game opens on the player; the switcher enables it on the swap.
            catMovement.enabled = false;

            // 2026-08-29, cat combat (slice 2). Camera shake on both cameras; the cat's combat
            // feedback only ever pokes the CatCamera one. Scene-wide hitstop controller lives on
            // CameraPossession (always-active, not a camera).
            var catCameraShake = EnsureComponent<CameraShake>(catCamera);
            EnsureComponent<CameraShake>(mainCamera);

            var catControl = CollectCatControl(cat, catMovement);
            BuildSwitcher(mainCamera, catCamera, playerControl, catControl);
            EnsureHitStopController();
            WireCombatFeedbackRefs(cat, catCameraShake); // after BuildSwitcher created the switcher

            // 2026-08-29, user request ("貓咪死後5秒復活"). Same in-place 5s respawn every other
            // character in this scene uses. NOTE: while you're possessing the cat and it dies,
            // control/view is frozen for those 5s (same tradeoff Player's own RespawnController
            // already makes - simplest death handling, no game-over screen).
            RespawnWiring.EnsureRespawnController(cat, cat.GetComponent<Health>(), 5f);

            // 2026-08-29, user request ("讓 player 守望者/cat 三者可以互相切換視角") - link the cat into
            // the Watcher director so T works from the cat too. No-op if WatcherSetup hasn't run.
            WatcherCatWiring.Wire();

            // 2026-08-29, user request ("讓貓咪也可以使用車輛 F功能 以及模型塞進車裡") - link the cat
            // into the on-foot VehicleEntrySystem so F drives the car while possessing the cat.
            // No-op if the vehicle isn't in the scene.
            VehicleCatWiring.Wire();

            // 2026-08-31, user request ("為貓咪補上三個血量條 能量條 架式條" - "只在操控貓時顯示") -
            // builds CatCornerHud (生命/能量/架式) from the player's HUD and a PossessionHud that
            // swaps it in while you're the cat. No-op if PlayerCornerHud isn't in the scene.
            CatBarsWiring.Wire();

            // 2026-08-31, user request ("讓 cat 能量滿格時可以施放...這個技能特效") - fill in the cat's
            // ultimate: castVfxPrefab (the baked dark-sword-qi flipbook), the CatDarkQi AttackData,
            // re-point the CatCornerHud 能量 bar at the skill meter, add CatUltimateAbility to
            // catControl. No-op for the VFX ref if the atlas/prefab haven't been baked yet - run
            // 'Tools/Live2DAction/Add Cat Dark Sword-Qi Skill' once for that.
            CatDarkQiVfxSetup.Wire();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("Added Cat + CatCamera + CameraPossession + melee combat (PlayerCombat, " +
                      "CatSwipe1/2/3 + CatHeavy + CatPounce assets, pose/charge/pounce/aerial/feedback). " +
                      "Press C in Play to swap; left-click swipe, hold = heavy, move+click = pounce.");
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            // Explicit == null (not ??) - the null-coalescing operator doesn't respect
            // UnityEngine.Object's overloaded == and can return a "missing" component wrapper.
            T existing = go.GetComponent<T>();
            return existing != null ? existing : go.AddComponent<T>();
        }

        private static void EnsureHitStopController()
        {
            GameObject possession = null;
            foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (go != null && go.name == "CameraPossession" && go.transform.parent == null)
                {
                    possession = go;
                    break;
                }
            }
            if (possession != null)
            {
                EnsureComponent<HitStopController>(possession);
            }
        }

        // Everything that should be enabled ONLY while the cat is possessed - movement + the
        // whole melee input/combat stack (CatAttackPose / CatCombatFeedback stay always-on: they
        // self-noop when PlayerCombat is Idle / the cat isn't possessed).
        // Everything on the Player that consumes player input - disabled by the switcher while the
        // cat is possessed so a left-click / R / F / lock-on press only ever drives the character
        // you're actually looking at. 2026-08-31, user report ("cat視角下攻擊會連帶觸發 player 攻擊"):
        // the array used to hold only CharacterMovement, so the player kept swinging (PlayerCombat),
        // ulting (UltimateAbility), locking on, etc. off the shared mouse/keyboard while you were the
        // cat. Mirror of CollectCatControl. CharacterMovement stays first (BuildSwitcher / OnDisable
        // conventions don't depend on order, but keep it parallel to the cat side).
        private static Behaviour[] CollectPlayerControl(GameObject player)
        {
            var list = new List<Behaviour>();
            void Add<T>() where T : Behaviour
            {
                var c = player.GetComponent<T>();
                if (c != null)
                {
                    list.Add(c);
                }
            }
            Add<CharacterMovement>();
            Add<PlayerCombat>();
            Add<TargetLockController>();
            Add<UltimateAbility>();
            Add<RangedWeapon>();   // retired 2026-08-31 (no longer on Player) - no-op, kept in case it returns
            Add<PlayerGuard>();    // 2026-08-31 katana guard - disable it too while you're the cat
            Add<ExecutionAbility>();
            return list.ToArray();
        }

        private static Behaviour[] CollectCatControl(GameObject cat, CharacterMovement catMovement)
        {
            var list = new List<Behaviour> { catMovement };
            void Add<T>() where T : Behaviour
            {
                var c = cat.GetComponent<T>();
                if (c != null)
                {
                    c.enabled = false;
                    list.Add(c);
                }
            }
            Add<PlayerCombat>();
            Add<CatChargeAttack>();
            Add<CatPounce>();
            Add<CatAerialJudgment>();
            Add<CatUltimateAbility>(); // R = dark sword-qi cast (2026-08-31); only while possessing the cat
            return list.ToArray();
        }

        // GameObject.Find only returns ACTIVE objects - CatCamera is created SetActive(false), so a
        // plain Find would never see it and every re-run would leak another orphan CatCamera into the
        // scene. This walks the loaded scene objects (inactive included) and removes every match.
        private static void DestroyExisting(string name)
        {
            foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (go != null && go.name == name && go.transform.parent == null)
                {
                    Object.DestroyImmediate(go);
                }
            }
        }

        private static GameObject BuildCat(GameObject modelAsset, float groundTopY)
        {
            DestroyExisting("Cat");

            var cat = new GameObject("Cat");

            CharacterController cc = cat.AddComponent<CharacterController>();
            cc.height = CatControllerHeight;
            cc.radius = CatControllerRadius;
            cc.center = Vector3.zero;
            cc.skinWidth = CatControllerSkin;
            cc.slopeLimit = 50f;
            cc.stepOffset = 0f;          // project-wide: step-climbing is intentionally off (see FixCharacterControllerStepOffset)
            cc.minMoveDistance = 0f;

            cat.transform.position = new Vector3(
                CatSpawnXz.x,
                groundTopY + CatControllerHeight / 2f + CatControllerSkin,
                CatSpawnXz.y);

            // "Visual" child = the imported .glb, same wrapper/Visual split every other character
            // setup uses so a swap doesn't disturb the logic components on the root.
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, cat.transform);
            visual.name = "Visual";
            visual.transform.localPosition = new Vector3(0f, -CatControllerHeight / 2f, 0f);
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one * CatScale;

            foreach (SkinnedMeshRenderer smr in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                // The .glb's serialized SkinnedMeshRenderer bounds are degenerate (0,0,0) - Unity's
                // frustum culling would drop the cat from most angles. Recomputing the skinned
                // bounds from the live bones every frame is the standard fix for one character.
                smr.updateWhenOffscreen = true;
            }

            cat.AddComponent<PlayerInputProvider>();

            CharacterMovement movement = cat.AddComponent<CharacterMovement>();
            var so = new SerializedObject(movement);
            so.FindProperty("inputSource").objectReferenceValue = cat.GetComponent<PlayerInputProvider>();
            so.FindProperty("moveSpeed").floatValue = CatMoveSpeed;
            // dodgeData / lockOnSource / health / stance / flightEnergy all left null - null-safe in
            // CharacterMovement (every read is guarded), so the cat is a bare walk rig.
            so.ApplyModifiedPropertiesWithoutUndo();

            WireProceduralWalk(cat, visual, movement);
            WireFlightAndDash(cat, movement);
            WireCombat(cat, visual, movement);

            return cat;
        }

        // 2026-08-29, cat combat design (Docs/CAT_COMBAT_DESIGN.md, slices 2-1..2-7). The cat
        // reuses the player's whole combo pipeline (PlayerCombat / ComboAttackState / AttackData /
        // AttackResolver - the attack side is a pure Physics.OverlapCapsule/Sphere query, it
        // needs NO hitbox collider, only an attackOrigin transform). This adds:
        //   - PlayerCombat with a 3-step combo (CatSwipe1/2/3) + an attackOrigin child at the
        //     mouth. inputSource is left NULL: the cat's melee button is release-triggered and
        //     mediated by CatChargeAttack (tap = swipe, hold = CatHeavy, move+press = CatPounce).
        //   - CatAerialJudgment (sphere judgment while airborne), CatChargeAttack, CatPounce,
        //     CatAttackPose (multi-bone procedural swing over the front paws / spine / head).
        //   - Health + MeleeKnockback so the cat can be hit back (slice 2-7), plus CombatSfx +
        //     CatCombatFeedback for hitstop (cat-possession only) / camera shake / SFX.
        private static void WireCombat(GameObject cat, GameObject visual, CharacterMovement movement)
        {
            AttackData swipe1 = CreateOrUpdateAttackData("CatSwipe1", Swipe1);
            AttackData swipe2 = CreateOrUpdateAttackData("CatSwipe2", Swipe2);
            AttackData swipe3 = CreateOrUpdateAttackData("CatSwipe3", Swipe3);
            AttackData heavy = CreateOrUpdateAttackData("CatHeavy", Heavy);
            AttackData pounce = CreateOrUpdateAttackData("CatPounce", Pounce);

            var input = cat.GetComponent<PlayerInputProvider>();

            // attackOrigin at the cat's mouth, facing the cat's forward (root yaw = facing).
            var attackOrigin = new GameObject("AttackOrigin");
            attackOrigin.transform.SetParent(cat.transform);
            attackOrigin.transform.localPosition = new Vector3(0f, CatAttackOriginUp, CatAttackOriginForward);
            attackOrigin.transform.localRotation = Quaternion.identity;

            var health = cat.AddComponent<Health>();
            var healthSo = new SerializedObject(health);
            healthSo.FindProperty("maxHealth").floatValue = CatMaxHealth;
            healthSo.ApplyModifiedPropertiesWithoutUndo();
            cat.AddComponent<MeleeKnockback>();

            // 2026-08-31 - Souls-like poise bar (see CatMaxStance). Auto-accumulates from any hit
            // that goes through Health.Damaged; PlayerCombat / CharacterMovement below gate their
            // "can act" on stance.IsStaggered (both null-safe, so this stays optional for anything
            // that doesn't want it).
            var stance = cat.AddComponent<StancePoise>();
            var stanceSo = new SerializedObject(stance);
            stanceSo.FindProperty("maxStance").floatValue = CatMaxStance;
            stanceSo.FindProperty("staggerDurationSeconds").floatValue = CatStaggerSeconds;
            stanceSo.ApplyModifiedPropertiesWithoutUndo();

            // Let dodge i-frames actually land on the cat's Health (CharacterMovement mirrors
            // IsDodgeInvulnerable -> health.IsInvulnerable every frame, null-safe).
            var mvSo = new SerializedObject(movement);
            mvSo.FindProperty("health").objectReferenceValue = health;
            mvSo.FindProperty("stance").objectReferenceValue = stance; // freeze movement while staggered
            mvSo.ApplyModifiedPropertiesWithoutUndo();

            var combat = cat.AddComponent<PlayerCombat>();
            var combatSo = new SerializedObject(combat);
            SerializedProperty combo = combatSo.FindProperty("comboAttacks");
            combo.arraySize = 3;
            combo.GetArrayElementAtIndex(0).objectReferenceValue = swipe1;
            combo.GetArrayElementAtIndex(1).objectReferenceValue = swipe2;
            combo.GetArrayElementAtIndex(2).objectReferenceValue = swipe3;
            combatSo.FindProperty("attackOrigin").objectReferenceValue = attackOrigin.transform;
            combatSo.FindProperty("health").objectReferenceValue = health;
            combatSo.FindProperty("stance").objectReferenceValue = stance; // can't start a swing while staggered
            combatSo.FindProperty("hitEffectPrefab").objectReferenceValue = HitEffectSetup.CreateOrLoadHitEffectPrefab();
            // inputSource left null on purpose - see this method's comment / CatChargeAttack.
            combatSo.ApplyModifiedPropertiesWithoutUndo();

            var aerial = cat.AddComponent<CatAerialJudgment>();
            var aerialSo = new SerializedObject(aerial);
            aerialSo.FindProperty("speedSource").objectReferenceValue = movement;
            aerialSo.ApplyModifiedPropertiesWithoutUndo();

            var charge = cat.AddComponent<CatChargeAttack>();
            var chargeSo = new SerializedObject(charge);
            chargeSo.FindProperty("input").objectReferenceValue = input;
            chargeSo.FindProperty("heavyAttack").objectReferenceValue = heavy;
            chargeSo.ApplyModifiedPropertiesWithoutUndo();

            var pounceComp = cat.AddComponent<CatPounce>();
            var pounceSo = new SerializedObject(pounceComp);
            pounceSo.FindProperty("input").objectReferenceValue = input;
            pounceSo.FindProperty("movement").objectReferenceValue = movement;
            pounceSo.FindProperty("chargeAttack").objectReferenceValue = charge;
            pounceSo.FindProperty("pounceAttack").objectReferenceValue = pounce;
            pounceSo.ApplyModifiedPropertiesWithoutUndo();

            // 2026-08-31, user request ("讓 cat 能量滿格時可以施放...這個技能特效") - the cat's ultimate:
            // R while a DEDICATED skill-energy meter is full. That meter is its own UltimateEnergy on
            // a Cat/SkillEnergy child (NOT the flight-energy instance on the root - the two are kept
            // independent, exactly like the player has two). CatDarkQiVfxSetup fills in castVfxPrefab
            // / attack / stance / health and re-points the CatCornerHud 能量 bar at THIS meter.
            var skillEnergyGo = new GameObject("SkillEnergy");
            skillEnergyGo.transform.SetParent(cat.transform, false);
            var skillEnergy = skillEnergyGo.AddComponent<Live2DAction.Core.UltimateEnergy>();
            var seSo = new SerializedObject(skillEnergy);
            seSo.FindProperty("maxEnergy").floatValue = CatSkillEnergyMax;
            seSo.FindProperty("regenAmount").floatValue = 5f;   // 5 / 1s -> 20s to full, same as the player's ultimate
            seSo.FindProperty("regenIntervalSeconds").floatValue = 1f;
            seSo.FindProperty("regenIdleDelaySeconds").floatValue = 0f;
            seSo.ApplyModifiedPropertiesWithoutUndo();

            var ult = cat.AddComponent<CatUltimateAbility>();
            var ultSo = new SerializedObject(ult);
            ultSo.FindProperty("inputSource").objectReferenceValue = input;
            ultSo.FindProperty("energy").objectReferenceValue = skillEnergy;
            ultSo.FindProperty("stance").objectReferenceValue = stance;
            ultSo.FindProperty("health").objectReferenceValue = health;
            ultSo.ApplyModifiedPropertiesWithoutUndo();
            ult.enabled = false; // enabled by CameraPossessionSwitcher.catControl when you possess the cat

            WireAttackPose(cat, visual, combat);

            var audioSource = EnsureComponent<AudioSource>(cat);
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            var sfx = cat.AddComponent<CombatSfx>();

            var feedback = cat.AddComponent<CatCombatFeedback>();
            var feedbackSo = new SerializedObject(feedback);
            feedbackSo.FindProperty("combat").objectReferenceValue = combat;
            feedbackSo.FindProperty("sfx").objectReferenceValue = sfx;
            // possession + catCameraShake filled in by WireCombatFeedbackRefs after the camera/
            // switcher exist.
            feedbackSo.ApplyModifiedPropertiesWithoutUndo();
        }

        // The four front-paw / spine / head bones CatAttackPose hinges. Bone ids from the same
        // BakeMesh + bone-tree dump WireProceduralWalk uses. axis/degrees are eyeballed starting
        // points - the generic auto-rig's per-bone orientation is unknown, hand-tune in Play.
        private static void WireAttackPose(GameObject cat, GameObject visual, PlayerCombat combat)
        {
            var pose = cat.AddComponent<CatAttackPose>();
            var so = new SerializedObject(pose);
            so.FindProperty("combatSource").objectReferenceValue = combat;
            so.FindProperty("walk").objectReferenceValue = cat.GetComponent<CatProceduralWalk>();

            // name, localAxis(x,y,z), windUpDegrees, strikeDegrees, pawSide (0 Both / 1 Left / 2 Right)
            var defs = new[]
            {
                new object[] { "Bone_034", 1f, 0f, 0f, 18f, 55f, 2 }, // front-right shoulder
                new object[] { "Bone_032", 1f, 0f, 0f, 25f, 40f, 2 }, // front-right elbow
                new object[] { "Bone_042", 1f, 0f, 0f, 18f, 55f, 1 }, // front-left shoulder
                new object[] { "Bone_040", 1f, 0f, 0f, 25f, 40f, 1 }, // front-left elbow
                new object[] { "Bone_011", 1f, 0f, 0f, 6f, 14f, 0 },  // shoulder/chest hub - lean into the swing
                // 2026-08-29: neck chain confirmed (028 base -> 027 mid -> 026 head tip), local X
                // axis pitches the head cleanly down/up (verified via screenshot). Two bones so
                // the head THRUSTS through the strike (~32deg down combined) instead of the old
                // barely-visible 16deg on one bone; ~18deg up on wind-up.
                new object[] { "Bone_028", 1f, 0f, 0f, 10f, 18f, 0 }, // neck base
                new object[] { "Bone_027", 1f, 0f, 0f, 8f, 14f, 0 },  // neck mid - head dips/thrusts through
            };
            SerializedProperty bones = so.FindProperty("bones");
            bones.arraySize = defs.Length;
            int missing = 0;
            for (int i = 0; i < defs.Length; i++)
            {
                SerializedProperty b = bones.GetArrayElementAtIndex(i);
                Transform bone = FindBone(visual, (string)defs[i][0]);
                if (bone == null) missing++;
                b.FindPropertyRelative("bone").objectReferenceValue = bone;
                b.FindPropertyRelative("localAxis").vector3Value = new Vector3((float)defs[i][1], (float)defs[i][2], (float)defs[i][3]);
                b.FindPropertyRelative("windUpDegrees").floatValue = (float)defs[i][4];
                b.FindPropertyRelative("strikeDegrees").floatValue = (float)defs[i][5];
                b.FindPropertyRelative("pawSide").enumValueIndex = (int)defs[i][6];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            if (missing > 0)
            {
                Debug.LogWarning("CatAttackPose: " + missing + " bone(s) not found by name in Cat.glb - " +
                                 "auto-rig ids may have changed on re-import.");
            }
        }

        private static void WireCombatFeedbackRefs(GameObject cat, CameraShake catCameraShake)
        {
            var feedback = cat.GetComponent<CatCombatFeedback>();
            if (feedback == null) return;
            CameraPossessionSwitcher switcher = null;
            foreach (var s in Object.FindObjectsByType<CameraPossessionSwitcher>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                switcher = s;
                break;
            }
            var so = new SerializedObject(feedback);
            so.FindProperty("possession").objectReferenceValue = switcher;
            so.FindProperty("catCameraShake").objectReferenceValue = catCameraShake;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // name -> { damage, startup, active, recovery, comboWindow, range, radius [, knockbackForce] }.
        private static AttackData CreateOrUpdateAttackData(string assetName, float[] v)
        {
            EnsureFolder(CombatFolder);
            string path = CombatFolder + "/" + assetName + ".asset";
            var data = AssetDatabase.LoadAssetAtPath<AttackData>(path);
            bool isNew = data == null;
            if (isNew)
            {
                data = ScriptableObject.CreateInstance<AttackData>();
            }
            var so = new SerializedObject(data);
            so.FindProperty("attackId").stringValue = assetName;
            so.FindProperty("damage").floatValue = v[0];
            so.FindProperty("startupFrames").intValue = Mathf.RoundToInt(v[1]);
            so.FindProperty("activeFrames").intValue = Mathf.RoundToInt(v[2]);
            so.FindProperty("recoveryFrames").intValue = Mathf.RoundToInt(v[3]);
            so.FindProperty("comboWindowFrames").intValue = Mathf.RoundToInt(v[4]);
            so.FindProperty("range").floatValue = v[5];
            so.FindProperty("radius").floatValue = v[6];
            so.FindProperty("knockbackForce").floatValue = v.Length > 7 ? v[7] : 0f;
            so.FindProperty("knockbackLaunches").boolValue = assetName == "CatHeavy" || assetName == "CatPounce";
            so.FindProperty("alwaysSpawnHitEffect").boolValue = assetName == "CatHeavy" || assetName == "CatPounce";
            so.ApplyModifiedPropertiesWithoutUndo();
            if (isNew)
            {
                AssetDatabase.CreateAsset(data, path);
            }
            else
            {
                EditorUtility.SetDirty(data);
            }
            return data;
        }

        // 2026-08-29, explicit user request ("讓貓就有飛行和衝刺功能 參考player"). CharacterMovement
        // already contains all the flight/boost/dodge logic (null-safe, opt-in) - this only adds
        // the flight-energy component + a cat-scaled DodgeData asset and points the movement's own
        // serialized fields at them, the same two moves FlightSetup + FixDodgeSetup make on the
        // player. Called from BuildCat, which always creates a fresh "Cat" GameObject, so there's
        // never a stale UltimateEnergy to clear first.
        private static void WireFlightAndDash(GameObject cat, CharacterMovement movement)
        {
            var flightEnergy = cat.AddComponent<UltimateEnergy>();
            var energySo = new SerializedObject(flightEnergy);
            energySo.FindProperty("maxEnergy").floatValue = CatFlightMaxEnergy;
            energySo.FindProperty("regenAmount").floatValue = CatFlightRegenAmount;
            energySo.FindProperty("regenIntervalSeconds").floatValue = CatFlightRegenInterval;
            energySo.FindProperty("regenIdleDelaySeconds").floatValue = CatFlightRegenIdleDelay;
            energySo.ApplyModifiedPropertiesWithoutUndo();

            DodgeData dodgeData = CreateOrUpdateCatDodgeData();

            var so = new SerializedObject(movement);
            so.FindProperty("flightEnergy").objectReferenceValue = flightEnergy;
            so.FindProperty("dodgeData").objectReferenceValue = dodgeData;
            so.FindProperty("flightMoveSpeed").floatValue = CatFlightMoveSpeed;
            so.FindProperty("flightAscendSpeed").floatValue = CatFlightAscendSpeed;
            so.FindProperty("flightDescendSpeed").floatValue = CatFlightDescendSpeed;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Re-applies the Cat* constants to the asset every run (creating it the first time), the
        // same "edit the constant, re-run the menu, it takes effect" contract the flight numbers
        // above already have - the earlier create-or-return-as-is version meant a tuning change to
        // CatDodgeDistance silently did nothing on re-run (2026-08-29, the "空中沒法衝刺" fix was
        // exactly such a change). Nothing hand-tunes this asset directly; it's setup-script-owned.
        private static DodgeData CreateOrUpdateCatDodgeData()
        {
            EnsureFolder(System.IO.Path.GetDirectoryName(CatDodgeDataPath).Replace('\\', '/'));

            var data = AssetDatabase.LoadAssetAtPath<DodgeData>(CatDodgeDataPath);
            bool isNew = data == null;
            if (isNew)
            {
                data = ScriptableObject.CreateInstance<DodgeData>();
            }

            var so = new SerializedObject(data);
            so.FindProperty("distance").floatValue = CatDodgeDistance;
            so.FindProperty("durationFrames").intValue = CatDodgeDurationFrames;
            so.FindProperty("invulnerabilityFrames").intValue = CatDodgeInvulnerabilityFrames;
            so.FindProperty("cooldownFrames").intValue = CatDodgeCooldownFrames;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (isNew)
            {
                AssetDatabase.CreateAsset(data, CatDodgeDataPath);
            }
            else
            {
                EditorUtility.SetDirty(data);
            }
            return data;
        }

        // "Assets/_Project/Settings/Movement/Cat" - AssetDatabase.CreateAsset can't create missing
        // parent folders, so walk up and create each segment. "Assets" is always valid (base case).
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        // The generic auto-rig has no bone names that mean anything, so the four leg chains are
        // identified once here by their known Bone_XXX ids (mapped from a live BakeMesh + bone-tree
        // dump of Cat.glb: front legs Bone_034/033/032.. and Bone_042/041/040.., back legs
        // Bone_018/017/016.. and Bone_023/022/021..; Bone_000 = pelvis root, Bone_004-008 = tail).
        // CatProceduralWalk swings the shoulder/hip bone and bends the elbow/knee bone in a
        // diagonal trot (FL+BR together, FR+BL half a cycle later), amplitude scaled by
        // CharacterMovement.CurrentHorizontalSpeed.
        private static void WireProceduralWalk(GameObject cat, GameObject visual, CharacterMovement movement)
        {
            var walk = cat.AddComponent<Live2DAction.Characters.CatProceduralWalk>();
            var so = new SerializedObject(walk);
            so.FindProperty("catRoot").objectReferenceValue = cat.transform;
            so.FindProperty("speedSource").objectReferenceValue = movement;
            so.FindProperty("speedForFullStride").floatValue = CatMoveSpeed;
            so.FindProperty("bodyBobBone").objectReferenceValue = FindBone(visual, "Bone_000");

            // swingBone, bendBone, phaseOffset (0..1), bendSign (front elbow folds opposite the back knee)
            var legDefs = new[]
            {
                new object[] { "Bone_034", "Bone_032", 0.0f, -1f }, // front-left
                new object[] { "Bone_042", "Bone_040", 0.5f, -1f }, // front-right
                new object[] { "Bone_018", "Bone_016", 0.5f, 1f },  // back-left
                new object[] { "Bone_023", "Bone_021", 0.0f, 1f },  // back-right
            };
            SerializedProperty legs = so.FindProperty("legs");
            legs.arraySize = legDefs.Length;
            for (int i = 0; i < legDefs.Length; i++)
            {
                SerializedProperty leg = legs.GetArrayElementAtIndex(i);
                leg.FindPropertyRelative("swingBone").objectReferenceValue = FindBone(visual, (string)legDefs[i][0]);
                leg.FindPropertyRelative("bendBone").objectReferenceValue = FindBone(visual, (string)legDefs[i][1]);
                leg.FindPropertyRelative("phaseOffset").floatValue = (float)legDefs[i][2];
                leg.FindPropertyRelative("bendSign").floatValue = (float)legDefs[i][3];
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            int missing = 0;
            foreach (var d in legDefs)
            {
                if (FindBone(visual, (string)d[0]) == null || FindBone(visual, (string)d[1]) == null) missing++;
            }
            if (missing > 0)
            {
                Debug.LogWarning("CatProceduralWalk: " + missing + " leg bone(s) not found by name in Cat.glb - " +
                                 "the auto-rig bone ids may have changed on re-import. Walk will run with fewer legs.");
            }
        }

        private static Transform FindBone(GameObject visual, string boneName)
        {
            foreach (Transform t in visual.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == boneName) return t;
            }
            return null;
        }

        private static GameObject BuildCatCamera(GameObject mainCamera, Transform catTarget)
        {
            DestroyExisting("CatCamera");

            // Clone the whole player camera rig so CatCamera inherits the exact Camera settings
            // (FOV 65, clear flags, culling mask) + UniversalAdditionalCameraData - same approach
            // VehicleCamera takes (a sibling camera, SetActive-swapped).
            GameObject catCamera = Object.Instantiate(mainCamera);
            catCamera.name = "CatCamera";
            catCamera.tag = "MainCamera";      // so Camera.main resolves to whichever camera is active
            catCamera.SetActive(false);

            var tpc = catCamera.GetComponent<ThirdPersonCameraController>();
            var so = new SerializedObject(tpc);
            so.FindProperty("target").objectReferenceValue = catTarget;
            so.FindProperty("distance").floatValue = CatCamDistance;
            so.FindProperty("targetOffset").vector3Value = CatCamTargetOffset;
            so.FindProperty("initialPitch").floatValue = CatCamInitialPitch;
            so.FindProperty("minPitch").floatValue = CatCamMinPitch;
            so.FindProperty("maxPitch").floatValue = CatCamMaxPitch;
            // The cat has no first-person aim / lock-on / ultimate / flight - clear the player-only
            // wiring so none of those code paths ever engage on this camera.
            so.FindProperty("lockOnSource").objectReferenceValue = null;
            so.FindProperty("inputSource").objectReferenceValue = null;
            so.FindProperty("ultimateAbility").objectReferenceValue = null;
            so.FindProperty("enableDescendAutoPitch").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            catCamera.transform.SetParent(null);
            return catCamera;
        }

        private static void BuildSwitcher(GameObject mainCamera, GameObject catCamera,
            Behaviour[] playerControlBehaviours, Behaviour[] catControlBehaviours)
        {
            DestroyExisting("CameraPossession");

            var go = new GameObject("CameraPossession");
            var switcher = go.AddComponent<CameraPossessionSwitcher>();
            var so = new SerializedObject(switcher);
            so.FindProperty("playerCamera").objectReferenceValue = mainCamera;
            so.FindProperty("catCamera").objectReferenceValue = catCamera;

            SerializedProperty playerControl = so.FindProperty("playerControl");
            playerControl.arraySize = playerControlBehaviours.Length;
            for (int i = 0; i < playerControlBehaviours.Length; i++)
            {
                playerControl.GetArrayElementAtIndex(i).objectReferenceValue = playerControlBehaviours[i];
            }

            SerializedProperty catControl = so.FindProperty("catControl");
            catControl.arraySize = catControlBehaviours.Length;
            for (int i = 0; i < catControlBehaviours.Length; i++)
            {
                catControl.GetArrayElementAtIndex(i).objectReferenceValue = catControlBehaviours[i];
            }

            // Auto-drop back to the player if the cat dies while possessed (see catHealth).
            if (catControlBehaviours.Length > 0 && catControlBehaviours[0] != null)
            {
                so.FindProperty("catHealth").objectReferenceValue = catControlBehaviours[0].GetComponent<Health>();
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
