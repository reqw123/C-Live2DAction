using UnityEngine;
using Live2DAction.Core;

namespace Live2DAction.UI
{
    // 2026-08-16, explicit user follow-up to the energy bar pulse ("不夠炫 加個閃電繞圈的特效") -
    // visible only while UltimateEnergy.IsFull. Player-only (not wired to Enemy the way
    // HealthRegeneration/the energy bar itself are) - Enemy has no UltimateAbility and
    // EnemyAI never presses the ultimate key (see EnemyAI.UltimatePressed's own comment), so a
    // circling "ready" aura on an enemy that can never act on it would read as a UI bug, not a
    // hint.
    //
    // 2026-08-16 rewrite, explicit user request ("閃電改為只有一條，從角色底部任意往上環繞，循環，
    // 就像是動漫獵人x獵人的奇犽一樣") - was N separate bolts standing in a static ring; now a
    // single bolt that spirals up from the feet, grows, holds, fades, and loops (see
    // LightningAuraUtility's own comment for the exact grow/hold/fade timing).
    public class UltimateReadyAura : MonoBehaviour
    {
        [SerializeField] private UltimateEnergy energy;
        [SerializeField] private LineRenderer bolt;
        [SerializeField] private float radius = 0.55f;
        [SerializeField] private float baseHeight = 0.05f;
        // 2026-08-16 correction, real feedback ("這個閃電太高了 往下一點從角色腳底繞道頭上") -
        // 1.3 was calibrated against a bad measurement (Player's Visual renderer bounds
        // included the WolfsGravestone weapon, which can be scaled 5x during the ultimate
        // buff, badly inflating the reading). Re-measured with the weapon explicitly excluded:
        // the character's actual head sits at local Y ~0.83 above Player's own transform, not
        // ~1.6-1.7. 0.78 (+ baseHeight 0.05 = top at 0.83) now lands the spiral's top right at
        // the head instead of well above it.
        [SerializeField] private float climbHeight = 0.78f;
        [SerializeField] private float spiralTurns = 2.5f;
        [SerializeField] private float loopDurationSeconds = 1.2f;
        [SerializeField] private float growFraction = 0.5f;
        [SerializeField] private float fadeStart01 = 0.8f;
        [SerializeField] private float crackleIntervalSeconds = 0.06f;
        [SerializeField] private float jitterAmount = 0.05f;
        [SerializeField] private int segmentCount = 24;

        private float _elapsedInLoop;
        private float _crackleTimer;
        private float _previousLoopProgress;
        private Vector2[] _jitterOffsets;
        private Color _baseColor;
        private bool _baseColorCaptured;
        private readonly System.Random _random = new System.Random();

        private void Update()
        {
            if (energy == null || bolt == null)
            {
                return;
            }

            if (!energy.IsFull)
            {
                if (bolt.gameObject.activeSelf)
                {
                    bolt.gameObject.SetActive(false);
                }

                return;
            }

            if (!bolt.gameObject.activeSelf)
            {
                bolt.gameObject.SetActive(true);
            }

            if (!_baseColorCaptured)
            {
                _baseColor = bolt.startColor;
                _baseColorCaptured = true;
            }

            _elapsedInLoop += Time.deltaTime;
            float loopProgress = LightningAuraUtility.ComputeLoopProgress(_elapsedInLoop, loopDurationSeconds);

            // Detect a wrap (progress dropped since last frame) to re-roll the jitter shape for
            // the new cycle - gives loop-to-loop variety ("任意", arbitrary path) instead of the
            // exact same crackle shape repeating forever.
            bool wrapped = loopProgress < _previousLoopProgress;
            _previousLoopProgress = loopProgress;

            _crackleTimer += Time.deltaTime;
            bool shouldCrackle = _crackleTimer >= crackleIntervalSeconds;
            if (shouldCrackle)
            {
                _crackleTimer = 0f;
            }

            if (_jitterOffsets == null || wrapped || shouldCrackle)
            {
                _jitterOffsets = LightningAuraUtility.ComputeJitterOffsets(segmentCount, jitterAmount, _random);
            }

            float growthAmount = LightningAuraUtility.ComputeGrowthAmount(loopProgress, growFraction);
            float brightness = LightningAuraUtility.ComputeBrightnessMultiplier(loopProgress, fadeStart01);

            Vector3[] points = LightningAuraUtility.BuildSpiralPoints(growthAmount, baseHeight, climbHeight, radius, spiralTurns, _jitterOffsets);
            bolt.positionCount = points.Length;
            bolt.SetPositions(points);

            Color scaled = new Color(_baseColor.r * brightness, _baseColor.g * brightness, _baseColor.b * brightness, _baseColor.a);
            bolt.startColor = scaled;
            bolt.endColor = scaled;
        }
    }
}
