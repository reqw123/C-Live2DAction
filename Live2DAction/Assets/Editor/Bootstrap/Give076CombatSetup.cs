using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2D.Cubism.Core;
using Live2D.Cubism.Framework.Json;
using Live2D.Cubism.Rendering;
using Live2DAction.AI;
using Live2DAction.Characters;
using Live2DAction.Combat;
using Live2DAction.Core;
using Live2DAction.Targeting;

namespace Live2DAction.EditorTools
{
    // 2026-08-20, explicit user request ("接下來把076賦予攻擊 復活機制") - 076 has been a purely
    // visual Live2D billboard (see Live2DStandeeSetup.cs) with no Collider/Health/combat logic
    // for most of the project's history (KNOWN_ISSUES.md, 2026-08-13 collider audit: "076/077
    // Live2D 立牌...刻意沒有碰撞體...從來沒有接過戰鬥邏輯"). BUT EnemyAI.cs already carries
    // extensive 2026-08-17/08-18 comments describing real bugs fixed for 076 SPECIFICALLY once
    // it briefly WAS wired up as a full combat AI (height-only-horizontal detection distance for
    // a tall standee, "統一面對玩家" replacing CubismBillboard's camera-facing to stop it fighting
    // EnemyAI's own facing logic, opting out of StancePoise) - that live setup was lost in the
    // same disappearance event that deleted the 076/077 GameObjects entirely (see this session's
    // own investigation), while the underlying reusable logic (EnemyAI/PlayerCombat/Health/
    // RespawnController, all shared with Enemy/Mecha) stayed intact in the codebase the whole
    // time. This tool re-wires 076 onto that same existing system rather than building anything
    // new - same pattern as GreyboxSceneBuilder.CreateEnemy + EnemyRespawnSetup.cs, just applied
    // to 076's GameObject instead of Enemy's.
    internal static class Give076CombatSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string EnemyAttackAssetPath = "Assets/_Project/Settings/Combat/EnemyAttack.asset";
        private const string EnemyAttack076AssetPath = "Assets/_Project/Settings/Combat/EnemyAttack076.asset";
        private const string ShaderName = "Live2DAction/CubismUnlitURP";
        private const float TargetHeightMeters = 1.8f;

        private static readonly Vector3 Position076 = new Vector3(-6f, 0f, -8f);
        private static readonly Vector3 Position077 = new Vector3(-3f, 0f, -8f);
        private const string Model076Path = "Assets/_Project/Live2D/PlaceholderCharacter/c_7001.model3.json";
        private const string Model077Path = "Assets/_Project/Live2D/PlaceholderCharacter077/c_7002.model3.json";

