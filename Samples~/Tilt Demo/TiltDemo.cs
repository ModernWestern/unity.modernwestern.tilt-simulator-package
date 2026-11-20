using UnityEngine;
using UnityEngine.InputSystem;

namespace ModernWestern.TiltSimulator.Samples
{
    /// <summary>
    /// A scene that reacts to the phone's motion sensors, as a game would: hills that shift with the tilt (parallax)
    /// and a marble that rolls on a tray. It reads the Input System's sensors only, so it runs the same on a phone and
    /// in the editor with the Tilt Simulator (Window > Analysis > Tilt Simulator). Everything is built in code.
    /// </summary>
    public sealed class TiltDemo : MonoBehaviour
    {
        [Tooltip("How fast the rest follows the way the phone is held, in seconds: tilts are measured from it.")]
        [SerializeField] private float restFollow = 2.5f;

        [Tooltip("How far the nearest layer shifts at a full tilt, in world units.")]
        [SerializeField] private float parallax = 1.6f;

        [Tooltip("How hard the marble rolls for a full tilt, in world units per second squared.")]
        [SerializeField] private float roll = 14f;

        private static readonly Color Paper = new(0.957f, 0.953f, 0.973f);

        private static readonly Color Ink = new(0.169f, 0.145f, 0.251f);

        private static readonly Color Accent = new(0.486f, 0.361f, 1f);

        private readonly Transform[] _layers = new Transform[4];

        private readonly float[] _depths = { 0.12f, 0.3f, 0.6f, 1f };

        private Transform _marble;

        private Vector2 _marbleVelocity;

        private Vector3 _rest;

        private bool _hasRest;

        private Vector3 _gravity;

        private const float TrayRadius = 1.5f;

        private const float MarbleRadius = 0.28f;

        private static readonly Vector2 TrayCenter = new(0f, -2.9f);

        private void OnEnable()
        {
            // On a phone the sensors start off: a game turns on the ones it reads.
            Enable_Internal(GravitySensor.current);

            Enable_Internal(Accelerometer.current);

            Enable_Internal(AttitudeSensor.current);

            Enable_Internal(UnityEngine.InputSystem.Gyroscope.current);

            InputSystem.onDeviceChange += DeviceChanged_Internal;
        }

        private void OnDisable()
        {
            InputSystem.onDeviceChange -= DeviceChanged_Internal;
        }

        private void Start()
        {
            Build_Internal();
        }

        private void Update()
        {
            if (!TryGravity_Internal(out var gravity))
            {
                gravity = new Vector3(0f, -0.77f, -0.64f);
            }

            _gravity = gravity;

            if (!_hasRest)
            {
                _rest = gravity;

                _hasRest = true;
            }

            // The rest drifts toward how the phone is held now: tilts are always measured from there.
            _rest = Vector3.Lerp(_rest, gravity, 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, restFollow)));

            var tilt = new Vector2(gravity.x - _rest.x, gravity.y - _rest.y);

            for (var i = 0; i < _layers.Length; i++)
            {
                // Farther layers shift less; the near ones slide toward the side that goes down.
                _layers[i].localPosition = new Vector3(tilt.x, tilt.y, 0f) * (parallax * _depths[i]);
            }

