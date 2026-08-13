using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.AI;
using Live2DAction.CameraSystem;
using Live2DAction.Characters;
using Live2DAction.Combat;
using Live2DAction.Core;
using Live2DAction.Input;
using Live2DAction.Targeting;

namespace Live2DAction.EditorTools
{
    internal static class GreyboxSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string ComboAttacksFolder = "Assets/_Project/Settings/Combat";
        private const string EnvironmentMaterialsFolder = "Assets/_Project/Environment/Materials";
        private const string GroundTexturesFolder = "Assets/_Project/Environment/Textures/StoneFloor";
        private const string GroundDiffusePath = GroundTexturesFolder + "/stone_floor_diff_1k.jpg";
        private const string GroundNormalPath = GroundTexturesFolder + "/stone_floor_nor_gl_1k.jpg";

        [MenuItem("Tools/Live2DAction/Build Greybox Test Scene")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateLight();
            CreateSkybox();
            CreateGround();
            CreateBackgroundTerrain();
            CreateBoundaryWalls();
            CreateCoverBlocks();

            GameObject player = CreatePlayer();
            CreateEnemy(player.transform);
            ThirdPersonCameraController cameraController = CreateCamera(player.transform);

            // AttackPoseVisualizer is NOT wired here: it needs Player's Animator (added later
            // by PlayerMayaVisualSetup, run manually after this builder) to find the arm bone.
            // See Tools/Live2DAction/Wire Attack Pose Visualizers (WireAttackPoseVisualizers.cs),
            // which must be run after the Maya visual and CharacterAnimatorLink are wired -
            // same reasoning as why CharacterAnimatorLink itself isn't wired in here either.

            // viewOrigin drives which direction TargetLockController searches for the closest
            // candidate in (see TargetLockUtility.FindBestTarget). 2026-08-12 explicit request
            // had this as Player's own forward (character-facing decides what's lockable);
            // 2026-08-13 explicit request reversed that - now the camera's forward (mouse-
            // driven, tracks yaw AND pitch every LateUpdate) decides instead, so pressing
            // lock-on acquires whatever the camera/mouse is currently pointed at rather than
            // requiring the character to physically turn toward it first. Only acquisition
            // changes - range/distance is still measured from the character (transform.position,
            // unchanged), and locking still doesn't rotate the camera; only the character's own
            // facing turns to the target afterward (see ThirdPersonCameraController's class
            // comment).
            TargetLockController lockController = player.GetComponent<TargetLockController>();
            var lockSo = new SerializedObject(lockController);
            lockSo.FindProperty("viewOrigin").objectReferenceValue = cameraController.transform;
            lockSo.ApplyModifiedPropertiesWithoutUndo();

            CharacterMovement movement = player.GetComponent<CharacterMovement>();
            var movementSo2 = new SerializedObject(movement);
            movementSo2.FindProperty("cameraYawSource").objectReferenceValue = cameraController;
            movementSo2.FindProperty("lockOnSource").objectReferenceValue = lockController;
            movementSo2.ApplyModifiedPropertiesWithoutUndo();

            // The camera's yaw/pitch still never react to a lock-on directly (only
            // CharacterMovement's facing does, wired above) - lockOnSource here is only
            // consulted by the auto-center feature, to defer to lock-on rather than fight it.
            var cameraLockSo = new SerializedObject(cameraController);
            cameraLockSo.FindProperty("lockOnSource").objectReferenceValue = lockController;
            cameraLockSo.ApplyModifiedPropertiesWithoutUndo();

            CreatePlayerRespawnController(player);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Live2DAction greybox test scene built at " + ScenePath);
        }

