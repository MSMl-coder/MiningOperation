using UnityEngine;

namespace MiningOperation.Asteroids
{
    /// <summary>
    /// Test-only free camera. Supports old Input Manager controls when enabled;
    /// on Input-System-only projects uses explicit OnGUI repeat buttons instead.
    /// No dependency on the Input System assembly.
    /// </summary>
    public sealed class AsteroidTestFlightCamera : MonoBehaviour
    {
        [Min(1f)] public float speedMetersPerSecond = 300f;
        public float acceleratedMultiplier = 7f;
        public float lookSensitivity = 1.8f;
        private bool faster;

        private void Update()
        {
            #if ENABLE_LEGACY_INPUT_MANAGER
            float rate = speedMetersPerSecond * (Input.GetKey(KeyCode.LeftShift) ? acceleratedMultiplier : 1f);
            Vector3 motion = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) motion += Vector3.forward;
            if (Input.GetKey(KeyCode.S)) motion += Vector3.back;
            if (Input.GetKey(KeyCode.A)) motion += Vector3.left;
            if (Input.GetKey(KeyCode.D)) motion += Vector3.right;
            if (Input.GetKey(KeyCode.E)) motion += Vector3.up;
            if (Input.GetKey(KeyCode.Q)) motion += Vector3.down;
            transform.position += transform.TransformDirection(motion.normalized) * rate * Time.unscaledDeltaTime;
            if (Input.GetMouseButton(1))
            {
                transform.Rotate(Vector3.up, Input.GetAxis("Mouse X") * lookSensitivity, Space.World);
                transform.Rotate(Vector3.right, -Input.GetAxis("Mouse Y") * lookSensitivity, Space.Self);
            }
            #endif
        }

        private void OnGUI()
        {
            float w = 192f;
            float x = Screen.width - w - 12f;
            float y = Screen.height - 205f;
            float dt = Mathf.Min(0.04f, Time.unscaledDeltaTime);
            float step = speedMetersPerSecond * (faster ? acceleratedMultiplier : 1f) * dt;
            GUI.Box(new Rect(x - 8, y - 24, w + 16, 228), "CAMERA / TEST");
            if (GUI.RepeatButton(new Rect(x + 66, y, 60, 27), "▲")) transform.position += transform.forward * step;
            if (GUI.RepeatButton(new Rect(x + 66, y + 30, 60, 27), "▼")) transform.position -= transform.forward * step;
            if (GUI.RepeatButton(new Rect(x, y + 30, 60, 27), "◀")) transform.position -= transform.right * step;
            if (GUI.RepeatButton(new Rect(x + 132, y + 30, 60, 27), "▶")) transform.position += transform.right * step;
            if (GUI.RepeatButton(new Rect(x, y + 64, 93, 27), "UP")) transform.position += Vector3.up * step;
            if (GUI.RepeatButton(new Rect(x + 98, y + 64, 94, 27), "DOWN")) transform.position -= Vector3.up * step;
            if (GUI.RepeatButton(new Rect(x, y + 98, 60, 27), "YAW ◀")) transform.Rotate(Vector3.up, -35f * dt, Space.World);
            if (GUI.RepeatButton(new Rect(x + 132, y + 98, 60, 27), "YAW ▶")) transform.Rotate(Vector3.up, 35f * dt, Space.World);
            if (GUI.RepeatButton(new Rect(x, y + 130, 93, 27), "PITCH ▲")) transform.Rotate(Vector3.right, -35f * dt, Space.Self);
            if (GUI.RepeatButton(new Rect(x + 98, y + 130, 94, 27), "PITCH ▼")) transform.Rotate(Vector3.right, 35f * dt, Space.Self);
            faster = GUI.Toggle(new Rect(x + 4, y + 163, 180, 26), faster, "FAST x" + acceleratedMultiplier.ToString("F0"));
        }
    }
}
