using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.Characters;
using Live2DAction.Combat;
using Live2DAction.Core;
using Live2DAction.Targeting;

namespace Live2DAction.EditorTools
{
    // 2026-08-19, explicit user request ("幫我導入 Unity-Chan! Model 機制與玩家一致 只是不會動") -
    // adds a new "UnityChan" character carrying Player's full COMBAT mechanic stack (movement/
    // input explicitly excluded per follow-up clarification: "只拿掉移動與輸入" - everything else,
    // including StancePoise/execution both ways/Ultimate/death, stays). Same overall shape as
    // TrainingDummySetup.cs (stationary, no CharacterController/PlayerInputProvider/
    // CharacterMovement, CapsuleCollider instead) but a strict superset of TrainingDummy's own
    // component list - TrainingDummy was deliberately scoped down to "just a punching bag"
    // (explicit user confirmation at the time), this character is explicitly the opposite ask.
    //
    // Scope explicitly excludes UltimateReadyAura/UltimateActivationBurst (2026-08-19, explicit
    // user choice from a clarifying question) - both are procedural-VFX bootstrap scripts
    // hardcoded to GameObject.Find("Player") with no generic/reusable entry point, so wiring them
    // onto a third character is a separate, larger follow-up if ever asked for. UltimateEnergy/
    // UltimateAbility/UltimateAttackAnimationSwap (the CORE ultimate mechanic - meter, R-key
    // activation, damage buff) are included; UltimateAbility's own weapon-scale-on-activation is
    // a no-op here (FindWeapon() looks for a child literally named "WolfsGravestone", which
    // UnityChan doesn't have - null-safe, confirmed by reading UltimateAbility.cs), and
    // UltimateAttackAnimationSwap is left with no override clip (also null-safe/inert) since no
    // "swap her attack animation during Ultimate" request was ever made - both components exist
    // structurally for "same mechanism" parity even though neither has cosmetic content to drive
    // yet.
    //
    // Visual/animation: Unity-Chan! Model (Unity Technologies Japan / UCL license) ships as a
    // Humanoid-rigged FBX with no prefab and no combat animations of her own - reuses Maya's
    // EXACT shared AnimatorController (same cross-rig Humanoid retargeting CombatAnimatorSetup's
    // own comment already documents working for Maya/Arisa). Real bug hunt along the way: she
    // first rendered as an inverted handstand regardless of which controller was assigned, or
    // even with none at all - eventually traced (not guessed) to the FBX importer's own
    // `Bake Axis Conversion` flag defaulting OFF for this asset, which left her skeleton's
    // authored bone rotations (e.g. Character1_Hips) mismatched against the mesh's imported
    // bindpose matrices. Flipping that ON (`UnityChanFbxImportFix` below) fixed it outright -
    // see that method's own comment for the full diagnostic trail (raw Mesh.vertices check,
    // Human-vs-Generic animationType test, bone-rotation dump) that ruled out every other
    // candidate first.
    //
    // IMPORTANT interaction to be aware of: ExecutionAbility reads the F key directly from the
    // global Keyboard state (not scoped to "the player specifically" - see that class's own
    // comment), and finds its target purely by physical proximity to ITS OWN transform. Adding a
    // second ExecutionAbility-bearing character means an F press while standing in range of both
    // it and a staggered target could resolve from either/both components independently in the
    // same frame (each runs its own OverlapSphere + EndStagger). Harmless today because
    // UnityChan is stationary and placed away from the combat characters, but worth remembering
    // if she's ever repositioned closer to the Player/Enemy fight.
    internal static class UnityChanCompanionSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string CharacterName = "UnityChan";

        private const string FbxPath = "Assets/unity-chan!/Unity-chan! Model/Art/Models/unitychan.fbx";
        private const string MayaControllerPath = "Assets/_Project/Characters/Placeholder/MayaAnime/Animator/NewAnimator.controller";

