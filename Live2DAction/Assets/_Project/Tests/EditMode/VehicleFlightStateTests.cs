using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Live2DAction.Vehicles;

public class VehicleFlightStateTests
{
    private const float Dt = 0.02f;

    private static VehicleFlightData MakeData()
    {
        var d = ScriptableObject.CreateInstance<VehicleFlightData>();
        SetField(d, "ascendSpeed", 8f);
        SetField(d, "descendSpeed", 8f);
        SetField(d, "verticalSmoothTime", 0.05f);
        SetField(d, "cruiseSpeed", 20f);
        SetField(d, "boostMultiplier", 2f);
        SetField(d, "horizontalSmoothTime", 0.05f);
        SetField(d, "yawSpeedDegrees", 90f);
        SetField(d, "pitchTowardVerticalDegrees", 18f);
        SetField(d, "levelOutSmoothTime", 0.2f);
        SetField(d, "liftoffBoost", 4f);
        SetField(d, "energyDrainPerSecond", 15f);
        SetField(d, "boostExtraDrainPerSecond", 10f);
        SetField(d, "resumeEnergyThreshold", 30f);
        return d;
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo f = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(f, $"no private field '{name}' on {target.GetType().Name}");
        f.SetValue(target, value);
    }

    // helper: one Tick with the common defaults, forward = +Z, well above the ground
    private static VehicleFlightOutput Tick(VehicleFlightState s, bool fly, bool descend = false, bool boost = false,
        float throttle = 0f, float steer = 0f, Vector3? vel = null, bool grounded = false, float height = 999f,
        float energy = 500f, bool hasMeter = true, bool canControl = true)
    {
        return s.Tick(Dt, canControl, fly, descend, boost, throttle, steer, vel ?? Vector3.zero, Vector3.forward,
            grounded, height, energy, hasMeter);
    }

    [Test]
    public void NotFlying_NoFlyHeld_StaysGrounded()
    {
        var s = new VehicleFlightState(MakeData());
        var o = Tick(s, fly: false);
        Assert.IsFalse(o.IsFlying);
        Assert.IsFalse(s.IsFlying);
    }

    [Test]
    public void FlyHeld_WithReserve_Engages_AndSeedsUpwardVelocity()
    {
        var s = new VehicleFlightState(MakeData());
        var o = Tick(s, fly: true);
        Assert.IsTrue(o.IsFlying);
        Assert.Greater(o.LinearVelocity.y, 0f, "should get an upward liftoff kick");
    }

    [Test]
    public void FlyHeld_BelowResumeThreshold_DoesNotEngage()
    {
        var s = new VehicleFlightState(MakeData());
        var o = Tick(s, fly: true, energy: 10f); // threshold is 30
        Assert.IsFalse(o.IsFlying);
    }

    [Test]
    public void NoEnergyMeter_AlwaysAllowedToEngage()
    {
        var s = new VehicleFlightState(MakeData());
        var o = Tick(s, fly: true, energy: 0f, hasMeter: false);
        Assert.IsTrue(o.IsFlying);
    }

    [Test]
    public void Flying_ReleasingFly_HoversInsteadOfEnding()
    {
        var s = new VehicleFlightState(MakeData());
        Tick(s, fly: true);                       // engage
        var o = Tick(s, fly: false, grounded: false); // released, still airborne
        Assert.IsTrue(o.IsFlying, "flight persists once engaged while airborne");
    }

    [Test]
    public void Flying_HoverTarget_VerticalVelocityEasesTowardZero()
    {
        var s = new VehicleFlightState(MakeData());
        Tick(s, fly: true);                       // engage (vy seeded to +4)
        VehicleFlightOutput o = default;
        for (int i = 0; i < 40; i++) o = Tick(s, fly: false); // hover
        Assert.AreEqual(0f, o.LinearVelocity.y, 0.2f);
    }

    [Test]
    public void Flying_AscendTarget_ClimbsAtAscendSpeed()
    {
        var s = new VehicleFlightState(MakeData());
        VehicleFlightOutput o = default;
        for (int i = 0; i < 60; i++) o = Tick(s, fly: true);
        Assert.AreEqual(8f, o.LinearVelocity.y, 0.3f);
    }

