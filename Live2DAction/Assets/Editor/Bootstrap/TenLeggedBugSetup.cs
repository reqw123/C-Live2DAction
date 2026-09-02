using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Live2DAction.AI;
using Live2DAction.CameraSystem;
using Live2DAction.Combat.Boss;
using Live2DAction.Core;
using Live2DAction.UI;

namespace Live2DAction.EditorTools
{
    // 2026-08-31, explicit user request ("將十足蟲.glb 實作為可戰鬥的高速近戰敵人" - full spec).
    //
    // Turns the scene's placeholder `十足蟲` (a 0.3-scale prefab instance of Shizuchong.glb - a
    // Meshy model, generic Bone_XXX rig, no animation clips) into a live TenLeggedBugController
    // enemy, integrated with the EXISTING project systems (Health / IDamageable / NavPathFollower /
    // BossTeamMember / CharacterController) rather than a parallel one.
    //
    // Almost all of the wiring is baked into Shizuchong.prefab so it stays a reusable enemy prefab:
    //   - a "Rig" wrapper child rotated 180 on Y, because the raw model's head/horn points at
    //     local -Z; the controller (and CharacterController, and the attack direction) all use the
    //     ROOT's +Z as "forward", so the visual has to be turned to match. Left/right of the model
    //     therefore also swap - the leg-number mapping below already accounts for that.
    //   - CharacterController (the one body capsule, per the confirmed decision), Health (100 HP,
    //     defers deactivation so the flip-over death can play), BossTeamMember ("Bug"),
    //     NavMeshModifier (ignoreFromBuild - don't carve the mesh), NavPathFollower, the controller.
    //   - a "HornHitbox" trigger box under the horn bone (Bone_002) with TenLeggedBugHornHitbox.
    //   - every bone reference the controller needs, resolved by name HERE (Editor-time), never at
    //     runtime (spec section 1).
    //
    // The only scene-instance override is `target = Player` (a scene reference can't live in a
    // prefab). Re-runnable: re-applies the prefab surgery and re-wires the instance every run.
    //
    // After this: run Tools/Live2DAction/Bake Navigation Mesh so the follower has a mesh, and
    // Play. Hand-tune the controller's serialized numbers in Play (they're all exposed).
    internal static class TenLeggedBugSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string PrefabPath = "Assets/Prefabs/Shizuchong/Shizuchong.prefab";
        private const string BugName = "十足蟲";

        // Bone names inside Shizuchong.glb, identified by hand + confirmed against a marked render
        // (2026-08-31 follow-up: the FIRST guess was 180 degrees wrong - it pitched Bone_002, which
        // is actually the segmented TAIL/stinger at model -Z, and the "Rig" wrapper was yawed 180
        // so the bug walked tail-first. Corrected below.). NOT used at runtime.
        //
        // The real HEAD (big curved rhino horn + compound eye + mandibles) is the Bone_005 ->
        // Bone_004 -> Bone_048 chain at model +Z. So: NO Rig yaw (model +Z head = the root's +Z
        // "forward"), and the horn bone is Bone_004 (the head bone - pitching it swings the whole
        // horn down for the stab and presses the head/face down, exactly the spec's telegraph).
        private const string BodyRootBoneName = "Bone_001"; // body trunk
        private const string HornBoneName = "Bone_004";     // head bone - carries the horn, pitches for the stab
        private const string HornTipBoneName = "Bone_048";  // tip of the horn - where the hitbox sits

        // Leg mapping - 2026-08-31, RE-DONE after a second analysis + a ground-contact vertex
        // cluster check confirmed the model actually rigs 10 feet (5 clean symmetric pairs), not
        // the 8 the first pass found. Order: index 0 = leg 1 (front-LEFT), 1 = leg 2 (front-right),
        // then alternating L/R, front (+Z head) to back (-Z). Each entry: the bone that best
        // swings the whole limb; the parallel LegBendBoneNames array is the knee (null = none).
        //
        // TWO legs are rigged messily (both analyses agree, unfixable without a re-rig in Blender):
        //   * leg 1  (Bone_046): parented under the HEAD chain (Bone_005/Bone_004), its skin
        //     weights blend with the body - the leg won't deform perfectly cleanly.
        //   * legs 6 & 8 (Bone_025 / Bone_024): Bone_025 is the PARENT of leg 8's bone, so
        //     stepping leg 6 drags leg 8 a little. Leg 8 stepping does NOT affect leg 6.
        private static readonly string[] LegRootBoneNames =
        {
            "Bone_046", // leg 1  - L front  (foot x-0.64 z+0.88) - messy: under the head chain
            "Bone_029", // leg 2  - R front  (foot x+0.64 z+0.88)
            "Bone_013", // leg 3  - L 2nd    (foot x-0.87 z+0.42)
            "Bone_017", // leg 4  - R 2nd    (foot x+0.87 z+0.42)
            "Bone_009", // leg 5  - L 3rd    (foot x-0.95 z-0.03)
            "Bone_025", // leg 6  - R 3rd    (foot x+0.95 z-0.03) - parent of leg 8's chain
            "Bone_021", // leg 7  - L 4th    (foot x-0.88 z-0.60)
            "Bone_024", // leg 8  - R 4th    (foot x+0.88 z-0.61) - child of Bone_025 (leg 6)
            "Bone_037", // leg 9  - L rear   (foot x-0.59 z-1.03)
            "Bone_033", // leg 10 - R rear   (foot x+0.61 z-1.04)
        };

