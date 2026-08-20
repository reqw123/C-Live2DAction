using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Live2DAction.EditorTools
{
    // 2026-08-17, user requested richer/more "精緻" scene dressing beyond the Quaternius
    // low-poly nature ring (see BackgroundSceneryStandeeSetup/DistantMountainsSetup) - three
    // CC-BY East Asian architecture models sourced from Sketchfab (see Docs/ASSET_LICENSES.md:
    // Torii/StoneLantern/Pagoda entries), imported via com.unity.cloud.gltfast (added to
    // Packages/manifest.json for this) since they ship as .glb, not .fbx.
    //
    // Composed as one fixed "shrine vista" along a single direction outside the boundary walls
    // (VistaAngleDegrees) rather than scattered randomly like BackgroundSceneryStandeeSetup -
    // two stone lanterns flank the near view, the pagoda sits mid-distance, and the torii piece
    // anchors the far background. All three are scaled by measuring each instance's actual
    // imported bounds and computing the multiplier needed to hit a target height, same
    // self-correcting approach as BackgroundSceneryStandeeSetup/DistantMountainsSetup use for
    // the Quaternius pack, since Sketchfab exports have no guaranteed common native scale.
    internal static class JapaneseShrineVistaSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string AssetRoot = "Assets/_Project/Environment/Placeholder/JapaneseArchitecture";
        private const string ParentName = "JapaneseShrineVista";

        private const float VistaAngleDegrees = 200f;
        private const float LanternRadius = 19f;
        private const float LanternLateralOffset = 2.5f;
        private const float ToriiRadius = 78f;
        private const float ToriiElevation = 22f;

        private const float LanternTargetHeight = 1.4f;
        private const float PagodaTargetHeight = 12f;

        // 2026-08-17, user requested the pagoda become an actual climbable landmark instead of
        // a distant backdrop piece. Went through two placements before this one: PagodaRadius=42
        // (well outside the boundary walls, purely decorative - the original) and then radius=5
        // (moved in close to center so the outward-leaning tip wouldn't clip the wall) - the
        // second one drew a real complaint: "塔現在卡到的了地圖內地形" (the tower now clips into
        // in-map terrain, near the spawn/stone-floor dressing) since radius 5 sits deep inside
        // the already-decorated play area. Per the explicit request that followed ("塔底中心定位
        // 在地圖邊界" - base center positioned AT the map boundary), this now matches
        // GreyboxSceneBuilder.CreateBoundaryWalls' halfExtent (Ground spans X/Z [-15,15]) exactly
        // - the base sits right at the boundary line, not inside the decorated interior and not
        // out past the wall either. The leaning tip and the ramp's own box-thickness offset still
        // push the actual climbable structure a bit further out past this pivot (see
        // BuildClimbRamp) - that's fine here since the boundary walls have no renderer (see
        // GreyboxSceneBuilder's "invisible collider-only walls" comment), so overlapping them
        // causes no visible clipping, only harmless overlapping invisible collision volumes.
        private const float PagodaBoundaryRadius = 15f;

        // 2026-08-17: the Pagoda .glb has the same Blender Z-up vs glTF Y-up mismatch as the
        // Torii (see PlaceToriiVista's own comment) - just without an intermediate baked-rotation
        // node exposing it, so it's baked directly into this asset's single mesh. Confirmed live:
        // measuring the model's bounds at identity gives size (0.53, 0.64, 1.00) - Z, not Y, is
        // the dominant axis - and a temporary instance rotated by (-90,0,0) and screenshotted
        // came back as an unmistakable upright tiered pagoda with a spire top (bounds becoming
        // (0.53, 1.00, 0.64), Y now dominant). Without this correction the "pagoda" has been
        // effectively lying on its side since it was first placed - not obvious at a glance
        // because a sideways stack of roof tiers can still superficially read as "a building" in
        // a screenshot, unlike the Torii's much more obviously-wrong floating-terrain jumble. This
        // is also the real reason the first climb-ramp attempt produced a ~21-unit-thick box and a
        // corner 30+ units from center: it was sized off the sideways (wrong-axis) bounds.
        private const float PagodaAxisCorrectionXDegrees = -90f;

        // User spec: "與地面夾角60度" - the pagoda's own long axis should sit 60 degrees from
        // the ground (was 90, i.e. standing straight up). This is layered ON TOP of
        // PagodaAxisCorrectionXDegrees (both rotate about the same local X axis, so they simply
        // add together - see PlaceClimbablePagoda) - pitching an additional (90 - 60) = 30
        // degrees away from the now-corrected upright achieves the 60-degrees-from-ground lean.
        //
        // 2026-08-17 correction: this was originally +30 (leans the top TOWARD the map center,
        // dotTowardCenter == 1) on the reasoning that keeping the climbable top away from the
        // wall was safer. That was backwards - real user report after playtesting: "正面連第一
        // 階層都上不去，而從塔中間卻可以穿模直接上去" (can't get up even the first tier from the
        // front; clips straight through the middle instead). Root cause confirmed live: for a
        // box tilted by pitch about local X, only ONE of its two Z faces ends up with an
        // upward-facing normal (the other is a >90-degree overhang - literally the underside of
        // the lean, like the sheltered side of the actual Leaning Tower of Pisa) - and with
        // +30, yaw already orients local +Z toward the map center, so the WALKABLE face ended up
        // facing the wall (away from the play area). A player walking out from center hit the
        // overhang face first and got stopped dead (no stepOffset to climb it - see
        // BuildClimbRamp's own comment), while the ramp box (a fixed 6 units wide at the time,
        // much narrower than the visual mesh's own ~19-unit footprint) left the wider visual mesh
        // - which has no collider of its own - free to walk straight through everywhere outside
        // that narrow box. -30 flips which face is walkable so it faces the play area instead,
        // matching how a player actually approaches.
        // Also matches the later request "塔尖往天空方向傾斜" (tip tilts toward the sky) - leaning
        // away from center points the tip up and outward over the boundary rather than drooping
        // in over the play area.
        private const float PagodaLeanPitchDegrees = -30f;

        // The ramp collider's walkable face ends up at this incline (measured the same way
        // GroundSlopeUtility/CharacterController.slopeLimit do: angle between the face's normal
        // and Vector3.up) because it's cut parallel to the pagoda's own leaning axis - a face
        // that runs along a 30-degree-from-vertical axis is itself 60 degrees from horizontal,
        // not 30. Verified live post-build (see ConfigurePlayerClimbSlopeLimit's own comment).
        //
        // Fixed-width (6, then 14) attempts both under-covered the visual mesh - real playtest
        // report: "中途只要在塔的外觀輪廓上就不會掉落" (should never fall as long as they're within
        // the tower's visual silhouette) means the collision needs to track the ACTUAL measured
        // mesh footprint, not a guessed constant. BuildClimbRamp now sizes the box from
        // PlaceClimbablePagoda's own MeasureCombinedBounds() measurement instead - this margin is
        // applied on top of that measurement so the invisible box comfortably exceeds the visual
        // silhouette on every side rather than exactly matching it (measurement is axis-aligned
        // against the unrotated model, so it's already a fair approximation, not an
        // underestimate, but a bit of slack is cheap insurance against ever falling through at
        // the visual edge).
        private const float PagodaClimbRampCoverageMargin = 1.15f;

        // CharacterController.slopeLimit defaults to 45 degrees, well under the ramp's 60 -
        // without raising it GroundSlopeUtility.IsTooSteepToStandOn (same angle convention)
        // would classify the ramp as "too steep" and slide the player straight back off it.
        // 65 clears 60 with a bit of margin for float error at the very top/bottom of the ramp.
        private const float PlayerClimbSlopeLimitDegrees = 65f;

        // Target for the WHOLE torii piece's combined bounds, not just the gate - see
        // ApplyToriiVista's own comment: the source .glb is a full floating-island diorama
        // (bamboo grove, rocks, terrain, water), not a bare torii gate, so its native
        // proportions include far more vertical extent than the gate alone.
        private const float ToriiVistaTargetHeight = 24f;

        [MenuItem("Tools/Live2DAction/Add Japanese Shrine Vista")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject existing = GameObject.Find(ParentName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            var parent = new GameObject(ParentName);

            float angleRad = VistaAngleDegrees * Mathf.Deg2Rad;
            var direction = new Vector3(Mathf.Cos(angleRad), 0f, Mathf.Sin(angleRad));
            var lateral = new Vector3(-direction.z, 0f, direction.x);
            float facingYaw = Mathf.Atan2(-direction.x, -direction.z) * Mathf.Rad2Deg;

            PlaceCalibratedProp(
                parent.transform, "StoneLantern", "StoneLantern_Left",
                direction * LanternRadius + lateral * LanternLateralOffset,
                facingYaw, LanternTargetHeight);
            PlaceCalibratedProp(
                parent.transform, "StoneLantern", "StoneLantern_Right",
                direction * LanternRadius - lateral * LanternLateralOffset,
                facingYaw, LanternTargetHeight);
            PlaceClimbablePagoda(parent.transform, direction, facingYaw);

            PlaceToriiVista(parent.transform, direction * ToriiRadius + Vector3.up * ToriiElevation, facingYaw);

            ConfigurePlayerClimbSlopeLimit();

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Added Japanese shrine vista (stone lanterns, climbable pagoda, torii island backdrop).");
        }

        // Leans the pagoda against the map boundary at PagodaLeanPitchDegrees and adds an
        // invisible box ramp collider along its leaning axis so the player can actually walk up
        // it. NOT using the pagoda's own imported mesh for collision: it's a tiered-roof shape
        // (visible steps between each level), and this project's CharacterController.stepOffset
        // is deliberately 0 (see FixCharacterControllerStepOffset.cs - a real bug fix, walking
        // into any ledge taller than stepOffset just stops dead) rather than the Unity default
        // of 0.3, so those roof ledges would block the player outright instead of reading as
        // stairs. A smooth box ramp sidesteps that entirely - it's one continuous slope with no
        // discrete steps, so only slopeLimit (not stepOffset) governs whether it's climbable.
        private static void PlaceClimbablePagoda(Transform parent, Vector3 direction, float yaw)
        {
            GameObject prefab = LoadModel("Pagoda");
            if (prefab == null)
            {
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = "Pagoda";
            instance.transform.localPosition = Vector3.zero;
            // Axis correction applied BEFORE measuring, not after - see
            // PagodaAxisCorrectionXDegrees's own comment. Unlike the Torii (whose correction
            // lives in a baked child-node matrix and so applies regardless of the root's own
            // rotation), this asset's mismatch is baked straight into the mesh vertices, so
            // measuring at true identity would capture the still-sideways shape.
            instance.transform.localRotation = Quaternion.Euler(PagodaAxisCorrectionXDegrees, 0f, 0f);
            instance.transform.localScale = Vector3.one;

            // See FixDegenerateMeshBounds's own comment - fixes a real report ("某些視角塔會突然
            // 消失" - the tower suddenly disappears from certain camera angles), root-caused to
            // the same zero-size cached Mesh.bounds already known to affect this asset (see
            // MeasureCombinedBounds's comment) also feeding Unity's frustum culling, not just
            // this script's own height measurement.
            FixDegenerateMeshBounds(instance);

            // Measured with the axis correction already applied, so X/Z are the model's true
            // (corrected) native width/depth - reused both to scale the visual mesh (Y) and to
            // size the climb ramp collider (X/Z) so it actually tracks the real mesh footprint
            // instead of a guessed constant. See PagodaClimbRampCoverageMargin's own comment.
            Bounds nativeBounds = MeasureCombinedBounds(instance);
            float scale = 0f;
            if (nativeBounds.size.y > 0.000001f)
            {
                scale = PagodaTargetHeight / nativeBounds.size.y;
                instance.transform.localScale = Vector3.one * scale;
            }
            else
            {
                Debug.LogWarning("Pagoda has no measurable renderer bounds - leaving at native scale.");
            }

            instance.transform.localPosition = direction * PagodaBoundaryRadius;
            // PagodaLeanPitchDegrees and PagodaAxisCorrectionXDegrees both rotate about the same
            // local X axis, so they simply add - the pagoda's own visible tilt ends up being
            // (PagodaLeanPitchDegrees + 90) degrees away from the corrected upright, i.e. 60
            // degrees from the ground per the user's spec (matches PagodaLeanPitchDegrees's own
            // comment).
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f)
                * Quaternion.Euler(PagodaLeanPitchDegrees, 0f, 0f)
                * Quaternion.Euler(PagodaAxisCorrectionXDegrees, 0f, 0f);

            // Real report: "大部分的塔樓面積都在地圖下方，塔尖只露出一點點" (most of the tower sits
            // below the map, only the tip pokes out) - direction * PagodaBoundaryRadius only sets
            // X/Z, leaving Y at 0, which places the model's PIVOT at ground level, not its base.
            // The pivot isn't at the base even before any lean (StoneLantern/Torii tolerate this
            // because MeasureCombinedHeight's target-height scaling happens to keep their pivots
            // close enough to their visual base), and the lean rotation here moves it further
            // still. Re-measuring bounds at the FINAL rotation (not the correction-only rotation
            // used above for sizing) and shifting the whole instance vertically so its lowest
            // point sits at ground level - same "measure and correct" approach BuildClimbRamp
            // already uses for the ramp's own base - fixes this regardless of where the pivot
            // happens to sit.
            float groundEmbed = -0.05f;
            float lowestY = MeasureCombinedBounds(instance).min.y;
            instance.transform.position += Vector3.up * (groundEmbed - lowestY);

            // Real report: flush-to-ground (above) fixed most of the tower sitting underground,
            // but overcorrected - the base is a wide flat-ish plinth, and aligning only its single
            // LOWEST point to ground level left the rest of that tilted plinth's underside fully
            // exposed above ground, reading as "the whole base surface is showing, floating in
            // midair" (confirmed live via screenshot: a large flat wedge visible under the
            // tiers). The fix is sliding the whole tower further down along its OWN tilt axis
            // (transform.forward, not straight down in world Y - "沿著當前角度往下移動") so most of
            // that plinth sinks below ground and only a slim edge of it remains visible where it
            // meets the ground, the rest hidden "outside the map" as asked. Tuned by eye via a
            // few live screenshots at 3 and 4.5 units - 4.5 overshot and exposed the plinth's
            // back-side support struts instead (visible from the OTHER direction), 2.5 reads
            // clean from the player's actual approach side.
            const float baseSinkDepth = 2.5f;
            instance.transform.position -= instance.transform.forward * baseSinkDepth;

            float rampWidth = nativeBounds.size.x * scale * PagodaClimbRampCoverageMargin;
            float rampThickness = nativeBounds.size.z * scale * PagodaClimbRampCoverageMargin;
            BuildClimbRamp(parent, instance, rampWidth, rampThickness);
        }

        private static void BuildClimbRamp(Transform parent, GameObject pagodaInstance, float rampWidth, float rampThickness)
        {
            var ramp = new GameObject("Pagoda_ClimbRamp");
            ramp.transform.SetParent(parent, worldPositionStays: false);
            ramp.transform.rotation = pagodaInstance.transform.rotation;

            // IMPORTANT: with PagodaAxisCorrectionXDegrees folded in, the pagoda's true climb
            // axis is transform.forward (local Z), NOT transform.up (local Y) - both correction
            // and lean rotate about the same local X axis, and their combined angle
            // (PagodaLeanPitchDegrees + PagodaAxisCorrectionXDegrees = -30 + -90 = -120) is past
            // the halfway point where local Z (not Y) ends up the one near-vertical. Confirmed
            // live: Vector3.Angle(transform.forward, Vector3.up) == 30 (matches the lean spec)
            // while Vector3.Angle(transform.up, Vector3.up) comes back 120 - .up points markedly
            // downward here and is not the axis to use. Centered along that true climb axis, from
            // the pagoda's base (ground level) up to its tip.
            ramp.transform.position = pagodaInstance.transform.position
                + ramp.transform.forward * (PagodaTargetHeight / 2f);

            // Length goes on local Z now (see above), not Y - Y takes over Z's old role as the
            // "thickness" axis (the one whose two faces split into one walkable, one overhang).
            // X remains the safe "width" axis either way - it's perpendicular to the local X
            // rotation axis that both correction and lean rotate about, so it's never affected.
            BoxCollider collider = ramp.AddComponent<BoxCollider>();
            collider.size = new Vector3(rampWidth, rampThickness, PagodaTargetHeight);

            // Which local Y face ends up walkable (upward-facing normal) flips with the sign of
            // PagodaLeanPitchDegrees, same as the old local-Z logic did before the axis-swap
            // above - confirmed live via execute_code: local -Y comes out ~60 degrees from
            // vertical (walkable), local +Y ~120 (overhang, matches how .up itself measured).
            float frontLocalY = -rampThickness / 2f;

            // The box's bottom-front edge (where the player is meant to step on) does NOT sit
            // at the pagoda's own ground-level base - a tilted box's thickness makes one edge of
            // each end dip below the tilt axis and the opposite edge rise above it. Measuring the
            // actual front-bottom-edge world position and shifting the whole ramp vertically so
            // that edge sits just at/under ground level (rather than hand-deriving the offset
            // algebraically) keeps this correct even if the width/thickness/lean constants above
            // are retuned later.
            Vector3 frontBottomEdge = ramp.transform.TransformPoint(
                new Vector3(0f, frontLocalY, -PagodaTargetHeight / 2f));
            float groundEmbed = -0.05f;
            ramp.transform.position += Vector3.up * (groundEmbed - frontBottomEdge.y);
        }

        // Real report: "某些視角塔會突然消失" (the tower suddenly disappears from certain camera
        // angles). Root cause: Unity's frustum culling uses each Renderer's bounds, which is
        // derived from Mesh.bounds - and MeasureCombinedBounds's own comment already established
        // this asset's imported mesh(es) came back with a zero-size CACHED Mesh.bounds despite
        // having real geometry (11282 verts). A zero-size bounds sits entirely at one point, so
        // the camera culls the whole renderer the moment that single point leaves the frustum,
        // regardless of where the actual (large, tilted) mesh is - matching "disappears from
        // certain angles" exactly. RecalculateBounds() derives real bounds from the mesh's own
        // vertex data, fixing culling for every future frame this mesh is used, not just this
        // instance.
        private static void FixDegenerateMeshBounds(GameObject instance)
        {
            foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh != null && mesh.bounds.size.sqrMagnitude < 0.0001f)
                {
                    mesh.RecalculateBounds();
                }
            }
        }

        // See PlayerClimbSlopeLimitDegrees's own comment for why this needs raising at all.
        // Applied to the live scene's Player only (not Enemy/TrainingDummy) - this is a
        // player-traversal feature, not a general AI-navigation change, and EnemyAI's own
        // pathing never needs to climb the pagoda.
        private static void ConfigurePlayerClimbSlopeLimit()
        {
            GameObject player = GameObject.Find("Player");
            CharacterController controller = player != null ? player.GetComponent<CharacterController>() : null;
            if (controller == null)
            {
                Debug.LogWarning("Could not find Player's CharacterController - pagoda ramp may be unclimbable (slopeLimit still at its old value).");
                return;
            }

            controller.slopeLimit = PlayerClimbSlopeLimitDegrees;
        }

        private static void PlaceCalibratedProp(Transform parent, string assetName, string instanceName, Vector3 localPosition, float yaw, float targetHeight)
        {
            GameObject prefab = LoadModel(assetName);
            if (prefab == null)
            {
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = instanceName;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            float nativeHeight = MeasureCombinedBounds(instance).size.y;
            if (nativeHeight > 0.000001f)
            {
                instance.transform.localScale = Vector3.one * (targetHeight / nativeHeight);
            }
            else
            {
                Debug.LogWarning($"'{instanceName}' has no measurable renderer bounds - leaving at native scale.");
            }

            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        // The source .glb (Sketchfab "Low Poly Japanese Torii") is a full floating-island
        // diorama - bamboo grove, rocks, terrain, water, plus its own Camera and a Point light -
        // not a standalone gate. Rather than fight the composition by hand-picking which of its
        // 100+ nodes belong to "the torii," this keeps the whole piece intact (it reads well as
        // a self-contained floating vista element, similar in spirit to
        // DistantMountainsSetup's distant silhouettes) and only strips the embedded Camera/Light
        // so they don't leak into the scene's own lighting/camera setup.
        private static void PlaceToriiVista(Transform parent, Vector3 localPosition, float yaw)
        {
            GameObject prefab = LoadModel("Torii");
            if (prefab == null)
            {
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = "Torii_FloatingIsland";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            foreach (Camera cam in instance.GetComponentsInChildren<Camera>(true))
            {
                Object.DestroyImmediate(cam.gameObject);
            }

            foreach (Light light in instance.GetComponentsInChildren<Light>(true))
            {
                Object.DestroyImmediate(light.gameObject);
            }

            float nativeHeight = MeasureCombinedBounds(instance).size.y;
            if (nativeHeight > 0.000001f)
            {
                instance.transform.localScale = Vector3.one * (ToriiVistaTargetHeight / nativeHeight);
            }
            else
            {
                Debug.LogWarning("Torii_FloatingIsland has no measurable renderer bounds - leaving at native scale.");
            }

            instance.transform.localPosition = localPosition;

            // The source .glb's mesh node (the child directly under this instance, named after
            // its Sketchfab material hash) carries a baked-in localRotation of (90, 0, 0) - a
            // Blender Z-up -> glTF Y-up conversion matrix that glTFast imported as a literal node
            // rotation instead of folding away. Left uncorrected the whole floating island lies
            // on its side. Confirmed live via manage_camera screenshots (see PR discussion): a
            // -90 deg X correction applied here, before yaw, cancels it out exactly and leaves
            // the piece upright with only the intended yaw remaining.
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(-90f, 0f, 0f);
        }

        // Deliberately NOT using Renderer.bounds/Mesh.bounds - the Pagoda glTF import (single
        // flat primitive) came back with a Renderer that has a real MeshFilter (11282 verts,
        // confirmed live via a throwaway diagnostic script) but a zero-size cached bounds, while
        // the other two models' cached bounds were fine (this same zero-size cached bounds is
        // also what fed Unity's frustum culling and caused the tower to vanish from certain
        // camera angles - see FixDegenerateMeshBounds). Rather than assume every future glTFast
        // import will populate bounds correctly, this recomputes world-space bounds directly
        // from each MeshFilter's actual vertex positions, which can't go stale like a cached
        // value can. Called while the instance's transform is still identity (localPosition
        // zero, localRotation identity, localScale one) at every call site, so "world-space"
        // here is equivalent to the model's own native local-space bounds.
        private static Bounds MeasureCombinedBounds(GameObject instance)
        {
            MeshFilter[] filters = instance.GetComponentsInChildren<MeshFilter>();
            bool hasBounds = false;
            var bounds = new Bounds();

            foreach (MeshFilter filter in filters)
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                Matrix4x4 localToWorld = filter.transform.localToWorldMatrix;
                foreach (Vector3 vertex in mesh.vertices)
                {
                    Vector3 worldPoint = localToWorld.MultiplyPoint3x4(vertex);
                    if (!hasBounds)
                    {
                        bounds = new Bounds(worldPoint, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(worldPoint);
                    }
                }
            }

            return bounds;
        }

        private static GameObject LoadModel(string name)
        {
            string path = $"{AssetRoot}/{name}.glb";
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
            {
                Debug.LogError("Could not find Japanese architecture model at " + path);
            }

            return asset;
        }
    }
}