        [MenuItem("Tools/Live2DAction/Add Combat + Respawn To 076")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 2026-08-20, real observed instability (this session's own investigation) - 076/077's
            // CubismModel root transform (not just its name - see the well-documented name bug in
            // KNOWN_ISSUES.md) has been seen drifting to an unrelated position with no explicit
            // scene-reload/save in between. Rather than trust whatever GameObject.Find("076_
            // DoNotShip") happens to locate (which may be stale, blank-named, or sitting at a
            // drifted position), this rebuilds BOTH standees fresh from their model3.json every
            // time this tool runs - identical construction to Live2DStandeeSetup.CreateStandee,
            // duplicated here (that method is private to that file) so this stays a single atomic
            // Apply() with one Scene load/save instead of chaining two separate tool invocations,
            // which is exactly where the drift was actually observed to happen.
            DestroyExistingStandaloneCubismModels();
            GameObject standee = CreateStandee("076_DoNotShip", Model076Path, Position076);
            CreateStandee("077_DoNotShip", Model077Path, Position077); // untouched by combat wiring below - stays a pure visual standee, keeps its CubismBillboard

            // Measured BEFORE adding 076's own CharacterController below - a raycast run after
            // would hit that fresh capsule's own top first (confirmed live: Physics.RaycastAll at
            // this exact spot returned CheckpointGate_0's touch-trigger sphere, THEN 076's own
            // just-added capsule, THEN the real Ground, in that order) and report a wildly wrong
            // "ground" height. FindGroundY also explicitly ignores triggers for the same reason -
            // Ground is the only thing that should ever answer this question.
            float groundY = FindGroundY(standee.transform.position);

            // 2026-08-17 decision (see EnemyAI.cs's own header comment) - CubismBillboard always
            // re-faces the root at Camera.main every LateUpdate, which fights EnemyAI's facing
            // logic below (alwaysFaceTarget) and was the actual root cause of that day's "076攻擊
            // 不到我" bug. 077 is untouched by this tool and keeps its CubismBillboard - only 076
            // is becoming a combat character.
            CubismBillboard billboard = standee.GetComponent<CubismBillboard>();
            if (billboard != null)
            {
                Object.DestroyImmediate(billboard);
            }

            CharacterController controller = standee.GetComponent<CharacterController>();
            if (controller == null)
            {
                controller = standee.AddComponent<CharacterController>();
            }
            // 2026-08-20, explicit user request ("設計玩家076近距離貼身才會打得到") - WORLD-space
            // targets (2m tall, 0.4 radius - matches Player/Enemy exactly), divided by lossyScale
            // to get the LOCAL values CharacterController expects. Un-corrected local values
            // (height=1,radius=0.4) at 076's 5x visual scale gave a world radius of 2.0 - bigger
            // than Player's own body radius (0.4) - which physically kept the two
            // CharacterControllers too far apart for the melee attack (reach 0.3+0.5=0.8) to ever
            // land, regardless of AttackData tuning.
            controller.height = 2f / standee.transform.lossyScale.y;
            controller.radius = 0.4f / standee.transform.lossyScale.x;
            // Matches Enemy's own tuning (CreateEnemy/live scene) - prevents climbing onto
            // another character's rounded capsule top, see that field's own precedent comment.
            controller.stepOffset = 0f;
            controller.minMoveDistance = 0f;
            // Reverse-engineered from the real Ground collider under 076's CURRENT position
            // (same "don't assume a constant, measure the real geometry" convention as
            // FixPlayerGroundedSpawn.cs) rather than assuming the root sits exactly at foot
            // level - 076's root Y has drifted before (see this session's own investigation), so
            // this keeps the capsule's bottom flush with the actual ground regardless.
            //
            // 2026-08-20, real playtested bug ("076 play mode下y軸會不斷變小") - CharacterController.
            // center is defined in LOCAL space and gets multiplied by the transform's own
            // lossyScale before becoming a world-space offset (same class of bug Portal.cs's own
            // Move() already had to account for - see that method's "height is in local space and
            // scales with the character's own transform" comment). 076 is scaled up 5x for
            // visibility, so the original (unscaled) formula computed a center.y that, once
            // multiplied by 5, put the capsule's real world bottom ~8 units below the visible
            // model - isGrounded could never read true, so EnemyAI's gravity accumulated forever
            // (confirmed live: manually stepping CharacterController.Move in Play mode showed
            // isGrounded stuck false and position.y decreasing every step). Dividing by
            // lossyScale.y here converts the desired WORLD offset back into the LOCAL value
            // CharacterController.center actually expects, at any scale.
            float worldOffsetNeeded = groundY - standee.transform.position.y;
            controller.center = new Vector3(0f, worldOffsetNeeded / standee.transform.lossyScale.y + controller.height / 2f, 0f);

            Health health = standee.GetComponent<Health>();
            if (health == null)
            {
                health = standee.AddComponent<Health>();
            }

            if (standee.GetComponent<LockOnTarget>() == null)
            {
                standee.AddComponent<LockOnTarget>();
            }

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("Player not found - 076 needs it as EnemyAI's chase/attack target.");
                return;
            }

            EnemyAI ai = standee.GetComponent<EnemyAI>();
            if (ai == null)
            {
                ai = standee.AddComponent<EnemyAI>();
            }
            var aiSo = new SerializedObject(ai);
            aiSo.FindProperty("target").objectReferenceValue = player.transform;
            // 2026-08-20, explicit user request ("EnemyAI.detectionRange 改為3") - tuned down
            // twice this session (8 -> 1.6 -> 3), this is the latest value, not the original
            // "matches Enemy" starting point.
            aiSo.FindProperty("detectionRange").floatValue = 3f;
            aiSo.FindProperty("moveSpeed").floatValue = 2f;
            // See CubismBillboard removal above - this is what "統一面對玩家" actually becomes
            // now (EnemyAI.alwaysFaceTarget's own comment documents 076 by name as the character
            // this flag was originally added for).
            aiSo.FindProperty("alwaysFaceTarget").boolValue = true;
            // 2026-08-20, real playtested bug ("076真的能貼著玩家追擊 但076打不到我") - see
            // Reimport076Clean.cs's own comment / EnemyAI.alwaysUseSphericalJudgment's field
            // comment - a directional capsule swung mid-rotation can whiff an adjacent target.
            aiSo.FindProperty("alwaysUseSphericalJudgment").boolValue = true;
            // 2026-08-20, real playtested bug ("076沒辦法主動靠我靠得很近") - see
            // Reimport076Clean.cs's own comment / EnemyAI.alwaysChaseWhileAttacking's field
            // comment - ordinary ground melee stops moving as soon as it enters attack range,
            // short of true body contact.
            aiSo.FindProperty("alwaysChaseWhileAttacking").boolValue = true;
            aiSo.FindProperty("health").objectReferenceValue = health;
            // Deliberately left unset (null) - see EnemyAI.stance's own comment: "076, any future
            // enemy that never gets a StancePoise" was the explicit precedent for opting out of
            // the stagger/execution mechanic.
            aiSo.ApplyModifiedPropertiesWithoutUndo();

            PlayerCombat combat = standee.GetComponent<PlayerCombat>();
            if (combat == null)
            {
                combat = standee.AddComponent<PlayerCombat>();
            }
            // 2026-08-20 - superseded by Reimport076Clean.cs's own dedicated
            // EnemyAttack076.asset (see that file's comment: 076 used to share Enemy's own
            // EnemyAttack.asset directly, which meant tuning 076's Range/Radius also retuned
            // Enemy). Kept in sync here too so THIS tool doesn't silently re-point 076 back at
            // the shared asset (and blow away 076's own Range=0.7/Radius=0.5/detectionRange=3
            // tuning) if it's ever rerun instead of Reimport076Clean.
            AttackData enemyAttack = AssetDatabase.LoadAssetAtPath<AttackData>(EnemyAttack076AssetPath);
            if (enemyAttack == null)
            {
                AttackData shared = AssetDatabase.LoadAssetAtPath<AttackData>(EnemyAttackAssetPath);
                if (shared == null)
                {
                    Debug.LogError("Neither EnemyAttack076.asset nor the shared EnemyAttack.asset exist.");
                    return;
                }
                enemyAttack = Object.Instantiate(shared);
                AssetDatabase.CreateAsset(enemyAttack, EnemyAttack076AssetPath);
                var freshSo = new SerializedObject(enemyAttack);
                // See Reimport076Clean.cs's own comment - 1.2 total reach (0.7+0.5), not the
                // theoretical-but-untested 0.8, which left zero margin over the real measured
                // ~0.88 resting distance between two pushed-together CharacterControllers.
                freshSo.FindProperty("range").floatValue = 0.7f;
                freshSo.FindProperty("radius").floatValue = 0.5f;
                freshSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // 2026-08-20, real playtested bug ("攻擊距離不夠 還是沒打到") - see Reimport076Clean.cs's
            // own comment: 076's root sits at world Y~2 (raised so the visual feet reach the real
            // ground), but the Player's body sits around Y~0.6 - the default attackOrigin
            // (=root) never vertically overlapped the player regardless of Range/Radius.
            // AttackOrigin is a dedicated child positioned at the player's own capsule-center
            // height instead.
            GameObject attackOriginGo = new GameObject("AttackOrigin");
            attackOriginGo.transform.SetParent(standee.transform, false);
            CharacterController playerCc = player.GetComponent<CharacterController>();
            float targetWorldY = player.transform.position.y + playerCc.center.y;
            float localY = (targetWorldY - standee.transform.position.y) / standee.transform.lossyScale.y;
            attackOriginGo.transform.localPosition = new Vector3(0f, localY, 0f);

            var combatSo = new SerializedObject(combat);
            combatSo.FindProperty("inputSource").objectReferenceValue = ai;
            combatSo.FindProperty("attackOrigin").objectReferenceValue = attackOriginGo.transform;
            SerializedProperty comboProperty = combatSo.FindProperty("comboAttacks");
            comboProperty.arraySize = 1;
            comboProperty.GetArrayElementAtIndex(0).objectReferenceValue = enemyAttack;
            combatSo.FindProperty("hitEffectPrefab").objectReferenceValue = HitEffectSetup.CreateOrLoadHitEffectPrefab();
            combatSo.FindProperty("health").objectReferenceValue = health;
            combatSo.ApplyModifiedPropertiesWithoutUndo();

            // MeasureVisualTopLocalY (inside AddHealthBar) looks for a child named "Visual" -
            // 076 has no such child (its Cubism model root IS the whole visual), so it falls
            // back to the CharacterController's own capsule top, which is a fine placement here.
            HealthBarSetup.AddHealthBar(standee);

            WireRespawnController(standee, health);

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("076 now chases/attacks the player (EnemyAttack data, same as Enemy), has a health bar, and respawns in place 5s after dying. 077 is untouched (still a pure visual standee).");
        }

        // Same reclaim-orphan-before-adding-new pattern as EnemyRespawnSetup.cs/
        // MechaRespawnSetup.cs - see EnemyRespawnSetup's own comment for the 2026-08-13 bug
        // (duplicate RespawnControllers piling up on GameManager) this avoids repeating.
        private static void WireRespawnController(GameObject standee, Health health)
        {
            GameObject managerGo = GameObject.Find("GameManager");
            if (managerGo == null)
            {
                managerGo = new GameObject("GameManager");
            }

            RespawnController respawnController = null;
            RespawnController orphan = null;
            foreach (RespawnController candidate in managerGo.GetComponents<RespawnController>())
            {
                var candidateSo = new SerializedObject(candidate);
                Object candidateTarget = candidateSo.FindProperty("target").objectReferenceValue;
                if (candidateTarget == standee)
                {
                    respawnController = candidate;
                    break;
                }
                if (candidateTarget == null && orphan == null)
                {
                    orphan = candidate;
                }
            }
            respawnController = respawnController != null ? respawnController : orphan;
            if (respawnController == null)
            {
                respawnController = managerGo.AddComponent<RespawnController>();
            }

            var so = new SerializedObject(respawnController);
            so.FindProperty("target").objectReferenceValue = standee;
            so.FindProperty("targetHealth").objectReferenceValue = health;
            so.FindProperty("respawnDelaySeconds").floatValue = 5f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // QueryTriggerInteraction.Ignore - confirmed live this matters: CheckpointGate_0's own
        // touch-trigger SphereCollider (radius 2, see CheckpointGate/SkyIslandTimeTrialSetup)
        // happens to sit in the same XZ column as 076's spawn point up near the sky course, and a
        // default Raycast (which hits triggers too) reported ITS surface as "the ground" instead
        // of continuing down to the real one.
        private static float FindGroundY(Vector3 from)
        {
            if (Physics.Raycast(from + Vector3.up * 30f, Vector3.down, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore))
            {
                return hit.point.y;
            }

            return from.y; // no ground found directly below - assume the root is already at ground level
        }

        // Catches every standalone (unparented) CubismModel root regardless of what name it
        // currently has (blank or otherwise) or where it's drifted to - see this method's own
        // call site comment. Only touches root-level models (mirrors FixLive2DStandeeNames'
        // same parent==null filter), so this can never reach into some other nested Cubism
        // model this project might add later under a different parent.
        private static void DestroyExistingStandaloneCubismModels()
        {
            CubismModel[] models = Object.FindObjectsByType<CubismModel>(FindObjectsSortMode.None);
            foreach (CubismModel model in models)
            {
                if (model.transform.parent == null)
                {
                    Object.DestroyImmediate(model.gameObject);
                }
            }
        }

        // Identical construction to Live2DStandeeSetup.CreateStandee - see this file's own
        // class-level comment for why this is duplicated here instead of calling into that
        // (private) method directly.
        private static GameObject CreateStandee(string name, string model3JsonPath, Vector3 position)
        {
            CubismModel3Json modelJson = CubismModel3Json.LoadAtPath(model3JsonPath);
            CubismModel model = modelJson.ToModel();
            GameObject modelGo = model.gameObject;
            modelGo.name = name;

            ApplyUrpShader(modelGo);

            float canvasHeightUnityUnits = model.CanvasInformation.CanvasHeight / model.CanvasInformation.PixelsPerUnit;
            float scale = canvasHeightUnityUnits > 0.0001f ? TargetHeightMeters / canvasHeightUnityUnits : 1f;

            modelGo.transform.position = position;
            modelGo.transform.rotation = Quaternion.identity;
            modelGo.transform.localScale = Vector3.one * scale;

            modelGo.AddComponent<CubismBillboard>();

            return modelGo;
        }

        private static void ApplyUrpShader(GameObject modelGo)
        {
            Shader urpShader = Shader.Find(ShaderName);
            if (urpShader == null)
            {
                Debug.LogError("Could not find shader " + ShaderName + " - model will render with the incompatible built-in RP shader.");
                return;
            }

            CubismRenderer[] renderers = modelGo.GetComponentsInChildren<CubismRenderer>();
            foreach (CubismRenderer renderer in renderers)
            {
                if (renderer.Material != null)
                {
                    renderer.Material.shader = urpShader;
                }
            }
        }
    }
}