        // Knee bone per leg (same order). "" = no separate knee (leg 1's bone is a leaf; leg 6
        // must not auto-grab its only child Bone_024, which belongs to leg 8).
        private static readonly string[] LegBendBoneNames =
        {
            "",         // leg 1
            "Bone_028", // leg 2
            "Bone_012", // leg 3
            "Bone_016", // leg 4
            "Bone_008", // leg 5
            "",         // leg 6  (Bone_024 is leg 8, do NOT use)
            "Bone_020", // leg 7
            "Bone_023", // leg 8
            "Bone_036", // leg 9
            "Bone_032", // leg 10
        };

        // CharacterController dims at the prefab's own scale 1 (the scene instance is 0.3, so the
        // world capsule is ~0.51 tall / ~0.21 radius - fits the 0.66 x 0.51 x 0.86 body).
        private const float CcHeight = 1.7f;
        private const float CcRadius = 0.7f;
        private static readonly Vector3 CcCenter = new Vector3(0f, 0.85f, 0f);

        private const float MaxHealth = 100f;
        private const float HornDamage = 10f;
        private const string TeamName = "Bug";

        [MenuItem("Tools/Live2DAction/Build Ten-Legged Bug Enemy (十足蟲)")]
        public static void Apply()
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                BuildPrefab(prefabRoot);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
            AssetDatabase.SaveAssets();

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject bug = GameObject.Find(BugName);
            GameObject player = GameObject.Find("Player");
            if (bug == null || player == null)
            {
                Debug.LogError($"[TenLeggedBugSetup] '{BugName}' or 'Player' not found in {ScenePath}.");
                return;
            }

            // Prefab connection carries every component + child; only the scene target is local.
            var so = new SerializedObject(bug.GetComponent<TenLeggedBugController>());
            so.FindProperty("target").objectReferenceValue = player.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Seat the CharacterController on the ground (capsule bottom = local 0 -> world = pos.y).
            GameObject ground = GameObject.Find("Ground");
            float groundTopY = ground != null && ground.GetComponent<Collider>() != null
                ? ground.GetComponent<Collider>().bounds.max.y
                : 0.5f;

            // 2026-08-31, user request ("移動到右下角圍牆下(與屁孩王直行對其)") - south-east corner of
            // the 本地 Ground, on the SAME X column as 屁孩王 so the two line up straight, tucked
            // against the south boundary wall.
            GameObject pihaiwang = GameObject.Find("屁孩王");
            float lineUpX = pihaiwang != null ? pihaiwang.transform.position.x : 12f;
            float southEdgeZ = ground != null && ground.GetComponent<Collider>() != null
                ? ground.GetComponent<Collider>().bounds.min.z + 1.5f  // ~1.5m in from the wall
                : -13.5f;
            bug.transform.position = new Vector3(lineUpX, groundTopY + 0.02f, southEdgeZ);
            bug.transform.rotation = Quaternion.identity; // controller yaws it at runtime

            // Tighter patrol radius for that corner so it doesn't wander through the wall / far off.
            var patrolSo = new SerializedObject(bug.GetComponent<TenLeggedBugController>());
            patrolSo.FindProperty("patrolRadius").floatValue = 5f;
            patrolSo.ApplyModifiedPropertiesWithoutUndo();

            // --- view-only spectator camera (key B) ------------------------------------------
            BuildSpectatorCamera(bug);