    [Test]
    public void Flying_DescendHeld_DivesNegative()
    {
        var s = new VehicleFlightState(MakeData());
        Tick(s, fly: true); // engage
        VehicleFlightOutput o = default;
        for (int i = 0; i < 60; i++) o = Tick(s, fly: false, descend: true);
        Assert.Less(o.LinearVelocity.y, -1f);
    }

    [Test]
    public void Flying_Throttle_ThrustsAlongChassisForward()
    {
        var s = new VehicleFlightState(MakeData());
        Tick(s, fly: true); // engage
        VehicleFlightOutput o = default;
        for (int i = 0; i < 60; i++) o = Tick(s, fly: false, throttle: 1f);
        Assert.AreEqual(20f, o.LinearVelocity.z, 0.5f);
        Assert.AreEqual(0f, o.LinearVelocity.x, 0.01f);
    }

    [Test]
    public void Flying_BoostHeld_RaisesCruiseAndDrain()
    {
        var s = new VehicleFlightState(MakeData());
        Tick(s, fly: true); // engage
        VehicleFlightOutput o = default;
        for (int i = 0; i < 60; i++) o = Tick(s, fly: false, boost: true, throttle: 1f);
        Assert.AreEqual(40f, o.LinearVelocity.z, 1f, "cruise * boostMultiplier");
        Assert.AreEqual((15f + 10f) * Dt, o.EnergyToDrain, 0.0001f);
    }

    [Test]
    public void Flying_SteerRight_YawsPositive()
    {
        var s = new VehicleFlightState(MakeData());
        Tick(s, fly: true);
        var o = Tick(s, fly: true, steer: 1f);
        Assert.AreEqual(90f * Dt, o.YawDeltaDegrees, 0.0001f);
    }

    [Test]
    public void Flying_GroundedAndFlyReleased_Lands()
    {
        var s = new VehicleFlightState(MakeData());
        Tick(s, fly: true);
        var o = Tick(s, fly: false, grounded: true);
        Assert.IsFalse(o.IsFlying);
        Assert.IsTrue(o.JustEnded);
    }

    [Test]
    public void Flying_DescendedWithinLandingClearance_AndFlyReleased_Lands()
    {
        var s = new VehicleFlightState(MakeData()); // landingClearance default = 1.6
        Tick(s, fly: true);
        var o = Tick(s, fly: false, descend: true, grounded: false, height: 1.0f);
        Assert.IsFalse(o.IsFlying, "hovering low + not climbing = land, no wheel contact needed");
        Assert.IsTrue(o.JustEnded);
    }

    [Test]
    public void Flying_LowButHoldingFly_DoesNotLand()
    {
        var s = new VehicleFlightState(MakeData());
        Tick(s, fly: true);
        var o = Tick(s, fly: true, grounded: false, height: 0.5f); // skimming low, still climbing
        Assert.IsTrue(o.IsFlying, "holding Ctrl aborts the landing");
    }

    [Test]
    public void Flying_GroundedButStillHoldingFly_DoesNotLand()
    {
        var s = new VehicleFlightState(MakeData());
        Tick(s, fly: true);
        var o = Tick(s, fly: true, grounded: true); // e.g. clipped a hill mid-climb
        Assert.IsTrue(o.IsFlying);
    }

    [Test]
    public void Flying_EnergyHitsZero_EndsFlight()
    {
        var s = new VehicleFlightState(MakeData());
        Tick(s, fly: true);
        var o = Tick(s, fly: true, grounded: false, energy: 0f);
        Assert.IsFalse(o.IsFlying);
        Assert.IsTrue(o.JustEnded);
    }

    [Test]
    public void NullData_NeverFlies()
    {
        var s = new VehicleFlightState(null);
        var o = s.Tick(Dt, true, true, false, false, 0f, 0f, Vector3.zero, Vector3.forward, false, 999f, 500f, true);
        Assert.IsFalse(o.IsFlying);
    }

    [Test]
    public void LosingControlMidFlight_EndsFlightImmediately()
    {
        var s = new VehicleFlightState(MakeData());
        Tick(s, fly: true);                               // engage
        var o = Tick(s, fly: true, grounded: false, canControl: false); // dismounted mid-air
        Assert.IsFalse(o.IsFlying);
        Assert.IsTrue(o.JustEnded);
    }
}
