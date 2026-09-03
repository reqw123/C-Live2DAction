using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.Input;

namespace Live2DAction.World
{
    // 2026-09-02, user request ("想要做成開放世界遊戲那樣,角色待在哪個地圖,就只占用那些地圖資源,
    // 需要時才載入") - phase 1 of map streaming: the persistent scene (GreyboxTest) stays loaded the
    // whole time and carries 本地 + 空島 + every cross-map object (Player/Cat/Buggy/cameras/HUD);
    // heavy region scenes are additively loaded only while a tracked character is near them and
    // unloaded (+ Resources.UnloadUnusedAssets) once it leaves. The first region wired this way is
    // 學校 (學校 ground + walls + the four ~3M-tri yuanpei buildings), anchored south of 本地 down
    // the VehicleRoad - which stays in the persistent scene as the always-visible connector.
    //
    // Deliberately distance-from-anchor rather than a trigger volume: no volume to author/keep in
    // sync with the map's footprint, and load/unload hysteresis (loadRadius < unloadRadius) is one
    // number each. One MapStreamer component per streamed region.
    //
    // NOT YET (bigger follow-ups, see Docs): a fade/loading curtain over the pop-in, async mesh
    // collider cooking (a 3M-tri MeshCollider still bakes on the main thread at scene activation -
    // expect a hitch), NavMesh per region, and moving the Player itself out into a Core scene.
    public class MapStreamer : MonoBehaviour
    {
        [Tooltip("Scene name (must also be added to Build Settings > Scenes In Build, enabled). " +
                 "Loaded LoadSceneMode.Additive.")]
        [SerializeField] private string sceneName = "Map_School";

        [Tooltip("World point the load/unload distance is measured to (flat, XZ only) - usually the " +
                 "centre of the streamed region.")]
        [SerializeField] private Vector3 anchor = new Vector3(0f, 0f, -115f);

        [Tooltip("Load the region once a tracked character is within this many metres of the anchor.")]
        [SerializeField] private float loadRadius = 75f;

        [Tooltip("Unload it again once every tracked character is beyond this many metres. Must be " +
                 "> loadRadius so a character loitering on the boundary doesn't thrash load/unload, " +
                 "AND < the distance from 本地's spawn to the anchor (~115m for the school) or a " +
                 "character idling at spawn (the Cat does) keeps the region resident forever.")]
        [SerializeField] private float unloadRadius = 100f;

        [Tooltip("Characters whose proximity keeps the region loaded. Left empty => auto-track the " +
                 "root of every PlayerInputProvider in the scene on first Update (Player AND Cat - " +
                 "both carry one). Fill it explicitly to also track a vehicle, or to override.")]
        [SerializeField] private List<Transform> trackedCharacters = new List<Transform>();

        [SerializeField] private bool logStateChanges = true;

        [Header("Load curtain (Phase 2)")]
        [Tooltip("Fade the screen to black while the region streams in, hiding the pop-in (geometry " +
                 "appearing from nothing + the main-thread MeshCollider cook on scene activation). " +
                 "Triggers when a tracked character is within curtainRadius as the load runs. Keep " +
                 "curtainRadius >= loadRadius so a normal walk-up gets the curtain; only lower it " +
                 "below loadRadius once a far-ahead preload is added (then a distant load streams " +
                 "in silently and the curtain is just a safety net for a fast/teleported approach). " +
                 "No-ops gracefully if there's no ScreenFader in the scene.")]
        [SerializeField] private bool useLoadCurtain = true;
        [SerializeField] private float curtainRadius = 80f;
        [SerializeField] private float curtainFadeSeconds = 0.35f;
        [Tooltip("Extra frames to stay covered after the scene reports loaded, so the first rendered " +
                 "frame + collider cook settle before the reveal.")]
        [SerializeField] private int curtainSettleFrames = 2;

        private enum RegionState { Unloaded, Loading, Settling, Loaded, Unloading }
        private RegionState _state = RegionState.Unloaded;
        private AsyncOperation _op;
        private bool _autoTrackResolved;
        private bool _curtainActive;
        private int _settleFramesLeft;

        private void OnValidate()
        {
            if (unloadRadius <= loadRadius)
            {
                unloadRadius = loadRadius + 20f;
            }
        }

        private void Start()
        {
            // A domain reload / editor entering play with the region scene still additively loaded
            // from a previous run (or hand-loaded for editing) - adopt it instead of trying to
            // load a duplicate.
            Scene existing = SceneManager.GetSceneByName(sceneName);
            if (existing.IsValid() && existing.isLoaded)
            {
                _state = RegionState.Loaded;
            }
        }