        private const string LightAttack1Path = "Assets/_Project/Settings/Combat/LightAttack1.asset";
        private const string LightAttack2Path = "Assets/_Project/Settings/Combat/LightAttack2.asset";
        private const string LightAttack3Path = "Assets/_Project/Settings/Combat/LightAttack3.asset";
        private const string LightAttack4Path = "Assets/_Project/Settings/Combat/LightAttack4.asset";

        // Clear of every existing placed object (Player -2.5,0 / Mecha 2.5,-2 / Enemy 5,-8 /
        // TrainingDummy 5,0 / HealingSpring -2.5,4 / Updraft -2.5,-6 / FemaleStandee 0,-8),
        // safely inside the +-15.5 boundary walls.
        private static readonly Vector3 SpawnXz = new Vector3(8f, 0f, 4f);

        [MenuItem("Tools/Live2DAction/Add Unity-Chan Companion (Player-Equivalent, Stationary)")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            AttackData attack1 = AssetDatabase.LoadAssetAtPath<AttackData>(LightAttack1Path);
            AttackData attack2 = AssetDatabase.LoadAssetAtPath<AttackData>(LightAttack2Path);
            AttackData attack3 = AssetDatabase.LoadAssetAtPath<AttackData>(LightAttack3Path);
            AttackData attack4 = AssetDatabase.LoadAssetAtPath<AttackData>(LightAttack4Path);
            if (attack1 == null || attack2 == null || attack3 == null || attack4 == null)
            {
                Debug.LogError("Could not load one or more LightAttack AttackData assets.");
                return;
            }

            UnityChanFbxImportFix();

            GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbxAsset == null)
            {
                Debug.LogError("Could not load Unity-Chan FBX at " + FbxPath + " - import the asset first.");
                return;
            }

            RuntimeAnimatorController mayaController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(MayaControllerPath);
            if (mayaController == null)
            {
                Debug.LogError("Could not load Maya's AnimatorController at " + MayaControllerPath);
                return;
            }

            GameObject existing = GameObject.Find(CharacterName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            var go = new GameObject(CharacterName);

            // Same physical footprint as TrainingDummySetup's own capsule (radius 0.5, height 1,
            // matching Player's CharacterController) - no CharacterController here either, same
            // "nothing to drive" reasoning, since movement is explicitly out of scope.
            CapsuleCollider collider = go.AddComponent<CapsuleCollider>();
            collider.center = Vector3.zero;
            collider.radius = 0.5f;
            collider.height = 1f;

            GameObject ground = GameObject.Find("Ground");
            float groundTopY = ground != null ? ground.GetComponent<Collider>().bounds.max.y : 0f;
            go.transform.position = new Vector3(
                SpawnXz.x,
                groundTopY + collider.center.y + collider.height / 2f,
                SpawnXz.z);

            // --- Visual ---
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset, go.transform);
            visual.name = "Visual";
            visual.transform.localPosition = new Vector3(0f, collider.center.y - collider.height / 2f, 0f);
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            Animator animator = visual.GetComponent<Animator>();
            if (animator == null)
            {
                animator = visual.AddComponent<Animator>();
            }
            animator.applyRootMotion = false;
            animator.runtimeAnimatorController = mayaController;

            PlayerMayaVisualSetup.RemoveEmbeddedCameraRig(visual);
            PlayerMayaVisualSetup.RemoveEmbeddedPhysicsRig(visual);

            // Unity-Chan's own materials use her package's custom toon shader (Built-in RP only -
            // confirmed magenta/pink under this project's URP on first import), same class of
            // issue PlayerMayaVisualSetup/EnemyAnimeVisualSetup's own ConvertMaterialsToUrp
            // already fixes for their assets. Converts whatever's ACTUALLY assigned on the
            // instantiated renderers (not a folder-path guess - her package ships two near-
            // identical material sets, Art/Materials and Art/UnityChanShader/Materials) so this
            // is correct regardless of which one the FBX happens to reference.
            ConvertRendererMaterialsToUrp(visual);

            // --- Core stats ---
            Health health = go.AddComponent<Health>();

