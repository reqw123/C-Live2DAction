using UnityEngine;

namespace Live2DAction.Combat
{
    // 2026-09-01, user request ("我不要攻擊的碰撞顯示 改成防禦"). The in-world telegraph for the
    // player's GUARD, in the same spirit as the boss's BossHitboxVisualizer: a bright unlit mesh
    // that appears only while the thing it represents is actually live.
    //
    // PlayerGuardUtility.IsFrontalBlock judges a block purely on the HORIZONTAL angle between the
    // player's facing and the incoming hit, within PlayerGuard.GuardArcDegrees (a full cone). So
    // the visual is a flat horizontal pie-slice fanning out in front of the player across exactly
    // that arc, shown while PlayerGuard.IsBlocking (right mouse held, not dead / staggered).
    [RequireComponent(typeof(PlayerGuard))]
    public class PlayerGuardVisualizer : MonoBehaviour
    {
        [SerializeField] private PlayerGuard guard;

        [Tooltip("How far the wedge reaches out from the player, metres.")]
        [SerializeField] private float range = 2f;

        [Tooltip("Height above the player's pivot the flat wedge sits at (chest-ish).")]
        [SerializeField] private float height = 1.1f;

        [Tooltip("Bright blue - a defensive colour, distinct from the boss's red 'this hits you' " +
                 "and the green used for player attack ranges. Shown during the normal guard window.")]
        [SerializeField] private Color blockColor = new Color(0.2f, 0.6f, 1f, 1f);

        [Tooltip("Colour during the parry window (first parryWindowDuration after the press edge).")]
        [SerializeField] private Color parryColor = new Color(1f, 1f, 1f, 1f);

        [Tooltip("Fan resolution. Higher = smoother arc edge.")]
        [SerializeField, Range(6, 96)] private int segments = 48;

        private Transform _visual;
        private MeshRenderer _renderer;
        private float _builtArc = -1f;

        private void Awake()
        {
            if (guard == null)
            {
                guard = GetComponent<PlayerGuard>();
            }
            BuildVisual();
        }

        private void BuildVisual()
        {
            var go = new GameObject(gameObject.name + "_GuardWedgeVisual");
            go.hideFlags = HideFlags.DontSave;
            _visual = go.transform;
            // Parented to the player so it tracks position AND facing for free - the block check
            // is relative to transform.forward, and the player root only yaws.
            _visual.SetParent(transform, false);
            _visual.localPosition = new Vector3(0f, height, 0f);
            _visual.localRotation = Quaternion.identity;

            go.AddComponent<MeshFilter>();
            _renderer = go.AddComponent<MeshRenderer>();
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = blockColor };
            _renderer.sharedMaterial = mat;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.enabled = false;

            RebuildMesh(guard != null ? guard.GuardArcDegrees : 150f);
        }

        // Fan vertices in the local XZ plane: [0] = apex at the origin, then seg+1 rim points from
        // -arc/2 to +arc/2 swept around local +Z at `range` out. Pure so it's unit-testable.
        public static Vector3[] BuildFanVertices(float arcDegrees, float range, int segments)
        {
            int seg = Mathf.Clamp(segments, 6, 96);
            var verts = new Vector3[seg + 2];
            verts[0] = Vector3.zero;
            float half = arcDegrees * 0.5f;
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.Lerp(-half, half, i / (float)seg) * Mathf.Deg2Rad;
                verts[i + 1] = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * Mathf.Max(0f, range);
            }
            return verts;
        }

        // Triangle fan in the local XZ plane, apex at the origin, centred on local +Z, spanning
        // the full arc. Double-sided (two winding orders) so it reads from above and below.
        private void RebuildMesh(float arcDegrees)
        {
            _builtArc = arcDegrees;
            int seg = Mathf.Clamp(segments, 6, 96);
            Vector3[] verts = BuildFanVertices(arcDegrees, range, seg);

            var tris = new int[seg * 6];
            for (int i = 0; i < seg; i++)
            {
                // top face
                tris[i * 6 + 0] = 0;
                tris[i * 6 + 1] = i + 1;
                tris[i * 6 + 2] = i + 2;
                // bottom face (reversed winding)
                tris[i * 6 + 3] = 0;
                tris[i * 6 + 4] = i + 2;
                tris[i * 6 + 5] = i + 1;
            }

            var mesh = new Mesh { name = "GuardWedge" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            var mf = _visual.GetComponent<MeshFilter>();
            if (mf != null)
            {
                if (mf.sharedMesh != null)
                {
                    Destroy(mf.sharedMesh);
                }
                mf.sharedMesh = mesh;
            }
        }

        private void LateUpdate()
        {
            if (_renderer == null || guard == null)
            {
                return;
            }
            if (!Mathf.Approximately(_builtArc, guard.GuardArcDegrees))
            {
                RebuildMesh(guard.GuardArcDegrees); // arc tuned in the Inspector at runtime
            }

            PlayerGuard.DefenseState state = guard.CurrentDefense;
            _renderer.enabled = state != PlayerGuard.DefenseState.None;
            if (_renderer.enabled && _renderer.sharedMaterial != null)
            {
                _renderer.sharedMaterial.color = state == PlayerGuard.DefenseState.Parry ? parryColor : blockColor;
            }
        }

        private void OnDestroy()
        {
            if (_visual != null)
            {
                Destroy(_visual.gameObject);
            }
        }

        public void EditorConfigure(PlayerGuard playerGuard, Color color)
        {
            guard = playerGuard;
            blockColor = color;
        }
    }
}
