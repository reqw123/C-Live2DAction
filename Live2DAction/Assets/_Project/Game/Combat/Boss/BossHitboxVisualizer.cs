using UnityEngine;

namespace Live2DAction.Combat.Boss
{
    // 2026-08-26, explicit user request ("能否在武士刀受擊區域增加特效覆蓋 讓我知道具體碰到刀的哪個
    // 區塊會受傷") - purely diagnostic: renders a bright, exactly-collider-shaped mesh over a
    // BossHitbox that's only visible while BossHitbox.IsActive is true (i.e. while its
    // Collider.enabled is actually on, matching real hit-window timing exactly - not an
    // approximation of when the window is "supposed" to be open). Answers "which part of the
    // blade/foot counts as the hit region, and exactly when" directly in the Game View during
    // real play, which is what this session's still-open "劈砍動作是否真的命中" investigation
    // actually needs - a Scene-View-only Gizmo wouldn't help while actually playing/dodging.
    //
    // Deliberately opaque, not alpha-blended - a URP transparent material needs several surface-
    // type keywords/render-queue settings to actually blend correctly, fragile to get right from a
    // freshly-generated Material in script; a solid bright color is just as legible for "where is
    // the hit region" and has zero chance of silently rendering wrong.
    [RequireComponent(typeof(BossHitbox))]
    [RequireComponent(typeof(Collider))]
    public class BossHitboxVisualizer : MonoBehaviour
    {
        [SerializeField] private Color activeColor = new Color(1f, 0.1f, 0.1f, 1f);

        private BossHitbox _hitbox;
        private MeshRenderer _visualRenderer;

        private void Awake()
        {
            _hitbox = GetComponent<BossHitbox>();
            BuildVisual();
        }

        private void BuildVisual()
        {
            Collider col = GetComponent<Collider>();
            PrimitiveType primitiveType;
            Vector3 localPosition;
            Vector3 localScale;
            Quaternion localRotation = Quaternion.identity;

            if (col is BoxCollider box)
            {
                primitiveType = PrimitiveType.Cube;
                localPosition = box.center;
                localScale = box.size;
            }
            else if (col is SphereCollider sphere)
            {
                primitiveType = PrimitiveType.Sphere;
                localPosition = sphere.center;
                localScale = Vector3.one * (sphere.radius * 2f);
            }
            else if (col is CapsuleCollider capsule)
            {
                // Unity's built-in capsule primitive is height=2/radius=0.5 along its own local Y -
                // scale to match, then rotate so that local Y lines up with the collider's own
                // configured axis (direction: 0=X, 1=Y, 2=Z).
                primitiveType = PrimitiveType.Capsule;
                localPosition = capsule.center;
                localScale = new Vector3(capsule.radius * 2f, capsule.height / 2f, capsule.radius * 2f);
                if (capsule.direction == 0) localRotation = Quaternion.Euler(0f, 0f, 90f);
                else if (capsule.direction == 2) localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            else
            {
                // No supported shape (e.g. MeshCollider) - nothing to draw, but don't error out;
                // the underlying BossHitbox itself is unaffected either way.
                return;
            }

            GameObject visualGo = GameObject.CreatePrimitive(primitiveType);
            visualGo.name = gameObject.name + "_HitVisual";
            visualGo.transform.SetParent(transform, false);
            visualGo.transform.localPosition = localPosition;
            visualGo.transform.localRotation = localRotation;
            visualGo.transform.localScale = localScale;

            // Purely visual - the primitive's own auto-added Collider would otherwise create a
            // second, untriggered, non-kinematic physical shape stacked on the real BossHitbox
            // trigger for no reason.
            Collider visualCollider = visualGo.GetComponent<Collider>();
            if (visualCollider != null) Destroy(visualCollider);

            _visualRenderer = visualGo.GetComponent<MeshRenderer>();
            Material material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.color = activeColor;
            _visualRenderer.sharedMaterial = material;
            _visualRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _visualRenderer.receiveShadows = false;
            _visualRenderer.enabled = false; // starts hidden - Update below shows it only while the window is actually live
        }

        private void Update()
        {
            if (_visualRenderer != null) _visualRenderer.enabled = _hitbox.IsActive;
        }
    }
}
