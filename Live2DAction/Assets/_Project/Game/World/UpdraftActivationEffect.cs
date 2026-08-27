using System.Collections;
using UnityEngine;

namespace Live2DAction.World
{
    // 2026-08-22, explicit user request ("當這個機關被啟動時 上升氣流渲染成鮮紅色 並且由下而上逐漸渲染
    // 遊戲結束後恢復正常") - purely a visual "the course is now live" tell on Updraft_MainArea's own
    // wind column + particles, driven by polling TimeTrialController.IsRunning the same way
    // TimeTrialStartMechanism already does (that script has no begin/end C# events to subscribe to,
    // so Update()-polling a public bool is this project's existing convention here, not a new one).
    //
    // Reads LightPillarURP's own _ActiveColor/_ActiveBlend/_FillHeight01 properties (added alongside
    // this script) - both default to their no-op values (blend 0, fill 1) so LightPillar.mat's
    // portal beam, which shares the same shader, is completely unaffected.
    public class UpdraftActivationEffect : MonoBehaviour
    {
        [SerializeField] private TimeTrialController controller;
        [SerializeField] private MeshRenderer windColumnRenderer;
        [SerializeField] private ParticleSystem windWisps;

        // 2026-08-22 - how long the red tint takes to sweep from the base of the column to the top
        // once the challenge starts. Deliberately much shorter than challengeDurationSeconds - this
        // is a one-shot "you're live now" flourish, not something meant to still be climbing partway
        // through the run.
        [SerializeField] private float fillSweepSeconds = 1.4f;

        // 2026-08-22 - how fast the red tint itself fades in/out, independent of the fill sweep
        // above (blend reaches 1 well before the sweep finishes revealing the top of the column, so
        // the whole column already reads as red while the sweep is still playing).
        [SerializeField] private float tintFadeSeconds = 0.35f;

        private static readonly int ActiveBlendId = Shader.PropertyToID("_ActiveBlend");
        private static readonly int FillHeight01Id = Shader.PropertyToID("_FillHeight01");

        private Material _columnMaterialInstance;
        private Coroutine _transitionCoroutine;
        private bool _wasRunning;

        private Color _normalStartColor;
        private ParticleSystem.MinMaxGradient _normalColorOverLifetime;
        private static readonly Color ActiveParticleColor = new Color(1f, 0.15f, 0.1f, 0.7f);

        private void Awake()
        {
            if (windColumnRenderer != null)
            {
                // .material (not .sharedMaterial) instances the material on first access - required
                // so tweaking _ActiveBlend/_FillHeight01 here can't bleed into UpdraftWind.mat itself
                // or any other renderer sharing it.
                _columnMaterialInstance = windColumnRenderer.material;
            }

            if (windWisps != null)
            {
                ParticleSystem.MainModule main = windWisps.main;
                _normalStartColor = main.startColor.color;

                ParticleSystem.ColorOverLifetimeModule colorOverLifetime = windWisps.colorOverLifetime;
                _normalColorOverLifetime = colorOverLifetime.color;
            }
        }

        private void Update()
        {
            bool isRunning = controller != null && controller.IsRunning;
            if (isRunning == _wasRunning)
            {
                return;
            }

            _wasRunning = isRunning;

            if (_transitionCoroutine != null)
            {
                StopCoroutine(_transitionCoroutine);
            }
            _transitionCoroutine = StartCoroutine(isRunning ? ActivateCoroutine() : DeactivateCoroutine());
        }

        private IEnumerator ActivateCoroutine()
        {
            SetParticleColor(ActiveParticleColor);

            float blendStart = _columnMaterialInstance != null ? _columnMaterialInstance.GetFloat(ActiveBlendId) : 0f;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.0001f, tintFadeSeconds);
                SetColumnBlend(Mathf.Lerp(blendStart, 1f, Mathf.Clamp01(t)));
                yield return null;
            }

            // Fill sweep starts from the base regardless of how far tintFadeSeconds already got
            // through the blend fade above - the two run concurrently, not sequentially.
            float sweepT = 0f;
            while (sweepT < 1f)
            {
                sweepT += Time.deltaTime / Mathf.Max(0.0001f, fillSweepSeconds);
                SetColumnFillHeight(Mathf.Clamp01(sweepT));
                yield return null;
            }

            _transitionCoroutine = null;
        }

        private IEnumerator DeactivateCoroutine()
        {
            SetColumnFillHeight(1f); // fully revealed again - only the activation sweep hides the top, not the resting state

            float blendStart = _columnMaterialInstance != null ? _columnMaterialInstance.GetFloat(ActiveBlendId) : 1f;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.0001f, tintFadeSeconds);
                SetColumnBlend(Mathf.Lerp(blendStart, 0f, Mathf.Clamp01(t)));
                yield return null;
            }

            SetParticleColor(_normalStartColor, _normalColorOverLifetime);
            _transitionCoroutine = null;
        }

        private void SetColumnBlend(float value)
        {
            if (_columnMaterialInstance != null)
            {
                _columnMaterialInstance.SetFloat(ActiveBlendId, value);
            }
        }

        private void SetColumnFillHeight(float value)
        {
            if (_columnMaterialInstance != null)
            {
                _columnMaterialInstance.SetFloat(FillHeight01Id, value);
            }
        }

        private void SetParticleColor(Color flatColor)
        {
            if (windWisps == null)
            {
                return;
            }

            ParticleSystem.MainModule main = windWisps.main;
            main.startColor = flatColor;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = windWisps.colorOverLifetime;
            colorOverLifetime.color = flatColor;
        }

        private void SetParticleColor(Color flatColor, ParticleSystem.MinMaxGradient gradient)
        {
            if (windWisps == null)
            {
                return;
            }

            ParticleSystem.MainModule main = windWisps.main;
            main.startColor = flatColor;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = windWisps.colorOverLifetime;
            colorOverLifetime.color = gradient;
        }
    }
}
