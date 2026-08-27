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
    // 2026-08-20, explicit user request ("我想你把有關076的物件移除再重新導入乾淨的") - this
    // session's own investigation found 076's CubismModel root keeps drifting on its own (name
    // going blank AND the transform position itself changing between tool calls with no explicit
    // edit in between - most recently caught red-handed: set to (-6, 1.98, -14) one message ago,
    // found at (-6.04, 3.16, -14) this message with nothing in between touching it). Rather than
    // patch the drift again, this deletes 076's GameObject entirely and rebuilds it fresh from
    // c_7001.model3.json - same "when an object's accumulated state can't be trusted, start over
    // from the source of truth" approach Give076CombatSetup.cs already used for both standees.
    //
    // ONLY 076 - 077 is explicitly left untouched (user: "我已經固定077位置"). Re-wires the exact
    // same combat setup 076 already had (CharacterController/Health/EnemyAI/PlayerCombat/
    // LockOnTarget/HealthBar/RespawnController), reusing the EXISTING EnemyAttack076.asset (kept
    // at the user's own tuned Range=1.0) rather than re-cloning it from EnemyAttack fresh, which
    // would have silently reset that tuning back to Range=4.
    internal static class Reimport076Clean
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string ShaderName = "Live2DAction/CubismUnlitURP";
        private const string Model076Path = "Assets/_Project/Live2D/PlaceholderCharacter/c_7001.model3.json";
        private const string EnemyAttackAssetPath = "Assets/_Project/Settings/Combat/EnemyAttack.asset";
        private const string EnemyAttack076AssetPath = "Assets/_Project/Settings/Combat/EnemyAttack076.asset";
        private const float TargetHeightMeters = 1.8f;

        [MenuItem("Tools/Live2DAction/[Fix] Reimport 076 Clean")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // Preserve WHERE 076 currently belongs (X/Z placement next to 077, and whatever scale
            // it was last set to) before destroying it - only the corrupted/drifted state is being
            // thrown away, not the user's own placement decisions. Y is deliberately NOT carried
            // over - that's exactly the value that keeps drifting, and it gets recomputed properly
            // below from the real render bounds instead.
            GameObject oldStandee = FindStandee076();
            Vector3 xzAndScale = oldStandee != null ? oldStandee.transform.position : new Vector3(-6f, 0f, -14f);
            Vector3 scale = oldStandee != null ? oldStandee.transform.localScale : Vector3.one * TargetHeightMeters;
            if (oldStandee != null)
            {
                Object.DestroyImmediate(oldStandee);
            }

            CubismModel3Json modelJson = CubismModel3Json.LoadAtPath(Model076Path);
            CubismModel model = modelJson.ToModel();
            GameObject standee = model.gameObject;
            standee.name = "076_DoNotShip";
            standee.transform.rotation = Quaternion.identity;
            standee.transform.localScale = scale;
            standee.transform.position = new Vector3(xzAndScale.x, 0f, xzAndScale.z);

            ApplyUrpShader(standee);

            // Places the rendered feet exactly AT the real Ground surface (not just "some Y value")
            // - see this session's own "076只有身體上半身在上面" investigation for why trusting a
            // remembered/copied Y instead of the actual render bounds caused that clipping bug.
            float groundY = FindGroundY(standee.transform.position);
            Renderer[] renderers = standee.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            Vector3 pos = standee.transform.position;
            pos.y += groundY - bounds.min.y;
            standee.transform.position = pos;

            CharacterController controller = standee.AddComponent<CharacterController>();
            // 2026-08-20, explicit user request ("設計玩家076近距離貼身才會打得到") - height/radius
            // are WORLD-space targets (2m tall, 0.4 radius - matches Player/Enemy exactly) divided
            // by lossyScale to get the correct LOCAL values CharacterController actually expects
            // (same lossyScale-correction this file's own center calculation below already needed
            // - see that field's comment). Un-corrected local values (height=1,radius=0.4) at
            // 076's 5x visual scale produced a world radius of 2.0 - more than Player's own body
            // radius (0.4) alone - which physically prevented the two CharacterControllers from
            // ever getting close enough for a melee attack (reach 0.3+0.5=0.8) to land at all.
            controller.height = 2f / standee.transform.lossyScale.y;
            controller.radius = 0.4f / standee.transform.lossyScale.x;
            controller.stepOffset = 0f;
            controller.minMoveDistance = 0f;
            // 2026-08-20, real playtested bug ("076 play mode下y軸會不斷變小") - CharacterController.
            // center is LOCAL space and gets multiplied by the transform's lossyScale before
            // becoming a world offset (same bug class as Portal.cs's own height*lossyScale.y
            // handling). 076 is scaled 5x, so the naive (unscaled) formula put the capsule's real
            // world bottom several units below the visible model - isGrounded never read true,
            // gravity accumulated forever. Divide by lossyScale.y to convert the desired WORLD
            // offset into the LOCAL value CharacterController.center actually expects.
            controller.center = new Vector3(0f, (groundY - standee.transform.position.y) / standee.transform.lossyScale.y + controller.height / 2f, 0f);

            Health health = standee.AddComponent<Health>();
            standee.AddComponent<LockOnTarget>();

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("Player not found - 076 needs it as EnemyAI's chase/attack target.");
                return;
            }

            EnemyAI ai = standee.AddComponent<EnemyAI>();
            var aiSo = new SerializedObject(ai);
            aiSo.FindProperty("target").objectReferenceValue = player.transform;
            // 2026-08-20, explicit user request ("EnemyAI.detectionRange 改為3") - tuned down
            // twice this session (8 -> 1.6 -> 3), this is the latest value, not the original.
            aiSo.FindProperty("detectionRange").floatValue = 3f;
            aiSo.FindProperty("moveSpeed").floatValue = 2f;
            aiSo.FindProperty("alwaysFaceTarget").boolValue = true;
            // 2026-08-20, real playtested bug ("076真的能貼著玩家追擊 但076打不到我") - see
            // EnemyAI.alwaysUseSphericalJudgment's own field comment: a directional capsule swung
            // while alwaysFaceTarget's gradual RotateTowards hasn't quite finished turning yet
            // can whiff a genuinely-adjacent player on facing angle alone. Omnidirectional
            // judgment makes that timing irrelevant - confirmed live by deliberately facing 076
            // directly AWAY from the player and re-running the exact hit query, which still hit.
            aiSo.FindProperty("alwaysUseSphericalJudgment").boolValue = true;
            // 2026-08-20, real playtested bug ("076沒辦法主動靠我靠得很近") - ordinary ground melee
            // stops moving the instant it enters attack range (up to 1.2 units away, not
            // touching) and just plants/swings from there. This keeps 076 walking all the way in
            // through its own Attacking state, so physics naturally presses it to the real
            // body-contact minimum (~0.88) instead of stopping short - confirmed live by
            // simulating a 2.5-unit approach, which converged to exactly 0.88 instead of
            // stalling at 1.2.
            aiSo.FindProperty("alwaysChaseWhileAttacking").boolValue = true;
            aiSo.FindProperty("health").objectReferenceValue = health;
            aiSo.ApplyModifiedPropertiesWithoutUndo();

            PlayerCombat combat = standee.AddComponent<PlayerCombat>();
            // Reuse the EXISTING 076-specific attack asset (keeps the user's own tuning from
            // this session) - only falls back to cloning fresh from the shared EnemyAttack.asset
            // if EnemyAttack076.asset has somehow never existed, in which case it's immediately
            // retuned to the melee values below rather than left at the shared asset's own
            // (probably longer-range) numbers.
            AttackData enemyAttack076 = AssetDatabase.LoadAssetAtPath<AttackData>(EnemyAttack076AssetPath);
            if (enemyAttack076 == null)
            {
                AttackData shared = AssetDatabase.LoadAssetAtPath<AttackData>(EnemyAttackAssetPath);
                enemyAttack076 = Object.Instantiate(shared);
                AssetDatabase.CreateAsset(enemyAttack076, EnemyAttack076AssetPath);
                var freshSo = new SerializedObject(enemyAttack076);
                // 2026-08-20, explicit user request ("設計...近距離貼身才會打得到") - real measured
                // resting distance between two radius-0.4 CharacterControllers pushed fully
                // together is ~0.88 (radius sum 0.8 + CharacterController's own skin width), not
                // the theoretical 0.8 - a reach of exactly 0.8 left ZERO margin and never
                // actually triggered even standing pressed together. 1.2 total reach (Range 0.7 +
                // Radius 0.5) gives real margin while still requiring near-total body overlap.
                freshSo.FindProperty("range").floatValue = 0.7f;
                freshSo.FindProperty("radius").floatValue = 0.5f;
                freshSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // 2026-08-20, real playtested bug ("攻擊距離不夠 還是沒打到") - PlayerCombat's attack
            // capsule fires from attackOrigin, which defaults to the character's own root
            // transform. 076's root sits at world Y~2 (raised so the visual feet reach the real
            // ground - see the groundY alignment above), but the Player's own body sits down
            // around Y~0.6 - the attack capsule at Y~2 never vertically overlapped the player at
            // all, regardless of how generous Range/Radius were (confirmed live: Physics.
            // OverlapCapsule at the raw root found 0 player hits at any reach; moving the origin
            // down to the player's own capsule-center height found the player immediately).
            // AttackOrigin is a dedicated child positioned at that height instead of reusing the
            // (visually-necessary, but combat-wrong) root height.
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
            comboProperty.GetArrayElementAtIndex(0).objectReferenceValue = enemyAttack076;
            combatSo.FindProperty("hitEffectPrefab").objectReferenceValue = HitEffectSetup.CreateOrLoadHitEffectPrefab();
            combatSo.FindProperty("health").objectReferenceValue = health;
            combatSo.ApplyModifiedPropertiesWithoutUndo();

            HealthBarSetup.AddHealthBar(standee);

            WireRespawnController(standee, health);

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("076 removed and reimported clean from c_7001.model3.json - same position (X=" + xzAndScale.x + ", Z=" + xzAndScale.z + ") and scale as before, feet re-measured flush with the real ground, same combat setup (Range=" + enemyAttack076.Range + "). 077 untouched.");
        }

        private static GameObject FindStandee076()
        {
            CubismModel[] models = Object.FindObjectsByType<CubismModel>(FindObjectsSortMode.None);
            foreach (CubismModel m in models)
            {
                if (m.transform.parent == null && m.GetComponent<CharacterController>() != null)
                {
                    return m.gameObject;
                }
            }
            return null;
        }

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
                if (candidateTarget != null && candidateTarget.name == "076_DoNotShip")
                {
                    respawnController = candidate;
                    break;
                }
                // The just-destroyed old 076 leaves its own RespawnController's target reading
                // as null (Unity's "missing reference" for a destroyed Object) - reclaim that
                // slot instead of adding a new component, same orphan-handling this project has
                // used since the 2026-08-13 duplicate-RespawnController bug (see
                // EnemyRespawnSetup.cs's own comment).
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

        private static float FindGroundY(Vector3 from)
        {
            if (Physics.Raycast(from + Vector3.up * 30f, Vector3.down, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore))
            {
                return hit.point.y;
            }

            return 0f;
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
