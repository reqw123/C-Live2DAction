using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Live2DAction.EditorTools
{
    // 2026-08-20, real bug report ("空島的地板沒有阻擋 會人物穿透") - root cause has nothing to do
    // with any collider being disabled (Terrain/Terrain.001's 4 MeshColliders were all already
    // enabled=true, confirmed live) or with Mesh.bounds (that was the earlier, DIFFERENT pond bug
    // already fixed by MeshBoundsFixer - this is a fresh, separate issue).
    //
    // The real cause: the whole Torii_FloatingIsland import (everything under it - terrain,
    // torii gate parts, bamboo, rocks, water) sits on the "Scenery" layer, same as every other
    // purely-decorative background prop in this scene (DistantMountains, MidDistanceTrees,
    // BackgroundScenery, FemaleStandee, the shrine's own lanterns/pagoda - confirmed live, ~305
    // objects total on that layer). Project Physics Settings has Default<->Scenery collision
    // globally IGNORED (Physics.GetIgnoreLayerCollision confirmed this live) - presumably set up
    // so the player walks through all that background dressing without being blocked by it, which
    // is correct for literally everything else on that layer. The sky island's actual load-bearing
    // ground got miscategorized into the same "purely visual" layer as its own decoration during
    // import, so even though its MeshColliders were always enabled, the layer-collision matrix
    // silently discarded every contact with the player regardless.
    //
    // Fix is deliberately NOT flipping the global Default<->Scenery ignore setting back on - that
    // would also make the player start colliding with distant mountains/trees/bushes/standees
    // everywhere else in the scene, which are correctly non-collidable by design. Instead, only
    // the 4 actually load-bearing terrain sub-meshes move to Default (matching Ground's own
    // layer), leaving every purely-decorative part of the island (torii gate, lanterns, bamboo,
    // rocks, water) on Scenery exactly as before - walking into a torii pillar was never the
    // complaint, falling through the ground was.
    internal static class FixSkyIslandTerrainCollisionLayer
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";

        private static readonly string[] TerrainMeshNames =
        {
            "Terrain_RockTerrain_Material_0",
            "Terrain_GrassTerrain_Material_0",
            "Terrain.001_Material_0",
            "Terrain.001_GrassTerrain_Material_0",
        };

        [MenuItem("Tools/Live2DAction/[Fix] Sky Island Terrain Collision Layer")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject island = GameObject.Find("Torii_FloatingIsland");
            if (island == null)
            {
                Debug.LogError("Torii_FloatingIsland not found.");
                return;
            }

            int defaultLayer = LayerMask.NameToLayer("Default");
            int fixedCount = 0;

            MeshCollider[] colliders = island.GetComponentsInChildren<MeshCollider>(true);
            foreach (MeshCollider mc in colliders)
            {
                bool isTerrain = false;
                foreach (string name in TerrainMeshNames)
                {
                    if (mc.gameObject.name == name)
                    {
                        isTerrain = true;
                        break;
                    }
                }

                if (!isTerrain)
                {
                    continue;
                }

                if (mc.gameObject.layer != defaultLayer)
                {
                    mc.gameObject.layer = defaultLayer;
                    fixedCount++;
                }
            }

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Moved " + fixedCount + " sky island terrain sub-meshes from Scenery to Default layer (Default<->Scenery collision is globally ignored in this project, which was silently making the island floor non-collidable). Every other part of the island (torii gate, bamboo, rocks, water) stays on Scenery.");
        }
    }
}