            TargetLockController lockController = go.AddComponent<TargetLockController>();
            // inputSource intentionally left unset - no PlayerInputProvider, so LockOnPressed is
            // always false (see TargetLockController.InputCommand's null-safe cast).

            PlayerCombat combat = go.AddComponent<PlayerCombat>();
            var combatSo = new SerializedObject(combat);
            SerializedProperty comboProperty = combatSo.FindProperty("comboAttacks");
            comboProperty.arraySize = 4;
            comboProperty.GetArrayElementAtIndex(0).objectReferenceValue = attack1;
            comboProperty.GetArrayElementAtIndex(1).objectReferenceValue = attack2;
            comboProperty.GetArrayElementAtIndex(2).objectReferenceValue = attack3;
            comboProperty.GetArrayElementAtIndex(3).objectReferenceValue = attack4;
            combatSo.FindProperty("hitEffectPrefab").objectReferenceValue = HitEffectSetup.CreateOrLoadHitEffectPrefab();
            combatSo.FindProperty("lockOnSource").objectReferenceValue = lockController;
            // stance is wired below, once StancePoise exists (PlayerCombat is added before it).
            combatSo.FindProperty("health").objectReferenceValue = health;
            combatSo.ApplyModifiedPropertiesWithoutUndo();
            // inputSource intentionally left unset - same "never actually attacks" guarantee
            // TrainingDummySetup relies on (AttackPressed always false with no input source).

            CharacterAttackAnimationLink attackLink = go.AddComponent<CharacterAttackAnimationLink>();
            var attackLinkSo = new SerializedObject(attackLink);
            attackLinkSo.FindProperty("animator").objectReferenceValue = animator;
            attackLinkSo.ApplyModifiedPropertiesWithoutUndo();

            HealthRegeneration regen = go.AddComponent<HealthRegeneration>();
            var regenSo = new SerializedObject(regen);
            regenSo.FindProperty("health").objectReferenceValue = health;
            regenSo.ApplyModifiedPropertiesWithoutUndo();

            // --- Stance / stagger / execution ---
            StancePoise stance = go.AddComponent<StancePoise>();

            var combatSo2 = new SerializedObject(combat);
            combatSo2.FindProperty("stance").objectReferenceValue = stance;
            combatSo2.ApplyModifiedPropertiesWithoutUndo();

            StaggerAnimationLink staggerLink = go.AddComponent<StaggerAnimationLink>();
            var staggerLinkSo = new SerializedObject(staggerLink);
            staggerLinkSo.FindProperty("animator").objectReferenceValue = animator;
            staggerLinkSo.ApplyModifiedPropertiesWithoutUndo();

            ExecutionAbility execution = go.AddComponent<ExecutionAbility>();
            var executionSo = new SerializedObject(execution);
            executionSo.FindProperty("animator").objectReferenceValue = animator;
            executionSo.ApplyModifiedPropertiesWithoutUndo();
            // attackOrigin left unset - ExecutionAbility.Awake() defaults it to its own transform.

            // --- Ultimate (core mechanic only - see class comment for the excluded VFX layers) ---
            UltimateEnergy energy = go.AddComponent<UltimateEnergy>();

            UltimateAbility ultimate = go.AddComponent<UltimateAbility>();
            var ultimateSo = new SerializedObject(ultimate);
            ultimateSo.FindProperty("energy").objectReferenceValue = energy;
            ultimateSo.ApplyModifiedPropertiesWithoutUndo();
            // inputSource/burst intentionally left unset - no input to drive UltimatePressed, no
            // UltimateActivationBurst instance (excluded from this pass, see class comment).

            UltimateAttackAnimationSwap swap = go.AddComponent<UltimateAttackAnimationSwap>();
            var swapSo = new SerializedObject(swap);
            swapSo.FindProperty("animator").objectReferenceValue = animator;
            swapSo.ApplyModifiedPropertiesWithoutUndo();
            // ultimateAttack1Clip intentionally left unset - inert without it (see Awake's own
            // early-return), no "swap her attack animation during Ultimate" request was made.

