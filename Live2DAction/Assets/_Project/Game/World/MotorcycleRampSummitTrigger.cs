using UnityEngine;
using Live2DAction.Vehicles;

namespace Live2DAction.World
{
    // 2026-09-12, user request ("希望可以駕駛摩托車到梯子頂端-小牛坐墊前的交會點時...觸發動畫") - a small
    // trigger zone sitting exactly where CampGardenHauler_Ramp's top meets CampGardenHauler_
    // SeatPlatform. Reuses MotorcycleMountFX's own swoop-camera flourish (see that class's
    // PlayFlourish, extracted specifically for this) rather than building a second camera/coroutine
    // system - this is the same "wide angle swooping into the real driving camera" effect, just
    // fired by reaching a place instead of by mounting.
    [RequireComponent(typeof(Collider))]
    public class MotorcycleRampSummitTrigger : MonoBehaviour
    {
        [SerializeField] private VehicleController motorcycleController;
        [SerializeField] private MotorcycleMountFX mountFx;
        [SerializeField] private Vector3 flourishStartLocalOffset = new Vector3(8f, 4f, -6f);
        [SerializeField] private float flourishSeconds = 1.2f;

        private bool _inZone;

        private void Reset()
        {
            var c = GetComponent<Collider>();
            if (c != null) c.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_inZone || !IsMotorcycle(other)) return;
            _inZone = true;
            if (motorcycleController != null && motorcycleController.enabled && mountFx != null)
                mountFx.PlayFlourish(flourishStartLocalOffset, flourishSeconds);
        }

        private void OnTriggerExit(Collider other)
        {
            if (IsMotorcycle(other)) _inZone = false; // re-armed for the next time it drives through
        }

        private bool IsMotorcycle(Collider other) =>
            motorcycleController != null && other.transform.root == motorcycleController.transform.root;
    }
}
