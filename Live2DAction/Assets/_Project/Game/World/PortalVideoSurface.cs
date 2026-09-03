using System.Collections;
using UnityEngine;
using UnityEngine.Video;
using Live2DAction.Input;

namespace Live2DAction.World
{
    // 2026-09-03 - the SceneGate portal visual, driven by a looping mp4 (PortalVortexVideo.mp4).
    //
    // HISTORY (~11 iterations):
    //  - 續 91: serialized VideoPlayer + RenderTexture mode + playOnAwake. EXIT gate worked (scene
    //    loads additively mid-game); ENTER gate stayed invisible - the scene-0 VideoPlayer never
    //    started, so its RenderTexture stayed blank -> keyed shader drew nothing.
    //  - 續 92: VideoRenderMode.APIOnly dodged the RenderTexture but its vp.texture read back with
    //    the RED channel dropped on this D3D11 box -> a flat teal rectangle. Reverted.
    //  - 續 93: RenderTexture mode + a coroutine that does Prepare() -> wait isPrepared -> Play().
    //    The coroutine (not playOnAwake) is what reliably starts the scene-0 gate.
    //  - 續 94 (this): proximity-gated. The portal is a "materialises out of thin air" effect, so
    //    it only plays while a player is near the gate (user request). Prepared at load but not
    //    played; MeshRenderer off; walking within activateRange starts it from frame 0 with a
    //    short intensity fade-in; leaving (or teleporting through) past deactivateRange cuts it.
    //
    // The scene provides: this component + a VideoPlayer (clip assigned, renderMode RenderTexture,
    // targetTexture = the per-gate RT asset) + a MeshRenderer whose material (Live2DAction/
    // PortalVideoURP) samples that same RT.
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(VideoPlayer))]
    public class PortalVideoSurface : MonoBehaviour
    {
        [Header("Proximity - portal materialises when a player is near")]
        [Tooltip("Off = always playing (the 續 91-93 behaviour).")]
        [SerializeField] private bool proximityActivated = true;
        [Tooltip("Player within this many metres (horizontal) -> portal appears.")]
        [SerializeField] private float activateRange = 32f;
        [Tooltip("Player beyond this -> portal gone. > activateRange for hysteresis.")]
        [SerializeField] private float deactivateRange = 40f;
        [SerializeField] private float appearFadeSeconds = 0.45f;

        [Header("Look")]
        [Tooltip("Rotate the quad (yaw only, stays upright) to face the camera - gives a flat " +
                 "portal video a fake-3D read. Use for videos that already contain the whole gate.")]
        [SerializeField] private bool billboard = false;
        [SerializeField] private float pulseAmount = 0.03f;
        [SerializeField] private float pulseSpeed = 0.4f;
        [SerializeField] private bool verbose = true;

        private VideoPlayer _vp;
        private MeshRenderer _mr;
        private MaterialPropertyBlock _mpb;
        private Vector3 _baseScale;
        private float _pulsePhase;

        private bool _active;
        private float _appearT;
        private Transform[] _playerRoots;
        private float _nextScan;

        private string GateName => transform.parent != null ? transform.parent.name : name;

        private void Awake()
        {
            _vp = GetComponent<VideoPlayer>();
            _mr = GetComponent<MeshRenderer>();
            _mpb = new MaterialPropertyBlock();
            _baseScale = transform.localScale;
            _pulsePhase = Random.value * 10f;

            _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _mr.receiveShadows = false;

            _vp.renderMode = VideoRenderMode.RenderTexture;
            _vp.playOnAwake = false;            // the coroutine starts it - scene-0 playOnAwake is unreliable
            _vp.isLooping = true;
            _vp.skipOnDrop = true;
            _vp.audioOutputMode = VideoAudioOutputMode.None;
            _vp.source = VideoSource.VideoClip;

            if (proximityActivated) _mr.enabled = false;
        }

        private void OnEnable()
        {
            StopAllCoroutines();
            StartCoroutine(DriveVideo());
        }

        private IEnumerator DriveVideo()
        {
            yield return null;
            yield return null;

            if (_vp.clip == null)
            {
                if (verbose) Debug.LogWarning("[PortalVideoSurface] " + GateName + ": no clip assigned.", this);
                yield break;
            }

            // get it ready but don't play yet - avoids a prepare hitch on first approach
            _vp.Prepare();
            float t = 0f;
            while (!_vp.isPrepared && t < 8f) { t += Time.unscaledDeltaTime; yield return null; }
            if (verbose)
                Debug.Log("[PortalVideoSurface] " + GateName + ": prepared=" + _vp.isPrepared +
                          " after " + t.ToString("F1") + "s (frames=" + _vp.frameCount + ")", this);

            if (!proximityActivated) SetActive(true);

            var wait = new WaitForSecondsRealtime(0.4f);
            while (enabled)
            {
                if (_active && !_vp.isPlaying)
                {
                    if (_vp.isPrepared) _vp.Play();
                    else _vp.Prepare();
                }
                yield return wait;
            }
        }

        private void Update()
        {
            if (proximityActivated)
            {
                Transform p = NearestPlayer();
                if (p != null)
                {
                    Vector3 a = transform.position; a.y = 0f;
                    Vector3 b = p.position; b.y = 0f;
                    float d = Vector3.Distance(a, b);
                    if (!_active && d <= activateRange) SetActive(true);
                    else if (_active && d >= deactivateRange) SetActive(false);
                }
            }

            // fade-in while materialising (drives _PortalFade on both portal shaders -
            // additive PortalVideoURP and alpha PortalVideoAlphaURP)
            if (_active && _mr.enabled)
            {
                _appearT += Time.deltaTime;
                float k = appearFadeSeconds > 0.001f ? Mathf.Clamp01(_appearT / appearFadeSeconds) : 1f;
                k = k * k * (3f - 2f * k);
                _mr.GetPropertyBlock(_mpb);
                _mpb.SetFloat("_PortalFade", k);
                _mr.SetPropertyBlock(_mpb);
            }
        }

        private void SetActive(bool on)
        {
            if (_active == on) return;
            _active = on;
            _mr.enabled = on;

            if (on)
            {
                _appearT = 0f;
                if (_vp.isPrepared) _vp.frame = 0;
                _vp.Play();
                if (verbose) Debug.Log("[PortalVideoSurface] " + GateName + ": materialise (player near)", this);
            }
            else
            {
                _vp.Pause();
                if (_vp.targetTexture != null)
                {
                    var prev = RenderTexture.active;
                    RenderTexture.active = _vp.targetTexture;
                    GL.Clear(true, true, Color.black);
                    RenderTexture.active = prev;
                }
                if (verbose) Debug.Log("[PortalVideoSurface] " + GateName + ": hide (player left)", this);
            }
        }

        private Transform NearestPlayer()
        {
            if (_playerRoots == null || Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + 1f;
                var pips = FindObjectsByType<PlayerInputProvider>(FindObjectsSortMode.None);
                _playerRoots = new Transform[pips.Length];
                for (int i = 0; i < pips.Length; i++) _playerRoots[i] = pips[i].transform.root;
            }

            Transform best = null;
            float bd = float.MaxValue;
            Vector3 c = transform.position; c.y = 0f;
            for (int i = 0; i < _playerRoots.Length; i++)
            {
                var r = _playerRoots[i];
                if (r == null) continue;
                Vector3 q = r.position; q.y = 0f;
                float d = (q - c).sqrMagnitude;
                if (d < bd) { bd = d; best = r; }
            }
            return best;
        }

        private void LateUpdate()
        {
            if (billboard && _mr.enabled)
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    Vector3 flat = cam.transform.position - transform.position;
                    flat.y = 0f;
                    if (flat.sqrMagnitude > 0.01f)
                    {
                        Quaternion want = Quaternion.LookRotation(flat.normalized, Vector3.up);
                        transform.rotation = Quaternion.Slerp(transform.rotation, want,
                            1f - Mathf.Exp(-9f * Time.deltaTime));
                    }
                }
            }

            if (pulseAmount > 0.0001f && _mr.enabled)
            {
                float k = 1f + Mathf.Sin((Time.time + _pulsePhase) * pulseSpeed * Mathf.PI * 2f) * pulseAmount;
                transform.localScale = new Vector3(_baseScale.x * k, _baseScale.y * k, _baseScale.z);
            }
        }
    }
}
