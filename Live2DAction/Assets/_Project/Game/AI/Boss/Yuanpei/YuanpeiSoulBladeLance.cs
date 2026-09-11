using UnityEngine;
using UnityEngine.Rendering;

namespace Live2DAction.AI.Boss.Yuanpei
{
    // 續191 (user: 魂刃劍氣「不要做成子彈型，而是做成一次性爆發，將特效不斷延伸拉長然後遠距離攻擊到玩家，
    // 相當於我把長矛伸縮自如站在原地攻擊」). Pure VISUAL for SoulBladeQi - the hit is done in
    // YuanpeiAttacks.SoulBladeQi (a per-frame RayHitsPlayer line check).
    //
    // Reads as a TELESCOPING SPEAR of sword-qi shot from a standing boss:
    //   * anchored at the boss's muzzle (`anchor`, read every frame so it tracks a drifting boss)
    //   * the whole thing extends from 0 -> `reach` along the locked lane direction (ease-out)
    //   * SHAFT = a thin bright additive streak from the muzzle up to just behind the head
    //   * HEAD  = the SoulBladeQi_Atlas flipbook card at a FIXED length riding the leading tip (NOT
    //     stretched - so the jagged blade art stays undistorted), billboarded around the lane axis.
    //     Frames advance form->blade while extending, then burst frames over the hold window so the
    //     head detonates where it reaches the player.
    // Flipbook without a ParticleSystem: SlashFlipbookURP applies TRANSFORM_TEX, so setting the
    // material's _MainTex Tiling(1/cols,1/rows)/Offset(tile) per frame on a plain MeshRenderer works.
    // Self-destroys after grow + hold + a short fade.
    [DisallowMultipleComponent]
    public class YuanpeiSoulBladeLance : MonoBehaviour
    {
        private Transform _anchor;
        private Vector3 _dir;
        private float _reach, _width, _extendSeconds, _holdSeconds, _life, _t, _headLen;
        private int _cols, _rows, _frames, _bladeEndFrame;
        private Material _mat;
        private Transform _head;
        private Transform _shaft;
        private Renderer _shaftRend;
        private Color _shaftColor;
        private Light _light;

        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        public void Configure(Material src, int cols, int rows, int frames, Transform anchor, Vector3 dir,
            float reach, float width, float extendSeconds, float holdSeconds, Color fallbackColor)
        {
            _anchor = anchor;
            _dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward;
            _reach = Mathf.Max(0.5f, reach);
            _width = Mathf.Max(0.1f, width);
            _extendSeconds = Mathf.Max(0.05f, extendSeconds);
            _holdSeconds = Mathf.Max(0.05f, holdSeconds);
            _cols = Mathf.Max(1, cols);
            _rows = Mathf.Max(1, rows);
            _frames = Mathf.Max(1, frames);
            _bladeEndFrame = Mathf.Clamp(Mathf.RoundToInt(_frames * 0.58f), 1, _frames - 1);
            _life = _extendSeconds + _holdSeconds + 0.3f;
            // blade head keeps the atlas cell's 160:90 aspect; scaled off the width, clamped so it
            // never swallows a short-range lance or looks tiny on a long one.
            _headLen = Mathf.Clamp(_width * (160f / 90f) * 1.15f, 2.6f, 5.0f);

            // ---- blade head (flipbook card) ----
            var headGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            headGo.name = "Head";
            Destroy(headGo.GetComponent<Collider>());
            _head = headGo.transform;
            _head.SetParent(transform, false);
            // Unity Quad spans local +X/+Y, normal -Z. Euler(-90,0,0): local +Y -> parent +Z (lane
            // length), local +X stays parent +X (width), normal -Z -> parent +Y (kept camera-facing).
            _head.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var headRend = headGo.GetComponent<MeshRenderer>();
            headRend.shadowCastingMode = ShadowCastingMode.Off;
            headRend.receiveShadows = false;
            headRend.lightProbeUsage = LightProbeUsage.Off;
            headRend.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _mat = src != null
                ? new Material(src)
                : new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color")) { color = fallbackColor };
            _mat.mainTextureScale = new Vector2(1f / _cols, 1f / _rows);
            headRend.material = _mat;
            SetFrame(0);

            // ---- shaft (thin bright additive streak) ----
            _shaftColor = Color.Lerp(fallbackColor, Color.white, 0.35f);
            var shaftGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            shaftGo.name = "Shaft";
            Destroy(shaftGo.GetComponent<Collider>());
            _shaft = shaftGo.transform;
            _shaft.SetParent(transform, false);
            _shaft.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            _shaftRend = shaftGo.GetComponent<MeshRenderer>();
            _shaftRend.shadowCastingMode = ShadowCastingMode.Off;
            _shaftRend.receiveShadows = false;
            var shaftShader = Shader.Find("Live2DAction/VFX/AdditiveUnlit")
                              ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var shaftMat = new Material(shaftShader);
            ApplyShaftColor(shaftMat, _shaftColor);
            _shaftRend.material = shaftMat;

            // light on its OWN child - putting it on this GameObject and then moving _light.transform
            // would move the whole lance root (it IS transform), detaching the head/shaft from the muzzle.
            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(transform, false);
            _light = lightGo.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = fallbackColor;
            _light.intensity = 3.5f;
            _light.range = 6f;
            _light.shadows = LightShadows.None;
        }

