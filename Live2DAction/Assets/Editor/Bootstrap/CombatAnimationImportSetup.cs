using UnityEditor;
using UnityEngine;

namespace Live2DAction.EditorTools
{
    // Configures the 3 raw Mixamo FBX files (2026-08-12, downloaded via the user's own Adobe
    // login, free/no-attribution-required per Mixamo's standard license - see
    // Docs/ASSET_LICENSES.md) as Humanoid rigs so their animation can be retargeted onto both
    // Maya's and Arisa's Humanoid avatars (see CombatAnimatorSetup.cs, which wires the
    // resulting clips into both characters' Animator Controllers). "Create From This Model" is
    // the standard Mixamo->Unity workflow - Mixamo's own bone naming convention
    // (mixamorig:Hips etc.) auto-maps to Unity's Humanoid muscle definitions reliably without
    // needing to copy an avatar from either character.
    internal static class CombatAnimationImportSetup
    {
        private const string Folder = "Assets/_Project/Characters/Placeholder/CombatAnimations/Mixamo";

        // 2026-08-17, explicit user request ("把它變成滑鼠左鍵續力攻擊") - MmaKick added as a
        // 4th combo step (see CombatAnimatorSetup's Attack4 entry and
        // CharacterAttackAnimationLink.TriggerNameForComboIndex). Same Mixamo source/license as
        // the original 3 (see this class's own header comment) - user downloaded it themselves
        // from Mixamo and dropped it in as "Mma Kick.fbx", renamed here to match this folder's
        // no-spaces convention.
        //
        // 2026-08-17 follow-up (same session, three more): BreakdanceUltimate drives Player4's
        // new EnemyUltimateAbility (see that class's own comment), ZombiePunching is an
        // AnimatorOverrideController swap-in for Attack1 during the player's UltimateAbility
        // window (see UltimateAttackAnimationSwap), FlyingKick drives the player's new
        // ExecutionAbility finisher. All three need the same Humanoid/loopTime=false treatment
        // as the original clips even though none of them are wired as new combo steps.
        private static readonly string[] FbxNames =
        {
            "CrossPunch", "HookPunch", "Uppercut", "MmaKick",
            "BreakdanceUltimate", "ZombiePunching", "FlyingKick",
        };

        // 2026-08-17, explicit user request ("陷入僵直時採用蹲下動作") - KneelingDown needs
        // loopTime=TRUE, unlike every clip above: a punch/kick is a one-shot swing that should
        // stop and hand control back once it's done, but the stagger pose has to hold for
        // however long StancePoise's own timer/execution decides the stagger lasts - a
        // non-looping clip would just freeze on its last sampled frame once it "finishes"
        // playing, which isn't necessarily even a good held pose, and reads as broken rather
        // than "kneeling and dazed". See StaggerAnimationLink for how this actually gets driven
        // (a bool, not a Trigger, precisely because its duration isn't fixed).
        private static readonly string[] LoopingFbxNames = { "KneelingDown" };

        // 2026-08-18, explicit user request ("將這個動作作為所有角色死亡時的共同動作") - Dying
        // needs loopTime=FALSE (unlike KneelingDown - a death pose should play once and hold, not
        // cycle) but heightFromFeet=TRUE like KneelingDown (not the standing-swing clips' false) -
        // same reasoning as KneelingDown's own comment: a lying-down take's raw Mixamo root height
        // wasn't authored against this rig's floor the way the standing punches/kicks were, so it
        // needs to be re-grounded from the feet bones per-frame rather than trusting the raw
        // translation, or the corpse floats. Neither existing array's (loop, heightFromFeet)
        // combination fits, hence its own array/call rather than joining either.
        private static readonly string[] NonLoopingGroundedFbxNames = { "Dying" };

        [MenuItem("Tools/Live2DAction/Configure Mixamo Combat Animations As Humanoid")]
        public static void Apply()
        {
            foreach (string name in FbxNames)
            {
                Configure(name, loopTime: false, groundHeightFromFeet: false);
            }

            foreach (string name in NonLoopingGroundedFbxNames)
            {
                Configure(name, loopTime: false, groundHeightFromFeet: true);
            }

            foreach (string name in LoopingFbxNames)
            {
                // 2026-08-18, real bug report ("硬值狀態下的蹲下動作會浮在空中") - every clip
                // above (and, before this fix, KneelingDown too) imports with
                // keepOriginalPositionY=true, which just carries over whatever raw Hips-Y Mixamo
                // baked into that specific take verbatim. That happens to line up with this
                // rig's actual floor for every standing swing (CrossPunch etc), but Mixamo's
                // "Kneeling Down" take wasn't authored on the same floor reference - its raw root
                // height sits measurably above where this character's feet actually touch ground
                // in every OTHER clip, so the whole rig visibly floats the instant this state
                // becomes active. heightFromFeet=true is the standard fix: it re-derives the
                // clip's root height per-frame from where its own feet bones actually are,
                // instead of trusting the raw authored translation - grounds it to this
                // character's real floor regardless of what floor Mixamo originally authored the
                // take against. Scoped to LoopingFbxNames only (not the one-shot swings, which
                // already look correct) to avoid touching anything that isn't reported broken.
                Configure(name, loopTime: true, groundHeightFromFeet: true);
            }
        }

        private static void Configure(string name, bool loopTime, bool groundHeightFromFeet)
        {
            string path = $"{Folder}/{name}.fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("Could not find ModelImporter at " + path);
                return;
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i].loopTime = loopTime;
                clips[i].name = name;
                clips[i].heightFromFeet = groundHeightFromFeet;
                clips[i].keepOriginalPositionY = !groundHeightFromFeet;
            }
            importer.clipAnimations = clips;

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            Debug.Log($"Configured {path} as Humanoid (clip renamed to '{name}', loopTime={loopTime}, heightFromFeet={groundHeightFromFeet}).");
        }
    }
}
