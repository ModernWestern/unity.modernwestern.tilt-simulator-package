using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace ModernWestern.TiltSimulator
{
    /// <summary>
    /// The motion sensors the simulator can add to the Input System.
    /// </summary>
    [Flags]
    public enum SimulatedSensors
    {
        None = 0,

        /// <summary><see cref="AttitudeSensor"/>: the phone's orientation.</summary>
        Attitude = 1 << 0,

        /// <summary><see cref="GravitySensor"/>: gravity alone, in g.</summary>
        Gravity = 1 << 1,

        /// <summary><see cref="UnityEngine.InputSystem.Gyroscope"/>: the turn rate, in radians per second.</summary>
        Gyroscope = 1 << 2,

        /// <summary><see cref="Accelerometer"/>: the acceleration, in g; the phone is held still, so it is gravity.</summary>
        Accelerometer = 1 << 3,

        All = Attitude | Gravity | Gyroscope | Accelerometer
    }

    /// <summary>
    /// The virtual sensor devices: adds and removes them in the Input System, keeps them current, and answers the
    /// sampling frequency commands a real sensor answers. Their names start with <see cref="NamePrefix"/>.
    /// </summary>
    internal static class SimulatedSensorDevices
    {
        public const string NamePrefix = "TiltSimulator";

        private const float DefaultFrequency = 60f;

        private static readonly FourCC QuerySamplingFrequency = new('S', 'M', 'P', 'L');

        private static readonly FourCC SetSamplingFrequency = new('S', 'S', 'P', 'L');

        private static float _frequency = DefaultFrequency;

        public static AttitudeSensor Attitude => Find_Internal<AttitudeSensor>(SimulatedSensors.Attitude);

        public static GravitySensor Gravity => Find_Internal<GravitySensor>(SimulatedSensors.Gravity);

        public static UnityEngine.InputSystem.Gyroscope Gyroscope => Find_Internal<UnityEngine.InputSystem.Gyroscope>(SimulatedSensors.Gyroscope);

        public static Accelerometer Accelerometer => Find_Internal<Accelerometer>(SimulatedSensors.Accelerometer);

        /// <summary>
        /// Adds the sensors in <paramref name="wanted"/> that are missing, removes the others the simulator added, and
        /// any duplicate a domain reload may leave.
        /// </summary>
        public static void Sync(SimulatedSensors wanted)
        {
            Hook_Internal(wanted != SimulatedSensors.None);

            Sync_Internal<AttitudeSensor>(SimulatedSensors.Attitude, wanted);

            Sync_Internal<GravitySensor>(SimulatedSensors.Gravity, wanted);

            Sync_Internal<UnityEngine.InputSystem.Gyroscope>(SimulatedSensors.Gyroscope, wanted);

            Sync_Internal<Accelerometer>(SimulatedSensors.Accelerometer, wanted);
        }

        /// <summary>
        /// Sends a reading to every simulated sensor, in the device's own axes, and makes each one the current device of
        /// its kind.
        /// </summary>
        public static void Send(Quaternion attitude, Vector3 gravity, Vector3 angularVelocity)
        {
            Send_Internal(Attitude, device => InputSystem.QueueDeltaStateEvent(device.attitude, attitude));

            Send_Internal(Gravity, device => InputSystem.QueueDeltaStateEvent(device.gravity, gravity));

            Send_Internal(Gyroscope, device => InputSystem.QueueDeltaStateEvent(device.angularVelocity, angularVelocity));

            Send_Internal(Accelerometer, device => InputSystem.QueueDeltaStateEvent(device.acceleration, gravity));
        }

        /// <summary>
        /// Whether <paramref name="device"/> is one of the simulator's.
        /// </summary>
        public static bool IsSimulated(InputDevice device)
        {
            return device != null && device.name.StartsWith(NamePrefix, StringComparison.Ordinal);
        }

        private static string NameOf_Internal(SimulatedSensors sensor)
        {
            return NamePrefix + sensor;
        }

        private static T Find_Internal<T>(SimulatedSensors sensor) where T : InputDevice
        {
            var name = NameOf_Internal(sensor);

            foreach (var device in InputSystem.devices)
            {
                if (device is T typed && device.name.StartsWith(name, StringComparison.Ordinal))
                {
                    return typed;
                }
            }

            return null;
        }

        private static void Sync_Internal<T>(SimulatedSensors sensor, SimulatedSensors wanted) where T : InputDevice
        {
            var name = NameOf_Internal(sensor);

            var keep = (wanted & sensor) != 0;

            InputDevice kept = null;

            // Copy first: removing changes the list.
            foreach (var device in InputSystem.devices.ToArray())
            {
                if (device is not T || !device.name.StartsWith(name, StringComparison.Ordinal))
                {
                    continue;
                }

                if (keep && kept == null)
                {
                    kept = device;

                    continue;
                }

                InputSystem.RemoveDevice(device);
            }

            if (keep && kept == null)
            {
                InputSystem.AddDevice<T>(name);
            }
        }

        private static void Send_Internal<T>(T device, Action<T> send) where T : InputDevice
        {
            if (device == null)
            {
                return;
            }

            device.MakeCurrent();

            if (device.enabled)
            {
                send(device);
            }
        }

        // Removed first every time: the Input System can be swapped under the simulator (a domain reload, a test's
        // InputTestFixture), which drops the callbacks it had.
        private static unsafe void Hook_Internal(bool on)
        {
            InputSystem.onDeviceCommand -= Command_Internal;

            if (on)
            {
                InputSystem.onDeviceCommand += Command_Internal;
            }
        }

        // A real sensor answers its sampling frequency; a virtual one would make Sensor.samplingFrequency throw.
        private static unsafe long? Command_Internal(InputDevice device, InputDeviceCommand* command)
        {
            if (!IsSimulated(device))
            {
                return null;
            }

            var payload = (float*)((byte*)command + InputDeviceCommand.BaseCommandSize);

            if (command->type == QuerySamplingFrequency)
            {
                *payload = _frequency;

                return InputDeviceCommand.GenericSuccess;
            }

            if (command->type == SetSamplingFrequency)
            {
                _frequency = *payload > 0f ? *payload : DefaultFrequency;

                return InputDeviceCommand.GenericSuccess;
            }

            return null;
        }
    }
}
