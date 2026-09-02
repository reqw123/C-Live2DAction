using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Live2DAction.EditorTools
{
    // 2026-09-02, spec WUSHI_COMBAT_ENGINEERING_SPEC.md §6.3 (M3 項目 5B) - "做法 A" (user's choice):
    // normalise the 武士's GAMEPLAY ROOT to localScale 1 while keeping the visible model, the skeleton,
    // the katana, and every bone-parented hitbox byte-for-byte identical.
    //
    // How it stays identical: the root's lossyScale drops from 4 -> 1, so every DIRECT child's
    // lossyScale AND its position-from-root would drop 4x too. For each direct child:
    //   - localScale    *= 4  -> lossyScale unchanged
    //   - localPosition *= 4  -> world offset from the root unchanged
    // and everything DEEPER inherits an unchanged parent chain, so the whole visible/collidable
    // hierarchy renders and collides exactly as before. Exception: a ParticleSystem whose
    // scalingMode is Local reads ONLY its own transform.localScale and ignores the hierarchy, so
    // scaling it would make it 4x too big - that one keeps its localScale (position still x4).
    // The only thing genuinely living at the root that needs compensation is the
    // CharacterController's own dimensions (they scale with the root's lossyScale).
    //
    // Why bother if nothing visibly changes: transform.position / CharacterController / hurtbox
    // world sizes are now readable in plain metres (spec §6.3), and it's the stated prerequisite for
    // 5C. The heavy "shrink the visible boss + re-author clips" work is deferred to a full 武士 copy
    // (做法 B) - see the conversation.
    //
    // Idempotent + reversible: Normalise only acts when the root isn't already 1; Restore puts the
    // factor back.
    internal static class WushiRootScaleSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";

        [MenuItem("Tools/Live2DAction/[5B] Normalise 武士 Root Scale To 1")]
        public static void Normalise()
        {
            var (scene, wushi) = Open();
            if (wushi == null) return;

            float f = wushi.transform.localScale.x;
            if (Mathf.Approximately(f, 1f))
            {
                Debug.LogWarning("WushiRootScaleSetup: 武士 root scale is already 1 - nothing to do.");
                return;
            }

            Rescale(wushi, targetRootScale: 1f, factor: f);
            Save(scene);
            Debug.Log($"WushiRootScaleSetup: 武士 root {f} -> 1 (children x{f}, CC x{f}). Visible model / skeleton / " +
                      "katana / bone hitboxes unchanged. Verify with the world-bounds log above.");
        }

        [MenuItem("Tools/Live2DAction/[5B] Restore 武士 Root Scale To 4")]
        public static void Restore()
        {
            var (scene, wushi) = Open();
            if (wushi == null) return;

            float cur = wushi.transform.localScale.x;
            if (cur > 1.5f)
            {
                Debug.LogWarning($"WushiRootScaleSetup: 武士 root scale is {cur}, not 1 - nothing to restore.");
                return;
            }
            Rescale(wushi, targetRootScale: 4f, factor: 0.25f);
            Save(scene);
            Debug.Log("WushiRootScaleSetup: 武士 root 1 -> 4 (children /4, CC /4). Reverted to the placeholder-giant setup.");
        }

        private static void Rescale(GameObject wushi, float targetRootScale, float factor)
        {
            LogWorld(wushi, "BEFORE");

            // Every direct child: localScale *= factor (lossyScale unchanged) and localPosition *=
            // factor (world offset from the root unchanged). Everything deeper inherits an unchanged
            // chain. A Local-scaling ParticleSystem is the one exception - see the header comment.
            foreach (Transform child in wushi.transform)
            {
                Undo.RecordObject(child, "Wushi rescale");
                child.localPosition *= factor;

                var ps = child.GetComponent<ParticleSystem>();
                bool localScalingPs = ps != null &&
                                      ps.main.scalingMode == ParticleSystemScalingMode.Local;
                if (!localScalingPs)
                {
                    child.localScale *= factor;
                }
            }

            Undo.RecordObject(wushi.transform, "Wushi rescale");
            wushi.transform.localScale = Vector3.one * targetRootScale;

            // The CharacterController's dimensions scale with the root's own lossyScale - compensate.
            var cc = wushi.GetComponent<CharacterController>();
            if (cc != null)
            {
                Undo.RecordObject(cc, "Wushi rescale");
                cc.height *= factor;
                cc.radius *= factor;
                cc.center *= factor;
                cc.skinWidth *= factor;
                cc.minMoveDistance *= factor;
                // stepOffset stays 0 (intentional), slopeLimit is an angle.
            }

            EditorUtility.SetDirty(wushi);
            LogWorld(wushi, "AFTER");
        }

        private static void LogWorld(GameObject wushi, string tag)
        {
            var sb = new System.Text.StringBuilder($"[WushiRootScaleSetup] {tag} world measures:\n");
            sb.AppendLine($"  root pos={wushi.transform.position} lossyScale={wushi.transform.lossyScale}");
            var cc = wushi.GetComponent<CharacterController>();
            if (cc != null)
            {
                float wh = cc.height * wushi.transform.lossyScale.y;
                sb.AppendLine($"  CC world height≈{wh:F3} radius≈{cc.radius * wushi.transform.lossyScale.x:F3}");
            }
            foreach (var col in wushi.GetComponentsInChildren<Collider>(true))
            {
                var b = col.bounds;
                sb.AppendLine($"  {col.name.PadRight(20)} bounds c={b.center.ToString("F2")} size={b.size.ToString("F2")}");
            }
            foreach (var smr in wushi.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var b = smr.bounds;
                sb.AppendLine($"  SMR {smr.name.PadRight(16)} bounds c={b.center.ToString("F2")} size={b.size.ToString("F2")}");
            }
            Debug.Log(sb.ToString());
        }

        private static (Scene, GameObject) Open()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("WushiRootScaleSetup: exit Play Mode first.");
                return (default, null);
            }
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var wushi = GameObject.Find("武士");
            if (wushi == null) Debug.LogError("WushiRootScaleSetup: no '武士' in " + ScenePath);
            return (scene, wushi);
        }

        private static void Save(Scene scene)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
