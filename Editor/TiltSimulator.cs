using System;
using UnityEditor;
using UnityEngine;

namespace ModernWestern.TiltSimulator
{
    /// <summary>
    /// A virtual phone for the editor. While it is <see cref="Enabled"/>, the Input System gets virtual motion sensors
    /// (<see cref="Sensors"/>) that read the phone as it is held (<see cref="Pose"/>) and tilted (<see cref="Tilt"/>),
    /// every editor frame, in Play mode and out of it. A game reads them like a phone's: <c>AttitudeSensor.current</c>,
    /// <c>GravitySensor.current</c>, <c>Gyroscope.current</c>, <c>Accelerometer.current</c>.
    /// <para>
    /// The readings are in the device's own axes, as a phone sends them: with the Input System's
    /// <c>compensateForScreenOrientation</c> on, or turning them by <c>Screen.orientation</c> itself, a game gets the
    /// screen's axes back in every orientation (the Device Simulator's rotation included).
    /// </para>
    /// <para>Scripts and editor tests can drive it too. The state lasts until the editor closes.</para>
    /// </summary>
    [InitializeOnLoad]
    public static class TiltSimulator
    {
        /// <summary>
        /// The largest tilt on each axis, in degrees.
        /// </summary>
        public const float MaxTilt = 60f;

        private const string EnabledKey = "TiltSimulator.Enabled";

        private const string TiltXKey = "TiltSimulator.TiltX";

        private const string TiltYKey = "TiltSimulator.TiltY";

        private const string PoseKey = "TiltSimulator.Pose";

        private const string SensorsKey = "TiltSimulator.Sensors";

        private static bool _enabled;

        private static Vector2 _tilt;

        private static HoldPose _pose;

        private static SimulatedSensors _sensors;

        private static Quaternion _lastOrientation = Quaternion.identity;

        private static double _lastTime = -1;

        static TiltSimulator()
        {
            _enabled = SessionState.GetBool(EnabledKey, false);

            _tilt = new Vector2(SessionState.GetFloat(TiltXKey, 0f), SessionState.GetFloat(TiltYKey, 0f));

            _pose = (HoldPose)EditorPrefs.GetInt(PoseKey, (int)HoldPose.InHand);

            _sensors = (SimulatedSensors)EditorPrefs.GetInt(SensorsKey, (int)SimulatedSensors.All);

            // Devices only change in the editor's update: right after a domain reload the Input System is still
            // bringing back the ones it had, and adding them here would leave duplicates.
            EditorApplication.update += Update_Internal;
        }

        /// <summary>
        /// Raised when any setting of the virtual phone changes.
        /// </summary>
        public static event Action Changed;

        /// <summary>
        /// Whether the virtual sensors exist. Off, the simulator removes them.
        /// </summary>
        public static bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled == value)
                {
                    return;
                }

                _enabled = value;

                SessionState.SetBool(EnabledKey, value);

                Apply_Internal();
            }
        }

        /// <summary>
        /// The tilt from the pose, in degrees, up to <see cref="MaxTilt"/> on each axis: x turns the phone about the
        /// screen's vertical (its right side goes away from the player when positive), y about the screen's horizontal
        /// (its top goes away when positive).
        /// </summary>
        public static Vector2 Tilt
        {
            get => _tilt;
            set
            {
                value = new Vector2(Mathf.Clamp(value.x, -MaxTilt, MaxTilt), Mathf.Clamp(value.y, -MaxTilt, MaxTilt));

                if (_tilt == value)
                {
                    return;
                }

                _tilt = value;

                SessionState.SetFloat(TiltXKey, value.x);

                SessionState.SetFloat(TiltYKey, value.y);

                Apply_Internal();
            }
        }

        /// <summary>
        /// How the phone is held before it is tilted.
        /// </summary>
        public static HoldPose Pose
        {
            get => _pose;
            set
            {
                if (_pose == value)
                {
                    return;
                }

                _pose = value;

                EditorPrefs.SetInt(PoseKey, (int)value);

                Apply_Internal();
            }
        }

        /// <summary>
        /// Which sensors the phone has. Leaving one out tests a game's fallback, as on phones without it.
        /// </summary>
        public static SimulatedSensors Sensors
        {
            get => _sensors;
            set
            {
                value &= SimulatedSensors.All;

                if (_sensors == value)
                {
                    return;
                }

                _sensors = value;

                EditorPrefs.SetInt(SensorsKey, (int)value);

                Apply_Internal();
            }
        }

        /// <summary>
        /// The attitude the phone reads now, in the screen's axes: the identity with the phone lying flat, face up.
        /// </summary>
        public static Quaternion Attitude => TiltMath.AttitudeOf(_pose, _tilt);

        /// <summary>
        /// The gravity the phone feels now, in g and in the screen's axes.
        /// </summary>
        public static Vector3 Gravity => TiltMath.GravityOf(_pose, _tilt);

        /// <summary>
        /// The phone's turn rate on the last update, in radians per second about the screen's axes.
        /// </summary>
        public static Vector3 AngularVelocity { get; private set; }

        /// <summary>
        /// Puts the phone back in its pose, untilted.
        /// </summary>
        public static void Rest()
        {
            Tilt = Vector2.zero;
        }

        /// <summary>
        /// Adds or removes the virtual sensors and sends the readings now, instead of on the next editor update.
        /// </summary>
        public static void Refresh()
        {
            Update_Internal();
        }

        private static void Apply_Internal()
        {
            Changed?.Invoke();
        }

        private static void Update_Internal()
        {
            SimulatedSensorDevices.Sync(_enabled ? _sensors : SimulatedSensors.None);

            var now = EditorApplication.timeSinceStartup;

            var orientation = TiltMath.OrientationOf(_pose, _tilt);

            AngularVelocity = _lastTime >= 0 ? TiltMath.AngularVelocityOf(_lastOrientation, orientation, (float)(now - _lastTime)) : Vector3.zero;

            _lastOrientation = orientation;

            _lastTime = now;

            if (!_enabled)
            {
                return;
            }

            var screen = Screen.orientation;

            SimulatedSensorDevices.Send(TiltMath.ToDeviceAxes(Attitude, screen), TiltMath.ToDeviceAxes(Gravity, screen),
                TiltMath.ToDeviceAxes(AngularVelocity, screen));
        }
    }
}
