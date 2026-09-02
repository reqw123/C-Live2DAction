using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.Core;
using Live2DAction.Dev;

namespace Live2DAction.EditorTools
{
    // 2026-09-01, user playtest report:
    //   (2) "cat沒有正確受到傷害" - the Cat only had its tiny movement CharacterController (world
    //       Y ~0.5-1.3) as a hit target, so a 4x boss's blade/kick arc sails over it. Same
    //       "受擊區域和身高差距" fix the Player already got: a separate, larger HurtboxLink capsule.
    //   (3) "提供一個按鍵讓畫面可以直接停止(模擬play mode stop)" - a DevTimeFreeze on a scene object.
    // Both re-runnable.
    internal static class PlaytestFixesSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";

        [MenuItem("Tools/Live2DAction/Add Cat Hurtbox")]
        public static void AddCatHurtbox()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Exit Play Mode first."); return; }
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject cat = GameObject.Find("Cat");
            if (cat == null) { Debug.LogError("PlaytestFixesSetup: no 'Cat' in " + ScenePath); return; }
            Health health = cat.GetComponent<Health>();
            if (health == null) { Debug.LogError("PlaytestFixesSetup: Cat has no Health."); return; }

            Transform existing = cat.transform.Find("CatHurtbox");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var go = new GameObject("CatHurtbox");
            go.transform.SetParent(cat.transform, false);
            go.transform.localPosition = new Vector3(0f, 0.15f, 0f);

            var cap = go.AddComponent<CapsuleCollider>();
            cap.isTrigger = true;
            cap.direction = 1;      // Y
            cap.radius = 0.6f;      // the cat's body is long/wide; a forgiving cross-section
            cap.height = 1.9f;      // tall enough to sit in a 4x boss's blade/kick arc (mirrors Player/PlayerHurtbox)
            cap.center = Vector3.zero;

            var link = go.AddComponent<HurtboxLink>();
            link.Configure(health);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("PlaytestFixesSetup: Cat/CatHurtbox added (CapsuleCollider r0.6 h1.9 -> HurtboxLink -> Cat Health). The cat can now be struck by the 4x boss.");
        }

        [MenuItem("Tools/Live2DAction/Add Dev Time Freeze Key (Backquote)")]
        public static void AddTimeFreeze()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Exit Play Mode first."); return; }
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject host = GameObject.Find("DevTools");
            if (host == null) host = new GameObject("DevTools");
            // Re-create the component so it picks up the current code default rather than a stale
            // serialized key from an earlier run.
            var stale = host.GetComponent<DevTimeFreeze>();
            if (stale != null) Object.DestroyImmediate(stale);
            host.AddComponent<DevTimeFreeze>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("PlaytestFixesSetup: DevTools/DevTimeFreeze added. Press ` (backquote) in Play to freeze/resume (Time.timeScale 0).");
        }
    }
}
