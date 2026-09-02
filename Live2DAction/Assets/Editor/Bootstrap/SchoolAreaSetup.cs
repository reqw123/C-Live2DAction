using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Live2DAction.EditorTools
{
    // 2026-08-29, explicit user request ("接下來我要製作新城市... 通道頂端再銜接建立一個像 ground 一樣
    // 的土地，命名為'學校'" -> follow-up: "學校的圍牆要跟本地一樣只留一個洞口，圍牆用不同顏色").
    //
    // A 30x30 greybox ground slab "學校" flush-connected to the south (far) end of VehicleRoad,
    // with a FULL perimeter wall broken by a single opening on the north side where the road
    // comes in - the same "solid wall + one driveable gap" shape 本地's BoundaryWall_South has.
    // Wall material is a distinct colour (teal) so 學校 reads apart from 本地's orange walls.
    // Same ground material / top-surface height as GreyboxSceneBuilder's 本地 Ground.
    //
    // Re-runnable (delete + rebuild "學校" and its SchoolWall_* siblings). Buildings / a proper
    // road merge / spawn points / portals are follow-up steps.
    internal static class SchoolAreaSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string GroundMaterialPath = "Assets/_Project/Environment/Materials/Ground_StoneFloor.mat";
        private const string WallMaterialPath = "Assets/_Project/Environment/Materials/SchoolWall.mat";

        private const string GroundName = "學校";
        // 2026-08-30: 30 -> 60. 30x30 (one "room", same as 本地) was too cramped for a campus with
        // 元培大樓 + two libraries + a courtyard. 60x60 gives each building a real footprint, a
        // ~20-25m central plaza, and room to move/fight. North edge still flush with VehicleRoad's
        // (now longer) far end.
        private const float AreaSize = 60f;
        private const float GroundTopY = 0.5f;
        private const float RoadFarEndFallbackZ = -80f;
        private const float RoadHalfWidthFallback = 3f;

        private const float WallHeight = 6f;
        private const float WallThickness = 1f;
        private const float GapMarginPerSide = 0.6f;   // gap = road width + this each side

        private static readonly string[] WallNames =
        {
            "SchoolWall_South", "SchoolWall_East", "SchoolWall_West",
            "SchoolWall_NorthLeft", "SchoolWall_NorthRight",
        };

        [MenuItem("Tools/Live2DAction/Add School Area (學校 ground + walls)")]
        public static void Apply()
        {
            if (Application.isPlaying)
            {
                Debug.LogError("Exit Play Mode before running SchoolAreaSetup (it opens/saves the scene).");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // Read the live road collider so the connection + gap stay in sync with it.
            float roadEndZ = RoadFarEndFallbackZ;
            float roadHalfWidth = RoadHalfWidthFallback;
            GameObject road = GameObject.Find("VehicleRoad");
            if (road != null && road.GetComponent<Collider>() != null)
            {
                Bounds rb = road.GetComponent<Collider>().bounds;
                roadEndZ = rb.min.z;
                roadHalfWidth = rb.extents.x;
            }
            else
            {
                Debug.LogWarning("SchoolAreaSetup: 'VehicleRoad' not found - using fallback far-end z " + RoadFarEndFallbackZ +
                                 " / half-width " + RoadHalfWidthFallback + ". Run 'Add Vehicle Wall Opening + Road' first.");
            }

            float half = AreaSize / 2f;
            float centreZ = roadEndZ - half;                    // north edge flush with the road end
            float gapHalf = Mathf.Min(half - WallThickness, roadHalfWidth + GapMarginPerSide);

            DestroyExisting(GroundName);
            foreach (string n in WallNames) DestroyExisting(n);

            // --- ground slab (top surface at GroundTopY, like 本地 Ground) ---
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = GroundName;
            ground.transform.position = new Vector3(0f, GroundTopY - 0.5f, centreZ);
            ground.transform.localScale = new Vector3(AreaSize, 1f, AreaSize);
            ground.transform.rotation = Quaternion.identity;
            var groundMat = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
            if (groundMat != null)
            {
                ground.GetComponent<MeshRenderer>().sharedMaterial = groundMat;
            }

            // --- full perimeter wall with ONE opening on the north (road) side ---
            Material wallMat = LoadOrCreateWallMaterial();
            float centreOffset = half + WallThickness / 2f;     // inner face flush with the ground edge
            float centreY = WallHeight / 2f;
            float span = AreaSize + WallThickness * 2f;          // overlap the corners

            CreateWall("SchoolWall_South", new Vector3(0f, centreY, centreZ - centreOffset), new Vector3(span, WallHeight, WallThickness), wallMat);
            CreateWall("SchoolWall_East", new Vector3(centreOffset, centreY, centreZ), new Vector3(WallThickness, WallHeight, span), wallMat);
            CreateWall("SchoolWall_West", new Vector3(-centreOffset, centreY, centreZ), new Vector3(WallThickness, WallHeight, span), wallMat);

            // North side: two segments flanking the road gap (centred on x = 0, same as the road).
            float northZ = centreZ + centreOffset;
            float leftInner = -gapHalf;
            float rightInner = gapHalf;
            float outer = half + WallThickness;
            float leftWidth = leftInner - (-outer);
            float rightWidth = outer - rightInner;
            CreateWall("SchoolWall_NorthLeft", new Vector3((-outer + leftInner) / 2f, centreY, northZ), new Vector3(leftWidth, WallHeight, WallThickness), wallMat);
            CreateWall("SchoolWall_NorthRight", new Vector3((rightInner + outer) / 2f, centreY, northZ), new Vector3(rightWidth, WallHeight, WallThickness), wallMat);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"SchoolAreaSetup: '{GroundName}' {AreaSize}x{AreaSize} at z [{centreZ - half:F0}, {centreZ + half:F0}] " +
                      $"(north edge {roadEndZ:F0} = VehicleRoad far end), top y {GroundTopY}. " +
                      $"Full perimeter wall (teal), single {gapHalf * 2f:F1}-wide opening on the north for the road.");
        }

        private static void CreateWall(string name, Vector3 position, Vector3 size, Material mat)
        {
            if (size.x <= 0.01f || size.z <= 0.01f)
            {
                return;
            }
            var wall = new GameObject(name);
            wall.transform.position = position;
            wall.transform.rotation = Quaternion.identity;

            BoxCollider collider = wall.AddComponent<BoxCollider>();
            collider.size = size;

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            Object.DestroyImmediate(visual.GetComponent<BoxCollider>());
            visual.transform.SetParent(wall.transform, false);
            visual.transform.localScale = size;
            if (mat != null)
            {
                visual.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
        }

        private static Material LoadOrCreateWallMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(WallMaterialPath);
            if (mat != null)
            {
                return mat;
            }
            EnsureFolder("Assets/_Project/Environment/Materials");
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            // Teal - clearly not 本地's orange, not the grey greybox default.
            mat.SetColor("_BaseColor", new Color(0.13f, 0.55f, 0.5f));
            mat.SetFloat("_Smoothness", 0.1f);
            AssetDatabase.CreateAsset(mat, WallMaterialPath);
            return mat;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static void DestroyExisting(string name)
        {
            foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (go != null && go.name == name && go.transform.parent == null)
                {
                    Object.DestroyImmediate(go);
                }
            }
        }
    }
}
