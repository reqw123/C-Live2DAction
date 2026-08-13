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
        private static readonly string[] FbxNames = { "CrossPunch", "HookPunch", "Uppercut" };

        [MenuItem("Tools/Live2DAction/Configure Mixamo Combat Animations As Humanoid")]
        public static void Apply()
        {
            foreach (string name in FbxNames)
            {
                string path = $"{Folder}/{name}.fbx";
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                {
                    Debug.LogError("Could not find ModelImporter at " + path);
                    continue;
                }

                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

                ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
                for (int i = 0; i < clips.Length; i++)
                {
                    // A punch/uppercut is a one-shot action, not a looping locomotion cycle -
                    // left at Mixamo's own default this would otherwise happily loop forever if
                    // an Animator transition doesn't interrupt it in time.
                    clips[i].loopTime = false;
                    clips[i].name = name;
                }
                importer.clipAnimations = clips;

                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
                Debug.Log($"Configured {path} as Humanoid (clip renamed to '{name}', loopTime=false).");
            }
        }
    }
}
