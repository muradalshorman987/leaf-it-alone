using FallenYard.Core;
using FallenYard.Settings;
using UnityEngine;

namespace FallenYard.Player
{
    /// <summary>
    /// CharacterController-based first-person walker with mouse look, optional
    /// head bob and a sprint FOV kick. Sensitivity, invert Y, FOV and head bob
    /// follow <see cref="SettingsManager"/> when one is in the scene.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        [Tooltip("Usually the Camera transform, child of this object at eye height.")]
        public Transform cameraPivot;

        [Header("Movement")]
        public float walkSpeed = 4f;
        public float sprintSpeed = 7f;
        public float jumpHeight = 0.9f;
        public float gravity = -20f;

        [Header("Look")]
        public float lookSensitivity = 2f;
        public float maxPitch = 85f;
        [Tooltip("Flip vertical mouse look.")]
        public bool invertY;
        [Tooltip("Accessibility: press sprint once to run, again (or stop moving) to walk.")]
        public bool toggleSprint;

        [Header("Camera feel")]
        [Tooltip("Gentle camera bob while walking. Off for motion-sensitive players.")]
        public bool headBobEnabled = true;
        [Tooltip("Bob depth in metres at full walking speed.")]
        public float headBobAmount = 0.035f;
        [Tooltip("Metres per footstep (longer = slower bob).")]
        public float stepLength = 0.8f;
        [Tooltip("Field of view in degrees while walking. 0 = keep the camera's own value.")]
        public float baseFov = 0f;
        [Tooltip("Extra FOV in degrees at full sprint.")]
        public float sprintFovKick = 6f;
        [Tooltip("How quickly FOV and bob settle (higher = snappier).")]
        public float cameraSharpness = 8f;

        public float CurrentSpeed { get; private set; }

        CharacterController _cc;
        Camera _cam;
        Vector3 _pivotRestPos;
        float _pitch;
        float _verticalVelocity;
        float _bobPhase;
        float _bobIntensity;
        readonly HoldToggle _sprint = new HoldToggle();

        /// <summary>True while sprinting (held, or toggled on).</summary>
        public bool IsSprinting => _sprint.Active;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (cameraPivot == null)
            {
                var cam = GetComponentInChildren<Camera>();
                if (cam != null) cameraPivot = cam.transform;
            }
            if (cameraPivot != null)
            {
                _cam = cameraPivot.GetComponent<Camera>();
                _pivotRestPos = cameraPivot.localPosition;
            }
            if (_cam != null && baseFov <= 0f) baseFov = _cam.fieldOfView;
        }

        void OnEnable() => SettingsManager.Changed += ApplySettings;
        void OnDisable() => SettingsManager.Changed -= ApplySettings;

        void Start()
        {
            if (SettingsManager.Instance != null && SettingsManager.Instance.Current != null)
                ApplySettings(SettingsManager.Instance.Current);
            else if (GameManager.Instance != null)
                lookSensitivity = GameManager.Instance.Data.mouseSensitivity;
            SetCursorLocked(true);
        }

        /// <summary>Takes sensitivity, invert Y, FOV and head bob from the player's settings.</summary>
        public void ApplySettings(GameSettings s)
        {
            if (s == null) return;
            lookSensitivity = s.mouseSensitivity;
            invertY = s.invertY;
            headBobEnabled = s.headBob;
            toggleSprint = s.toggleSprint;
            if (s.fieldOfView > 0f) baseFov = s.fieldOfView;
        }

        public static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void Update()
        {
            bool blocked = GameState.InputBlocked;

            if (!blocked && cameraPivot != null)
            {
                Vector2 look = GameInput.Look * lookSensitivity;
                transform.Rotate(0f, look.x, 0f, Space.Self);
                _pitch = Mathf.Clamp(_pitch + (invertY ? look.y : -look.y), -maxPitch, maxPitch);
                cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }

            Vector2 input = blocked ? Vector2.zero : GameInput.Move;
            float speed = UpdateSprint(!blocked && GameInput.Sprint, !blocked && GameInput.SprintPressed, input) ? sprintSpeed : walkSpeed;
            Vector3 move = (transform.right * input.x + transform.forward * input.y) * speed;
            CurrentSpeed = move.magnitude;

            if (_cc.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
            if (!blocked && _cc.isGrounded && GameInput.JumpPressed)
                _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            _verticalVelocity += gravity * Time.deltaTime;

            _cc.Move((move + Vector3.up * _verticalVelocity) * Time.deltaTime);
            UpdateCameraFeel(Time.deltaTime);
        }

        /// <summary>Hold or toggle sprint. Toggle mode switches off when the player stops moving. Public for tests.</summary>
        public bool UpdateSprint(bool held, bool pressedThisFrame, Vector2 moveInput)
        {
            _sprint.ToggleMode = toggleSprint;
            bool active = _sprint.Update(held, pressedThisFrame);
            if (toggleSprint && active && moveInput.sqrMagnitude < 0.01f && !pressedThisFrame)
            {
                _sprint.Reset();
                active = false;
            }
            return active;
        }

        void UpdateCameraFeel(float dt)
        {
            if (cameraPivot == null) return;

            bool grounded = _cc.isGrounded;
            float targetBob = headBobEnabled && grounded ? Mathf.Clamp01(CurrentSpeed / Mathf.Max(0.1f, walkSpeed)) : 0f;
            _bobIntensity = CameraFeel.Damp(_bobIntensity, targetBob, cameraSharpness, dt);
            _bobPhase = CameraFeel.AdvancePhase(_bobPhase, CurrentSpeed, dt, stepLength);
            cameraPivot.localPosition = _pivotRestPos + CameraFeel.BobOffset(_bobPhase, headBobAmount, _bobIntensity);

            if (_cam != null && baseFov > 0f)
            {
                float target = CameraFeel.TargetFov(baseFov, sprintFovKick, CurrentSpeed, walkSpeed, sprintSpeed);
                _cam.fieldOfView = CameraFeel.Damp(_cam.fieldOfView, target, cameraSharpness, dt);
            }
        }
    }
}
