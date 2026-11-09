using UnityEngine;

namespace FallenYard.Tools
{
    /// <summary>
    /// Simple procedural animation for first-person tool models until real
    /// animations exist: sway against mouse look, bob while walking, and a
    /// per-tool effort pose driven by the tool's throttle (rake leans into the
    /// stroke, blower/vacuum buzz, mulcher shakes).
    /// Put it on the view-model root (child of the camera).
    /// </summary>
    public class ToolViewModelAnimator : MonoBehaviour
    {
        public enum EffortStyle { Lean, Buzz, Shake }

        [Tooltip("The tool whose throttle drives the effort pose.")]
        public LeafTool tool;
        [Tooltip("How the model reacts to effort.")]
        public EffortStyle style = EffortStyle.Lean;

        [Header("Sway")]
        [Tooltip("Degrees of sway per unit of mouse look.")]
        public float swayAmount = 1.5f;
        [Tooltip("Largest sway in degrees.")]
        public float maxSway = 6f;
        [Tooltip("How fast the model settles back, per second.")]
        public float swaySmoothing = 10f;

        [Header("Walk bob")]
        [Tooltip("Bob height in metres at walking speed.")]
        public float bobAmount = 0.012f;
        [Tooltip("Bob cycles per metre walked.")]
        public float bobFrequency = 0.9f;
        [Tooltip("Optional: reads CurrentSpeed from the player for walk bob.")]
        public Player.FirstPersonController player;

        [Header("Effort")]
        [Tooltip("Lean: degrees of forward tilt at full stroke.")]
        public float leanDegrees = 12f;
        [Tooltip("Buzz/Shake: offset amplitude in metres at full throttle.")]
        public float vibration = 0.004f;
        [Tooltip("Buzz/Shake: vibration frequency in Hz.")]
        public float vibrationHz = 38f;

        Vector3 _restPos;
        Quaternion _restRot;
        Vector2 _sway;
        float _bobPhase;
        float _effort;

        void Awake()
        {
            _restPos = transform.localPosition;
            _restRot = transform.localRotation;
            if (player == null) player = GetComponentInParent<Player.FirstPersonController>();
        }

        void OnDisable()
        {
            transform.localPosition = _restPos;
            transform.localRotation = _restRot;
            _sway = Vector2.zero;
            _effort = 0f;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            Vector2 look = Core.GameState.InputBlocked ? Vector2.zero : Core.GameInput.Look;
            Vector2 targetSway = Vector2.ClampMagnitude(-look * swayAmount, maxSway);
            _sway = Vector2.Lerp(_sway, targetSway, 1f - Mathf.Exp(-swaySmoothing * dt));

            float speed = player != null ? player.CurrentSpeed : 0f;
            _bobPhase += speed * bobFrequency * dt * Mathf.PI * 2f;
            float bobScale = Mathf.Clamp01(speed / 4f);
            Vector3 bob = new Vector3(Mathf.Cos(_bobPhase * 0.5f) * bobAmount * 0.6f,
                Mathf.Abs(Mathf.Sin(_bobPhase * 0.5f)) * bobAmount, 0f) * bobScale;

            float throttle = tool != null ? tool.Throttle : 0f;
            _effort = Mathf.Lerp(_effort, throttle, 1f - Mathf.Exp(-12f * dt));

            Vector3 pos = _restPos + bob;
            Quaternion rot = _restRot * Quaternion.Euler(_sway.y, _sway.x, -_sway.x * 0.5f);
            switch (style)
            {
                case EffortStyle.Lean:
                    rot *= Quaternion.Euler(leanDegrees * _effort, 0f, 0f);
                    pos += new Vector3(0f, -0.03f, 0.05f) * _effort;
                    break;
                case EffortStyle.Buzz:
                    pos += Vibration(Time.time, vibrationHz) * (vibration * _effort);
                    break;
                case EffortStyle.Shake:
                    pos += Vibration(Time.time, vibrationHz * 0.5f) * (vibration * 2f * _effort);
                    rot *= Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * vibrationHz * 0.7f) * 2f * _effort);
                    break;
            }
            transform.localPosition = pos;
            transform.localRotation = rot;
        }

        static Vector3 Vibration(float t, float hz) => new Vector3(
            Mathf.Sin(t * hz * 6.283f), Mathf.Sin(t * hz * 5.17f + 1.3f), Mathf.Sin(t * hz * 4.31f + 2.1f));
    }
}
