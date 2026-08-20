namespace Live2DAction.CameraSystem
{
    // A yaw value driven only by explicit look input (e.g. mouse), never by anything the
    // player character itself does. See CharacterMovement's use of this for why: reading
    // the camera's fully-composed Transform.forward is NOT safe for computing movement
    // direction, because a "look at" camera's aim reactively sweeps as its target
    // translates sideways past it - which then feeds back into movement direction, which
    // feeds back into the camera's aim, and so on. The camera's raw orbital angle has no
    // such feedback path.
    public interface ICameraYawSource
    {
        float YawDegrees { get; }

        // 2026-08-20, flight system design (Docs/FLIGHT_SYSTEM_DESIGN.md) - lets
        // CharacterMovement read how far down the camera is currently looking, for the
        // dive-speed-boost condition (needs both "holding descend" AND "looking down past a
        // threshold" - see that doc's own 2.4). Positive = looking down, same sign convention
        // ThirdPersonCameraController's own _pitch already uses.
        float PitchDegrees { get; }
    }
}
