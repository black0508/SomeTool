using UnityEngine;
using UnityEngine.InputSystem;

namespace Carve
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class CarvePlayer : MonoBehaviour
    {
        public float MoveSpeed = 4.6f;
        public float LookSensitivity = 0.12f;
        public Transform Look;

        CharacterController _body;
        float _pitch;

        void Awake()
        {
            _body = GetComponent<CharacterController>();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null || Look == null)
                return;

            if (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)
                Cursor.lockState = CursorLockMode.Locked;
            if (keyboard.escapeKey.wasPressedThisFrame)
                Cursor.lockState = CursorLockMode.None;

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 delta = mouse.delta.ReadValue();
                transform.Rotate(0f, delta.x * LookSensitivity, 0f);
                _pitch = Mathf.Clamp(_pitch - delta.y * LookSensitivity, -88f, 88f);
                Look.localEulerAngles = new Vector3(_pitch, 0f, 0f);
            }

            Vector3 move = Vector3.zero;
            if (keyboard.wKey.isPressed) move += transform.forward;
            if (keyboard.sKey.isPressed) move -= transform.forward;
            if (keyboard.aKey.isPressed) move -= transform.right;
            if (keyboard.dKey.isPressed) move += transform.right;
            move = Vector3.ClampMagnitude(move, 1f) * MoveSpeed;
            move.y = -9.8f;
            _body.Move(move * Time.deltaTime);
        }
    }
}
