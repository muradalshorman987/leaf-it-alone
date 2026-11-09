using FallenYard.Core;
using UnityEngine;

namespace FallenYard.Player
{
    /// <summary>Simple CharacterController-based first-person walker with mouse look.</summary>
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

        public float CurrentSpeed { get; private set; }

        CharacterController _cc;
        float _pitch;
        float _verticalVelocity;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (cameraPivot == null)
            {
                var cam = GetComponentInChildren<Camera>();
                if (cam != null) cameraPivot = cam.transform;
            }
        }

        void Start()
        {
            if (GameManager.Instance != null)
                lookSensitivity = GameManager.Instance.Data.mouseSensitivity;
            SetCursorLocked(true);
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
                _pitch = Mathf.Clamp(_pitch - look.y, -maxPitch, maxPitch);
                cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }

            Vector2 input = blocked ? Vector2.zero : GameInput.Move;
            float speed = GameInput.Sprint ? sprintSpeed : walkSpeed;
            Vector3 move = (transform.right * input.x + transform.forward * input.y) * speed;
            CurrentSpeed = move.magnitude;

            if (_cc.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
            if (!blocked && _cc.isGrounded && GameInput.JumpPressed)
                _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            _verticalVelocity += gravity * Time.deltaTime;

            _cc.Move((move + Vector3.up * _verticalVelocity) * Time.deltaTime);
        }
    }
}
