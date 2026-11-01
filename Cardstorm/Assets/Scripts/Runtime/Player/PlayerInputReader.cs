using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cardstorm.Player
{
    public class PlayerInputReader : MonoBehaviour
    {
        const string PlayerMap = "Player";
        const string KeyboardGroup = "Keyboard&Mouse";
        const string GamepadGroup = "Gamepad";

        // Índice de ranura: 0 arriba, 1 izquierda, 2 derecha, 3 abajo.
        static readonly string[] CardActions = { "CardUp", "CardLeft", "CardRight", "CardDown" };

        InputActionMap map;
        InputAction moveAction;
        InputAction pauseAction;
        InputAction cardInfoAction;
        InputAction statsAction;
        InputAction[] cardActions;
        Action<InputAction.CallbackContext>[] cardHandlers;
        bool waitCardRelease;

        public Vector2 Move => moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;

        // Mantener pulsado para ver la descripción de las cartas.
        public bool CardInfoHeld => cardInfoAction != null && cardInfoAction.IsPressed();

        // Último dispositivo usado; decide qué etiquetas de botón se muestran.
        public bool UsingGamepad { get; private set; }

        public int CardSlotCount => CardActions.Length;

        public bool GameplayEnabled => map != null && map.enabled;

        public event Action PausePressed;
        public event Action StatsPressed;
        public event Action<int> CardPressed;
        public event Action DeviceChanged;

        void Awake()
        {
            map = InputSystem.actions.FindActionMap(PlayerMap, true);
            moveAction = map.FindAction("Move", true);
            pauseAction = map.FindAction("Pause", true);
            cardInfoAction = map.FindAction("CardInfo", true);
            statsAction = map.FindAction("Stats", true);

            cardActions = new InputAction[CardActions.Length];
            cardHandlers = new Action<InputAction.CallbackContext>[CardActions.Length];
            for (int i = 0; i < CardActions.Length; i++)
            {
                int slot = i;
                cardActions[i] = map.FindAction(CardActions[i], true);
                cardHandlers[i] = ctx => OnCardPerformed(slot, ctx);
            }
        }

        void OnEnable()
        {
            map.Enable();
            pauseAction.performed += OnPausePerformed;
            statsAction.performed += OnStatsPerformed;
            moveAction.performed += OnMovePerformed;
            for (int i = 0; i < cardActions.Length; i++)
                cardActions[i].performed += cardHandlers[i];
        }

        void OnDisable()
        {
            pauseAction.performed -= OnPausePerformed;
            statsAction.performed -= OnStatsPerformed;
            moveAction.performed -= OnMovePerformed;
            for (int i = 0; i < cardActions.Length; i++)
                cardActions[i].performed -= cardHandlers[i];
            map.Disable();
        }

        // Activa o desactiva los controles de juego (las pantallas de la tienda usan el mapa UI del EventSystem).
        // Al volver, las cartas no se juegan hasta soltar los botones: la pulsación que cierra la tienda no cuenta.
        public void SetGameplayEnabled(bool value)
        {
            if (value == map.enabled) return;
            if (value)
            {
                map.Enable();
                waitCardRelease = true;
            }
            else
            {
                map.Disable();
            }
        }

        void Update()
        {
            if (!waitCardRelease) return;
            for (int i = 0; i < cardActions.Length; i++)
                if (cardActions[i].IsPressed()) return;
            waitCardRelease = false;
        }

        // Etiqueta del botón de la ranura para el dispositivo actual. Se lee de los bindings,
        // así que cambia sola si se reasignan los controles.
        public string CardBindingLabel(int slot)
        {
            var action = cardActions[slot];
            string group = UsingGamepad ? GamepadGroup : KeyboardGroup;
            var bindings = action.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding.isComposite || binding.isPartOfComposite) continue;
                if (binding.groups == null || !binding.groups.Contains(group)) continue;

                if (UsingGamepad)
                {
                    string face = FaceButtonLabel(binding.effectivePath);
                    if (face != null) return face;
                }
                return action.GetBindingDisplayString(i);
            }
            return string.Empty;
        }

        // Nombres de los botones frontales con la disposición de Xbox.
        static string FaceButtonLabel(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (path.EndsWith("/buttonNorth")) return "Y";
            if (path.EndsWith("/buttonWest")) return "X";
            if (path.EndsWith("/buttonEast")) return "B";
            if (path.EndsWith("/buttonSouth")) return "A";
            return null;
        }

        void OnPausePerformed(InputAction.CallbackContext ctx)
        {
            NoteDevice(ctx);
            PausePressed?.Invoke();
        }

        void OnStatsPerformed(InputAction.CallbackContext ctx)
        {
            NoteDevice(ctx);
            StatsPressed?.Invoke();
        }

        void OnMovePerformed(InputAction.CallbackContext ctx) => NoteDevice(ctx);

        void OnCardPerformed(int slot, InputAction.CallbackContext ctx)
        {
            NoteDevice(ctx);
            if (!waitCardRelease) CardPressed?.Invoke(slot);
        }

        void NoteDevice(InputAction.CallbackContext ctx)
        {
            bool gamepad = ctx.control?.device is Gamepad;
            if (gamepad == UsingGamepad) return;
            UsingGamepad = gamepad;
            DeviceChanged?.Invoke();
        }
    }
}
