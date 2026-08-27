using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Temporary, one-off fix - run once via Tools menu (or -executeMethod), then delete.
//
// 2026-08-26, bug report ("屁孩王有時突然陷入地底下") - the merged FBX's two new clips
// (Breakdance_1990, Kneel_on_One_Knee_and_Stand) were left on Unity's raw AUTO-SPLIT take
// defaults (empty clipAnimations array => no custom baking at all). Every SIBLING clip in this
// pack (see e.g. PW2_HighKick's own .meta) explicitly sets heightFromFeet=1 and
// keepOriginalPositionY=0/keepOriginalPositionXZ=0 on its one clipAnimations entry - that's what
// re-anchors the clip's baked root Y translation to the character's actual feet instead of
// trusting the raw source take's root bone curve as-is. Without it, a take whose root bone
// height doesn't line up exactly with this rig's floor contact point plays back with the whole
// character offset vertically - explains a sudden dip/sink specifically when these two (and only
// these two) new moves play.
public static class PW2_FixBreakdanceKneelClipBaking
{
    private const string MergedFbxPath =
        "Assets/_Project/Characters/Placeholder/PiHaiWangV2/Animations/Meshy_AI_Man_in_Black_at_the_P_biped_Meshy_AI_Meshy_Merged_Animations.fbx";
    private const string ControllerPath =
        "Assets/_Project/Characters/Placeholder/PiHaiWangV2/Animator/PiHaiWangV2.controller";

    private const string BreakdanceStateName = "PW2_Breakdance1990";
    private const string KneelStateName = "PW2_KneelOnOneKneeAndStand";
    private const string BreakdanceClipName = "PW2_Breakdance1990";
    private const string KneelClipName = "PW2_KneelOnOneKneeAndStand";

    [MenuItem("Tools/PiHaiWangV2/Fix Breakdance+Kneel Clip Baking (heightFromFeet)")]
    public static void Run()
    {
        var importer = AssetImporter.GetAtPath(MergedFbxPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError($"[PW2 Fix] Could not load ModelImporter at {MergedFbxPath}");
            return;
        }

        var defaults = importer.defaultClipAnimations;
        Debug.Log("[PW2 Fix] Default (auto-split) take names: " + string.Join(", ", defaults.Select(c => c.name)));

        bool foundBreakdance = false, foundKneel = false;
        for (int i = 0; i < defaults.Length; i++)
        {
            var c = defaults[i];
            if (c.name.IndexOf("Breakdance", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                c.name = BreakdanceClipName;
                ApplyFeetAnchoredBaking(ref c);
                defaults[i] = c;
                foundBreakdance = true;
            }
            else if (c.name.IndexOf("Kneel", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                c.name = KneelClipName;
                ApplyFeetAnchoredBaking(ref c);
                defaults[i] = c;
                foundKneel = true;
            }
        }

        if (!foundBreakdance || !foundKneel)
        {
            Debug.LogError($"[PW2 Fix] foundBreakdance={foundBreakdance} foundKneel={foundKneel} - aborting.");
            return;
        }

        // Explicit clipAnimations for ALL takes (not just the 2 we need) - leaving some takes
        // implicit/auto and others explicit is undocumented/risky; every take gets a real,
        // named entry so the importer's behavior is fully deterministic.
        importer.clipAnimations = defaults;
        importer.SaveAndReimport();

        var subAssets = AssetDatabase.LoadAllAssetsAtPath(MergedFbxPath);
        var clips = subAssets.OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
        Debug.Log("[PW2 Fix] Clips after reimport: " + string.Join(", ", clips.Select(c => c.name)));

        var breakdanceClip = clips.FirstOrDefault(c => c.name == BreakdanceClipName);
        var kneelClip = clips.FirstOrDefault(c => c.name == KneelClipName);
        if (breakdanceClip == null || kneelClip == null)
        {
            Debug.LogError($"[PW2 Fix] Post-reimport lookup failed - breakdanceClip={breakdanceClip}, kneelClip={kneelClip}");
            return;
        }

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        var baseLayer = controller.layers.FirstOrDefault(l => l.name == "Base Layer") ?? controller.layers[0];
        var rootSm = baseLayer.stateMachine;

        RewireState(rootSm, BreakdanceStateName, breakdanceClip);
        RewireState(rootSm, KneelStateName, kneelClip);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("[PW2 Fix] Done - rewired states with feet-anchored clips.");
    }

    private static void ApplyFeetAnchoredBaking(ref ModelImporterClipAnimation c)
    {
        c.heightFromFeet = true;
        c.keepOriginalPositionY = false;
        c.keepOriginalPositionXZ = false;
        c.loopTime = false;
        c.loop = false;
        c.wrapMode = WrapMode.Default;
    }

    private static void RewireState(AnimatorStateMachine sm, string stateName, AnimationClip clip)
    {
        var existing = sm.states.FirstOrDefault(s => s.state.name == stateName);
        if (existing.state == null)
        {
            Debug.LogError($"[PW2 Fix] State '{stateName}' not found in controller - was it renamed?");
            return;
        }
        existing.state.motion = clip;
        Debug.Log($"[PW2 Fix] Rewired state '{stateName}' -> clip '{clip.name}' (feet-anchored).");
    }
}
