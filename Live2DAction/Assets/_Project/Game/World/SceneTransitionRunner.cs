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

        [Tooltip("Total seconds the video loading screen must stay up even if the scene loads " +
                 "instantly, so it never just flashes one frame (user request).")]
        [SerializeField] private float minLoadingScreenSeconds = 1.2f;

        public void Begin(string sceneToLoad, string sceneToUnload, Transform occupant,
                          Vector3 arrivalPos, float arrivalYaw, string loadingLabel,
                          float fadeSeconds, int settleFrames, bool useLoadingScreen = false)
        {
            if (IsRunning) return;
            StartCoroutine(Run(sceneToLoad, sceneToUnload, occupant, arrivalPos, arrivalYaw,
                               loadingLabel, fadeSeconds, Mathf.Max(0, settleFrames), useLoadingScreen));
        }

        private IEnumerator Run(string sceneToLoad, string sceneToUnload, Transform occupant,
                                Vector3 arrivalPos, float arrivalYaw, string loadingLabel,
                                float fadeSeconds, int settleFrames, bool useLoadingScreen)
        {
            IsRunning = true;

            // 2026-09-06 - the video loading screen sits ON TOP of ScreenFader. ScreenFader stays
            // covered the whole time = the black backing + the existing PlayerInputProvider
            // input-lock (movement / attack / dodge zeroed while ScreenFader.IsCovered). Nothing
            // is disabled; control returns automatically when the curtain lifts.
            var loading = (useLoadingScreen && BossLoadingScreen.Instance != null)
                ? BossLoadingScreen.Instance : null;

            var fader = ScreenFader.Instance;
            if (fader != null)
            {
                fader.SetLabel(loading != null ? null : loadingLabel);   // the video panel replaces the label
                fader.SetCovered(true, fadeSeconds);
                while (!fader.IsFullyCovered) yield return null;
            }

            float shownAt = Time.unscaledTime;
            if (loading != null) yield return loading.Show();   // waits for VideoPlayer.prepareCompleted

            if (!string.IsNullOrEmpty(sceneToLoad))
            {
                Scene s = SceneManager.GetSceneByName(sceneToLoad);
                if (!s.IsValid() || !s.isLoaded)
                {
                    AsyncOperation op = SceneManager.LoadSceneAsync(sceneToLoad, LoadSceneMode.Additive);
                    if (op == null)
                    {
                        Debug.LogError($"[SceneTransitionRunner] LoadSceneAsync('{sceneToLoad}') returned null - " +
                                       "is it in Build Settings? Aborting transition, restoring the player.");
                        if (loading != null) loading.AbortImmediate();
                        if (fader != null) { fader.ClearLabel(); fader.SetCovered(false, fadeSeconds); }
                        IsRunning = false;
                        yield break;
                    }

                    if (loading != null)
                    {
                        // Unity holds progress at 0.9 until allowSceneActivation - remap 0..0.9 -> 0..100%
                        op.allowSceneActivation = false;
                        while (op.progress < 0.9f)
                        {
                            loading.SetProgress(op.progress / 0.9f);
                            yield return null;
                        }
                        loading.SetProgress(0.99f);
                        op.allowSceneActivation = true;
                        while (!op.isDone) yield return null;
                        loading.SetProgress(1f);
                    }
                    else
                    {
                        while (!op.isDone) yield return null;
                    }
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

            if (loading != null)
            {
                // don't just flash one frame if the load was instant
                while (Time.unscaledTime - shownAt < minLoadingScreenSeconds) yield return null;
                yield return loading.Hide();
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
            var rb = cc == null ? occupant.GetComponent<Rigidbody>() : null;
            if (cc != null)
            {
                cc.enabled = false;
                occupant.SetPositionAndRotation(pos, rot);
                cc.enabled = true;
            }
            else if (rb != null)
            {
                // 2026-09-06 - a Rigidbody occupant (a vehicle) keeps its velocity through a raw
                // transform set and tunnels through the freshly-loaded map's still-cooking colliders
                // into the void. Go kinematic for the move, drop all momentum, then restore.
                // (SceneGate no longer hands vehicles here - it dismounts first - but keep this
                // correct for any other caller.)
                bool wasKinematic = rb.isKinematic;
                rb.isKinematic = true;
                occupant.SetPositionAndRotation(pos, rot);
                Physics.SyncTransforms();
                rb.isKinematic = wasKinematic;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            else
            {
                occupant.SetPositionAndRotation(pos, rot);
            }

            var cam = Camera.main != null
                ? Camera.main.GetComponent<Live2DAction.CameraSystem.ThirdPersonCameraController>()
                : null;
            if (cam != null)
            {
                // 2026-09-06 - every fight-end return (YuanpeiEncounter victory / defeat, including
                // the ChargeCrush void-punt death that deliberately leaves the controller OFF) funnels
                // through here. A boss death-dissolve / execution cinematic drives Camera.main directly
                // with ThirdPersonCameraController disabled and hands it back on its own last line - if
                // that coroutine faults partway, the camera stays frozen on the death angle ("視角沒有
                // 回到玩家身上"). Re-asserting it here is the one guaranteed choke point on the way back;
                // harmless for ordinary SceneGate transitions where it's already enabled.
                cam.enabled = true;
                cam.SnapYawToTarget();
            }
        }
    }
}
