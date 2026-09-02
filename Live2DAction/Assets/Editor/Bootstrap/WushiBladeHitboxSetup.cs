using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.Combat.Boss;

namespace Live2DAction.EditorTools
{
    // 2026-09-01, user report ("武士的刀不夠長 ... 刀柄和刀尖連成的攻擊判定區域沒有做得很好 也沒有搭配
    // 武士的攻擊距離"). The 武士's BladeHitbox CapsuleCollider was ~1.0m of world length sitting on the
    // hilt-side third of a ~3m visible katana, so the blade's outer half - and its tip - had no hit
    // volume, and it fell well short of the blade attacks' own maxDistance (2.5-3.5m).
    //
    // This resizes the capsule to run along the visible blade from just past the grip to the tip,
    // measured live from the KatanaMesh renderer bounds each run (so it self-corrects if the model /
    // 4x scale ever changes). BladeHitboxPart's own rule "只覆蓋有效刀刃,不能包含劍柄" - the grip
    // portion (gripSkipFraction from the hand end) is left out.
    internal static class WushiBladeHitboxSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const float GripSkipFraction = 0.14f;   // fraction of the hand->tip span left uncovered at the grip
        private const float TipMarginFraction = 0.02f;  // tiny bite off the very tip
        private const float LocalRadius = 0.1f;         // ~0.32m world at the 3.2x blade lossyScale

        [MenuItem("Tools/Live2DAction/Resize 武士 Blade Hitbox To Visible Katana")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Exit Play Mode first - this touches the scene.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject boss = GameObject.Find("武士");
            if (boss == null)
            {
                Debug.LogError("WushiBladeHitboxSetup: no '武士' in " + ScenePath);
                return;
            }

            Transform blade = boss.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "BladeHitbox");
            Transform katanaMesh = boss.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "KatanaMesh");
            Transform hand = boss.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "RightHand");
            if (blade == null || katanaMesh == null || hand == null)
            {
                Debug.LogError("WushiBladeHitboxSetup: missing BladeHitbox / KatanaMesh / RightHand.");
                return;
            }

            var cap = blade.GetComponent<CapsuleCollider>();
            if (cap == null)
            {
                Debug.LogError("WushiBladeHitboxSetup: BladeHitbox has no CapsuleCollider.");
                return;
            }

            // Combined visible-katana world bounds -> its 8 corners in BladeHitbox local space, and
            // the range along the capsule's own axis (direction 0 = local X for this rig).
            Bounds wb = default;
            bool init = false;
            foreach (Renderer r in katanaMesh.GetComponentsInChildren<Renderer>(true))
            {
                if (!init) { wb = r.bounds; init = true; }
                else wb.Encapsulate(r.bounds);
            }
            if (!init)
            {
                Debug.LogError("WushiBladeHitboxSetup: KatanaMesh has no renderers to measure.");
                return;
            }

            int axis = cap.direction; // 0/1/2 -> local X/Y/Z
            float minA = float.MaxValue, maxA = float.MinValue;
            Vector3 c = wb.center, e = wb.extents;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                float a = Axis(blade.InverseTransformPoint(corner), axis);
                minA = Mathf.Min(minA, a);
                maxA = Mathf.Max(maxA, a);
            }
            float handA = Axis(blade.InverseTransformPoint(hand.position), axis);

            // Which end is the tip (far from the hand)?
            float tipA, hiltA;
            if (Mathf.Abs(minA - handA) > Mathf.Abs(maxA - handA)) { tipA = minA; hiltA = maxA; }
            else { tipA = maxA; hiltA = minA; }

            float span = hiltA - tipA;                       // signed hand-side -> tip-side
            float gripEndA = tipA + span * (1f - GripSkipFraction); // stop this far short of the grip
            float tipEndA = tipA + span * TipMarginFraction;
            float lengthLocal = Mathf.Abs(gripEndA - tipEndA);
            float centerA = (gripEndA + tipEndA) * 0.5f;

            Vector3 newCenter = cap.center;
            SetAxis(ref newCenter, axis, centerA);

            var so = new SerializedObject(cap);
            so.FindProperty("m_Height").floatValue = lengthLocal;
            so.FindProperty("m_Radius").floatValue = LocalRadius;
            so.FindProperty("m_Center").vector3Value = newCenter;
            so.ApplyModifiedPropertiesWithoutUndo();

            float axisWorldScale = axis == 0 ? blade.lossyScale.x : axis == 1 ? blade.lossyScale.y : blade.lossyScale.z;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"WushiBladeHitboxSetup: BladeHitbox capsule -> height {lengthLocal:F3} (≈{lengthLocal * axisWorldScale:F2}m world), "
                      + $"center.{"xyz"[axis]} {centerA:F3}, radius {LocalRadius} (≈{LocalRadius * axisWorldScale:F2}m). Was ~1.0m world.");
        }

        private static float Axis(Vector3 v, int axis) => axis == 0 ? v.x : axis == 1 ? v.y : v.z;

        private static void SetAxis(ref Vector3 v, int axis, float value)
        {
            if (axis == 0) v.x = value;
            else if (axis == 1) v.y = value;
            else v.z = value;
        }
    }
}
