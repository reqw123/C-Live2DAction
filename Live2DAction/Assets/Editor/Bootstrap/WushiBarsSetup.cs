using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Live2DAction.Core;
using Live2DAction.Combat;

namespace Live2DAction.EditorTools
{
    // 2026-08-28, explicit user request:
    //   1. "為武士加上血量條/架勢條"
    //   2. "飛向天空那招 能量滿格才會觸發 100能量:20秒"
    //   3. follow-up: "由於武士較大 三個狀態調在頭上玩家看不見...改成像隻狼一樣的做法"
    //      => a fixed SCREEN-space boss HUD at the top of the screen (Sekiro-style), not a
    //         world-space bar over the boss's head.
    //
    // The Player's own PlayerCornerHud already has three finished screen-space tracks
    // (生命Track / 架勢Track / 必殺Track = PlayerHealthBarFx / StancePoiseBarFx / UltimateEnergyBarFx,
    // pixel units, billboard off, all reference-art layers + sparks). This clones those three
    // subtrees into a new top-centre WushiBossHud canvas, widens them for a boss bar, and
    // re-points the health/stance/energy references at 武士. Instantiate() already remaps every
    // reference that pointed within a cloned subtree.
    //
    // Also wires 武士's LeapSlam energy: an UltimateEnergy configured 100 / 5-per-1s (=> 20s to
    // full) pointed to by BossStateMachine.leapSlamEnergy.
    internal static class WushiBarsSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string PlayerHudName = "PlayerCornerHud";
        private const string BossName = "武士";
        private const string HudName = "WushiBossHud";

        // Sekiro-ish: posture on top, health under it, LeapSlam energy narrower below. Widths in
        // 1920x1080 reference px (CanvasScaler ScaleWithScreenSize). PlayerCornerHud's tracks are
        // 176px reference; these scale the child art up to boss width.
        private const float ReferenceTrackWidth = 176f;
        private const float HeightScale = 1.7f;

        private struct BarSpec
        {
            public string SourceTrack;   // child of PlayerCornerHud
            public string NewName;
            public float Width;
            public float TopY;           // anchoredPosition.y (negative = down from top edge)
        }

        // 2026-08-28, user feedback ("血量條應該在第一順位") - health on top, then posture, then
        // LeapSlam energy.
        private static readonly BarSpec[] Bars =
        {
            new BarSpec { SourceTrack = "生命Track", NewName = "武士_生命", Width = 760f, TopY = -42f },
            new BarSpec { SourceTrack = "架勢Track", NewName = "武士_架勢", Width = 700f, TopY = -70f },
            new BarSpec { SourceTrack = "必殺Track", NewName = "武士_能量", Width = 560f, TopY = -96f },
        };

