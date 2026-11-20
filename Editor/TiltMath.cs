using UnityEngine;

namespace ModernWestern.TiltSimulator
{
    /// <summary>
    /// How the virtual phone is held before it is tilted.
    /// </summary>
    public enum HoldPose
    {
        /// <summary>In the hand, as when playing: the top leans back 40° from upright.</summary>
        InHand,

        /// <summary>Upright, the screen facing the player, like a phone on a stand.</summary>
        Upright,

        /// <summary>Lying flat on a table, face up.</summary>
        Flat
    }

    /// <summary>
    /// The math of the virtual phone, with no editor state: its attitude, the gravity it feels, its turn rate, and the
    /// turn from the screen's axes to the device's own ones that the Input System undoes.
    /// <para>
    /// Vectors are in the device's frame as the Input System reports it: x to the right of the screen, y to its top, z
    /// out of the screen toward the player. Gravity is in g (1 = 9.81 m/s²), so a phone lying face up reads (0, 0, -1).
    /// The attitude is the identity with the phone lying flat, face up.
    /// </para>
    /// </summary>
    public static class TiltMath
    {
        /// <summary>
        /// How far the top leans back from upright in <see cref="HoldPose.InHand"/>, in degrees.
        /// </summary>
        public const float InHandLean = 40f;

        private const float HalfSqrtTwo = 0.70710678f;

        /// <summary>
        /// A tilt in degrees as a turn in the device's frame: x turns about the screen's vertical (the right side goes
        /// away from the player when positive), y about its horizontal (the top goes away when positive).
        /// </summary>
        public static Quaternion TurnOf(Vector2 tilt)
        {
            return Quaternion.AngleAxis(tilt.x, Vector3.up) * Quaternion.AngleAxis(-tilt.y, Vector3.right);
        }

        /// <summary>
        /// The phone's orientation in a world where y is up and the player stands on the +z side, before the attitude
        /// sensor's reference is applied: upright facing the player is the identity.
        /// </summary>
        public static Quaternion OrientationOf(HoldPose pose, Vector2 tilt)
        {
            return PoseTurn_Internal(pose) * TurnOf(tilt);
        }

        /// <summary>
        /// The attitude sensor's reading for the pose and tilt, in the screen's axes: the identity with the phone lying
        /// flat, face up.
        /// </summary>
        public static Quaternion AttitudeOf(HoldPose pose, Vector2 tilt)
        {
            return Quaternion.Inverse(PoseTurn_Internal(HoldPose.Flat)) * OrientationOf(pose, tilt);
        }

        /// <summary>
        /// The gravity the phone feels for the pose and tilt, in g and in the screen's axes.
        /// </summary>
        public static Vector3 GravityOf(HoldPose pose, Vector2 tilt)
        {
            return Quaternion.Inverse(OrientationOf(pose, tilt)) * Vector3.down;
        }

        /// <summary>
        /// The turn rate, in radians per second about the screen's axes, that takes the phone from
        /// <paramref name="from"/> to <paramref name="to"/> in <paramref name="seconds"/>.
        /// </summary>
        public static Vector3 AngularVelocityOf(Quaternion from, Quaternion to, float seconds)
        {
            if (seconds <= 0f)
            {
                return Vector3.zero;
            }

            var turn = Quaternion.Inverse(from) * to;

            turn.ToAngleAxis(out var angle, out var axis);

            if (angle > 180f)
            {
                angle -= 360f;
            }

            if (angle == 0f || float.IsNaN(axis.x) || float.IsInfinity(axis.x))
            {
                return Vector3.zero;
            }

            return axis * (angle * Mathf.Deg2Rad / seconds);
        }

        /// <summary>
        /// An attitude in the screen's axes as the device reports it, in its own (natural) axes: the inverse of the
        /// Input System's <c>CompensateRotation</c> processor, so a game that compensates for the screen's orientation
        /// reads the screen's attitude back.
        /// </summary>
        public static Quaternion ToDeviceAxes(Quaternion screenAttitude, ScreenOrientation orientation)
        {
            return screenAttitude * Quaternion.Inverse(RotationCompensation(orientation));
        }

        /// <summary>
        /// A vector in the screen's axes as the device reports it, in its own (natural) axes: the inverse of the Input
        /// System's <c>CompensateDirection</c> processor.
        /// </summary>
        public static Vector3 ToDeviceAxes(Vector3 screenVector, ScreenOrientation orientation)
        {
            return Quaternion.Inverse(DirectionCompensation(orientation)) * screenVector;
        }

        /// <summary>
        /// The turn the Input System's <c>CompensateRotation</c> processor applies (on the right) for an orientation.
        /// </summary>
        public static Quaternion RotationCompensation(ScreenOrientation orientation)
        {
            return orientation switch
            {
                ScreenOrientation.PortraitUpsideDown => new Quaternion(0f, 0f, 1f, 0f),
                ScreenOrientation.LandscapeLeft => new Quaternion(0f, 0f, HalfSqrtTwo, -HalfSqrtTwo),
                ScreenOrientation.LandscapeRight => new Quaternion(0f, 0f, -HalfSqrtTwo, -HalfSqrtTwo),
                _ => Quaternion.identity
            };
        }

        /// <summary>
        /// The turn the Input System's <c>CompensateDirection</c> processor applies (on the left) for an orientation.
        /// </summary>
        public static Quaternion DirectionCompensation(ScreenOrientation orientation)
        {
            return orientation switch
            {
                ScreenOrientation.PortraitUpsideDown => Quaternion.Euler(0f, 0f, 180f),
                ScreenOrientation.LandscapeLeft => Quaternion.Euler(0f, 0f, 90f),
                ScreenOrientation.LandscapeRight => Quaternion.Euler(0f, 0f, 270f),
                _ => Quaternion.identity
            };
        }

        private static Quaternion PoseTurn_Internal(HoldPose pose)
        {
            return pose switch
            {
                HoldPose.InHand => TurnOf(new Vector2(0f, InHandLean)),
                HoldPose.Flat => TurnOf(new Vector2(0f, 90f)),
                _ => Quaternion.identity
            };
        }
    }
}