        // Player dying otherwise leaves the whole GameObject deactivated with no way back -
        // see RespawnController's class comment for the full 2026-08-12 bug report this fixes
        // (component generalized from Player-only PlayerRespawnController on 2026-08-13 so
        // Player2 could reuse it too). Lives on a separate always-active "GameManager"
        // GameObject, not Player itself (that class comment explains why it can't).
        private static void CreatePlayerRespawnController(GameObject player)
        {
            var managerGo = new GameObject("GameManager");
            RespawnController respawnController = managerGo.AddComponent<RespawnController>();
            var so = new SerializedObject(respawnController);
            so.FindProperty("target").objectReferenceValue = player;
            so.FindProperty("targetHealth").objectReferenceValue = player.GetComponent<Health>();
            // Matches the component's own class default - explicit here anyway per this
            // project's convention of not leaving balance/tuning values to an implicit default
            // (CLAUDE.md rule 7). 2026-08-12: raised from 0.5s to 5s by explicit user request.
            so.FindProperty("respawnDelaySeconds").floatValue = 5f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateLight()
        {
            var lightGo = new GameObject("Directional Light");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static void CreateGround()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(30f, 1f, 30f);
            ground.GetComponent<Renderer>().sharedMaterial = CreateOrLoadGroundMaterial();
        }

        // Poly Haven "Stone Floor" texture (CC0, see Docs/ASSET_LICENSES.md) - swaps the flat
        // grey default material for an actual tiled ground texture. Diffuse + normal only (the
        // downloaded roughness map isn't wired in - URP/Lit's Metallic workflow wants a packed
        // Mask Map, not a plain roughness image, and a flat Smoothness slider is good enough
        // for this pass); kept alongside the diffuse/normal files for whoever authors a proper
        // Mask Map later.
        private static Material CreateOrLoadGroundMaterial()
        {
            const string path = EnvironmentMaterialsFolder + "/Ground_StoneFloor.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            EnsureFolder(EnvironmentMaterialsFolder);

            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            Texture2D diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(GroundDiffusePath);
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(GroundNormalPath);

            // 10x10 repeats across the 30-unit Ground cube so the ~1-2m-per-tile source
            // texture doesn't stretch into one giant blur across the whole arena floor.
            var tiling = new Vector2(10f, 10f);

            if (diffuse != null)
            {
                material.SetTexture("_BaseMap", diffuse);
                material.SetTextureScale("_BaseMap", tiling);
            }

            if (normal != null)
            {
                SetNormalTextureImportSettings(GroundNormalPath);
                material.SetTexture("_BumpMap", normal);
                material.SetTextureScale("_BumpMap", tiling);
                material.EnableKeyword("_NORMALMAP");
            }

            material.SetFloat("_Smoothness", 0.2f); // worn/rough cobblestone, not shiny

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void SetNormalTextureImportSettings(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }
        }

        // A big plain-colored plane under/around Ground so the horizon isn't a hard void past
        // the 15-unit boundary - purely visual (no collider), the player can never actually
        // reach it since BoundaryWalls block movement at Ground's edge first. Deliberately not
        // textured (no source asset for a huge tiled area) - flat color reads fine at a
        // distance behind BackgroundScenery's trees/rocks (see BackgroundSceneryStandeeSetup).
        private static void CreateBackgroundTerrain()
        {
            GameObject terrain = GameObject.CreatePrimitive(PrimitiveType.Plane);
            terrain.name = "BackgroundTerrain";
            // Unity's Plane primitive is 10x10 units at scale 1, so scale 30 -> 300x300.
            // Sits 5cm below Ground's top face (y=0) to avoid z-fighting at the shared edge.
            terrain.transform.position = new Vector3(0f, -0.05f, 0f);
            terrain.transform.localScale = new Vector3(30f, 1f, 30f);
            Object.DestroyImmediate(terrain.GetComponent<Collider>());
            terrain.GetComponent<Renderer>().sharedMaterial = CreateOrLoadBackgroundTerrainMaterial();
        }

        private static Material CreateOrLoadBackgroundTerrainMaterial()
        {
            const string path = EnvironmentMaterialsFolder + "/BackgroundTerrain.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            EnsureFolder(EnvironmentMaterialsFolder);

            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = new Color(0.30f, 0.36f, 0.22f); // muted grass green
            material.SetFloat("_Smoothness", 0.05f);

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // Skybox/Procedural is Unity's built-in legacy skybox shader - no external asset
        // needed, and RenderSettings.skybox is pipeline-agnostic so it renders fine under URP.
        // Good enough as a fixed-arena sky backdrop until real environment art replaces it.
        // NOTE: not yet added to Graphics Settings' "Always Included Shaders" - fine for the
        // Editor (which has every built-in shader available), but a stripped Player build could
        // lose this shader; revisit before the first real Build (see Docs/KNOWN_ISSUES.md).
        private static void CreateSkybox()
        {
            RenderSettings.skybox = CreateOrLoadSkyboxMaterial();

            GameObject lightGo = GameObject.Find("Directional Light");
            if (lightGo != null)
            {
                RenderSettings.sun = lightGo.GetComponent<Light>();
            }
        }

        private static Material CreateOrLoadSkyboxMaterial()
        {
            const string path = EnvironmentMaterialsFolder + "/Skybox_Procedural.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            EnsureFolder(EnvironmentMaterialsFolder);

            var sky = new Material(Shader.Find("Skybox/Procedural"));
            sky.SetFloat("_SunSize", 0.04f);
            sky.SetFloat("_AtmosphereThickness", 1f);
            sky.SetColor("_SkyTint", new Color(0.5f, 0.65f, 0.85f));
            sky.SetColor("_GroundColor", new Color(0.35f, 0.35f, 0.3f));
            sky.SetFloat("_Exposure", 1.1f);

            AssetDatabase.CreateAsset(sky, path);
            return sky;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
            string folderName = System.IO.Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }

        // Invisible collider-only walls just past Ground's edges (Ground is a 30x30 cube
        // centered on the origin, so it spans X/Z [-15, 15]) - reported as "the character
        // disappears at the boundary": with nothing stopping the player from walking past the
        // edge of the finite ground plane, they fall through empty space under gravity
        // indefinitely, which reads as the character vanishing. No MeshRenderer, collider only.
        private static void CreateBoundaryWalls()
        {
            const float halfExtent = 15f;
            const float wallHeight = 6f;
            const float wallThickness = 1f;
            // Slightly past the edge (halfExtent, not halfExtent - thickness/2) so the inner
            // face sits flush with Ground's edge rather than eating into the playable area.
            float wallCenterOffset = halfExtent + wallThickness / 2f;
            float wallCenterY = wallHeight / 2f;
            // Overlap past the corners so the four walls don't leave a diagonal gap.
            float wallSpan = halfExtent * 2f + wallThickness * 2f;

            CreateBoundaryWall("BoundaryWall_North", new Vector3(0f, wallCenterY, wallCenterOffset), new Vector3(wallSpan, wallHeight, wallThickness));
            CreateBoundaryWall("BoundaryWall_South", new Vector3(0f, wallCenterY, -wallCenterOffset), new Vector3(wallSpan, wallHeight, wallThickness));
            CreateBoundaryWall("BoundaryWall_East", new Vector3(wallCenterOffset, wallCenterY, 0f), new Vector3(wallThickness, wallHeight, wallSpan));
            CreateBoundaryWall("BoundaryWall_West", new Vector3(-wallCenterOffset, wallCenterY, 0f), new Vector3(wallThickness, wallHeight, wallSpan));
        }

        private static void CreateBoundaryWall(string name, Vector3 position, Vector3 size)
        {
            var wall = new GameObject(name);
            wall.transform.position = position;
            BoxCollider collider = wall.AddComponent<BoxCollider>();
            collider.size = size;
        }

        private static void CreateCoverBlocks()
        {
            Vector3[] positions =
            {
                new Vector3(3f, 0.5f, 2f),
                new Vector3(-3f, 0.5f, -2f),
                new Vector3(0f, 0.5f, 5f),
            };

            for (int i = 0; i < positions.Length; i++)
            {
                GameObject cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cover.name = "CoverBlock" + (i + 1);
                cover.transform.position = positions[i];
                cover.transform.localScale = Vector3.one;
            }
        }

        private static GameObject CreatePlayer()
        {
            var player = new GameObject("Player");

            CapsuleCollider capsuleReference = player.AddComponent<CapsuleCollider>();
            float height = capsuleReference.height;
            float radius = capsuleReference.radius;
            Object.DestroyImmediate(capsuleReference);

            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = height;
            controller.radius = radius;
            controller.center = Vector3.zero;
            // Default (0.001) silently drops any Move() call smaller than that - on a fast
            // enough machine (or headless batchmode, measured ~9000fps - see
            // Docs/KNOWN_ISSUES.md) moveSpeed*deltaTime falls below that threshold on nearly
            // every frame, making movement barely register. 0 disables the filtering.
            controller.minMoveDistance = 0f;
            // Default (0.3) lets this CharacterController auto-climb up to that height onto
            // whatever it's pushed against - including another character's own rounded
            // capsule top. Real 2026-08-12 bug report ("很靠近敵人時角色1突然消失，畫面定格"):
            // walking straight into Player4 (also a CharacterController) let Player climb up
            // its shoulder/head over a few seconds of continued forward input, launching from
            // Y=0.58 to Y=1.66 and then getting stuck oscillating back and forth at the top
            // (confirmed by a diagnostic PlayMode test that reproduced it, then confirmed
            // stepOffset=0 stops the climb entirely with Y staying flat). 0 is fine here since
            // this greybox scene has no actual stairs/curbs the player needs to step onto -
            // the cover blocks are meant to block movement, not be climbed over.
            controller.stepOffset = 0f;

            // Derived from Ground's actual collider bounds rather than a hardcoded Y so this
            // can't quietly drift into a floating-capsule bug if height/radius are tuned
            // later (see FixPlayerGroundedSpawn.cs for the bug this caused once already).
            // X/Z spawn beside TrainingDummy (at world origin) rather than in front of it -
            // reported as "I spawn on top of the pillar and fall off" (the two were 2 units
            // apart, not actually overlapping, but the very close default camera distance made
            // it read that way; spawning to the side removes the ambiguity entirely).
            GameObject ground = GameObject.Find("Ground");
            float groundTopY = ground != null ? ground.GetComponent<Collider>().bounds.max.y : 0f;
            player.transform.position = new Vector3(-2.5f, groundTopY + controller.center.y + controller.height / 2f, 0f);

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(player.transform, false);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            // Left visible: the camera sits pulled back at ThirdPersonCameraController's
            // default distance (0.5), not exactly at the eye point, so it clears this mesh
            // instead of sitting inside it. Only distance 0 (true first-person) needs this
            // hidden - see ThirdPersonCameraController's class comment.

            PlayerInputProvider inputProvider = player.AddComponent<PlayerInputProvider>();
            CharacterMovement movement = player.AddComponent<CharacterMovement>();
            PlayerCombat combat = player.AddComponent<PlayerCombat>();
            TargetLockController lockController = player.AddComponent<TargetLockController>();
            Health playerHealth = player.AddComponent<Health>();

            var lockControllerSo = new SerializedObject(lockController);
            lockControllerSo.FindProperty("inputSource").objectReferenceValue = inputProvider;
            lockControllerSo.FindProperty("maxLockRange").floatValue = 15f;
            lockControllerSo.FindProperty("maxLockAngleDegrees").floatValue = 60f;
            lockControllerSo.FindProperty("breakRange").floatValue = 20f;
            lockControllerSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject movementSo = new SerializedObject(movement);
            movementSo.FindProperty("inputSource").objectReferenceValue = inputProvider;
            movementSo.FindProperty("dodgeData").objectReferenceValue = CreateOrLoadDodgeData();
            movementSo.FindProperty("health").objectReferenceValue = playerHealth;
            movementSo.ApplyModifiedPropertiesWithoutUndo();

            AttackData[] comboAttacks = CreateOrLoadComboAttacks();
            SerializedObject combatSo = new SerializedObject(combat);
            combatSo.FindProperty("inputSource").objectReferenceValue = inputProvider;
            SerializedProperty comboProperty = combatSo.FindProperty("comboAttacks");
            comboProperty.arraySize = comboAttacks.Length;
            for (int i = 0; i < comboAttacks.Length; i++)
            {
                comboProperty.GetArrayElementAtIndex(i).objectReferenceValue = comboAttacks[i];
            }
            combatSo.FindProperty("hitEffectPrefab").objectReferenceValue = HitEffectSetup.CreateOrLoadHitEffectPrefab();
            combatSo.ApplyModifiedPropertiesWithoutUndo();

            return player;
        }

        // Default frame data (see AttackData.FramesPerSecond) is a reasoned starting point,
        // not tuned-by-feel numbers - matches common action-game proportions (each hit a bit
        // slower/heavier than the last) and is meant to be adjusted from these assets in the
        // Inspector, never by editing this script.
        private static AttackData[] CreateOrLoadComboAttacks()
        {
            return new[]
            {
                CreateOrLoadAttackData("LightAttack1", damage: 8f, startupFrames: 6, activeFrames: 4, recoveryFrames: 14, comboWindowFrames: 10),
                CreateOrLoadAttackData("LightAttack2", damage: 10f, startupFrames: 7, activeFrames: 4, recoveryFrames: 16, comboWindowFrames: 10),
                CreateOrLoadAttackData("LightAttack3", damage: 16f, startupFrames: 10, activeFrames: 5, recoveryFrames: 22, comboWindowFrames: 0),
            };
        }

        // Reasoned starting point (see DodgeData.FramesPerSecond): a quick 3-unit burst
        // (12 frames = 0.2s), fully invulnerable for its duration, with a 20-frame (~0.33s)
        // cooldown to prevent spamming - meant to be tuned from the asset, not this script.
        private static DodgeData CreateOrLoadDodgeData()
        {
            const string assetPath = "Assets/_Project/Settings/DodgeData.asset";
            var existing = AssetDatabase.LoadAssetAtPath<DodgeData>(assetPath);
            if (existing != null)
            {
                return existing;
            }

            var data = ScriptableObject.CreateInstance<DodgeData>();
            var so = new SerializedObject(data);
            so.FindProperty("distance").floatValue = 3f;
            so.FindProperty("durationFrames").intValue = 12;
            so.FindProperty("invulnerabilityFrames").intValue = 12;
            so.FindProperty("cooldownFrames").intValue = 20;
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(data, assetPath);
            return data;
        }

        private static AttackData CreateOrLoadAttackData(string assetName, float damage, int startupFrames, int activeFrames, int recoveryFrames, int comboWindowFrames)
        {
            string path = $"{ComboAttacksFolder}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<AttackData>(path);
            if (existing != null)
            {
                return existing;
            }

            if (!AssetDatabase.IsValidFolder(ComboAttacksFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Settings", "Combat");
            }

            var data = ScriptableObject.CreateInstance<AttackData>();
            var so = new SerializedObject(data);
            so.FindProperty("attackId").stringValue = assetName;
            so.FindProperty("damage").floatValue = damage;
            so.FindProperty("startupFrames").intValue = startupFrames;
            so.FindProperty("activeFrames").intValue = activeFrames;
            so.FindProperty("recoveryFrames").intValue = recoveryFrames;
            so.FindProperty("comboWindowFrames").intValue = comboWindowFrames;
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(data, path);
            return data;
        }

        // TrainingDummy is now an AI-driven enemy rather than a static target - it reuses
        // PlayerCombat for its attack (see EnemyAI: it implements IInputCommand purely so the
        // same frame-data combo pipeline the player uses can be shared, per the project rule
        // that player and AI input share one interface). Built here with a plain capsule
        // "Visual" child (function before art); EnemyHumanoidVisualSetup.cs swaps that for
        // the Quaternius Humanoid placeholder afterward, same two-step pattern as Player's
        // various visual swaps - not folded into this method for the same reason those aren't.
        private static GameObject CreateEnemy(Transform playerTarget)
        {
            var enemy = new GameObject("TrainingDummy");

            CapsuleCollider capsuleReference = enemy.AddComponent<CapsuleCollider>();
            float height = capsuleReference.height;
            float radius = capsuleReference.radius;
            Object.DestroyImmediate(capsuleReference);

            CharacterController controller = enemy.AddComponent<CharacterController>();
            controller.height = height;
            controller.radius = radius;
            controller.center = Vector3.zero;
            // Default (0.001) silently drops any Move() call smaller than that - on a fast
            // enough machine (or headless batchmode, measured ~9000fps - see
            // Docs/KNOWN_ISSUES.md) moveSpeed*deltaTime falls below that threshold on nearly
            // every frame, making movement barely register. 0 disables the filtering.
            controller.minMoveDistance = 0f;
            // See CreatePlayer's own stepOffset=0 comment - prevents this CharacterController
            // from climbing up onto the Player's (or any other character's) rounded capsule
            // top when pushed directly into it.
            controller.stepOffset = 0f;

            GameObject ground = GameObject.Find("Ground");
            float groundTopY = ground != null ? ground.GetComponent<Collider>().bounds.max.y : 0f;
            enemy.transform.position = new Vector3(0f, groundTopY + controller.center.y + controller.height / 2f, 0f);

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(enemy.transform, false);
            Object.DestroyImmediate(visual.GetComponent<Collider>());

            enemy.AddComponent<Health>();
            enemy.AddComponent<LockOnTarget>();

            EnemyAI ai = enemy.AddComponent<EnemyAI>();
            var aiSo = new SerializedObject(ai);
            aiSo.FindProperty("target").objectReferenceValue = playerTarget;
            // 0 = never chases (DetermineState's first check, distance > detectionRange, is
            // then true at any real distance, so it stays Idle forever) - a training dummy
            // that follows the player around was reported as confusing ("white pillar keeps
            // following me"), and a stationary target fits the name better anyway.
            aiSo.FindProperty("detectionRange").floatValue = 0f;
            aiSo.FindProperty("attackRange").floatValue = 2f;
            aiSo.FindProperty("moveSpeed").floatValue = 2f;
            aiSo.ApplyModifiedPropertiesWithoutUndo();

            PlayerCombat combat = enemy.AddComponent<PlayerCombat>();
            AttackData enemyAttack = CreateOrLoadEnemyAttack();
            var combatSo = new SerializedObject(combat);
            combatSo.FindProperty("inputSource").objectReferenceValue = ai;
            SerializedProperty comboProperty = combatSo.FindProperty("comboAttacks");
            comboProperty.arraySize = 1;
            comboProperty.GetArrayElementAtIndex(0).objectReferenceValue = enemyAttack;
            combatSo.FindProperty("hitEffectPrefab").objectReferenceValue = HitEffectSetup.CreateOrLoadHitEffectPrefab();
            combatSo.ApplyModifiedPropertiesWithoutUndo();

            return enemy;
        }

        // Lower damage than the player's combo (see CreateOrLoadComboAttacks) since the
        // player is expected to generally win a straight fight - a reasoned starting point,
        // not a balanced/tuned value.
        private static AttackData CreateOrLoadEnemyAttack()
        {
            return CreateOrLoadAttackData("EnemyAttack", damage: 5f, startupFrames: 10, activeFrames: 4, recoveryFrames: 20, comboWindowFrames: 0);
        }

        private static ThirdPersonCameraController CreateCamera(Transform followTarget)
        {
            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            Camera camera = cameraGo.AddComponent<Camera>();
            // Wider than a typical 50-60 degree default: at the close eye-level distance this
            // camera sits at, a narrow FOV's vertical slice crops the legs/feet off of any
            // nearby subject (reported for both Player and Player2). See
            // ThirdPersonCameraController's class comment.
            camera.fieldOfView = 65f;

            // Custom, Cinemachine-free camera: see ThirdPersonCameraController for why
            // Cinemachine's orbital/aim system was removed (Docs/KNOWN_ISSUES.md has the full
            // investigation). CharacterMovement reads YawDegrees from this same component for
            // its camera-relative movement math, so screen orientation and movement direction
            // can never disagree. Mouse-look (RPG-style, no button held) drives yaw/pitch once
            // playing.
            ThirdPersonCameraController controller = cameraGo.AddComponent<ThirdPersonCameraController>();
            var controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("target").objectReferenceValue = followTarget;
            // distance/targetOffset match the component's own field defaults, which are the
            // user's confirmed hands-on tuning (see ThirdPersonCameraController's field
            // comment) - do not "fix" these back toward an earlier/theoretical value without
            // asking first. (2026-08-12: briefly changed to a locked right-shoulder rig,
            // reverted back to this free-look scheme the same day by explicit request; later
            // the same day raised from 0.8 to 2 by explicit request alongside adding camera
            // collision avoidance.)
            controllerSo.FindProperty("distance").floatValue = 2f;
            controllerSo.FindProperty("targetOffset").vector3Value = new Vector3(0f, 0.5f, 0f);
            controllerSo.FindProperty("initialYaw").floatValue = 0f;
            controllerSo.FindProperty("initialPitch").floatValue = 0f;
            // Genshin/Wuthering-Waves-style auto-center (2026-08-12) - see
            // ThirdPersonCameraController's field comments for why this can't reintroduce the
            // reverted right-shoulder rig's feedback loop. lockOnSource is wired below in
            // Build(), once Player's TargetLockController exists.
            controllerSo.FindProperty("enableAutoCenter").boolValue = true;
            controllerSo.FindProperty("autoCenterDelay").floatValue = 0.8f;
            controllerSo.FindProperty("autoCenterSpeed").floatValue = 2f;
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            return controller;
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool alreadyPresent = scenes.Exists(s => s.path == scenePath);
            if (!alreadyPresent)
            {
                scenes.Add(new EditorBuildSettingsScene(scenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }
    }
}