        private static void ApplyShaftColor(Material m, Color c)
        {
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c);
            if (m.HasProperty(EmissionColorId)) m.SetColor(EmissionColorId, c * 2.5f);
            m.color = c;
        }

        private void SetFrame(int fi)
        {
            fi = Mathf.Clamp(fi, 0, _frames - 1);
            int col = fi % _cols;
            int row = fi / _cols;
            // atlas row 0 is the TOP row; texture V origin is the bottom -> flip vertically
            _mat.mainTextureOffset = new Vector2(col / (float)_cols, 1f - (row + 1) / (float)_rows);
        }

        private void LateUpdate()
        {
            _t += Time.deltaTime;
            if (_anchor == null || _t >= _life) { Destroy(gameObject); return; }

            Vector3 origin = _anchor.position;

            float k = Mathf.Clamp01(_t / _extendSeconds);
            float ext = 1f - (1f - k) * (1f - k);               // ease-out
            float len = Mathf.Max(0.05f, _reach * ext);

            // flipbook frame: 0..bladeEnd over the grow window, bladeEnd..last over the hold window
            int fi;
            if (_t <= _extendSeconds)
                fi = Mathf.RoundToInt(Mathf.Lerp(0f, _bladeEndFrame, k));
            else
            {
                float h = Mathf.Clamp01((_t - _extendSeconds) / _holdSeconds);
                fi = Mathf.RoundToInt(Mathf.Lerp(_bladeEndFrame, _frames - 1, h));
            }
            SetFrame(fi);

            // billboard around the lane axis: normal faces the camera, long axis stays on _dir
            Vector3 head = origin + _dir * (len * 0.5f);
            var cam = Camera.main;
            Vector3 toCam = (cam != null ? cam.transform.position : head + Vector3.up * 5f) - head;
            Vector3 sideAxis = Vector3.Cross(_dir, toCam);
            if (sideAxis.sqrMagnitude < 1e-5f) sideAxis = Vector3.Cross(_dir, Vector3.up);
            if (sideAxis.sqrMagnitude < 1e-5f) sideAxis = Vector3.right;
            Vector3 up = Vector3.Cross(sideAxis.normalized, _dir).normalized;

            transform.position = origin;
            transform.rotation = Quaternion.LookRotation(_dir, up);

            // blade head: fixed length, rides the leading tip (grows in from ~0 over the first ~25%)
            float headLen = Mathf.Min(_headLen, Mathf.Max(0.6f, len)) * Mathf.Clamp01(_t / (_extendSeconds * 0.35f + 0.001f));
            headLen = Mathf.Max(0.6f, headLen);
            float headCenterZ = Mathf.Max(headLen * 0.5f, len - headLen * 0.45f);
            _head.localScale = new Vector3(_width, headLen, 1f);
            _head.localPosition = new Vector3(0f, 0f, headCenterZ);

            // shaft: muzzle -> just under the head
            float shaftLen = Mathf.Max(0.05f, headCenterZ - headLen * 0.15f);
            float shaftW = _width * (_t <= _extendSeconds ? 0.14f : 0.06f);
            _shaft.localScale = new Vector3(shaftW, shaftLen, 1f);
            _shaft.localPosition = new Vector3(0f, 0f, shaftLen * 0.5f);

            if (_light != null) _light.transform.localPosition = new Vector3(0f, 0f, Mathf.Min(len, headCenterZ));

            // fade the last 0.3s
            float fade = _t > _life - 0.3f ? Mathf.InverseLerp(_life, _life - 0.3f, _t) : 1f;
            if (_mat.HasProperty(OpacityId)) _mat.SetFloat(OpacityId, fade);
            if (_shaftRend != null)
            {
                float shaftFade = fade * (_t <= _extendSeconds ? 1f : 0.3f);
                ApplyShaftColor(_shaftRend.material, _shaftColor * shaftFade);
            }
            if (_light != null) _light.intensity = 3.5f * fade;
        }

        private void OnDestroy()
        {
            if (_mat != null) Destroy(_mat);
            if (_shaftRend != null && _shaftRend.material != null) Destroy(_shaftRend.material);
        }
    }
}
