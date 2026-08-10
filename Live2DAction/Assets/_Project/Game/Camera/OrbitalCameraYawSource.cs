using UnityEngine;
using Unity.Cinemachine;

namespace Live2DAction.CameraSystem
{
    // Exposes CinemachineOrbitalFollow's raw Horizontal orbit angle as a yaw value driven
    // only by look input (mouse/right stick via CinemachineInputAxisController) - never by
    // the camera's composed aim, which reactively sweeps as its target translates and is
    // therefore unsafe to use as a movement-direction reference (see ICameraYawSource).
    [RequireComponent(typeof(CinemachineOrbitalFollow))]
    public class OrbitalCameraYawSource : MonoBehaviour, ICameraYawSource
    {
        private CinemachineOrbitalFollow _orbitalFollow;

        public float YawDegrees => _orbitalFollow.HorizontalAxis.Value;

        private void Awake()
        {
            _orbitalFollow = GetComponent<CinemachineOrbitalFollow>();
        }
    }
}
