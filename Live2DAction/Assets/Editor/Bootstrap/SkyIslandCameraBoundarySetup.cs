using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Live2DAction.EditorTools
{
    // 2026-08-19, explicit user request ("空島上無法自由遊走 且視角移動時偶爾會穿透到場景之外") -
    // two related sky-island bugs surfaced once the ground<->sky flight route made the island
    // actually walkable/explorable for the first time (previously only the Portal, which
    // teleports position and ignores colliders, ever put a player there):
    //
    // (1) "無法自由遊走" (can't roam freely) - traced to a leftover flat SkyIsland_Ground
    // BoxCollider (a rough placeholder from before the real Torii_FloatingIsland terrain asset
    // was ever placed) sitting at a fixed y~22.1 and physically conflicting with that asset's own
    // proper non-convex MeshColliders (Terrain_GrassTerrain_Material_0 etc., which already match
    // the real undulating grass/rock surface exactly). Two overlapping, mismatched collision
    // shapes fighting the CharacterController's ground resolution read as getting stuck/blocked.
    // Fixed by disabling SkyIsland_Ground's BoxCollider directly (not part of this script) -
    // the real mesh colliders are sufficient on their own.
    //
    // (2) "視角移動時偶爾會穿透到場景之外" - a direct regression from THIS session's own earlier
    // fix ("空島周圍解除邊界阻擋"): deleting SkyIsland_Boundary entirely (its 24 wall segments
    // fully closed off flight-entry from outside) also removed the only nearby geometry
    // ThirdPersonCameraController's own obstruction-avoidance SphereCast had to catch against near
    // the island's edges (see that class's FindObstructionDistance - null obstruction just means
    // "use the full desired distance unclamped", so with nothing there any more the camera happily
    // swings out past the edge into open air/the void). A solid wall can't come back without
    // reproducing the original entry-blocking bug, so this uses Unity's layer collision matrix
    // instead: the ring lives entirely on a dedicated "SkyIslandCameraBlocker" layer with player-
    // layer collision explicitly disabled (Physics.IgnoreLayerCollision), so the player's own
    // CharacterController (walking OR flying) passes straight through it, while
    // Physics.SphereCastAll - a spatial query, not a physical collision resolution, and therefore
    // NOT affected by the ignore-collision matrix - still detects it and pulls the camera in.
    internal static class SkyIslandCameraBoundarySetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string LayerName = "SkyIslandCameraBlocker";
        private const string RootName = "SkyIslandCameraBoundary";

        private static readonly Vector3 IslandCenter = new Vector3(-70f, 0f, -25.2f);
        private const float Radius = 17f;
        private const int SegmentCount = 16;
        private const float WallHeight = 6.3f; // same height convention as GreyboxSceneBuilder's own ground-map boundary walls
        private const float WallBaseY = 22.1f; // sits right at the island's own terrain surface height
        private const float WallThickness = 1.5f;

        [MenuItem("Tools/Live2DAction/Add Sky Island Camera Boundary")]
        public static void Apply()
        {
            int layer = LayerMask.NameToLayer(LayerName);
            if (layer < 0)
            {
                Debug.LogError($"Layer '{LayerName}' doesn't exist yet - add it via Edit > Project Settings > Tags and Layers first.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject existingRoot = GameObject.Find(RootName);
            if (existingRoot != null)
            {
                Object.DestroyImmediate(existingRoot);
            }
            var root = new GameObject(RootName);

            float arcLength = 2f * Mathf.PI * Radius / SegmentCount;
            for (int i = 0; i < SegmentCount; i++)
            {
                float angle = i * Mathf.PI * 2f / SegmentCount;
                Vector3 offset = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * Radius;
                Vector3 pos = IslandCenter + offset + new Vector3(0f, WallBaseY, 0f);

                var go = new GameObject($"CameraBlocker_{i}");
                go.transform.SetParent(root.transform, false);
                go.transform.position = pos;
                go.transform.rotation = Quaternion.LookRotation(offset.normalized, Vector3.up);
                go.layer = layer;

                BoxCollider box = go.AddComponent<BoxCollider>();
                box.size = new Vector3(arcLength * 1.15f, WallHeight, WallThickness); // slight overlap between segments so the ring has no gaps a SphereCast could slip through
            }

            // Camera obstruction detection (Physics.SphereCastAll) is a spatial query, unaffected
            // by the collision matrix - only actual physical collision resolution (CharacterController
            // movement, Rigidbody contacts) respects this, which is exactly the split this fix needs.
            int defaultLayer = LayerMask.NameToLayer("Default");
            Physics.IgnoreLayerCollision(layer, defaultLayer, true);

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"Added Sky Island camera boundary ({SegmentCount} segments) on layer '{LayerName}' - blocks camera obstruction-avoidance only, player movement passes through freely.");
        }
    }
}
