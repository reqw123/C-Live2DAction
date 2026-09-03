using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Live2DAction.World
{
    // 2026-09-03 - hosts the SceneGate load / teleport / unload coroutine on a PERSISTENT object.
    // The exit gate lives inside Map_School, and unloading Map_School would destroy that gate and
    // kill a coroutine running on it partway through - the screen stayed black and the return
    // never finished ("只能進去不能進來"). This runner sits in the persistent scene so the
    // sequence always completes regardless of which scene is being unloaded.
    [DefaultExecutionOrder(-40)]
    public class SceneTransitionRunner : MonoBehaviour
    {
        public static SceneTransitionRunner Instance { get; private set; }
        public bool IsRunning { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Begin(string sceneToLoad, string sceneToUnload, Transform occupant,
                          Vector3 arrivalPos, float arrivalYaw, string loadingLabel,
                          float fadeSeconds, int settleFrames)
        {
            if (IsRunning) return;
            StartCoroutine(Run(sceneToLoad, sceneToUnload, occupant, arrivalPos, arrivalYaw,
                               loadingLabel, fadeSeconds, Mathf.Max(0, settleFrames)));
        }

        private IEnumerator Run(string sceneToLoad, string sceneToUnload, Transform occupant,
                                Vector3 arrivalPos, float arrivalYaw, string loadingLabel,
                                float fadeSeconds, int settleFrames)
        {
            IsRunning = true;

            var fader = ScreenFader.Instance;
            if (fader != null)
            {
                fader.SetLabel(loadingLabel);
                fader.SetCovered(true, fadeSeconds);
                while (!fader.IsFullyCovered) yield return null;
            }

            if (!string.IsNullOrEmpty(sceneToLoad))
            {
                Scene s = SceneManager.GetSceneByName(sceneToLoad);
                if (!s.IsValid() || !s.isLoaded)
                {
                    AsyncOperation op = SceneManager.LoadSceneAsync(sceneToLoad, LoadSceneMode.Additive);
                    if (op == null)
                        Debug.LogError($"[SceneTransitionRunner] LoadSceneAsync('{sceneToLoad}') returned null - is it in Build Settings?");
                    else
                        while (!op.isDone) yield return null;
                }
            }

            for (int i = 0; i < settleFrames; i++) yield return null;

            Teleport(occupant, arrivalPos, arrivalYaw);

            // A couple of covered frames for the camera to catch up to the teleported player.
            for (int i = 0; i < 2; i++) yield return null;

            if (!string.IsNullOrEmpty(sceneToUnload))
            {
                Scene s = SceneManager.GetSceneByName(sceneToUnload);
                if (s.IsValid() && s.isLoaded)
                {
                    AsyncOperation op = SceneManager.UnloadSceneAsync(s);
                    if (op != null) while (!op.isDone) yield return null;
                    Resources.UnloadUnusedAssets();
                    yield return null;
                }
            }

            if (fader != null)
            {
                fader.ClearLabel();
                fader.SetCovered(false, fadeSeconds);
            }

            IsRunning = false;
        }

        private static void Teleport(Transform occupant, Vector3 pos, float yaw)
        {
            if (occupant == null) return;

            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            var cc = occupant.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = false;
                occupant.SetPositionAndRotation(pos, rot);
                cc.enabled = true;
            }
            else
            {
                occupant.SetPositionAndRotation(pos, rot);
            }

            var cam = Camera.main != null
                ? Camera.main.GetComponent<Live2DAction.CameraSystem.ThirdPersonCameraController>()
                : null;
            if (cam != null) cam.SnapYawToTarget();
        }
    }
}