            Roll_Internal(tilt);
        }

        private void Roll_Internal(Vector2 pull)
        {
            var dt = Time.deltaTime;

            _marbleVelocity += pull * (roll * dt);

            _marbleVelocity *= Mathf.Exp(-dt * 0.8f);

            var local = (Vector2)_marble.localPosition - TrayCenter + _marbleVelocity * dt;

            var limit = TrayRadius - MarbleRadius;

            if (local.magnitude > limit)
            {
                var normal = local.normalized;

                local = normal * limit;

                var along = Vector2.Dot(_marbleVelocity, normal);

                if (along > 0f)
                {
                    _marbleVelocity -= normal * (along * 1.5f);
                }
            }

            _marble.localPosition = new Vector3(TrayCenter.x + local.x, TrayCenter.y + local.y, -0.1f);
        }

        // Gravity in the screen's axes: x right, y up, z toward the player, in g.
        private static bool TryGravity_Internal(out Vector3 gravity)
        {
            if (GravitySensor.current is { enabled: true } gravitySensor)
            {
                gravity = gravitySensor.gravity.ReadValue();
            }
            else if (Accelerometer.current is { enabled: true } accelerometer)
            {
                // Held still, the accelerometer feels gravity alone.
                gravity = accelerometer.acceleration.ReadValue();
            }
            else
            {
                gravity = Vector3.zero;
            }

            if (gravity.sqrMagnitude < 1e-4f)
            {
                return false;
            }

            // The sensors report the device's own axes; turn them to the screen's, as the Input System does with
            // compensateForScreenOrientation.
            if (!InputSystem.settings.compensateForScreenOrientation)
            {
                gravity = Screen.orientation switch
                {
                    ScreenOrientation.PortraitUpsideDown => Quaternion.Euler(0f, 0f, 180f),
                    ScreenOrientation.LandscapeLeft => Quaternion.Euler(0f, 0f, 90f),
                    ScreenOrientation.LandscapeRight => Quaternion.Euler(0f, 0f, 270f),
                    _ => Quaternion.identity
                } * gravity;
            }

            return true;
        }

        private static void DeviceChanged_Internal(InputDevice device, InputDeviceChange change)
        {
            if (change == InputDeviceChange.Added && device is Sensor)
            {
                Enable_Internal(device);
            }
        }

        private static void Enable_Internal(InputDevice device)
        {
            if (device != null && !device.enabled)
            {
                InputSystem.EnableDevice(device);
            }
        }

        private void OnGUI()
        {
            var scale = Mathf.Max(1f, Screen.height / 540f);

            var style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(13 * scale), normal = { textColor = Ink } };

            var text = GravitySensor.current == null && Accelerometer.current == null
                ? "No motion sensors. In the editor: Window > Analysis > Tilt Simulator."
                : $"gravity {_gravity.x:+0.00;-0.00} {_gravity.y:+0.00;-0.00} {_gravity.z:+0.00;-0.00} g";

            GUI.Label(new Rect(14f * scale, 10f * scale, Screen.width, 30f * scale), text, style);
        }

        private void Build_Internal()
        {
            var camera = Camera.main;

            if (camera != null)
            {
                camera.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);

                camera.orthographic = true;

                camera.orthographicSize = 5f;

                camera.clearFlags = CameraClearFlags.SolidColor;

                camera.backgroundColor = Paper;
            }

            var sun = Layer_Internal("Far", 0);

            Shape_Internal(sun, "Sun", Circle_Internal(0.85f, 64), Accent, new Vector3(1.4f, 2.7f, 4f));

            Shape_Internal(Layer_Internal("Hills", 1), "Hills", Hills_Internal(1.3f, 0.8f, 0.55f, 1.3f), Color.Lerp(Paper, Ink, 0.22f), new Vector3(0f, 0f, 3f));

            Shape_Internal(Layer_Internal("Middle", 2), "Hills", Hills_Internal(0.1f, 0.6f, 0.8f, 2.1f), Color.Lerp(Paper, Ink, 0.45f), new Vector3(0f, 0f, 2f));

            var near = Layer_Internal("Near", 3);

            Shape_Internal(near, "Hills", Hills_Internal(-1.2f, 0.45f, 1.1f, 0.4f), Color.Lerp(Paper, Ink, 0.75f), new Vector3(0f, 0f, 1f));

            var tray = new GameObject("Tray").transform;

            tray.SetParent(transform, false);

            Shape_Internal(tray, "Rim", Circle_Internal(TrayRadius + 0.08f, 96), Ink, new Vector3(TrayCenter.x, TrayCenter.y, 0.2f));

            Shape_Internal(tray, "Floor", Circle_Internal(TrayRadius, 96), Paper, new Vector3(TrayCenter.x, TrayCenter.y, 0.1f));

            _marble = Shape_Internal(tray, "Marble", Circle_Internal(MarbleRadius, 48), Accent, new Vector3(TrayCenter.x, TrayCenter.y, -0.1f));

            Shape_Internal(_marble, "Shine", Circle_Internal(MarbleRadius * 0.3f, 24), new Color(1f, 1f, 1f, 0.8f), new Vector3(-0.08f, 0.09f, -0.01f));
        }

        private Transform Layer_Internal(string name, int index)
        {
            var layer = new GameObject(name).transform;

            layer.SetParent(transform, false);

            _layers[index] = layer;

            return layer;
        }

        private static Transform Shape_Internal(Transform parent, string name, Mesh mesh, Color color, Vector3 position)
        {
            var shape = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));

            shape.transform.SetParent(parent, false);

            shape.transform.localPosition = position;

            shape.GetComponent<MeshFilter>().sharedMesh = mesh;

            var material = new Material(Shader.Find("Sprites/Default")) { color = color };

            shape.GetComponent<MeshRenderer>().sharedMaterial = material;

            return shape.transform;
        }

        private static Mesh Circle_Internal(float radius, int segments)
        {
            var vertices = new Vector3[segments + 1];

            var triangles = new int[segments * 3];

            for (var i = 0; i < segments; i++)
            {
                var angle = i * Mathf.PI * 2f / segments;

                vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

                triangles[i * 3] = 0;

                triangles[i * 3 + 1] = (i + 1) % segments + 1;

                triangles[i * 3 + 2] = i + 1;
            }

            return new Mesh { vertices = vertices, triangles = triangles };
        }

        // A band of rolling hills, wider than the view so the parallax never shows its ends.
        private static Mesh Hills_Internal(float height, float amplitude, float frequency, float phase)
        {
            const int steps = 120;

            const float halfWidth = 14f;

            var vertices = new Vector3[(steps + 1) * 2];

            var triangles = new int[steps * 6];

            for (var i = 0; i <= steps; i++)
            {
                var x = -halfWidth + 2f * halfWidth * i / steps;

                var y = height + amplitude * (Mathf.Sin(x * frequency + phase) * 0.7f + Mathf.Sin(x * frequency * 2.3f + phase * 1.7f) * 0.3f);

                vertices[i * 2] = new Vector3(x, y);

                vertices[i * 2 + 1] = new Vector3(x, -12f);

                if (i == steps)
                {
                    continue;
                }

                var t = i * 6;

                triangles[t] = i * 2;

                triangles[t + 1] = i * 2 + 2;

                triangles[t + 2] = i * 2 + 1;

                triangles[t + 3] = i * 2 + 1;

                triangles[t + 4] = i * 2 + 2;

                triangles[t + 5] = i * 2 + 3;
            }

            return new Mesh { vertices = vertices, triangles = triangles };
        }
    }
}