        private void Update()
        {
            ResolveAutoTrackedCharacter();

            switch (_state)
            {
                case RegionState.Unloaded:
                    if (AnyTrackedWithin(loadRadius)) BeginLoad();
                    break;
                case RegionState.Loaded:
                    if (!AnyTrackedWithin(unloadRadius)) BeginUnload();
                    break;
                case RegionState.Loading:
                    // Player rushed into curtain range while the load was still running (it fired
                    // from beyond curtainRadius) - drop the curtain now so the tail of the pop-in
                    // is still covered.
                    if (useLoadCurtain && !_curtainActive && ScreenFader.Instance != null
                        && AnyTrackedWithin(curtainRadius))
                    {
                        ScreenFader.Instance.SetCovered(true, curtainFadeSeconds);
                        _curtainActive = true;
                    }
                    if (_op != null && _op.isDone)
                    {
                        _op = null;
                        if (_curtainActive)
                        {
                            _settleFramesLeft = Mathf.Max(0, curtainSettleFrames);
                            _state = RegionState.Settling;
                        }
                        else
                        {
                            _state = RegionState.Loaded;
                            Log($"'{sceneName}' loaded.");
                        }
                    }
                    break;
                case RegionState.Settling:
                    if (_settleFramesLeft > 0)
                    {
                        _settleFramesLeft--;
                        break;
                    }
                    ScreenFader.Instance?.SetCovered(false, curtainFadeSeconds);
                    _curtainActive = false;
                    _state = RegionState.Loaded;
                    Log($"'{sceneName}' loaded (curtain).");
                    break;
                case RegionState.Unloading:
                    if (_op != null && _op.isDone)
                    {
                        _op = null;
                        _state = RegionState.Unloaded;
                        Resources.UnloadUnusedAssets();
                        Log($"'{sceneName}' unloaded.");
                    }
                    break;
            }
        }

        private void ResolveAutoTrackedCharacter()
        {
            if (_autoTrackResolved || trackedCharacters.Count > 0)
            {
                _autoTrackResolved = true;
                return;
            }

            // Every IInputCommand-driven character, not just the first: this project puts a
            // PlayerInputProvider on BOTH the Player AND the Cat (the Cat is possessable), and
            // FindFirstObjectByType picked whichever loaded first - which was the Cat, so the
            // region never loaded when the actual player character walked up to it. Tracking all
            // of them is also just correct for streaming: whoever the user is driving toward a
            // region should pull it in.
            var providers = FindObjectsByType<PlayerInputProvider>(FindObjectsSortMode.None);
            foreach (var p in providers)
            {
                Transform root = p.transform.root;
                if (!trackedCharacters.Contains(root)) trackedCharacters.Add(root);
            }
            if (trackedCharacters.Count > 0) _autoTrackResolved = true;
        }

        private bool AnyTrackedWithin(float radius)
        {
            float sqr = radius * radius;
            for (int i = 0; i < trackedCharacters.Count; i++)
            {
                Transform t = trackedCharacters[i];
                if (t == null) continue;
                Vector3 d = t.position - anchor;
                d.y = 0f;
                if (d.sqrMagnitude <= sqr) return true;
            }
            return false;
        }

        private void BeginLoad()
        {
            Scene existing = SceneManager.GetSceneByName(sceneName);
            if (existing.IsValid() && existing.isLoaded)
            {
                _state = RegionState.Loaded;
                return;
            }
            _op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (_op == null)
            {
                Debug.LogError($"[MapStreamer] LoadSceneAsync('{sceneName}') returned null - is the scene in Build Settings?", this);
                return;
            }
            _state = RegionState.Loading;

            if (useLoadCurtain && ScreenFader.Instance != null && AnyTrackedWithin(curtainRadius))
            {
                ScreenFader.Instance.SetCovered(true, curtainFadeSeconds);
                _curtainActive = true;
            }
            Log($"loading '{sceneName}'{(_curtainActive ? " (curtain)" : "")}...");
        }

        private void OnDisable()
        {
            // Don't leave the screen black if this streamer is torn down mid-load (scene change,
            // domain reload in play, object destroyed).
            if (_curtainActive)
            {
                ScreenFader.Instance?.SetCovered(false, 0f);
                _curtainActive = false;
            }
        }

        private void BeginUnload()
        {
            Scene existing = SceneManager.GetSceneByName(sceneName);
            if (!existing.IsValid() || !existing.isLoaded)
            {
                _state = RegionState.Unloaded;
                return;
            }
            _op = SceneManager.UnloadSceneAsync(existing);
            _state = RegionState.Unloading;
            Log($"unloading '{sceneName}'...");
        }

        private void Log(string msg)
        {
            if (logStateChanges) Debug.Log($"[MapStreamer] {msg}", this);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 0.3f, 1f);
            DrawFlatRing(anchor, loadRadius);
            Gizmos.color = new Color(0.9f, 0.5f, 0.2f, 1f);
            DrawFlatRing(anchor, unloadRadius);
        }

        private static void DrawFlatRing(Vector3 centre, float radius)
        {
            const int seg = 48;
            Vector3 prev = centre + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= seg; i++)
            {
                float a = (i / (float)seg) * Mathf.PI * 2f;
                Vector3 next = centre + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