            EditorUtility.SetDirty(bug);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"[TenLeggedBugSetup] Built '{BugName}': TenLeggedBugController enemy " +
                      $"(100 HP, {LegRootBoneNames.Length} legs, horn hitbox) + world-space HP bar + " +
                      "5s revive + a view-only spectator camera on key B. " +
                      "Next: run 'Tools/Live2DAction/Bake Navigation Mesh', then Play. " +
                      "All feel values are on the controller's Inspector.");
        }

        // Low-eyeline camera aimed at the bug + a B-key toggle that only swaps the VIEW (the bug
        // keeps running its AI - user: "純粹觀看不影響行為"). Clones the Main Camera rig for exact
        // Camera/URP settings, same approach CatCharacterSetup/VehicleCamera take.
        // 2026-08-31, user follow-up ("蟲的視角不夠低") - dropped low and close, near the bug's own
        // eyeline rather than looking down at it from above.
        private const float SpectatorDistance = 1.05f;
        private static readonly Vector3 SpectatorTargetOffset = new Vector3(0f, 0.12f, 0f); // just above the shell (bug root ~0.5m)
        private const float SpectatorInitialPitch = 4f;    // nearly level with the bug, a hair down
        private const float SpectatorMinPitch = -30f;
        private const float SpectatorMaxPitch = 82f;

        private static void BuildSpectatorCamera(GameObject bug)
        {
            GameObject mainCamera = GameObject.Find("Main Camera");
            if (mainCamera == null)
            {
                Debug.LogWarning("[TenLeggedBugSetup] 'Main Camera' not found - skipping the spectator camera.");
                return;
            }

            // GameObject.Find skips INACTIVE objects, and the spectator camera starts inactive -
            // so re-runs would pile up duplicates. Sweep the scene roots by name instead.
            foreach (GameObject r in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (r.name == "BugSpectatorCamera" || r.name == "BugSpectator")
                {
                    Object.DestroyImmediate(r);
                }
            }

            GameObject cam = Object.Instantiate(mainCamera);
            cam.name = "BugSpectatorCamera";
            cam.tag = "MainCamera";      // so Camera.main resolves to it while it's the active camera
            cam.transform.SetParent(null);
            cam.SetActive(false);        // starts off - key B turns it on

            var tpc = cam.GetComponent<ThirdPersonCameraController>();
            if (tpc != null)
            {
                var so = new SerializedObject(tpc);
                so.FindProperty("target").objectReferenceValue = bug.transform;
                so.FindProperty("distance").floatValue = SpectatorDistance;
                so.FindProperty("targetOffset").vector3Value = SpectatorTargetOffset;
                so.FindProperty("initialPitch").floatValue = SpectatorInitialPitch;
                so.FindProperty("minPitch").floatValue = SpectatorMinPitch;
                so.FindProperty("maxPitch").floatValue = SpectatorMaxPitch;
                // No player systems on this camera.
                var p1 = so.FindProperty("lockOnSource"); if (p1 != null) p1.objectReferenceValue = null;
                var p2 = so.FindProperty("inputSource"); if (p2 != null) p2.objectReferenceValue = null;
                var p3 = so.FindProperty("ultimateAbility"); if (p3 != null) p3.objectReferenceValue = null;
                var p4 = so.FindProperty("enableDescendAutoPitch"); if (p4 != null) p4.boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var togGo = new GameObject("BugSpectator");
            var toggle = togGo.AddComponent<SpectatorCameraToggle>();
            var tso = new SerializedObject(toggle);
            tso.FindProperty("spectatorCamera").objectReferenceValue = cam;
            tso.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------------------------------
        private static void BuildPrefab(GameObject root)
        {
            // --- 1. Rig wrapper: reparent mesh + skeleton under a 180-yaw child so the head faces
            //        the root's +Z (= "forward" for the CC / controller / attack direction). ------
            Transform rig = root.transform.Find("Rig");
            if (rig == null)
            {
                var rigGo = new GameObject("Rig");
                rig = rigGo.transform;
                rig.SetParent(root.transform, false);
                // Move every current child of the root under Rig.
                var toMove = new List<Transform>();
                for (int i = 0; i < root.transform.childCount; i++)
                {
                    Transform c = root.transform.GetChild(i);
                    if (c != rig) toMove.Add(c);
                }
                foreach (Transform c in toMove) c.SetParent(rig, true);
            }
            // NO yaw: the model's head (Bone_004 chain) already sits at model +Z, which is the
            // root transform's +Z = "forward" for the controller / CharacterController / attack
            // direction. (The first build yawed this 180 on a wrong head/tail read - the bug then
            // walked tail-first at the player. See HornBoneName's comment.)
            rig.localPosition = Vector3.zero;
            rig.localRotation = Quaternion.identity;
            rig.localScale = Vector3.one;

            // --- 2. Strip the import Animator/Collider we no longer want ------------------------
            var animator = root.GetComponent<Animator>();
            if (animator != null) Object.DestroyImmediate(animator);
            var box = root.GetComponent<BoxCollider>();
            if (box != null) Object.DestroyImmediate(box);

            // --- 3. CharacterController (the one body capsule) ---------------------------------
            var cc = root.GetComponent<CharacterController>();
            if (cc == null) cc = root.AddComponent<CharacterController>();
            cc.height = CcHeight;
            cc.radius = CcRadius;
            cc.center = CcCenter;
            cc.minMoveDistance = 0f;  // Docs/KNOWN_ISSUES minMoveDistance entry
            cc.stepOffset = 0f;       // project convention - see GreyboxSceneBuilder.CreatePlayer

            // --- 4. Health (100 HP, defers deactivation so the death coroutine can run) --------
            var health = root.GetComponent<Health>();
            if (health == null) health = root.AddComponent<Health>();
            var hso = new SerializedObject(health);
            hso.FindProperty("maxHealth").floatValue = MaxHealth;
            var deferProp = hso.FindProperty("deferDeactivationToDeathAnimation");
            if (deferProp != null) deferProp.boolValue = true;
            hso.ApplyModifiedPropertiesWithoutUndo();

            // --- 5. Team tag + NavMesh integration -------------------------------------------
            var team = root.GetComponent<BossTeamMember>();
            if (team == null) team = root.AddComponent<BossTeamMember>();
            var tso = new SerializedObject(team);
            tso.FindProperty("team").stringValue = TeamName;
            tso.ApplyModifiedPropertiesWithoutUndo();

            var mod = root.GetComponent<NavMeshModifier>();
            if (mod == null) mod = root.AddComponent<NavMeshModifier>();
            mod.ignoreFromBuild = true; // don't carve a hole in the baked mesh
            mod.overrideArea = false;

            if (root.GetComponent<NavPathFollower>() == null) root.AddComponent<NavPathFollower>();

            // --- 6. Horn hitbox under the horn bone ------------------------------------------
            Transform hornBone = FindDeep(rig, HornBoneName);
            if (hornBone == null)
            {
                Debug.LogError($"[TenLeggedBugSetup] horn bone '{HornBoneName}' not found under Rig.");
            }
            Transform hornTipBone = FindDeep(rig, HornTipBoneName);
            TenLeggedBugHornHitbox hornHitbox = null;
            if (hornBone != null)
            {
                Transform existing = hornBone.Find("HornHitbox");
                GameObject hbGo = existing != null ? existing.gameObject : new GameObject("HornHitbox");
                if (existing == null) hbGo.transform.SetParent(hornBone, false);
                // Sit the box at the horn tip, expressed in the horn BONE's local space so it
                // tracks correctly whatever the generic bone's own orientation is. Then push it a
                // little further forward/down (toward where a downward stab actually lands) and
                // make it generous - at the scene's 0.3 scale this box is only ~0.3-0.5m across in
                // world space, and it has to bridge the gap to the player's hurtbox on the stab.
                Vector3 tipLocal = hornTipBone != null
                    ? hornBone.InverseTransformPoint(hornTipBone.position)
                    : new Vector3(0f, 0.3f, 0.5f);
                Vector3 fwdLocal = tipLocal.sqrMagnitude > 0.0001f ? tipLocal.normalized : Vector3.forward;
                hbGo.transform.localPosition = tipLocal + fwdLocal * 0.35f + Vector3.down * 0.1f;
                hbGo.transform.localRotation = Quaternion.identity;
                hbGo.transform.localScale = Vector3.one;

                var col = hbGo.GetComponent<BoxCollider>();
                if (col == null) col = hbGo.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.enabled = false; // OFF until the controller opens it on a strike frame
                col.size = new Vector3(1.1f, 1.1f, 1.8f); // world ~0.33 x 0.33 x 0.54 at 0.3 scale
                col.center = Vector3.zero;

                var rb = hbGo.GetComponent<Rigidbody>();
                if (rb == null) rb = hbGo.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;

                hornHitbox = hbGo.GetComponent<TenLeggedBugHornHitbox>();
                if (hornHitbox == null) hornHitbox = hbGo.AddComponent<TenLeggedBugHornHitbox>();
                var hbSo = new SerializedObject(hornHitbox);
                hbSo.FindProperty("damage").floatValue = HornDamage;
                hbSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // --- 7. Controller + all its bone references (resolved by name HERE only) ---------
            var ctrl = root.GetComponent<TenLeggedBugController>();
            if (ctrl == null) ctrl = root.AddComponent<TenLeggedBugController>();
            var cso = new SerializedObject(ctrl);
            cso.FindProperty("bodyRootBone").objectReferenceValue = FindDeep(rig, BodyRootBoneName);
            cso.FindProperty("hornBone").objectReferenceValue = hornBone;
            cso.FindProperty("hornHitbox").objectReferenceValue = hornHitbox;
            cso.FindProperty("hornDamage").floatValue = HornDamage;

            SerializedProperty legs = cso.FindProperty("legRootBones");
            legs.arraySize = LegRootBoneNames.Length;
            for (int i = 0; i < LegRootBoneNames.Length; i++)
            {
                Transform legBone = FindDeep(rig, LegRootBoneNames[i]);
                if (legBone == null)
                {
                    Debug.LogWarning($"[TenLeggedBugSetup] leg bone '{LegRootBoneNames[i]}' not found.");
                }
                legs.GetArrayElementAtIndex(i).objectReferenceValue = legBone;
            }

            // Explicit knee bones, one per leg (null allowed) - the controller uses this verbatim
            // when its Count matches legRootBones, so leg 6 can't auto-steal leg 8's bone.
            SerializedProperty bends = cso.FindProperty("legBendBones");
            bends.arraySize = LegBendBoneNames.Length;
            for (int i = 0; i < LegBendBoneNames.Length; i++)
            {
                bends.GetArrayElementAtIndex(i).objectReferenceValue =
                    string.IsNullOrEmpty(LegBendBoneNames[i]) ? null : FindDeep(rig, LegBendBoneNames[i]);
            }
            cso.ApplyModifiedPropertiesWithoutUndo();

            // --- 8. World-space health bar, sized to the bug's body ---------------------------
            AddHealthBar(root, rig);
        }

        // A plain red world-space HP bar above the bug's back, its width set from the actual body
        // LENGTH (user: "根據他的身長").
        private static void AddHealthBar(GameObject root, Transform rig)
        {
            var health = root.GetComponent<Health>();
            var smr = root.GetComponentInChildren<SkinnedMeshRenderer>();
            if (health == null || smr == null) return;

            // Measure the real skinned bounds (the serialized SMR bounds import degenerate on this
            // model - BakeMesh gives the true extent). These are in the skeleton's un-scaled frame;
            // the canvas is a child of the 0.3-scale root, so a value here of L becomes 0.3*L in
            // world - exactly what we want (the bar ends up ~body-length wide in metres).
            var baked = new Mesh();
            smr.BakeMesh(baked, true);
            Vector3 mn = baked.bounds.min, mx = baked.bounds.max;
            Object.DestroyImmediate(baked);
            float bodyLength = Mathf.Max(mx.z - mn.z, mx.x - mn.x); // longest horizontal axis
            float bodyTopLocalY = mx.y;                              // top of the shell (un-scaled frame)
            float rootScale = Mathf.Max(0.0001f, root.transform.localScale.x);

            var old = root.transform.Find("HealthBarCanvas");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var canvasGo = new GameObject("HealthBarCanvas");
            canvasGo.transform.SetParent(root.transform, false);
            // Sit it just above the shell. bodyTopLocalY is in the un-scaled frame; add ~0.18m of
            // world margin, converted back into that frame.
            canvasGo.transform.localPosition = new Vector3(0f, bodyTopLocalY + 0.18f / rootScale, 0f);
            canvasGo.transform.localRotation = Quaternion.identity;

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var canvasRect = canvasGo.GetComponent<RectTransform>();
            float barWidth = bodyLength * 0.9f;                        // ~90% of the body length
            canvasRect.sizeDelta = new Vector2(barWidth, barWidth * 0.13f);

            Image bg = CreateBarImage(canvasGo.transform, "Background", new Color(0.08f, 0.08f, 0.08f, 0.85f));
            bg.type = Image.Type.Simple;
            Image fill = CreateBarImage(canvasGo.transform, "Fill", Color.red);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;

            var bar = canvasGo.AddComponent<WorldSpaceHealthBar>();
            var bso = new SerializedObject(bar);
            bso.FindProperty("health").objectReferenceValue = health;
            bso.FindProperty("fillImage").objectReferenceValue = fill;
            bso.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Image CreateBarImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            // Image.Type.Filled needs a real sprite or fillAmount has no visual effect - use
            // Unity's built-in UI sprite (same fix HealthBarSetup.CreateStretchedImage documents).
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            return img;
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            if (parent == null) return null;
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform r = FindDeep(parent.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
