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
    // 2026-08-19, explicit user request ("請幫我把另外兩個也導入...匯入後也各建一個角色" then
    // "幫他們依序命名為 中立者1/2/3 且死亡後復活") - builds two MORE Player-equivalent, stationary
    // characters (same "只拿掉移動與輸入" scope as UnityChanCompanionSetup.cs - see that file's
    // own class comment for the full component-parity reasoning, not repeated here) from the
    // other two Asset Store packages imported this session, then renames all THREE (including
    // the already-built UnityChan) into a consistent "中立者1/2/3" sequence and adds
    // RespawnController to each so death is temporary, matching Player/Mecha/Enemy's own
    // precedent (see EnemyRespawnSetup.cs).
    //
    // Unlike UnityChan (a bare Humanoid FBX with no prefab, which needed the whole
    // Bake-Axis-Conversion investigation - see UnityChanCompanionSetup.UnityChanFbxImportFix),
    // both of these ship as ready-made PREFABS already correctly configured by their own asset
    // authors - same "just instantiate it" confidence PlayerMayaVisualSetup/EnemyAnimeVisualSetup
    // already have for Maya/Arisa. Each keeps its OWN native AnimatorController rather than
    // reusing Maya's shared one (deliberately NOT repeating the cross-rig retargeting gamble that
    // broke Unity-Chan) - correct idle/standing pose is guaranteed since it's each character's
    // own rig/clips, at the same accepted cost as UnityChan ended up NOT needing but these two
    // do: Attack1-4/Staggered/Execute/Dead are visually inert (harmless SetTrigger/SetBool
    // no-ops on a controller with no matching states) while the underlying StancePoise/Health/
    // Ultimate mechanics all still work correctly.
    internal static class NeutralCharacterSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";

        private const string HaonPrefabPath = "Assets/Haons SD series Pack/Prefab/CharacterSet/prf_Set Costume02 Misaki.prefab";
        private const string SapphiPrefabPath = "Assets/SapphiArt/SapphiArtchan/OBJ/SapphiArtchan.prefab";

        private const string LightAttack1Path = "Assets/_Project/Settings/Combat/LightAttack1.asset";
        private const string LightAttack2Path = "Assets/_Project/Settings/Combat/LightAttack2.asset";
        private const string LightAttack3Path = "Assets/_Project/Settings/Combat/LightAttack3.asset";
        private const string LightAttack4Path = "Assets/_Project/Settings/Combat/LightAttack4.asset";

        // "三角色並排在地圖角落" (explicit user request) - a back corner clear of every existing
        // placed object (see UnityChanCompanionSetup.SpawnXz's own clearance list), 1.2m apart
        // along X so their HUD bar stacks/execution indicators don't overlap.
        private static readonly Vector3 CornerOrigin = new Vector3(11f, 0f, 11f);
        private const float Spacing = 1.2f;

        [MenuItem("Tools/Live2DAction/Add Remaining Neutral Characters And Rename All Three")]
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
            var combo = new[] { attack1, attack2, attack3, attack4 };

            GameObject ground = GameObject.Find("Ground");
            float groundTopY = ground != null ? ground.GetComponent<Collider>().bounds.max.y : 0f;

            // --- Rename the already-built UnityChan into the new sequence first, and move it
            // into the corner line-up (position [0]) ---
            // Idempotent re-run: once renamed, "UnityChan" no longer exists to Find() - fall back
            // to the new name so running this tool a second time doesn't error out.
            GameObject unityChan = GameObject.Find("UnityChan");
            if (unityChan == null)
            {
                unityChan = GameObject.Find("中立者1");
            }
            if (unityChan == null)
            {
                Debug.LogError("Neither UnityChan nor 中立者1 found - run Add Unity-Chan Companion first.");
                return;
            }
            unityChan.name = "中立者1";
            unityChan.transform.position = new Vector3(CornerOrigin.x, unityChan.transform.position.y, CornerOrigin.z);

            // --- 中立者2: Haon SD series (Misaki costume set) ---
            GameObject neutral2 = BuildCharacter(
                "中立者2",
                HaonPrefabPath,
                new Vector3(CornerOrigin.x + Spacing, 0f, CornerOrigin.z),
                groundTopY,
                combo);

            // --- 中立者3: Amane Kisora-chan (SapphiArtchan) ---
            GameObject neutral3 = BuildCharacter(
                "中立者3",
                SapphiPrefabPath,
                new Vector3(CornerOrigin.x + Spacing * 2f, 0f, CornerOrigin.z),
                groundTopY,
                combo);

            if (neutral2 == null || neutral3 == null)
            {
                return;
            }

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            // ExecutionIndicatorSetup already derives its target list from every StancePoise
            // component in the scene, so re-running it picks up 中立者2/3 (and refreshes 中立者1's,
            // no-op since it's unchanged) for free - same reuse pattern UnityChanCompanionSetup
            // already established. DeathAnimationLink for all three is wired directly above
            // instead (中立者2/3's Animator lives under "Visual", not on it - DeathAnimationSetup's
            // own self-only GetComponent<Animator>() wouldn't find it; 中立者1 already has it from
            // when UnityChanCompanionSetup first built her, unaffected by the rename above since
            // that wiring is an object reference, not a name lookup).
            ExecutionIndicatorSetup.Apply();
            AddRespawn("中立者1");
            AddRespawn("中立者2");
            AddRespawn("中立者3");

            Debug.Log("Renamed UnityChan -> 中立者1 and built 中立者2 (Haon/Misaki)、中立者3 (SapphiArtchan), all three lined up in the map corner with respawn-after-death wired.");
        }

        private static GameObject BuildCharacter(string name, string prefabPath, Vector3 spawnXz, float groundTopY, AttackData[] combo)
        {
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset == null)
            {
                Debug.LogError("Could not load prefab at " + prefabPath);
                return null;
            }

            GameObject existing = GameObject.Find(name);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            var go = new GameObject(name);

            // Same footprint as TrainingDummySetup/UnityChanCompanionSetup - no CharacterController,
            // nothing to drive since movement is explicitly out of scope.
            CapsuleCollider collider = go.AddComponent<CapsuleCollider>();
            collider.center = Vector3.zero;
            collider.radius = 0.5f;
            collider.height = 1f;

            go.transform.position = new Vector3(
                spawnXz.x,
                groundTopY + collider.center.y + collider.height / 2f,
                spawnXz.z);

            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, go.transform);
            visual.name = "Visual";
            visual.transform.localPosition = new Vector3(0f, collider.center.y - collider.height / 2f, 0f);
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            Animator animator = visual.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
                // runtimeAnimatorController deliberately left as whatever the prefab already
                // ships with - see this file's own class comment for why (avoids repeating
                // UnityChan's cross-rig retargeting bug).
            }

            RemoveEmbeddedCameraRig(visual);
            RemoveEmbeddedPhysicsRig(visual);
            RemoveMissingScripts(visual);
            RemoveForeignAnimationManagers(visual);

            // 2026-08-19, real bug found by screenshot (same class of issue as
            // UnityChanCompanionSetup.ConvertRendererMaterialsToUrp) - SapphiArtchan's materials
            // use Built-in RP shaders (VertexLit / Toon-Lit-Outline, confirmed via each .mat's own
            // shader GUID), which render solid magenta under this project's URP. Haon's Misaki set
            // already ships URP-correct materials - HasProperty/already-urpLit guards make this a
            // safe no-op for her.
            ConvertRendererMaterialsToUrp(visual);

            Health health = go.AddComponent<Health>();

            TargetLockController lockController = go.AddComponent<TargetLockController>();

            PlayerCombat combat = go.AddComponent<PlayerCombat>();
            var combatSo = new SerializedObject(combat);
            SerializedProperty comboProperty = combatSo.FindProperty("comboAttacks");
            comboProperty.arraySize = combo.Length;
            for (int i = 0; i < combo.Length; i++)
            {
                comboProperty.GetArrayElementAtIndex(i).objectReferenceValue = combo[i];
            }
            combatSo.FindProperty("hitEffectPrefab").objectReferenceValue = HitEffectSetup.CreateOrLoadHitEffectPrefab();
            combatSo.FindProperty("lockOnSource").objectReferenceValue = lockController;
            combatSo.FindProperty("health").objectReferenceValue = health;
            combatSo.ApplyModifiedPropertiesWithoutUndo();
            // inputSource intentionally left unset - never actually attacks (see
            // TrainingDummySetup's own comment for why this alone guarantees it).

            if (animator != null)
            {
                CharacterAttackAnimationLink attackLink = go.AddComponent<CharacterAttackAnimationLink>();
                var attackLinkSo = new SerializedObject(attackLink);
                attackLinkSo.FindProperty("animator").objectReferenceValue = animator;
                attackLinkSo.ApplyModifiedPropertiesWithoutUndo();
            }

            HealthRegeneration regen = go.AddComponent<HealthRegeneration>();
            var regenSo = new SerializedObject(regen);
            regenSo.FindProperty("health").objectReferenceValue = health;
            regenSo.ApplyModifiedPropertiesWithoutUndo();

            StancePoise stance = go.AddComponent<StancePoise>();
            var combatSo2 = new SerializedObject(combat);
            combatSo2.FindProperty("stance").objectReferenceValue = stance;
            combatSo2.ApplyModifiedPropertiesWithoutUndo();

            if (animator != null)
            {
                StaggerAnimationLink staggerLink = go.AddComponent<StaggerAnimationLink>();
                var staggerLinkSo = new SerializedObject(staggerLink);
                staggerLinkSo.FindProperty("animator").objectReferenceValue = animator;
                staggerLinkSo.ApplyModifiedPropertiesWithoutUndo();
            }

            ExecutionAbility execution = go.AddComponent<ExecutionAbility>();
            if (animator != null)
            {
                var executionSo = new SerializedObject(execution);
                executionSo.FindProperty("animator").objectReferenceValue = animator;
                executionSo.ApplyModifiedPropertiesWithoutUndo();
            }

            UltimateEnergy energy = go.AddComponent<UltimateEnergy>();

            UltimateAbility ultimate = go.AddComponent<UltimateAbility>();
            var ultimateSo = new SerializedObject(ultimate);
            ultimateSo.FindProperty("energy").objectReferenceValue = energy;
            ultimateSo.ApplyModifiedPropertiesWithoutUndo();

            if (animator != null)
            {
                UltimateAttackAnimationSwap swap = go.AddComponent<UltimateAttackAnimationSwap>();
                var swapSo = new SerializedObject(swap);
                swapSo.FindProperty("animator").objectReferenceValue = animator;
                swapSo.ApplyModifiedPropertiesWithoutUndo();
            }

            lockController.gameObject.AddComponent<LockOnTarget>();

            if (animator != null)
            {
                DeathAnimationLink deathLink = go.AddComponent<DeathAnimationLink>();
                var deathLinkSo = new SerializedObject(deathLink);
                deathLinkSo.FindProperty("animator").objectReferenceValue = animator;
                deathLinkSo.ApplyModifiedPropertiesWithoutUndo();
                var healthSo = new SerializedObject(health);
                healthSo.FindProperty("deferDeactivationToDeathAnimation").boolValue = true;
                healthSo.ApplyModifiedPropertiesWithoutUndo();
            }

            HealthBarSetup.AddHealthBar(go);
            UltimateAbilitySetup.AddEnergyBar(go, energy);
            StanceBarSetup.AddStanceBar(go);

            return go;
        }

        private static void AddRespawn(string characterName)
        {
            GameObject character = GameObject.Find(characterName);
            if (character == null)
            {
                Debug.LogError(characterName + " GameObject not found - cannot wire respawn.");
                return;
            }

            Health targetHealth = character.GetComponent<Health>();
            if (targetHealth == null)
            {
                Debug.LogError(characterName + " has no Health component - cannot wire respawn.");
                return;
            }

            StancePoise targetStance = character.GetComponent<StancePoise>();

            GameObject managerGo = GameObject.Find("GameManager");
            if (managerGo == null)
            {
                managerGo = new GameObject("GameManager");
            }

            // Same reclaim-orphan-before-adding-new logic as EnemyRespawnSetup/MechaRespawnSetup
            // (see EnemyRespawnSetup.cs's own comment for the 2026-08-13 bug this avoids
            // repeating - re-running this tool must not pile up duplicate RespawnControllers).
            RespawnController respawnController = null;
            RespawnController orphan = null;
            foreach (RespawnController candidate in managerGo.GetComponents<RespawnController>())
            {
                var candidateSo = new SerializedObject(candidate);
                Object candidateTarget = candidateSo.FindProperty("target").objectReferenceValue;
                if (candidateTarget == character)
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
            so.FindProperty("target").objectReferenceValue = character;
            so.FindProperty("targetHealth").objectReferenceValue = targetHealth;
            so.FindProperty("targetStance").objectReferenceValue = targetStance;
            so.FindProperty("respawnDelaySeconds").floatValue = 5f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RemoveEmbeddedCameraRig(GameObject visual)
        {
            foreach (Camera embeddedCamera in visual.GetComponentsInChildren<Camera>(true))
            {
                Object.DestroyImmediate(embeddedCamera.gameObject);
            }
        }

        private static void RemoveEmbeddedPhysicsRig(GameObject visual)
        {
            foreach (Rigidbody rigidbody in visual.GetComponentsInChildren<Rigidbody>(true))
            {
                Object.DestroyImmediate(rigidbody);
            }

            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(collider);
            }
        }

        // 2026-08-19, real bug found in Play Mode (not caught by RemoveMissingScripts, which only
        // strips references that fail to resolve at all - this one compiles and runs fine, it's
        // just incompatible with our scene): SapphiArtchan's own prefab ships with
        // "SapphiArtChan_AnimManager" directly on its root, the asset author's own demo animation/
        // input-driven state machine - it expects other demo-scene-specific setup we don't have,
        // and spammed NullReferenceException every Update()/LateUpdate() once actually entering
        // Play Mode (silent in Edit Mode, since MonoBehaviour Update doesn't run there - this only
        // surfaced once the sword-showcase work actually needed a real Play Mode test). Same
        // "strip the author's own gameplay scripts, this project's own components replace them"
        // reasoning as EnemyAnimeVisualSetup stripping Arisa's PlayerBasicCode/PlayerMoveCode.
        // Matched by simple name (not a hard `using` reference) so this doesn't need a compile-
        // time dependency on either package's own namespace, and stays robust if a similar
        // demo-manager script turns up in some other imported character pack later.
        private static readonly string[] ForeignAnimationManagerTypeNames = { "SapphiArtChan_AnimManager" };

        private static void RemoveForeignAnimationManagers(GameObject visual)
        {
            foreach (MonoBehaviour mb in visual.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null)
                {
                    continue;
                }
                string typeName = mb.GetType().Name;
                foreach (string foreignName in ForeignAnimationManagerTypeNames)
                {
                    if (typeName == foreignName)
                    {
                        Object.DestroyImmediate(mb);
                        break;
                    }
                }
            }
        }

        private static void RemoveMissingScripts(GameObject visual)
        {
            foreach (Transform child in visual.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
            }
        }

        // Same approach as UnityChanCompanionSetup.ConvertRendererMaterialsToUrp - find URP/Lit,
        // copy _MainTex/_Color across if the source shader happens to expose them under those
        // names (HasProperty guards make this safe even when it doesn't), driven off whatever
        // renderers/materials are actually present on the instantiated visual.
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
