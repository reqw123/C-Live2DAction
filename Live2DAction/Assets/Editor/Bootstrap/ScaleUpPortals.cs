using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.World;

namespace Live2DAction.EditorTools
{
    // 2026-08-20, explicit user request ("傳送門外觀和判定範圍等比例放大2.5倍") - both existing
    // Portal instances (Portal_GroundToSky_Root, Portal_SkyToGround_Root) are a root GameObject
    // carrying the Portal component + the one BoxCollider (the detection/teleport trigger), with a
    // single visual-mesh child underneath it. Because BOTH the trigger's BoxCollider.size and the
    // visual child's own transform are defined in the ROOT's local space, scaling the root
    // transform uniformly scales appearance and detection range together automatically - no need
    // to touch the child or the collider size by hand, and no double-scaling risk the boundary
    // walls had (those had a second, independently-sized padded trigger living in the SAME local
    // space as the thing being scaled - Portal has only the one Collider).
    //
    // Position is deliberately left untouched: both portals' BoxCollider is centered at local
    // (0,1,0) with local size (1.2,2,1.2), i.e. its bottom face sits exactly at local Y=0 (the
    // root's own position) - scaling the root uniformly keeps that bottom face pinned to the same
    // world Y (the portal grows upward and outward from its own base, not through the floor), and
    // the X/Z footprint stays centered on the same root position too. Confirmed by direct
    // screenshot before/after rather than assumed.
    internal static class ScaleUpPortals
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const float ScaleFactor = 2.5f;

        [MenuItem("Tools/Live2DAction/[Fix] Scale Up Portals 2.5x")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Portal[] portals = Object.FindObjectsByType<Portal>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Portal portal in portals)
            {
                portal.transform.localScale = Vector3.one * ScaleFactor;
            }

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Scaled " + portals.Length + " portal(s) " + ScaleFactor + "x (visual + trigger, root transform only, position unchanged).");
        }
    }
}