        [MenuItem("Tools/Live2DAction/Add Wushi Bars (Sekiro-style Boss HUD + LeapSlam Energy)")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Exit Play Mode first - EditorSceneManager.OpenScene throws mid-Play.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject boss = GameObject.Find(BossName);
            GameObject playerHud = GameObject.Find(PlayerHudName);
            if (boss == null || playerHud == null)
            {
                Debug.LogError("Need both " + BossName + " and " + PlayerHudName + " in the scene.");
                return;
            }

            Health bossHealth = boss.GetComponent<Health>();
            StancePoise bossStance = boss.GetComponent<StancePoise>();
            if (bossHealth == null || bossStance == null)
            {
                Debug.LogError(BossName + " is missing Health or StancePoise.");
                return;
            }

            // ---- clean up the old world-space bars this script used to make ----
            foreach (string oldCanvas in new[] { "HealthBarCanvas", "StanceBarCanvas", "EnergyBarCanvas" })
            {
                Transform old = boss.transform.Find(oldCanvas);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }

            // ---- LeapSlam energy (100 in 20s = 5 / 1s) ----
            UltimateEnergy leapEnergy = boss.GetComponent<UltimateEnergy>();
            if (leapEnergy == null) leapEnergy = boss.AddComponent<UltimateEnergy>();
            var energySo = new SerializedObject(leapEnergy);
            energySo.FindProperty("maxEnergy").floatValue = 100f;
            energySo.FindProperty("regenAmount").floatValue = 5f;
            energySo.FindProperty("regenIntervalSeconds").floatValue = 1f;
            energySo.FindProperty("regenIdleDelaySeconds").floatValue = 0f;
            energySo.ApplyModifiedPropertiesWithoutUndo();

            foreach (MonoBehaviour mb in boss.GetComponents<MonoBehaviour>())
            {
                if (mb.GetType().Name != "BossStateMachine") continue;
                var bsmSo = new SerializedObject(mb);
                SerializedProperty prop = bsmSo.FindProperty("leapSlamEnergy");
                if (prop != null)
                {
                    prop.objectReferenceValue = leapEnergy;
                    bsmSo.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(mb);
                }
            }

            // ---- the screen-space boss HUD ----
            GameObject oldHud = GameObject.Find(HudName);
            if (oldHud != null) Object.DestroyImmediate(oldHud);

            var hudGo = new GameObject(HudName, typeof(RectTransform));
            var canvas = hudGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1; // above PlayerCornerHud
            var scaler = hudGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            hudGo.AddComponent<GraphicRaycaster>();

            // Show the HUD only while 武士 is in combat.
            var visibility = hudGo.AddComponent<Live2DAction.UI.WushiBossHudVisibility>();
            foreach (MonoBehaviour mb in boss.GetComponents<MonoBehaviour>())
            {
                if (mb.GetType().Name != "BossStateMachine") continue;
                var visSo = new SerializedObject(visibility);
                visSo.FindProperty("boss").objectReferenceValue = mb;
                visSo.ApplyModifiedPropertiesWithoutUndo();
            }

            foreach (BarSpec spec in Bars)
            {
                Transform src = FindChild(playerHud.transform, spec.SourceTrack);
                if (src == null)
                {
                    Debug.LogWarning(PlayerHudName + " has no " + spec.SourceTrack + " - skipped.");
                    continue;
                }

                var clone = Object.Instantiate(src.gameObject, hudGo.transform);
                clone.name = spec.NewName;

                var rt = (RectTransform)clone.transform;
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, spec.TopY);
                rt.sizeDelta = new Vector2(spec.Width, rt.sizeDelta.y * HeightScale);

                // Widen every explicit-size art layer to the boss width; move the end-anchored
                // EdgeGlow to the new right edge. Sparks are repositioned by the Fx at runtime.
                foreach (RectTransform child in rt)
                {
                    if (child.name == "EdgeGlow")
                    {
                        child.anchoredPosition = new Vector2(spec.Width, child.anchoredPosition.y);
                        child.sizeDelta *= HeightScale;
                        continue;
                    }
                    if (child.name.StartsWith("Spark") || child.name == "Value") continue;
                    child.sizeDelta = new Vector2(spec.Width, child.sizeDelta.y * HeightScale);
                }

                // Re-point the one/two references on the Fx that pointed at Player.
                foreach (MonoBehaviour mb in clone.GetComponents<MonoBehaviour>())
                {
                    if (mb is Image) continue;
                    var so = new SerializedObject(mb);
                    RepointIfPresent(so, "health", bossHealth);
                    RepointIfPresent(so, "stance", bossStance);
                    RepointIfPresent(so, "energy", leapEnergy);
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log(BossName + ": screen-space Sekiro-style boss HUD (posture + health + LeapSlam energy) " +
                      "built as " + HudName + " (cloned from " + PlayerHudName + "), and UltimateEnergy " +
                      "(100 / 5-per-1s => 20s) wired to BossStateMachine.leapSlamEnergy.");
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t;
            }
            return null;
        }

        private static void RepointIfPresent(SerializedObject so, string propName, Object value)
        {
            SerializedProperty p = so.FindProperty(propName);
            if (p != null && p.propertyType == SerializedPropertyType.ObjectReference)
            {
                p.objectReferenceValue = value;
            }
        }
    }
}
