using UnityEngine;

namespace Live2DAction.CameraSystem
{
    // 2026-08-28, explicit user request ("要能保存守望者視角中攝影機的變更設置") - a persistable
    // home pose for the Watcher view. While watching, W/A/S/D + mouse fly the whole 守望者 rig
    // around; pressing ViewFocusDirector.commitViewKey bakes the current pose into THIS asset
    // (ScriptableObject writes survive exiting Play Mode, unlike scene-transform edits), and every
    // later FocusWatcher() starts from it instead of the scene's authored Viewpoint.
    //
    // hasSavedView = false (the default, and what WatcherSetup leaves a freshly-created asset as)
    // means "ignore this asset, use the scene's authored Viewpoint" - so an un-tuned project
    // behaves exactly as if the asset weren't wired at all. Uncheck it (or delete the asset) to
    // go back to the authored framing.
    [CreateAssetMenu(fileName = "WatcherViewConfig", menuName = "Live2DAction/Watcher View Config")]
    public class WatcherViewConfig : ScriptableObject
    {
        [Tooltip("When true, ViewFocusDirector seeds the Watcher view from the values below instead of the scene's Viewpoint transform. Set by the commit key at runtime; uncheck to revert to the authored framing.")]
        public bool hasSavedView;

        [Tooltip("World position of the 守望者 root when the view was committed.")]
        public Vector3 rootPosition;

        [Tooltip("World Y rotation (degrees) of the 守望者 root - also the camera yaw.")]
        public float rootYaw;

        [Tooltip("Camera pitch (degrees) - the root itself stays upright, this is camera-only.")]
        public float cameraPitch;

        [Tooltip("Field of view committed with the view. 0 = leave the director's own watcherFieldOfView in charge.")]
        public float fieldOfView;
    }
}
