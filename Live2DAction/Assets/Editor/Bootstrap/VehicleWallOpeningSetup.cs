using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.World;

namespace Live2DAction.EditorTools
{
    // 2026-08-28, explicit user request ("目前圍牆是封死的，提供一個比車身大1.2倍的洞口" -> then
    // "接下來我想做一條道路個車通行(現在的洞口且洞口寬度增加長度1.5倍)") - cuts a driveable gap in one
    // of GreyboxSceneBuilder's four BoundaryWall_* boxes AND lays a road slab from the arena edge
    // out onto the BackgroundTerrain (which has no collider of its own - the road IS the drivable
    // surface out there).
    //
    // Repeatable (delete-and-rebuild):
    //   - the target wall: removes every BoxCollider (original solid + the BoundaryBlockEffect
    //     trigger), replaces them with TWO solid segment colliders one each side of the gap;
    //     disables the wall's own MeshRenderer and parents two "WallSegment_L/R" cube visuals
    //     (same material); disables BoundaryBlockEffect + the RippleEmitter child on this wall
    //     (it's an intentional exit now). The other three walls are untouched.
    //   - "VehicleRoad": a top-level slab (collider + visual), top flush with Ground (y 0.5),
    //     centred on the gap, running from the arena edge outward.
    internal static class VehicleWallOpeningSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string RoadMaterialPath = "Assets/_Project/Environment/Materials/RoadSurface.mat";
        private const string RoadName = "VehicleRoad";

        // Which wall gets the opening, and where along it (local units from the wall's centre).
        private const string WallName = "BoundaryWall_South";
        private const float GapCentreOffset = 0f;

        // Mirrors GreyboxSceneBuilder.CreateBoundaryWalls (halfExtent 15, thickness 1 -> span 32).
        private const float WallSpan = 32f;
        private const float WallHeight = 6f;
        private const float WallThickness = 1f;
        private const float ArenaHalfZ = 15f;   // Ground is 30x30 -> edge at |15|
        private const float GroundTopY = 0.5f;

        // Gap width = the Buggy's full track width (axle span + a tyre-radius margin each side) x
        // this. 1.8 = the original "比車身大1.2倍" x the later "寬度增加 1.5倍".
        private const float GapWidthMultiplier = 1.8f;
        private const float CarWidthFallback = 2.2f;
        private const float GapMinWidth = 2.4f;

        // Road: a bit wider than the gap so there's steering margin, and a good long run outward.
        // 2026-08-30: widened (2 -> 3.5) and lengthened (65 -> 70) so it reads as a real
        // inter-district road to the enlarged 60x60 學校, not a driveway. Far end stays within
        // BackgroundTerrain (z -150): -15 - 70 - 60 = -145.
        private const float RoadWidthOverGap = 3.5f;
        private const float RoadOutwardLength = 70f;

