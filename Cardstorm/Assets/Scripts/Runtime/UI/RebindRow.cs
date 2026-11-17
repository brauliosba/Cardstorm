using Cardstorm.Localization;
using Cardstorm.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Cardstorm.UI
{
    // Una acción en la pantalla de controles: su nombre y un botón por dispositivo con la tecla actual.
    public class RebindRow : MonoBehaviour
    {
        const string KeyboardGroup = "Keyboard&Mouse";
        const string GamepadGroup = "Gamepad";

        [Tooltip("Acción del mapa Player («CardUp»).")]
        [SerializeField] string actionName;
        [Tooltip("Parte del compuesto para el movimiento («up»). Vacío en las demás.")]
        [SerializeField] string compositePart;
        [SerializeField] string labelKey;

        [SerializeField] TMP_Text label;
        [SerializeField] Button keyboardButton;
        [SerializeField] TMP_Text keyboardText;
        [Tooltip("Opcional. Sin botón, la columna del mando muestra solo el texto («-»).")]
        [SerializeField] Button gamepadButton;
        [SerializeField] TMP_Text gamepadText;

        InputAction action;

        public string Id => string.IsNullOrEmpty(compositePart) ? actionName : $"{actionName}.{compositePart}";
        public string DisplayName => L10n.Get(labelKey);
        public InputAction Action
        {
            get
            {
                if (action == null && InputSystem.actions != null)
                    action = InputSystem.actions.FindAction($"Player/{actionName}");
                return action;
            }
        }
        public Button KeyboardButton => keyboardButton;
        public Button GamepadButton => gamepadButton;

        // Se reasigna el primer binding de cada dispositivo: WASD y no las flechas, I/J/L/K y no 1–4.
        public int BindingIndex(bool gamepad)
        {
            var a = Action;
            if (a == null) return -1;
            string group = gamepad ? GamepadGroup : KeyboardGroup;
            var bindings = a.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                var b = bindings[i];
                if (b.isComposite) continue;
                if (string.IsNullOrEmpty(compositePart))
                {
                    if (b.isPartOfComposite) continue;
                }
                else if (!b.isPartOfComposite || !string.Equals(b.name, compositePart, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (b.groups != null && b.groups.Contains(group)) return i;
            }
            return -1;
        }

        public string CurrentPath(bool gamepad)
        {
            int index = BindingIndex(gamepad);
            return index >= 0 ? Action.bindings[index].effectivePath : null;
        }

        public void Refresh()
        {
            if (label != null) label.text = DisplayName;
            SetText(false, KeyLabel(false));
            SetText(true, gamepadButton != null ? KeyLabel(true) : "-");
        }

        public void ShowWaiting(bool gamepad) => SetText(gamepad, L10n.Get(L10nKeys.ControlsPressKey));

        string KeyLabel(bool gamepad)
        {
            int index = BindingIndex(gamepad);
            if (index < 0) return "-";
            if (gamepad)
            {
                string face = PlayerInputReader.FaceButtonLabel(Action.bindings[index].effectivePath);
                if (face != null) return face;
            }
            return Action.GetBindingDisplayString(index);
        }

        void SetText(bool gamepad, string text)
        {
            var t = gamepad ? gamepadText : keyboardText;
            if (t != null) t.text = text;
        }
    }
}
