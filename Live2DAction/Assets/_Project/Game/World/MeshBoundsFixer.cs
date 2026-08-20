using UnityEngine;

namespace Live2DAction.World
{
    // Real playtested bug, explicit user request ("在池塘邊時依舊穿模 理論上看見的應該要是綠色草地")
    // - reproduced by standing right at the sky island pond's shore and pitching the camera
    // steeply down: instead of the grass shore, the view showed the rock terrain's underside
    // (a dark, cracked, "Cull Off" backface that's normally invisible, sitting far below near
    // SkyIsland_UndersideBlocker's level) straight through where the grass should have been.
    //
    // Root cause verified directly (triangle-level, not guessed): the grass mesh's own geometry
    // and collider both genuinely cover that spot (same shared Mesh asset for MeshFilter and
    // MeshCollider, upward-facing normal, correct green baseColorFactor) - it just never got
    // DRAWN from that camera angle. `Terrain.001_GrassTerrain_Material_0`'s (and several sibling
    // greybox meshes' - rock terrain, water, etc.) `Mesh.bounds` comes back completely
    // degenerate (zero size, sitting at the mesh's own local origin) despite the mesh's actual
    // vertex data spanning ~25 world units - these are glTF-imported meshes, and whatever
    // pipeline produced this project's imported FBX apparently never ran the usual
    // RecalculateBounds() step. Unity's per-renderer frustum culling trusts that (broken) bounds
    // rather than the real vertex data, so any camera angle that doesn't happen to contain the
    // mesh's arbitrary local-origin point gets the WHOLE renderer culled away, silently, even
    // though its actual geometry would be plainly in view - reads exactly like "camera clipping
    // through the world" the moment you look toward real geometry far from that origin point
    // (steeply down at the pond shore, here).
    //
    // Fixing the Mesh asset's bounds in the Editor doesn't stick - it's FBX/glTF sub-asset data,
    // regenerated (with the same broken bounds) on every reimport, not something a one-off
    // Editor bootstrap tool can permanently correct in place (unlike this project's other
    // Bootstrap/*Setup.cs scripts, which edit ordinary scene data). So this runs every time the
    // scene actually loads instead: [ExecuteAlways] so it also self-heals the Editor's own Scene
    // View preview, not just Play mode.
    [ExecuteAlways]
    public class MeshBoundsFixer : MonoBehaviour
    {
        private void Awake()
        {
            FixAll();
        }

        private void OnEnable()
        {
            FixAll();
        }

        private void FixAll()
        {
            foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;

                // A real mesh with more than one vertex never legitimately has an exactly
                // zero-sized bounds - only this broken-import case looks like that.
                if (mesh != null && mesh.vertexCount > 1 && mesh.bounds.size.sqrMagnitude <= 0f)
                {
                    mesh.RecalculateBounds();
                }
            }
        }
    }
}
