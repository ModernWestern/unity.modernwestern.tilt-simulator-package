using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ModernWestern.TiltSimulator
{
    /// <summary>
    /// The window of the <see cref="TiltSimulator"/>: drag on the pad to tilt the phone (right: its right side goes
    /// away; up: its top goes away), or use the arrow keys with the window focused. Rest puts it back in its pose,
    /// Wiggle rocks it side to side once and Nod tips it back and forth once.
    /// </summary>
    public sealed class TiltSimulatorWindow : EditorWindow
    {
        private const float KeyStep = 5f;

        private const float GestureSeconds = 1.6f;

        private const float GestureAngle = 30f;

        private const string OpenedKey = "TiltSimulator.Opened";

        private static readonly Color PadColor = new(0.16f, 0.16f, 0.16f);

        private static readonly Color Accent = new(0.55f, 0.42f, 0.95f);

        private double _gestureStart = -1;

        private Vector2 _gestureAxis;

        private Vector2 _scroll;

        [MenuItem("Window/Analysis/Tilt Simulator")]
        public static void Open()
        {
            var window = GetWindow<TiltSimulatorWindow>();

            window.titleContent = new GUIContent("Tilt Simulator");

            window.minSize = new Vector2(280f, 420f);

            window.Show();
        }

        private void OnEnable()
        {
            // The first time the window opens in an editor session, it turns the phone on: that is why it was opened.
            if (!SessionState.GetBool(OpenedKey, false))
            {
                SessionState.SetBool(OpenedKey, true);

                TiltSimulator.Enabled = true;
            }

            EditorApplication.update += Tick_Internal;

            TiltSimulator.Changed += Repaint;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick_Internal;

            TiltSimulator.Changed -= Repaint;
        }

        private void Tick_Internal()
        {
            if (_gestureStart >= 0)
            {
                var t = (float)((EditorApplication.timeSinceStartup - _gestureStart) / GestureSeconds);

                if (t >= 1f)
                {
                    _gestureStart = -1;

                    TiltSimulator.Rest();
                }
                else
                {
                    TiltSimulator.Tilt = _gestureAxis * (Mathf.Sin(t * Mathf.PI * 2f) * GestureAngle);
                }
            }

            // The readings change in Play mode (the game turns sensors on and off): keep them fresh.
            if (TiltSimulator.Enabled && (EditorApplication.isPlaying || _gestureStart >= 0))
            {
                Repaint();
            }
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            TiltSimulator.Enabled = EditorGUILayout.ToggleLeft("Simulate the phone's sensors", TiltSimulator.Enabled, EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(!TiltSimulator.Enabled))
            {
                TiltSimulator.Pose = (HoldPose)EditorGUILayout.EnumPopup("Held", TiltSimulator.Pose);

                TiltSimulator.Sensors = (SimulatedSensors)EditorGUILayout.EnumFlagsField("Sensors", TiltSimulator.Sensors);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Rest"))
                    {
                        _gestureStart = -1;

                        TiltSimulator.Rest();
                    }

                    if (GUILayout.Button("Wiggle"))
                    {
                        StartGesture_Internal(Vector2.right);
                    }

                    if (GUILayout.Button("Nod"))
                    {
                        StartGesture_Internal(Vector2.up);
                    }
                }

                EditorGUILayout.Space(6f);

                var size = Mathf.Clamp(position.width - 24f, 120f, 320f);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();

                    Pad_Internal(GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size)));

                    GUILayout.FlexibleSpace();
                }

                Keys_Internal();

                Readings_Internal();
            }

            EditorGUILayout.EndScrollView();
        }

        private void StartGesture_Internal(Vector2 axis)
        {
            _gestureAxis = axis;

            _gestureStart = EditorApplication.timeSinceStartup;
        }

        private void Pad_Internal(Rect pad)
        {
            var e = Event.current;

            var id = GUIUtility.GetControlID(FocusType.Passive, pad);

            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown when pad.Contains(e.mousePosition):
                    GUIUtility.hotControl = id;

                    _gestureStart = -1;

                    TiltSimulator.Tilt = TiltAt_Internal(pad, e.mousePosition);

                    e.Use();

                    break;

                case EventType.MouseDrag when GUIUtility.hotControl == id:
                    TiltSimulator.Tilt = TiltAt_Internal(pad, e.mousePosition);

                    e.Use();

                    break;

                case EventType.MouseUp when GUIUtility.hotControl == id:
                    GUIUtility.hotControl = 0;

                    e.Use();

                    break;

                case EventType.Repaint:
                    Draw_Internal(pad);

                    break;
            }
        }

        private void Keys_Internal()
        {
            var e = Event.current;

            if (e.type != EventType.KeyDown)
            {
                return;
            }

            var step = e.keyCode switch
            {
                KeyCode.LeftArrow => new Vector2(-KeyStep, 0f),
                KeyCode.RightArrow => new Vector2(KeyStep, 0f),
                KeyCode.UpArrow => new Vector2(0f, KeyStep),
                KeyCode.DownArrow => new Vector2(0f, -KeyStep),
                _ => Vector2.zero
            };

            if (step == Vector2.zero)
            {
                return;
            }

            _gestureStart = -1;

            TiltSimulator.Tilt += step;

            e.Use();
        }

        private void Readings_Internal()
        {
            var tilt = TiltSimulator.Tilt;

            EditorGUILayout.Space(4f);

            EditorGUILayout.LabelField("Tilt", $"{tilt.x:+0;-0;0}° sideways, {tilt.y:+0;-0;0}° forward");

            Reading_Internal("Attitude", SimulatedSensorDevices.Attitude, TiltSimulator.Attitude.eulerAngles.ToString("0"));

            Reading_Internal("Gravity", SimulatedSensorDevices.Gravity, TiltSimulator.Gravity.ToString("0.00"));

            Reading_Internal("Gyroscope", SimulatedSensorDevices.Gyroscope, TiltSimulator.AngularVelocity.ToString("0.00"));

            Reading_Internal("Accelerometer", SimulatedSensorDevices.Accelerometer, TiltSimulator.Gravity.ToString("0.00"));

            EditorGUILayout.HelpBox("Values in the screen's axes: x right, y up, z toward the player; gravity in g, the " +
                                    "turn rate in rad/s. (off): the game has turned that sensor off.", MessageType.None);
        }

        private static void Reading_Internal(string label, InputDevice device, string value)
        {
            var text = device == null ? "not simulated" : device.enabled ? value : $"{value}  (off)";

            EditorGUILayout.LabelField(label, text);
        }

        private static Vector2 TiltAt_Internal(Rect pad, Vector2 mouse)
        {
            var x = Mathf.Clamp((mouse.x - pad.center.x) / (pad.width * 0.5f), -1f, 1f);

            var y = Mathf.Clamp((pad.center.y - mouse.y) / (pad.height * 0.5f), -1f, 1f);

            return new Vector2(x, y) * TiltSimulator.MaxTilt;
        }

        // The pad, its cross and the phone drawn in perspective, seen from the player.
        private static void Draw_Internal(Rect pad)
        {
            EditorGUI.DrawRect(pad, PadColor);

            Handles.color = new Color(1f, 1f, 1f, 0.12f);

            Handles.DrawLine(new Vector3(pad.center.x, pad.yMin), new Vector3(pad.center.x, pad.yMax));

            Handles.DrawLine(new Vector3(pad.xMin, pad.center.y), new Vector3(pad.xMax, pad.center.y));

            var on = TiltSimulator.Enabled;

            var orientation = TiltMath.OrientationOf(TiltSimulator.Pose, TiltSimulator.Tilt);

            // The game view's shape: a landscape game is held sideways.
            var game = Handles.GetMainGameViewSize();

            var landscape = game.x > game.y;

            var half = landscape ? new Vector2(0.34f, 0.19f) * pad.width : new Vector2(0.19f, 0.34f) * pad.width;

            var distance = pad.width * 1.8f;

            var outline = Project_Internal(pad, orientation, distance, new[]
            {
                new Vector3(-half.x, -half.y), new Vector3(half.x, -half.y), new Vector3(half.x, half.y), new Vector3(-half.x, half.y)
            });

            var inset = half - Vector2.one * (pad.width * 0.025f);

            var screen = Project_Internal(pad, orientation, distance, new[]
            {
                new Vector3(-inset.x, -inset.y), new Vector3(inset.x, -inset.y), new Vector3(inset.x, inset.y), new Vector3(-inset.x, inset.y)
            });

            // Seen from behind when the screen faces away.
            var facing = (orientation * Vector3.forward).z > 0f;

            Handles.color = on ? new Color(0.08f, 0.08f, 0.08f) : new Color(0.25f, 0.25f, 0.25f);

            Handles.DrawAAConvexPolygon(outline);

            if (facing)
            {
                Handles.color = on ? new Color(0.78f, 0.8f, 0.76f) : new Color(0.4f, 0.4f, 0.4f);

                Handles.DrawAAConvexPolygon(screen);
            }

            // The top edge and the right edge, to tell which way the phone tips.
            Handles.color = on ? Accent : new Color(0.45f, 0.45f, 0.45f);

            Handles.DrawAAPolyLine(4f, outline[2], outline[3]);

            Handles.color = new Color(1f, 1f, 1f, on ? 0.55f : 0.25f);

            Handles.DrawAAPolyLine(3f, outline[1], outline[2]);

            var tilt = TiltSimulator.Tilt / TiltSimulator.MaxTilt;

            var dot = new Vector2(pad.center.x + tilt.x * pad.width * 0.5f, pad.center.y - tilt.y * pad.height * 0.5f);

            EditorGUI.DrawRect(new Rect(dot.x - 3f, dot.y - 3f, 6f, 6f), on ? Accent : Color.gray);
        }

        private static Vector3[] Project_Internal(Rect pad, Quaternion orientation, float distance, Vector3[] corners)
        {
            var points = new Vector3[corners.Length];

            for (var i = 0; i < corners.Length; i++)
            {
                var p = orientation * corners[i];

                // The player is on +z: nearer corners look bigger.
                var perspective = distance / (distance - p.z);

                points[i] = new Vector3(pad.center.x + p.x * perspective, pad.center.y - p.y * perspective);
            }

            return points;
        }
    }
}
