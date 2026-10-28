using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cardstorm.Player
{
    public class PlayerInputReader : MonoBehaviour
    {
        const string PlayerMap = "Player";

        InputActionMap map;
        InputAction moveAction;
        InputAction pauseAction;

        public Vector2 Move => moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;

        public event Action PausePressed;

        void Awake()
        {
            map = InputSystem.actions.FindActionMap(PlayerMap, true);
            moveAction = map.FindAction("Move", true);
            pauseAction = map.FindAction("Pause", true);
        }

        void OnEnable()
        {
            map.Enable();
            pauseAction.performed += OnPausePerformed;
        }

        void OnDisable()
        {
            pauseAction.performed -= OnPausePerformed;
            map.Disable();
        }

        void OnPausePerformed(InputAction.CallbackContext _) => PausePressed?.Invoke();
    }
}
