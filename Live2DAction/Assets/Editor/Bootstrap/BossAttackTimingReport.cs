using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Live2DAction.Combat.Boss;

namespace Live2DAction.EditorTools
{
    // 2026-09-02, spec WUSHI_COMBAT_ENGINEERING_SPEC.md §10.4 (M5 項目 9) acceptance condition:
    // "調整 state speed 後，自動重新計算並顯示實際首次接觸與有效窗毫秒數".
    //
    // Reads every Wushi_Attack_* BossAttackDefinition + the Wushi.controller state it maps to
    // (clipName -> state name), and prints each hit window's REAL first-contact second and effective
    // duration in ms at the state's current speed - plus how that compares to the player's 0.20s
    // parry window. This is the read-only measuring stick for the §10.3 tuning order (first Boss
    // state speed + telegraph, then hit-window position, and only then window length); it changes
    // nothing. Re-run it after any Animator state-speed or Wushi_Attack_*.asset window edit.
    internal static class BossAttackTimingReport
    {
        private const string ControllerPath =
            "Assets/_Project/Characters/Placeholder/Wushi/Animator/Wushi.controller";
        private const string AttackAssetFolder = "Assets/_Project/Settings/Combat/Boss";

        [MenuItem("Tools/Live2DAction/[9] 武士 Attack Timing Report")]
        public static void Report()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError($"BossAttackTimingReport: no AnimatorController at {ControllerPath}");
                return;
            }

            var states = new Dictionary<string, (float speed, float clipLen)>();
            foreach (var layer in controller.layers)
            {
                CollectStates(layer.stateMachine, states);
            }

            var sb = new StringBuilder();
            sb.AppendLine("[BossAttackTimingReport] 武士 attacks — real timing at current Animator state speeds");
            sb.AppendLine($"  (player parry window = {BossAttackTimingUtility.PlayerParryWindowSeconds * 1000f:F0}ms, spec §10 locked baseline)");
            sb.AppendLine();

            var guids = AssetDatabase.FindAssets("t:BossAttackDefinition", new[] { AttackAssetFolder });
            var defs = guids
                .Select(g => AssetDatabase.LoadAssetAtPath<BossAttackDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(d => d != null && d.name.StartsWith("Wushi_"))
                .OrderBy(d => d.name);

            foreach (var def in defs)
            {
                sb.AppendLine($"── {def.name}   (attackId \"{def.AttackId}\", clip \"{def.ClipName}\")");

                if (string.IsNullOrEmpty(def.ClipName) || !states.TryGetValue(def.ClipName, out var st))
                {
                    sb.AppendLine($"     ! no Animator state named \"{def.ClipName}\" in Wushi.controller — skipped");
                    sb.AppendLine();
                    continue;
                }

                float real = BossAttackTimingUtility.RealClipSeconds(st.clipLen, st.speed);
                sb.AppendLine($"     clip {st.clipLen:F2}s × speed {st.speed:F2}  →  real {real:F2}s" +
                              $"   startup {def.StartupSeconds:F2}s / recovery {def.RecoverySeconds:F2}s" +
                              (def.UseRootMotion ? "   [root motion]" : ""));

                var windows = def.HitWindows;
                if (windows == null || windows.Length == 0)
                {
                    sb.AppendLine("     (no hit windows)");
                }
                else
                {
                    for (int i = 0; i < windows.Length; i++)
                    {
                        var w = windows[i];
                        float contact = BossAttackTimingUtility.NormalizedToSeconds(w.startNormalized, real);
                        float contactEnd = BossAttackTimingUtility.NormalizedToSeconds(w.endNormalized, real);
                        float ms = BossAttackTimingUtility.WindowMilliseconds(w.startNormalized, w.endNormalized, real);
                        float ratio = BossAttackTimingUtility.ParryDifficultyRatio(ms);
                        string flag = ms <= 0f ? "  <-- EMPTY WINDOW"
                            : ratio < 0.6f ? "  <-- tight vs parry"
                            : ratio > 3f ? "  <-- very wide"
                            : "";
                        sb.AppendLine(
                            $"     window {i + 1}: nt {w.startNormalized:F2}-{w.endNormalized:F2} " +
                            $"part={w.part} react={w.deflectReaction} dmg×{w.damageMultiplier:F2}" +
                            (w.measured ? " (measured)" : "") + "\n" +
                            $"        → contact {contact:F2}s–{contactEnd:F2}s   dur {ms:F0}ms   parry ×{ratio:F2}{flag}");
                    }
                }
                sb.AppendLine();
            }

            Debug.Log(sb.ToString());
        }

        private static void CollectStates(AnimatorStateMachine sm, Dictionary<string, (float, float)> into)
        {
            foreach (var cs in sm.states)
            {
                var state = cs.state;
                float len = state.motion is AnimationClip clip ? clip.length : 0f;
                into[state.name] = (state.speed, len);
            }
            foreach (var child in sm.stateMachines)
            {
                CollectStates(child.stateMachine, into);
            }
        }
    }
}
