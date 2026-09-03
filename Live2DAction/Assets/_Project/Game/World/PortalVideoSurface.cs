using UnityEngine;

namespace Live2DAction.World
{
    // 2026-09-03 - the SceneGate portal visual. Originally a looping mp4 via VideoPlayer, but
    // that repeatedly failed to render on the enter gate (created at scene-0 load); after ~9
    // iterations, dropped for a PROCEDURAL swirl (Live2DAction/PortalVortexURP - same shader the
    // sky-island portal pads use, self-animating in the fragment shader, no VideoPlayer, no
    // RenderTexture, no timing to get wrong). Always renders. The mp4 (PortalVortexVideo.mp4) is
    // parked in Assets/_Project/VFX/Gate/ for a future proper VFX pass.
    //
    // Just applies a per-instance copy of the portal material to this object's quad + billboards
    // it toward the camera + a gentle scale pulse.
    [RequireComponent(typeof(MeshRenderer))]
    public class PortalVideoSurface : MonoBehaviour
    {
        [Tooltip("Portal material asset (Live2DAction/PortalVortexURP). Instanced per gate.")]
        [SerializeField] private Material materialTemplate;
        [Tooltip("Legacy field - the parked mp4. Unused.")]
        [SerializeField] private Object clip;
        [SerializeField] private Color tint = Color.white;
        [SerializeField] private bool billboard = true;
        [SerializeField] private float pulseAmount = 0.035f;
        [SerializeField] private float pulseSpeed = 0.45f;

        private Material _mat;
        private Vector3 _baseScale;
        private float _pulsePhase;

        private void Awake()
        {
            _baseScale = transform.localScale;
            _pulsePhase = Random.value * 10f;

            var mr = GetComponent<MeshRenderer>();
            Shader sh = Shader.Find("Live2DAction/PortalVortexURP");
            _mat = materialTemplate != null ? new Material(materialTemplate)
                 : sh != null ? new Material(sh)
                 : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            _mat.name = "GatePortal (instance)";
            ApplyTint();
            mr.sharedMaterial = _mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        private void ApplyTint()
        {
            if (_mat == null) return;
            // PortalVortexURP palette -> red/orange fire portal
            if (_mat.HasProperty("_ColorA")) _mat.SetColor("_ColorA", new Color(0.9f, 0.12f, 0.06f) * tint);
            if (_mat.HasProperty("_ColorB")) _mat.SetColor("_ColorB", new Color(1f, 0.5f, 0.12f) * tint);
            if (_mat.HasProperty("_RimColor")) _mat.SetColor("_RimColor", new Color(1f, 0.75f, 0.4f) * tint);
        }

        private void LateUpdate()
        {
            if (billboard)
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    Vector3 flat = transform.position - cam.transform.position;
                    flat.y = 0f;
                    if (flat.sqrMagnitude > 0.0001f)
                        transform.rotation = Quaternion.LookRotation(flat, Vector3.up);
                }
            }

            if (pulseAmount > 0.0001f)
            {
                float k = 1f + Mathf.Sin((Time.time + _pulsePhase) * pulseSpeed * Mathf.PI * 2f) * pulseAmount;
                transform.localScale = new Vector3(_baseScale.x * k, _baseScale.y * k, _baseScale.z);
            }
        }

        private void OnDestroy()
        {
            if (_mat != null) Destroy(_mat);
        }
    }
}
