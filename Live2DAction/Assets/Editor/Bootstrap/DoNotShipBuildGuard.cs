using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Live2DAction.EditorTools
{
    // Docs/ASSET_LICENSES.md's "禁止進入對外 Build" table (and Docs/BUILD_RELEASE_GUIDE.md's
    // manual checklist item "不包含 076/077 佔位素材或其他未授權素材") has, up to now, only been
    // enforced by someone remembering to read the docs before hitting Build. This turns that
    // into an actual build failure for any non-Development build - a Development build stays
    // on the developer's own machine, so it isn't "delivered to someone else" in the sense
    // ASSET_LICENSES.md's rule cares about ("不得出現在任何要交付/發布給他人的 Build"); a
    // non-Development build is exactly the kind meant to be handed to a tester/player, so
    // that's where this needs to be a hard stop, not a reminder.
    //
    // Path list must be kept in sync with ASSET_LICENSES.md's table by hand - there is no
    // single marker (folder suffix, GameObject name) consistently applied across all six
    // entries to derive this automatically: Player5Anime/WolfsGravestone carry no
    // "_DoNotShip" suffix at all, and the Live2D standees' GameObject names are known to
    // intermittently reset to an empty string on scene reload (see KNOWN_ISSUES.md's
    // "Live2D 立牌視覺" section), so name-based scene scanning alone would miss real risk here.
    // Existence in the project is the trigger (not "is it referenced by a build scene") because
    // ASSET_LICENSES.md's own rule is "must not be present", not "must not be wired up".
    internal class DoNotShipBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        private static readonly string[] BlockedAssetFolders =
        {
            "Assets/_Project/Live2D/PlaceholderCharacter", // 076 Natsu (Fairy Tail doujin model)
            "Assets/_Project/Live2D/PlaceholderCharacter077", // 077 Lucy (Fairy Tail doujin model)
            "Assets/_Project/Characters/Placeholder/MechaModel_DoNotShip", // unlicensed mecha standee
            "Assets/_Project/Characters/Placeholder/Player5Anime", // unlicensed "lacrimosa" model
            "Assets/_Project/Characters/Placeholder/Weapons/WolfsGravestone", // Genshin weapon replica
            "Assets/_Project/Environment/Placeholder/GenshinSwords", // 10 Genshin sword replicas
        };

        public void OnPreprocessBuild(BuildReport report)
        {
            if ((report.summary.options & BuildOptions.Development) != 0)
            {
                return;
            }

            List<string> found = BlockedAssetFolders.Where(AssetDatabase.IsValidFolder).ToList();
            if (found.Count == 0)
            {
                return;
            }

            throw new BuildFailedException(
                "Build blocked: unlicensed placeholder assets are still in the project (see Docs/ASSET_LICENSES.md's " +
                "\"禁止進入對外 Build\" table). Remove them before a non-Development build, or build with the " +
                "Development flag if this is only for internal testing:\n" + string.Join("\n", found));
        }
    }
}