            // --- Damageable/lockable parity with every other placed character ---
            lockController.gameObject.AddComponent<LockOnTarget>();

            // --- HUD (red/blue/orange bars, stacked in that order - see each Setup's own
            // "prefers stacking under the previous bar" fallback chain) ---
            HealthBarSetup.AddHealthBar(go);
            UltimateAbilitySetup.AddEnergyBar(go, energy);
            StanceBarSetup.AddStanceBar(go);

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            // Death animation (Dying.fbx -> deferred deactivation) and the heartbeat execution-
            // ready indicator both already derive their target list generically enough to just
            // re-run here rather than duplicating their wiring - see each script's own comment.
            DeathAnimationSetup.Apply();
            ExecutionIndicatorSetup.Apply();

            Debug.Log("Added UnityChan - Player-equivalent combat kit (combo/stance/execution/ultimate core/death), stationary (no movement/input), sharing Maya's AnimatorController via Humanoid retargeting (see UnityChanFbxImportFix's own comment for the axis-conversion bug this needed first).");
        }

        // 2026-08-19, real bug hunt: on first import, Unity-Chan rendered as an inverted
        // handstand - not a standing pose - REGARDLESS of which AnimatorController was assigned
        // (tried Maya's shared one, tried her own bundled UnityChanLocomotions.controller, tried
        // none at all/bind pose only - all three looked identical, still upside down). Ruled out
        // candidates one at a time rather than guessing: (1) disabled the Animator and read
        // Mesh.vertices directly off the sharedMesh asset, bypassing skinning entirely - the
        // AUTHORED bind-pose vertex data is genuinely upright (hair vertices sit higher than
        // torso vertices), so the mesh itself was never at fault; (2) switched the FBX's
        // animationType from Humanoid to Generic and reimported (rules out Mecanim
        // muscle-space/avatar calibration) - Character1_Hips's own local rotation didn't change
        // at all between the two, meaning it's not an avatar-remapping artifact, it's the FBX's
        // raw imported bone transform. That pointed at the bone hierarchy vs. the mesh's imported
        // bindpose matrices disagreeing - a classic symptom of the importer's own
        // `Bake Axis Conversion` option (defaults OFF) not being applied for a file whose
        // coordinate/handedness convention needs it. Flipping it on and reimporting fixed the
        // pose outright (confirmed by screenshot) - restored animationType to Human afterward so
        // Maya's controller can retarget onto her properly. Idempotent (checks current values
        // before writing) so re-running this tool doesn't force a reimport every time.
        private static void UnityChanFbxImportFix()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(FbxPath);
            if (importer == null)
            {
                Debug.LogError("Could not load ModelImporter for " + FbxPath);
                return;
            }

            bool dirty = false;
            if (!importer.bakeAxisConversion)
            {
                importer.bakeAxisConversion = true;
                dirty = true;
            }
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                dirty = true;
            }

            if (dirty)
            {
                importer.SaveAndReimport();
            }
        }

        // Same shader-swap approach as PlayerMayaVisualSetup.ConvertMaterialsToUrp (find URP/Lit,
        // copy _MainTex/_Color across if the source shader happens to expose them under those
        // names - HasProperty guards make this safe even when it doesn't), just driven off the
        // renderers actually present on the instantiated visual instead of a hardcoded materials
        // folder.
        private static void ConvertRendererMaterialsToUrp(GameObject visual)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogError("Could not find Universal Render Pipeline/Lit shader.");
                return;
            }

            var converted = new System.Collections.Generic.HashSet<Material>();
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                foreach (Material material in materials)
                {
                    if (material == null || material.shader == urpLit || !converted.Add(material))
                    {
                        continue;
                    }

                    Texture mainTex = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
                    Color color = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;

                    material.shader = urpLit;
                    if (mainTex != null)
                    {
                        material.SetTexture("_BaseMap", mainTex);
                    }

                    material.SetColor("_BaseColor", color);
                    EditorUtility.SetDirty(material);
                }
            }
        }
    }
}
