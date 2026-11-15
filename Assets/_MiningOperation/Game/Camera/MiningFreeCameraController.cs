using UnityEngine;
using UnityEngine.InputSystem;

namespace MiningOperation.CameraSystem
{
    /// <summary>
    /// Independent free-fly camera for Unity 6 (Input System).
    /// Move: WASD + Q/E. Look: hold RMB. Shift: boost. Ctrl: slow.
    /// Mouse wheel adjusts the base flight speed.
    /// Attach to the scene Camera (not to the player's ship).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class MiningFreeCameraController : MonoBehaviour
    {
        [Header("Flight")]
        [SerializeField, Min(0.01f)] private float startingSpeed = 30f;
        [SerializeField, Min(0.01f)] private float minimumSpeed = 0.5f;
        [SerializeField, Min(0.01f)] private float maximumSpeed = 2000f;
        [SerializeField, Min(1f)] private float boostMultiplier = 4f;
        [SerializeField, Range(0.01f, 1f)] private float precisionMultiplier = 0.2f;
        [SerializeField, Min(0f)] private float movementResponsiveness = 12f;
        [SerializeField] private bool requireRightMouseToMove = false;
        [SerializeField] private bool ignoreTimeScale = true;

        [Header("Mouse Look")]
        [SerializeField, Min(0.001f)] private float mouseSensitivity = 0.12f;
        [SerializeField] private bool invertVerticalLook = false;
        [SerializeField] private bool limitPitch = true;
        [SerializeField, Range(1f, 89.9f)] private float maximumPitch = 89f;
        [SerializeField] private bool lockCursorWhileLooking = true;

        [Header("Mouse Wheel: Speed Adjustment")]
        [SerializeField, Min(1.01f)] private float speedChangePerWheelNotch = 1.25f;

        public float CurrentSpeed => _currentSpeed;
        public bool IsLooking => _isLooking;
        // Set false when the tactical map or a menu needs keyboard/mouse input.
        public bool InputEnabled { get; set; } = true;

        private float _currentSpeed;
        private float _yaw;
        private float _pitch;
        private Vector3 _velocity;
        private bool _isLooking;
        private CursorLockMode _previousCursorLock;
        private bool _previousCursorVisible;
        private bool _ownsCursorLock;

        private void Awake()
        {
            _currentSpeed = Mathf.Clamp(startingSpeed, minimumSpeed, maximumSpeed);
            _yaw = transform.eulerAngles.y;
            _pitch = Mathf.DeltaAngle(0f, transform.eulerAngles.x);
            if (limitPitch)
                _pitch = Mathf.Clamp(_pitch, -maximumPitch, maximumPitch);
        }

        private void Update()
        {
            if (!InputEnabled)
            {
                StopLooking();
                _velocity = Vector3.zero;
                return;
            }

            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            if (mouse == null || keyboard == null)
            {
                StopLooking();
                return;
            }

            if (mouse.rightButton.wasPressedThisFrame)
                StartLooking();

            if (_isLooking && !mouse.rightButton.isPressed)
                StopLooking();

            if (_isLooking && !mouse.rightButton.wasPressedThisFrame)
                UpdateRotation(mouse);

            // Unity Input System normally reports ~120 units per wheel notch.
            // Fractional values also work (e.g. precision scrolling / trackpads).
            float scrollY = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scrollY) > 0.001f)
            {
                float wheelNotches = Mathf.Clamp(scrollY / 120f, -10f, 10f);
                _currentSpeed = Mathf.Clamp(
                    _currentSpeed * Mathf.Pow(speedChangePerWheelNotch, wheelNotches),
                    minimumSpeed,
                    maximumSpeed);
            }

            UpdateMovement(keyboard);
        }

        private void StartLooking()
        {
            _isLooking = true;
            if (!lockCursorWhileLooking)
                return;

            _previousCursorLock = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            _ownsCursorLock = true;
        }

        private void UpdateRotation(Mouse mouse)
        {
            Vector2 delta = mouse.delta.ReadValue();
            _yaw += delta.x * mouseSensitivity;
            _pitch += delta.y * mouseSensitivity * (invertVerticalLook ? 1f : -1f);

            if (limitPitch)
                _pitch = Mathf.Clamp(_pitch, -maximumPitch, maximumPitch);

            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        private void UpdateMovement(Keyboard keyboard)
        {
            float deltaTime = ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
            // Avoid a giant jump after an application/editor stall.
            deltaTime = Mathf.Min(deltaTime, 0.05f);
            if (deltaTime <= 0f)
                return;

            Vector3 input = Vector3.zero;
            if (!requireRightMouseToMove || _isLooking)
            {
                if (keyboard.wKey.isPressed) input.z += 1f;
                if (keyboard.sKey.isPressed) input.z -= 1f;
                if (keyboard.dKey.isPressed) input.x += 1f;
                if (keyboard.aKey.isPressed) input.x -= 1f;
                if (keyboard.eKey.isPressed) input.y += 1f;
                if (keyboard.qKey.isPressed) input.y -= 1f;
            }

            // Keep diagonals the same speed as straight flight.
            input = Vector3.ClampMagnitude(input, 1f);

            float speed = _currentSpeed;
            bool boosting = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            bool precision = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
            if (boosting) speed *= boostMultiplier;
            if (precision) speed *= precisionMultiplier;

            // Local-space movement: W/S follow the camera's viewing direction;
            // Q/E are up/down relative to its orientation.
            Vector3 targetVelocity = transform.TransformDirection(input) * speed;
            float blend = movementResponsiveness <= 0f
                ? 1f
                : 1f - Mathf.Exp(-movementResponsiveness * deltaTime);

            _velocity = Vector3.Lerp(_velocity, targetVelocity, blend);
            transform.position += _velocity * deltaTime;
        }

        private void StopLooking()
        {
            _isLooking = false;
            if (!_ownsCursorLock)
                return;

            Cursor.lockState = _previousCursorLock;
            Cursor.visible = _previousCursorVisible;
            _ownsCursorLock = false;
        }

        private void OnDisable()
        {
            StopLooking();
            _velocity = Vector3.zero;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                StopLooking();
        }

        private void OnValidate()
        {
            minimumSpeed = Mathf.Max(0.01f, minimumSpeed);
            maximumSpeed = Mathf.Max(minimumSpeed, maximumSpeed);
            startingSpeed = Mathf.Clamp(startingSpeed, minimumSpeed, maximumSpeed);
            boostMultiplier = Mathf.Max(1f, boostMultiplier);
            mouseSensitivity = Mathf.Max(0.001f, mouseSensitivity);
            speedChangePerWheelNotch = Mathf.Max(1.01f, speedChangePerWheelNotch);
        }
    }
}
