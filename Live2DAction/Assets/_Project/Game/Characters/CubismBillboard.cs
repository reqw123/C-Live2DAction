using UnityEngine;

namespace Live2DAction.Characters
{
    // Keeps a Live2D standee facing the camera (yaw only, so it never tilts or flips
    // upside down as the third-person camera orbits above/below it).
    public class CubismBillboard : MonoBehaviour
    {
        // Cubism models are normally authored to face -Z in an unrotated scene (visible to
        // a camera looking down +Z), so the default math points the model's front at the
        // camera under that assumption. Flip this if the imported model turns out to face
        // backwards - this can only be confirmed by looking at it in the Editor.
        [SerializeField] private bool faceAwayInstead;

        private void LateUpdate()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            Vector3 toCamera = mainCamera.transform.position - transform.position;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Vector3 facingDirection = faceAwayInstead ? toCamera : -toCamera;
            transform.rotation = Quaternion.LookRotation(facingDirection, Vector3.up);
        }
    }
}
