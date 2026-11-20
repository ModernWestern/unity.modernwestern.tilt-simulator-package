using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ModernWestern.TiltSimulator.Tests
{
    public sealed class TiltMathTests
    {
        [Test]
        public void Gravity_MatchesHowThePhoneIsHeld()
        {
            AssertNear(new Vector3(0f, 0f, -1f), TiltMath.GravityOf(HoldPose.Flat, Vector2.zero));

            AssertNear(new Vector3(0f, -1f, 0f), TiltMath.GravityOf(HoldPose.Upright, Vector2.zero));

            var inHand = TiltMath.GravityOf(HoldPose.InHand, Vector2.zero);

            Assert.Less(inHand.y, 0f, inHand.ToString());

            Assert.Less(inHand.z, 0f, inHand.ToString());

            Assert.AreEqual(1f, inHand.magnitude, 1e-4f);
        }

        [Test]
        public void TheTopGoingAway_TurnsTheScreenUp()
        {
            var gravity = TiltMath.GravityOf(HoldPose.Upright, new Vector2(0f, 30f));

            Assert.Less(gravity.z, -0.4f, gravity.ToString());

            Assert.Greater(gravity.y, -0.9f, gravity.ToString());
        }

        [Test]
        public void TheRightSideGoingAway_InTheHand_LowersTheRightSide()
        {
            var gravity = TiltMath.GravityOf(HoldPose.InHand, new Vector2(30f, 0f));

            Assert.Greater(gravity.x, 0.2f, gravity.ToString());
        }

        [Test]
        public void TheAttitude_IsTheIdentity_LyingFlat()
        {
            var attitude = TiltMath.AttitudeOf(HoldPose.Flat, Vector2.zero);

            Assert.Less(Quaternion.Angle(Quaternion.identity, attitude), 0.01f, attitude.eulerAngles.ToString());
        }

        [Test]
        public void TheAttitude_TurnsInThePhonesOwnAxes()
        {
            var rest = TiltMath.AttitudeOf(HoldPose.InHand, Vector2.zero);

            var tipped = TiltMath.AttitudeOf(HoldPose.InHand, new Vector2(20f, 0f));

            var turn = Quaternion.Inverse(rest) * tipped;

            turn.ToAngleAxis(out var angle, out var axis);

            Assert.AreEqual(20f, angle, 0.01f);

            AssertNear(Vector3.up, axis);
        }

        [Test]
        public void TheTurnRate_IsTheTurnOverTheTime()
        {
            var from = TiltMath.OrientationOf(HoldPose.Upright, Vector2.zero);

            var to = TiltMath.OrientationOf(HoldPose.Upright, new Vector2(45f, 0f));

            AssertNear(new Vector3(0f, Mathf.PI / 2f, 0f), TiltMath.AngularVelocityOf(from, to, 0.5f));

            Assert.AreEqual(Vector3.zero, TiltMath.AngularVelocityOf(from, from, 0.5f));
        }

        [TestCase(ScreenOrientation.Portrait)]
        [TestCase(ScreenOrientation.PortraitUpsideDown)]
        [TestCase(ScreenOrientation.LandscapeLeft)]
        [TestCase(ScreenOrientation.LandscapeRight)]
        public void TheInputSystemsCompensation_GivesTheScreensAxesBack(ScreenOrientation orientation)
        {
            var attitude = TiltMath.AttitudeOf(HoldPose.InHand, new Vector2(12f, -7f));

            var device = TiltMath.ToDeviceAxes(attitude, orientation);

            Assert.Less(Quaternion.Angle(attitude, device * TiltMath.RotationCompensation(orientation)), 0.01f);

            var gravity = TiltMath.GravityOf(HoldPose.InHand, new Vector2(12f, -7f));

            AssertNear(gravity, TiltMath.DirectionCompensation(orientation) * TiltMath.ToDeviceAxes(gravity, orientation));
        }

        [Test]
        public void InLandscape_TheDevicesAxes_AreTurnedAQuarter()
        {
            // Held sideways, the screen's right is the device's own up or down.
            var right = TiltMath.ToDeviceAxes(Vector3.right, ScreenOrientation.LandscapeLeft);

            Assert.AreEqual(0f, right.x, 1e-4f);

            Assert.AreEqual(1f, Mathf.Abs(right.y), 1e-4f);
        }

        internal static void AssertNear(Vector3 expected, Vector3 actual)
        {
            Assert.Less(Vector3.Distance(expected, actual), 1e-3f, $"expected {expected}, got {actual}");
        }
    }

    public sealed class TiltSimulatorTests : InputTestFixture
    {
        private bool _enabled;

        private Vector2 _tilt;

        private HoldPose _pose;

        private SimulatedSensors _sensors;

        private bool _compensate;

        public override void Setup()
        {
            base.Setup();

            _enabled = TiltSimulator.Enabled;

            _tilt = TiltSimulator.Tilt;

            _pose = TiltSimulator.Pose;

            _sensors = TiltSimulator.Sensors;

            _compensate = InputSystem.settings.compensateForScreenOrientation;

            TiltSimulator.Pose = HoldPose.InHand;

            TiltSimulator.Sensors = SimulatedSensors.All;

            TiltSimulator.Tilt = Vector2.zero;
        }

        public override void TearDown()
        {
            TiltSimulator.Enabled = _enabled;

            TiltSimulator.Tilt = _tilt;

            TiltSimulator.Pose = _pose;

            TiltSimulator.Sensors = _sensors;

            InputSystem.settings.compensateForScreenOrientation = _compensate;

            base.TearDown();
        }

        [Test]
        public void Enabled_AddsTheSensors_AndAGameReadsThePhone()
        {
            Assert.IsNull(AttitudeSensor.current, "no sensors before");

            TiltSimulator.Enabled = true;

            TiltSimulator.Tilt = new Vector2(15f, 10f);

            Send_Internal();

            Assert.IsNotNull(AttitudeSensor.current);

            Assert.IsNotNull(GravitySensor.current);

            Assert.IsNotNull(UnityEngine.InputSystem.Gyroscope.current);

            Assert.IsNotNull(Accelerometer.current);

            var orientation = Screen.orientation;

            var attitude = AttitudeSensor.current.attitude.ReadValue();

            Assert.Less(Quaternion.Angle(TiltMath.ToDeviceAxes(TiltSimulator.Attitude, orientation), attitude), 0.05f);

            TiltMathTests.AssertNear(TiltMath.ToDeviceAxes(TiltSimulator.Gravity, orientation), GravitySensor.current.gravity.ReadValue());

            TiltMathTests.AssertNear(TiltMath.ToDeviceAxes(TiltSimulator.Gravity, orientation), Accelerometer.current.acceleration.ReadValue());
        }

        [Test]
        public void WithCompensation_TheGameReadsTheScreensAxes()
        {
            InputSystem.settings.compensateForScreenOrientation = true;

            TiltSimulator.Enabled = true;

            TiltSimulator.Tilt = new Vector2(-20f, 5f);

            Send_Internal();

            Assert.Less(Quaternion.Angle(TiltSimulator.Attitude, AttitudeSensor.current.attitude.ReadValue()), 0.05f);

            TiltMathTests.AssertNear(TiltSimulator.Gravity, GravitySensor.current.gravity.ReadValue());
        }

        [Test]
        public void Disabled_RemovesTheSensors()
        {
            TiltSimulator.Enabled = true;

            Send_Internal();

            TiltSimulator.Enabled = false;

            Send_Internal();

            Assert.IsNull(AttitudeSensor.current);

            Assert.IsNull(GravitySensor.current);

            Assert.IsEmpty(InputSystem.devices.ToArray(), "nothing of the simulator stays");
        }

        [Test]
        public void LeavingASensorOut_TestsTheGamesFallback()
        {
            TiltSimulator.Enabled = true;

            TiltSimulator.Sensors = SimulatedSensors.Gravity;

            Send_Internal();

            Assert.IsNull(AttitudeSensor.current, "like a phone without one");

            Assert.IsNotNull(GravitySensor.current);

            Assert.IsNull(UnityEngine.InputSystem.Gyroscope.current);
        }

        [Test]
        public void ASensorTheGameTurnsOff_StopsReading_AndKeepsItsLastValue()
        {
            TiltSimulator.Enabled = true;

            TiltSimulator.Tilt = new Vector2(10f, 0f);

            Send_Internal();

            var before = GravitySensor.current.gravity.ReadValue();

            InputSystem.DisableDevice(GravitySensor.current);

            TiltSimulator.Tilt = new Vector2(-30f, 0f);

            Send_Internal();

            TiltMathTests.AssertNear(before, GravitySensor.current.gravity.ReadValue());
        }

        [Test]
        public void TheSamplingFrequency_IsAnswered_LikeARealSensor()
        {
            TiltSimulator.Enabled = true;

            Send_Internal();

            var gyroscope = UnityEngine.InputSystem.Gyroscope.current;

            Assert.Greater(gyroscope.samplingFrequency, 0f);

            gyroscope.samplingFrequency = 100f;

            Assert.AreEqual(100f, gyroscope.samplingFrequency);
        }

        [Test]
        public void TurningThePhone_ShowsOnTheGyroscope()
        {
            TiltSimulator.Enabled = true;

            Send_Internal();

            TiltSimulator.Tilt = new Vector2(40f, 0f);

            Send_Internal();

            Assert.Greater(UnityEngine.InputSystem.Gyroscope.current.angularVelocity.ReadValue().magnitude, 0.1f);
        }

        [Test]
        public void TippingTheRightSideAway_InTheHand_PullsGravityRight()
        {
            TiltSimulator.Enabled = true;

            TiltSimulator.Tilt = new Vector2(30f, 0f);

            Send_Internal();

            var gravity = TiltMath.DirectionCompensation(Screen.orientation) * GravitySensor.current.gravity.ReadValue();

            Assert.Greater(gravity.x, 0f, gravity.ToString());
        }

        // One editor update of the simulator, then one of the Input System to take the readings in.
        private static void Send_Internal()
        {
            TiltSimulator.Refresh();

            InputSystem.Update();
        }
    }
}
