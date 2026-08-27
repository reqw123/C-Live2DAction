using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.EditorTools;

namespace Live2DAction.EditorTools
{
    // 2026-08-20, explicit user request ("我把這四個牆面移除了 重新製作看的見的 確定有效後再改為
    // 透明牆面") - the user manually deleted BoundaryWall_North/South/East/West (the invisible,
    // collider-only Ground boundary walls - see GreyboxSceneBuilder.CreateBoundaryWalls and
    // BoundaryWallBlockEffectSetup) to sanity-check them by eye, and wants them rebuilt with a
    // visible mesh first so blocking can be confirmed visually in Play mode, then switched back
    // to invisible (collider-only) once confirmed - a two-step debug workflow, not a permanent
    // design change. GreyboxSceneBuilder itself is untouched - its walls stay invisible-by-default
    // for a from-scratch rebuild; this tool only operates on the already-open live scene.
    internal static class BoundaryWallVisibilityToggle
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string DebugMaterialPath = "Assets/_Project/VFX/BoundaryWallDebugVisible.mat";

        // Same geometry as GreyboxSceneBuilder.CreateBoundaryWalls - kept in sync by hand since
        // that method builds collider-only walls from an empty GameObject (no primitive mesh),
        // while this tool needs an actual Cube mesh to render, hence the different construction
        // path for the same numbers.
        private const float HalfExtent = 15f;
        private const float WallHeight = 6f;
        private const float WallThickness = 1f;

        private static readonly string[] WallNames =
        {
            "BoundaryWall_North", "BoundaryWall_South", "BoundaryWall_East", "BoundaryWall_West",
        };

        [MenuItem("Tools/Live2DAction/[Debug] Recreate Boundary Walls (Visible)")]
        public static void MakeVisible()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            float wallCenterOffset = HalfExtent + WallThickness / 2f;
            float wallCenterY = WallHeight / 2f;
            float wallSpan = HalfExtent * 2f + WallThickness * 2f;

            CreateOrUpdateVisibleWall("BoundaryWall_North", new Vector3(0f, wallCenterY, wallCenterOffset), new Vector3(wallSpan, WallHeight, WallThickness));
            CreateOrUpdateVisibleWall("BoundaryWall_South", new Vector3(0f, wallCenterY, -wallCenterOffset), new Vector3(wallSpan, WallHeight, WallThickness));
            CreateOrUpdateVisibleWall("BoundaryWall_East", new Vector3(wallCenterOffset, wallCenterY, 0f), new Vector3(WallThickness, WallHeight, wallSpan));
            CreateOrUpdateVisibleWall("BoundaryWall_West", new Vector3(-wallCenterOffset, wallCenterY, 0f), new Vector3(WallThickness, WallHeight, wallSpan));

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Boundary walls rebuilt with a visible bright-orange mesh so blocking can be confirmed in Play mode. Run '[Debug] Hide Boundary Walls (Restore Invisible)' once confirmed.");
        }

        [MenuItem("Tools/Live2DAction/[Debug] Hide Boundary Walls (Restore Invisible)")]
        public static void RestoreInvisible()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            foreach (string name in WallNames)
            {
                GameObject wall = GameObject.Find(name);
                if (wall == null)
                {
                    Debug.LogWarning(name + " not found - nothing to hide.");
                    continue;
                }

                MeshRenderer renderer = wall.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    Object.DestroyImmediate(renderer);
                }

                MeshFilter filter = wall.GetComponent<MeshFilter>();
                if (filter != null)
                {
                    Object.DestroyImmediate(filter);
                }
            }

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Boundary walls are invisible again (mesh removed, collider + block-touch effect untouched).");
        }

        private static void CreateOrUpdateVisibleWall(string name, Vector3 position, Vector3 size)
        {
            GameObject wall = GameObject.Find(name);
            bool isNew = wall == null;
            if (isNew)
            {
                wall = new GameObject(name);
            }

            wall.transform.position = position;
            // Deliberately kept at (1,1,1), NOT scaled to `size` - BoundaryWallBlockEffectSetup's
            // padded trigger collider is sized in this same GameObject's LOCAL units on top of the
            // solid BoxCollider's own size (see AddBlockEffectToWall), which only stays a thin
            // "just past the surface" shell in WORLD units if this transform's scale is identity.
            // A non-uniform scale here would blow the padding up non-uniformly (e.g. a 32-unit-
            // long wall would turn a 0.6-unit padding into ~19 units), making the touch effect
            // fire from far away instead of on contact - baking the actual dimensions into the
            // mesh and BoxCollider.size instead keeps this consistent with
            // GreyboxSceneBuilder.CreateBoundaryWall's own (scale-1, explicit-size) convention.
            wall.transform.localScale = Vector3.one;

            MeshFilter filter = wall.GetComponent<MeshFilter>();
            if (filter == null)
            {
                filter = wall.AddComponent<MeshFilter>();
            }
            filter.sharedMesh = BuildBoxMesh(size);

            MeshRenderer meshRenderer = wall.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = wall.AddComponent<MeshRenderer>();
            }
            meshRenderer.sharedMaterial = EnsureDebugMaterial();

            BoxCollider solid = wall.GetComponent<BoxCollider>();
            if (solid == null)
            {
                solid = wall.AddComponent<BoxCollider>();
            }
            solid.size = size;

            BoundaryWallBlockEffectSetup.EnsureHud();
            BoundaryWallBlockEffectSetup.AddBlockEffectToWall(wall);
        }

        // Bakes a cube mesh with vertices already at +/-size/2, so the GameObject itself can stay
        // at scale (1,1,1) - see the scale-vs-padding comment above for why that matters here.
        private static Mesh BuildBoxMesh(Vector3 size)
        {
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh mesh = Object.Instantiate(temp.GetComponent<MeshFilter>().sharedMesh);
            Object.DestroyImmediate(temp);

            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = Vector3.Scale(vertices[i], size);
            }
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material EnsureDebugMaterial()
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(DebugMaterialPath);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader);
            material.SetColor("_BaseColor", new Color(1f, 0.45f, 0.05f)); // bright orange - unmistakable as a debug/test wall

            if (!AssetDatabase.IsValidFolder("Assets/_Project/VFX"))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "VFX");
            }
            AssetDatabase.CreateAsset(material, DebugMaterialPath);
            return material;
        }
    }
}