        [MenuItem("Tools/Live2DAction/Add Vehicle Wall Opening + Road")]
        public static void Apply()
        {
            if (Application.isPlaying)
            {
                Debug.LogError("Exit Play Mode before running VehicleWallOpeningSetup (it opens/saves the scene).");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject wall = GameObject.Find(WallName);
            if (wall == null)
            {
                Debug.LogError($"'{WallName}' not found in {ScenePath} - run GreyboxSceneBuilder first?");
                return;
            }

            // N/S walls run along local X (thin in Z, so "outward" is Z); E/W run along Z (outward X).
            bool alongX = WallName.Contains("North") || WallName.Contains("South");
            float carWidth = MeasureBuggyWidth();
            float gapWidth = Mathf.Max(GapMinWidth, carWidth * GapWidthMultiplier);

            float half = WallSpan / 2f;
            float gapL = Mathf.Clamp(GapCentreOffset - gapWidth / 2f, -half + 0.5f, half - 0.5f);
            float gapR = Mathf.Clamp(GapCentreOffset + gapWidth / 2f, -half + 0.5f, half - 0.5f);
            float leftWidth = gapL - (-half);
            float leftCentre = (-half + gapL) / 2f;
            float rightWidth = half - gapR;
            float rightCentre = (gapR + half) / 2f;

            // --- repeatable cleanup ---
            for (int i = wall.transform.childCount - 1; i >= 0; i--)
            {
                Transform ch = wall.transform.GetChild(i);
                if (ch.name == "WallSegment_L" || ch.name == "WallSegment_R")
                {
                    Object.DestroyImmediate(ch.gameObject);
                }
            }
            foreach (BoxCollider bc in wall.GetComponents<BoxCollider>())
            {
                Object.DestroyImmediate(bc);
            }
            GameObject oldRoad = GameObject.Find(RoadName);
            if (oldRoad != null) Object.DestroyImmediate(oldRoad);

            // --- the wall's own MeshRenderer / block-effect: this tool owns them now ---
            var wallRenderer = wall.GetComponent<MeshRenderer>();
            Material wallMat = wallRenderer != null ? wallRenderer.sharedMaterial : null;
            if (wallRenderer != null) wallRenderer.enabled = false;
            var blockEffect = wall.GetComponent<BoundaryBlockEffect>();
            if (blockEffect != null) blockEffect.enabled = false;
            Transform ripple = wall.transform.Find("RippleEmitter");
            if (ripple != null) ripple.gameObject.SetActive(false);

            // --- two solid segment colliders + two visible segment cubes ---
            AddSegmentCollider(wall, alongX, leftCentre, leftWidth);
            AddSegmentCollider(wall, alongX, rightCentre, rightWidth);
            AddSegmentVisual(wall, "WallSegment_L", alongX, leftCentre, leftWidth, wallMat);
            AddSegmentVisual(wall, "WallSegment_R", alongX, rightCentre, rightWidth, wallMat);

            // --- the road slab ---
            // Gap centre in world space, and the outward direction (away from the arena centre).
            Vector3 gapWorld = wall.transform.position + (alongX ? new Vector3(GapCentreOffset, 0f, 0f) : new Vector3(0f, 0f, GapCentreOffset));
            float outwardSign = alongX ? Mathf.Sign(wall.transform.position.z) : Mathf.Sign(wall.transform.position.x);
            float roadWidth = gapWidth + RoadWidthOverGap;
            float nearAlong = ArenaHalfZ;                       // flush with the Ground edge
            float farAlong = ArenaHalfZ + RoadOutwardLength;
            float roadCentreAlong = outwardSign * (nearAlong + farAlong) / 2f;
            float roadLength = farAlong - nearAlong;

            // Thin slab, TOP surface 5 mm above Ground/Terrain (y 0.5) so it never z-fights the
            // BackgroundTerrain plane it lies on; a 5 mm lip is nothing for the Buggy's wheels.
            const float roadThickness = 0.2f;
            float roadCentreY = GroundTopY + 0.005f - roadThickness / 2f;
            var road = GameObject.CreatePrimitive(PrimitiveType.Cube);
            road.name = RoadName;
            road.transform.position = alongX
                ? new Vector3(gapWorld.x, roadCentreY, roadCentreAlong)
                : new Vector3(roadCentreAlong, roadCentreY, gapWorld.z);
            road.transform.localScale = alongX
                ? new Vector3(roadWidth, roadThickness, roadLength)
                : new Vector3(roadLength, roadThickness, roadWidth);
            road.layer = LayerMask.NameToLayer("Default"); // WheelCollider raycasts hit Default
            road.GetComponent<MeshRenderer>().sharedMaterial = LoadOrCreateRoadMaterial();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"VehicleWallOpeningSetup: {gapWidth:F2}-wide gap in {WallName} (car {carWidth:F2} x {GapWidthMultiplier}); " +
                      $"'{RoadName}' {roadWidth:F1} wide x {roadLength:F0} long, top y {GroundTopY}. " +
                      "BackgroundTerrain has no collider so the road is the drivable surface out there.");
        }

        private static Material LoadOrCreateRoadMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(RoadMaterialPath);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", new Color(0.16f, 0.16f, 0.17f));
            mat.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(mat, RoadMaterialPath);
            return mat;
        }

        private static float MeasureBuggyWidth()
        {
            GameObject buggy = GameObject.Find("Buggy");
            if (buggy == null) return CarWidthFallback;

            float minX = float.MaxValue, maxX = float.MinValue, maxRadius = 0f;
            foreach (WheelCollider wc in buggy.GetComponentsInChildren<WheelCollider>(true))
            {
                Vector3 local = buggy.transform.InverseTransformPoint(wc.transform.position);
                minX = Mathf.Min(minX, local.x);
                maxX = Mathf.Max(maxX, local.x);
                maxRadius = Mathf.Max(maxRadius, wc.radius);
            }
            if (minX > maxX)
            {
                foreach (BoxCollider bc in buggy.GetComponentsInChildren<BoxCollider>(true))
                {
                    return Mathf.Max(bc.size.x, bc.size.z) * Mathf.Max(buggy.transform.lossyScale.x, buggy.transform.lossyScale.z);
                }
                return CarWidthFallback;
            }
            return (maxX - minX) + 2f * maxRadius;
        }

        private static void AddSegmentCollider(GameObject wall, bool alongX, float centreAlong, float width)
        {
            if (width <= 0.01f) return;
            var bc = wall.AddComponent<BoxCollider>();
            bc.center = alongX ? new Vector3(centreAlong, 0f, 0f) : new Vector3(0f, 0f, centreAlong);
            bc.size = alongX
                ? new Vector3(width, WallHeight, WallThickness)
                : new Vector3(WallThickness, WallHeight, width);
        }

        private static void AddSegmentVisual(GameObject wall, string name, bool alongX, float centreAlong, float width, Material mat)
        {
            if (width <= 0.01f) return;
            var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seg.name = name;
            Object.DestroyImmediate(seg.GetComponent<BoxCollider>());
            seg.transform.SetParent(wall.transform, false);
            seg.transform.localRotation = Quaternion.identity;
            seg.transform.localPosition = alongX ? new Vector3(centreAlong, 0f, 0f) : new Vector3(0f, 0f, centreAlong);
            seg.transform.localScale = alongX
                ? new Vector3(width, WallHeight, WallThickness)
                : new Vector3(WallThickness, WallHeight, width);
            if (mat != null) seg.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }
    }
}
