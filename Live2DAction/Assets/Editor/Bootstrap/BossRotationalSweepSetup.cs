using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.Combat.Boss;

namespace Live2DAction.EditorTools
{
    // 2026-09-01, spec WUSHI_COMBAT_ENGINEERING_SPEC.md §4 (M2 項目 3). Turns on the root/mid/tip
    // rotational sweep for the 武士's katana BladeHitbox (the one CapsuleCollider BossHitbox on the
    // boss). Every other BossHitbox - the sphere hands/feet/body, 屁孩王's hitboxes - is left with
    // useRotationalSweep = false, i.e. the original centre-translation sweep, unchanged.
    // Re-runnable. Remove flips it back.
    internal static class BossRotationalSweepSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";

        [MenuItem("Tools/Live2DAction/Enable 武士 Rotational Blade Sweep")]
        public static void Enable() => SetFlag(true);

        [MenuItem("Tools/Live2DAction/Disable 武士 Rotational Blade Sweep")]
        public static void Disable() => SetFlag(false);

        private static void SetFlag(bool on)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Exit Play Mode first - this touches the scene.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            int changed = 0;
            foreach (BossHitbox hb in Object.FindObjectsByType<BossHitbox>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (hb.transform.root.name != "武士")
                {
                    continue;
                }
                if (hb.GetComponent<CapsuleCollider>() == null)
                {
                    continue; // rotational sweep only applies to the capsule BladeHitbox
                }
                var so = new SerializedObject(hb);
                SerializedProperty prop = so.FindProperty("useRotationalSweep");
                if (prop.boolValue != on)
                {
                    prop.boolValue = on;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    changed++;
                    Debug.Log((on ? "Enabled" : "Disabled") + " rotational sweep on " + Path(hb.transform));
                }
            }

            if (changed == 0)
            {
                Debug.LogWarning("BossRotationalSweepSetup: no 武士 capsule BladeHitbox needed changing.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static string Path(Transform t)
        {
            string s = t.name;
            while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
            return s;
        }
    }
}
