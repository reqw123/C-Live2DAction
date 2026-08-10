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
    }
}
