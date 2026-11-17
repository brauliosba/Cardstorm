using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Cardstorm.UI
{
    // Base de las pantallas que se abren con el juego en pausa.
    public abstract class ModalScreen : MonoBehaviour
    {
        [Tooltip("Panel hijo con CanvasGroup. Si no lo tiene, se añade al empezar.")]
        [SerializeField] protected GameObject root;

        [Tooltip("Segundos del fundido al abrir y cerrar. Usa tiempo real: el juego está en pausa.")]
        [SerializeField, Min(0f)] float fadeTime = 0.12f;

        [Tooltip("Segundos en tiempo real tras abrir desde la partida en los que se ignoran clics, Enviar y "
               + "navegación, para que la pulsación de una carta no elija nada. Después, la navegación espera "
               + "además a que se suelte el stick o las flechas.")]
        [SerializeField, Min(0f)] float inputDelay = 0.35f;

        CanvasGroup group;
        Selectable lastSelected;
        Tween fade;
        float readyTime;
        bool inputLocked;
        bool suspended;
        int frontFrame = -1;

        public bool IsVisible { get; private set; }

        protected bool AcceptsInput => IsVisible && !suspended && !inputLocked;

        protected virtual void Awake()
        {
            if (!root.TryGetComponent(out group)) group = root.AddComponent<CanvasGroup>();
            root.SetActive(true);
            SetVisible(false, instant: true);
        }

        protected virtual void OnDisable() => UnlockInput();

        protected virtual void OnDestroy() => fade?.Kill();

        protected virtual void Update()
        {
            if (inputLocked && Time.unscaledTime >= readyTime && !NavigationHeld()) UnlockInput();
            if (IsVisible && !suspended) KeepFocus();
        }

        // Primer botón que recibe el foco al abrir o cuando se pierde el último.
        protected abstract Selectable DefaultSelection();

        // lockInput solo hace falta al venir de la partida: entre pantallas no hay nada pulsado.
        protected void Open(bool lockInput)
        {
            suspended = false;
            frontFrame = Time.frameCount;
            if (!IsVisible) SetVisible(true, instant: false);
            ResetFocus();
            if (lockInput) LockInput();
        }

        // Pone el foco en la selección por defecto, por ejemplo al cambiar lo que se muestra.
        protected void ResetFocus()
        {
            lastSelected = DefaultSelection();
            var es = EventSystem.current;
            if (es != null && lastSelected != null) es.SetSelectedGameObject(lastSelected.gameObject);
        }

        public virtual void Hide()
        {
            var es = EventSystem.current;
            if (es != null && IsInside(es.currentSelectedGameObject)) es.SetSelectedGameObject(null);
            lastSelected = null;
            suspended = false;
            UnlockInput();
            SetVisible(false, instant: false);
        }

        // Mientras otra pantalla está encima, esta no se puede pulsar ni recupera la selección.
        public void SetSuspended(bool value)
        {
            if (suspended && !value) frontFrame = Time.frameCount;
            suspended = value;
            group.interactable = IsVisible && !value;
            group.blocksRaycasts = IsVisible && !value;
        }

        // Cancelar (Esc, B) con la pantalla delante. No cuenta en el fotograma en que se abre o vuelve a primer
        // plano: esa pulsación ya la ha usado la pantalla que se acaba de cerrar.
        protected bool CancelPressed()
        {
            if (!AcceptsInput || Time.frameCount == frontFrame) return false;
            var es = EventSystem.current;
            if (es == null || es.currentInputModule is not InputSystemUIInputModule module) return false;
            var action = module.cancel != null ? module.cancel.action : null;
            return action != null && action.WasPerformedThisFrame();
        }

        // Durante el bloqueo, el EventSystem no envía navegación ni Enviar/Cancelar, y los clics se ignoran
        // en los propios botones (AcceptsInput).
        void LockInput()
        {
            readyTime = Time.unscaledTime + inputDelay;
            if (inputLocked) return;
            var es = EventSystem.current;
            if (es == null) return;
            inputLocked = true;
            es.sendNavigationEvents = false;
        }

        void UnlockInput()
        {
            if (!inputLocked) return;
            inputLocked = false;
            if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = true;
        }

        // Si se entra con el stick inclinado, el módulo arrastra el contador de repetición de la partida
        // y la selección saltaría sola hasta el final de la fila.
        static bool NavigationHeld()
        {
            var es = EventSystem.current;
            if (es == null || es.currentInputModule is not InputSystemUIInputModule module) return false;
            var action = module.move != null ? module.move.action : null;
            return action != null && action.ReadValue<Vector2>().sqrMagnitude > 0.01f;
        }

        // Si se pierde la selección (clic en el fondo, botón que deja de poder pulsarse) o sale de la
        // pantalla, vuelve al último botón usado o, si ya no se puede pulsar, al de por defecto.
        void KeepFocus()
        {
            var es = EventSystem.current;
            if (es == null) return;

            var current = es.currentSelectedGameObject;
            if (IsInside(current) && current.TryGetComponent(out Selectable selectable) && selectable.IsInteractable())
            {
                lastSelected = selectable;
                return;
            }

            var target = IsSelectable(lastSelected) ? lastSelected : DefaultSelection();
            if (target != null) es.SetSelectedGameObject(target.gameObject);
        }

        // Mira el interactable propio y no IsInteractable(): justo al abrir, el CanvasGroup puede no haber
        // avisado todavía a los botones.
        protected static bool IsSelectable(Selectable s) => s != null && s.isActiveAndEnabled && s.interactable;

        // Al ocultarse, interactable y blocksRaycasts se quitan en el acto; solo el alfa se desvanece.
        void SetVisible(bool visible, bool instant)
        {
            IsVisible = visible;
            group.interactable = visible;
            group.blocksRaycasts = visible;
            UiTweens.Fade(group, visible, instant ? 0f : fadeTime, ref fade);
        }

        bool IsInside(GameObject go) => go != null && go.activeInHierarchy && go.transform.IsChildOf(root.transform);
    }
}
