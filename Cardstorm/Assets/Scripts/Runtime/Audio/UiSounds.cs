using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Cardstorm.Audio
{
    // Sonidos de los menús sin tocar los botones: mira cómo cambia la selección y las acciones Enviar y Cancelar.
    public class UiSounds : MonoBehaviour
    {
        [SerializeField] SfxPlayer player;
        [SerializeField] SfxCue move;
        [SerializeField] SfxCue submit;
        [SerializeField] SfxCue cancel;

        GameObject lastSelected;
        int quietFrame = -1;

        void Awake()
        {
            if (player == null) player = GetComponent<SfxPlayer>();
        }

        void Update()
        {
            var es = EventSystem.current;
            if (es == null || player == null) return;
            var module = es.currentInputModule as InputSystemUIInputModule;

            // Solo con un menú ya abierto: Esc también abre la pausa, y entonces no debe sonar Cancelar.
            // Con la navegación bloqueada (al abrir una pantalla), Enviar y Cancelar tampoco llegan.
            if (module != null && es.sendNavigationEvents && lastSelected != null && es.currentSelectedGameObject != null)
            {
                if (Pressed(module.submit)) Play(submit);
                else if (Pressed(module.cancel)) Play(cancel);
                else if (Pressed(module.leftClick) && es.IsPointerOverGameObject()) Play(submit);
            }

            // Cambiar de selección al abrir una pantalla o tras pulsar no suena: ya ha sonado Enviar.
            var current = es.currentSelectedGameObject;
            if (current != lastSelected)
            {
                if (lastSelected != null && current != null && Time.frameCount > quietFrame + 1) player.PlayUi(move);
                lastSelected = current;
            }
        }

        void Play(SfxCue cue)
        {
            player.PlayUi(cue);
            quietFrame = Time.frameCount;
        }

        static bool Pressed(InputActionReference reference)
        {
            var action = reference != null ? reference.action : null;
            return action != null && action.enabled && action.WasPressedThisFrame();
        }
    }
}
