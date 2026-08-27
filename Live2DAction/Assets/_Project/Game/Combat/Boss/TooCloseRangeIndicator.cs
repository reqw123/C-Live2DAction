using UnityEngine;
using Live2DAction.AI.Boss;

namespace Live2DAction.Combat.Boss
{
    // 2026-08-26, explicit user request ("把具體踢的範圍畫出來讓我排錯") - a ground ring at
    // BossStateMachine.EffectiveTooCloseDistance, visible in the actual Game View during real
    // play (not a Scene-View-only Gizmo - useless for "am I actually standing inside it right
    // now" while you're the one holding WASD). Doubles as a direct visual answer to the earlier,
    // still-open "實測並沒有觸發踢擊" report: the ring's own color sweeps from safeColor to
    // dangerColor as BossStateMachine.TooCloseProgress01 climbs toward 1 - if you stand inside the
    // ring and it visibly resets to safeColor before ever reaching dangerColor, the timer is being
    // interrupted (stepped out, or something else is resetting it); if it reaches full dangerColor
    // and the kick still doesn't land, the problem is in hit detection, not this trigger.
    [RequireComponent(typeof(BossStateMachine))]
    public class TooCloseRangeIndicator : MonoBehaviour
    {
        [SerializeField] private BossStateMachine boss;
        [SerializeField, Range(8, 128)] private int segments = 64;
        [SerializeField] private float lineWidth = 0.15f;
        [SerializeField] private float groundYOffset = 0.05f;
        [SerializeField] private Color safeColor = new Color(0.2f, 1f, 0.3f, 1f);
        [SerializeField] private Color dangerColor = new Color(1f, 0.1f, 0.1f, 1f);

        private LineRenderer _line;

        private void Reset()
        {
            boss = GetComponent<BossStateMachine>();
        }

        private void Awake()
        {
            if (boss == null) boss = GetComponent<BossStateMachine>();

            _line = gameObject.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.loop = true;
            _line.positionCount = segments;
            _line.startWidth = lineWidth;
            _line.endWidth = lineWidth;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            // Same URP-Unlit-material approach as BossHitboxVisualizer - reliable regardless of
            // scene lighting, no asset dependency.
            _line.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        }

        private void Update()
        {
            if (boss == null || _line == null) return;

            float radius = boss.EffectiveTooCloseDistance;
            Vector3 center = transform.position + Vector3.up * groundYOffset;
            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                Vector3 point = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                _line.SetPosition(i, point);
            }

            // 2026-08-26, real playtested bug ("有被踢擊但是圓圈沒變紅色") - LineRenderer.startColor/
            // endColor only ever take effect if the material's shader reads per-vertex color, which
            // "Universal Render Pipeline/Unlit" does NOT do by default - it renders flat using its
            // own _BaseColor uniform regardless of what startColor/endColor are set to. The ring's
            // POSITION/radius was correct (confirmed - the trigger itself was firing fine per the
            // Console log), only the color feedback was silently a no-op. Setting material.color
            // directly writes _BaseColor, which the shader actually uses.
            Color c = Color.Lerp(safeColor, dangerColor, boss.TooCloseProgress01);
            _line.startColor = c;
            _line.endColor = c;
            _line.material.color = c;
        }
    }
}
