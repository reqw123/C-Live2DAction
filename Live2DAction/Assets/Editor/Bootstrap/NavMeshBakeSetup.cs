using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Live2DAction.AI;

namespace Live2DAction.EditorTools
{
    // 2026-08-31, user request ("所有角色在移動時有可能會被地圖物件擋住路線從卡住 有沒有演算法可以避
    // 開這個問題" -> picked "AI: NavMesh 路徑跟隨"). Bakes ONE NavMesh over the walkable ground
    // (本地 Ground + VehicleRoad + 學校) so the new NavPathFollower has something to route around
    // obstacles with, and wires the follower onto the AI that walks (武士 / 屁孩王 / Enemy).
    //
    // Component-based (Unity.AI.Navigation.NavMeshSurface) on a dedicated "Navigation" root, fully
    // re-runnable. Buildings / cover / walls become obstacles automatically - the surface collects
    // Physics Colliders and they all have Box/MeshColliders. Characters and the vehicle are tagged
    // with NavMeshModifier(ignoreFromBuild) so they don't carve holes in the mesh at bake time.
    //
    // Re-run this after moving or adding map geometry. It is NOT called from any other setup script
    // (a full nav bake is slow and the map is not rebuilt every session) - it's a manual step.
    internal static class NavMeshBakeSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string NavRootName = "Navigation";

        // Roots whose colliders must NOT contribute to the bake (they move / are the agents).
        private static readonly string[] ExcludeFromBake =
        {
            "Player", "Cat", "Enemy", "武士", "屁孩王",
            "中立者1", "中立者2", "中立者3", "Buggy", "十足蟲",
        };

        // AI roots that walk and should get a NavPathFollower.
        private static readonly string[] PathFollowerTargets = { "武士", "屁孩王", "Enemy", "十足蟲" };

        [MenuItem("Tools/Live2DAction/Bake Navigation Mesh")]
        public static void Run()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject navRoot = GameObject.Find(NavRootName) ?? new GameObject(NavRootName);
            navRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            NavMeshSurface surface = navRoot.GetComponent<NavMeshSurface>();
            if (surface == null)
            {
                surface = navRoot.AddComponent<NavMeshSurface>();
            }
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders; // Box/MeshColliders, not visual meshes
            surface.agentTypeID = 0; // built-in Humanoid (radius 0.5 / height 2 / slope 45 / step 0.4 - fits every character here)
            // Everything except the sky-island backdrop layer. Characters/vehicle are excluded via
            // NavMeshModifier below rather than by layer (they're all on Default).
            int scenery = LayerMask.NameToLayer("Scenery");
            surface.layerMask = scenery >= 0 ? ~(1 << scenery) : ~0;

            int excluded = TagExclusions();
            int followers = WirePathFollowers();

            // Synchronous bake, then persist the data next to the scene so it survives a reload.
            surface.BuildNavMesh();
            PersistNavMeshData(surface, scene);
            surface.RemoveData();
            surface.AddData(); // re-register the now-persisted asset so it's live immediately

            EditorUtility.SetDirty(surface);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            var tri = NavMesh.CalculateTriangulation();
            Debug.Log($"[NavMeshBakeSetup] Baked NavMesh onto '{NavRootName}' " +
                      $"({tri.vertices.Length} verts). Excluded {excluded} mover(s) from the bake, " +
                      $"wired NavPathFollower onto {followers} AI. Re-run after changing map geometry.");
        }

        private static int TagExclusions()
        {
            int n = 0;
            foreach (string name in ExcludeFromBake)
            {
                GameObject go = GameObject.Find(name);
                if (go == null)
                {
                    continue;
                }
                NavMeshModifier mod = go.GetComponent<NavMeshModifier>();
                if (mod == null)
                {
                    mod = go.AddComponent<NavMeshModifier>();
                }
                mod.ignoreFromBuild = true;
                mod.overrideArea = false;
                EditorUtility.SetDirty(go);
                n++;
            }
            return n;
        }

        private static int WirePathFollowers()
        {
            int n = 0;
            foreach (string name in PathFollowerTargets)
            {
                GameObject go = GameObject.Find(name);
                if (go == null)
                {
                    continue;
                }
                if (go.GetComponent<NavPathFollower>() == null)
                {
                    go.AddComponent<NavPathFollower>();
                }
                EditorUtility.SetDirty(go);
                n++;
            }
            return n;
        }

        private static void PersistNavMeshData(NavMeshSurface surface, Scene scene)
        {
            NavMeshData data = surface.navMeshData;
            if (data == null)
            {
                Debug.LogError("[NavMeshBakeSetup] BuildNavMesh produced no data - is there any walkable collider under 本地?");
                return;
            }

            string sceneDir = Path.GetDirectoryName(scene.path).Replace('\\', '/');
            string folder = sceneDir + "/" + Path.GetFileNameWithoutExtension(scene.path);
            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder(sceneDir, Path.GetFileNameWithoutExtension(scene.path));
            }

            string assetPath = folder + "/NavMesh-Navigation.asset";
            data.name = Path.GetFileNameWithoutExtension(assetPath); // silence the file/object name-mismatch warning
            NavMeshData existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(assetPath);
            if (existing != null)
            {
                // Keep the existing asset's GUID so the scene reference stays valid across re-bakes.
                EditorUtility.CopySerialized(data, existing);
                existing.name = data.name;
                surface.navMeshData = existing;
            }
            else
            {
                AssetDatabase.CreateAsset(data, assetPath);
            }
        }
    }
}
