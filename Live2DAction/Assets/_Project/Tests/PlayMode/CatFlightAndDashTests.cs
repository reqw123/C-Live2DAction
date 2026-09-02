using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Live2DAction.Characters;
using Live2DAction.Core;
using Live2DAction.Input;
using Object = UnityEngine.Object;

// 2026-08-29, explicit user request ("讓貓就有飛行和衝刺功能 參考player"). Flight / flight-boost /
// ground-dash all already live in CharacterMovement; CatCharacterSetup just wires a flight-energy
// pool + a DodgeData asset onto the cat (see that setup's WireFlightAndDash). The one genuinely
// cat-specific risk is that the cat's Visual is scaled to 0.45 - CharacterMovement's own
// TryGetGroundNormal comments flag lossyScale hazards - so this fixture builds a 0.45-scaled rig
// and confirms both features still behave (lift off / drain energy / stay airborne / dash-displace)
// at that scale. Runs the real Update loop, same reasoning as DodgeMovementTests /
// CharacterMovementTests (CharacterController.Move only displaces under Unity's own tick).
public class CatFlightAndDashTests
{
    private class StubInputBehaviour : MonoBehaviour, IInputCommand
    {
        public Vector2 MoveInput { get; set; }
        public bool AttackPressed { get; set; }
        public bool DodgePressed { get; set; }
        public bool LockOnPressed { get; set; }
        public bool JumpPressed { get; set; }
        public bool UltimatePressed { get; set; }
        public bool FlyPressed { get; set; }
        public bool FlyDescendPressed { get; set; }
        public bool BoostPressed { get; set; }
        public bool AimPressed { get; set; }
        public bool FirePressed { get; set; }
        public bool ViewTogglePressed { get; set; }
        public bool ZoomInPressed { get; set; }
        public bool ZoomOutPressed { get; set; }
    }

    private GameObject _cat;
    private GameObject _camera;
    private StubInputBehaviour _input;
    private CharacterMovement _movement;
    private UltimateEnergy _flightEnergy;

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Expected a private field named '{fieldName}' on {target.GetType().Name}");
        field.SetValue(target, value);
    }

    private static DodgeData CreateCatDodgeData()
    {
        // Mirrors CatCharacterSetup's values (distance 3 - matched to the player 2026-08-29 after
        // "空中沒法衝刺", 12/12/20 frames).
        var data = ScriptableObject.CreateInstance<DodgeData>();
        SetField(data, "distance", 3f);
        SetField(data, "durationFrames", 12);
        SetField(data, "invulnerabilityFrames", 12);
        SetField(data, "cooldownFrames", 20);
        return data;
    }

    [SetUp]
    public void SetUp()
    {
        // See CharacterMovementTests.SetUp - wipe every root so a scene another fixture loaded
        // can't leave colliders/cameras behind.
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            Object.DestroyImmediate(root);
        }

        _camera = new GameObject("TestMainCamera");
        _camera.tag = "MainCamera";
        _camera.AddComponent<Camera>();
        _camera.transform.rotation = Quaternion.identity;

        _cat = new GameObject("Cat");
        // The real cat scales its "Visual" child, not the root - but scaling the root here is a
        // strictly harder case for CharacterController math, so if flight/dash survive this they
        // survive the real setup too.
        _cat.transform.localScale = Vector3.one * 0.45f;
        _cat.transform.position = new Vector3(0f, 5f, 0f); // airborne, no ground needed

        CharacterController controller = _cat.AddComponent<CharacterController>();
        controller.height = 0.76f;
        controller.radius = 0.2f;
        controller.minMoveDistance = 0f; // see CharacterMovementTests.SetUp

        _input = _cat.AddComponent<StubInputBehaviour>();

        _flightEnergy = _cat.AddComponent<UltimateEnergy>();
        SetField(_flightEnergy, "maxEnergy", 500f);
        SetField(_flightEnergy, "regenAmount", 30f);
        SetField(_flightEnergy, "regenIntervalSeconds", 1f);
        SetField(_flightEnergy, "regenIdleDelaySeconds", 3f);
        _flightEnergy.AddEnergy(500f); // UltimateEnergy starts empty; fill it so flight can start

        _movement = _cat.AddComponent<CharacterMovement>();
        SetField(_movement, "inputSource", _input);
        SetField(_movement, "flightEnergy", _flightEnergy);
        SetField(_movement, "dodgeData", CreateCatDodgeData());
        SetField(_movement, "flightMoveSpeed", 7f);
        SetField(_movement, "flightAscendSpeed", 5f);
        SetField(_movement, "flightDescendSpeed", 5f);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_cat);
        Object.DestroyImmediate(_camera);
    }

    private IEnumerator RunForSeconds(float seconds)
    {
        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < seconds)
        {
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator HoldingFly_AtCatScale_LiftsOffAndDrainsFlightEnergy()
    {
        float startY = _cat.transform.position.y;

        _input.FlyPressed = true;
        yield return RunForSeconds(0.6f);

        Assert.IsTrue(_movement.IsFlying, "Holding Ctrl (FlyPressed) with a full flight-energy pool should enter flight");
        Assert.Less(_flightEnergy.CurrentEnergy, 500f, "Flight should drain the flight-energy pool");
        Assert.Greater(_cat.transform.position.y, startY, "Ascending flight should gain altitude even at 0.45 scale");
    }

    [UnityTest]
    public IEnumerator ReleasingFly_WhileAirborne_KeepsHovering()
    {
        _input.FlyPressed = true;
        yield return RunForSeconds(0.4f);
        Assert.IsTrue(_movement.IsFlying, "Test precondition: should be flying before releasing the key");

        _input.FlyPressed = false;
        yield return RunForSeconds(0.4f);

        // CharacterMovement's flight has a deliberately sticky exit: releasing the key hovers,
        // it doesn't end flight (only landing / empty energy does) - see UpdateFlightState.
        Assert.IsTrue(_movement.IsFlying, "Releasing Ctrl while still airborne should keep hovering, not drop out of flight");
    }

    [UnityTest]
    public IEnumerator TappingDodge_AtCatScale_DashesAndBecomesInvulnerable()
    {
        SetField(_movement, "gravity", 0f); // isolate the dash from falling
        Vector3 start = _cat.transform.position;

        _input.DodgePressed = true;
        yield return null;
        _input.DodgePressed = false;

        Assert.AreEqual(DodgePhase.Dodging, _movement.CurrentDodgePhase, "Tapping Shift should start a dash");
        Assert.IsTrue(_movement.IsDodgeInvulnerable, "The dash window should be invulnerable");

        // Dash is 12 frames (0.2s) + 20 frames (0.33s) cooldown = ~0.53s; comfortable margin over that.
        yield return RunForSeconds(0.9f);

        float travelled = Vector3.Distance(_cat.transform.position, start);
        Assert.Greater(travelled, 0.3f, "The dash should visibly displace the cat");
        Assert.AreEqual(DodgePhase.Idle, _movement.CurrentDodgePhase, "The dash should return to Idle after its cooldown");
    }
}
