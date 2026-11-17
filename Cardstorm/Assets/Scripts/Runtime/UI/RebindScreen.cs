using System;
using System.Collections.Generic;
using Cardstorm.Core;
using Cardstorm.Localization;
using Cardstorm.Player;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Cardstorm.UI
{
    // Reasignación de controles en teclado y mando. Un conflicto intercambia las teclas de las dos filas.
    public class RebindScreen : ModalScreen
    {
        [SerializeField] Button backButton;

        [Header("Opcionales")]
        [SerializeField] Button resetButton;
        [Tooltip("Aviso tras un intercambio. Se vacía al empezar otra reasignación.")]
        [SerializeField] TMP_Text statusText;
        [Tooltip("Si se deja vacío, se buscan en los hijos.")]
        [SerializeField] RebindRow[] rows;

        [Tooltip("Segundos sin pulsar nada tras los que se deja la tecla como estaba. En mando no hay otra forma "
               + "de cancelar: Start y B también se pueden reasignar.")]
        [SerializeField, Min(1f)] float listenTimeout = 5f;

        readonly List<(string id, string path)> current = new List<(string id, string path)>();
        Action closed;
        InputActionRebindingExtensions.RebindingOperation operation;
        RebindRow listeningRow;
        bool listeningGamepad;
        float listenStart;
        bool waitRelease;
        bool playerMapWasEnabled;
        bool uiMapWasEnabled;
        InputActionMap uiMap;

        protected override void Awake()
        {
            base.Awake();
            if (rows == null || rows.Length == 0) rows = root.GetComponentsInChildren<RebindRow>(true);
            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row.KeyboardButton != null) row.KeyboardButton.onClick.AddListener(() => StartRebind(row, false));
                if (row.GamepadButton != null) row.GamepadButton.onClick.AddListener(() => StartRebind(row, true));
            }
            if (backButton != null) backButton.onClick.AddListener(Back);
            if (resetButton != null) resetButton.onClick.AddListener(ResetAll);
            L10n.Changed += RefreshAll;
        }

        protected override void OnDestroy()
        {
            CancelListening();
            base.OnDestroy();
            L10n.Changed -= RefreshAll;
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i].KeyboardButton != null) rows[i].KeyboardButton.onClick.RemoveAllListeners();
                if (rows[i].GamepadButton != null) rows[i].GamepadButton.onClick.RemoveAllListeners();
            }
            if (backButton != null) backButton.onClick.RemoveListener(Back);
            if (resetButton != null) resetButton.onClick.RemoveListener(ResetAll);
        }

        protected override void OnDisable()
        {
            CancelListening();
            RestoreMaps();
            base.OnDisable();
        }

        public void Show(Action onClosed)
        {
            closed = onClosed;
            SetStatus(string.Empty);
            RefreshAll();
            Open(lockInput: false);
        }

        public override void Hide()
        {
            CancelListening();
            RestoreMaps();
            base.Hide();
        }

        protected override Selectable DefaultSelection()
        {
            for (int i = 0; i < rows.Length; i++)
                if (IsSelectable(rows[i].KeyboardButton)) return rows[i].KeyboardButton;
            return backButton;
        }

        protected override void Update()
        {
            base.Update();

            if (operation != null)
            {
                if (Time.unscaledTime - listenStart > listenTimeout) operation.Cancel();
                return;
            }

            // La tecla que acaba la reasignación sigue pulsada: no debe navegar ni jugar una carta al volver.
            if (waitRelease)
            {
                if (!AnyButtonHeld()) RestoreMaps();
                return;
            }

            if (CancelPressed()) Back();
        }

        void Back()
        {
            if (!AcceptsInput || operation != null || waitRelease) return;
            Hide();
            var callback = closed;
            closed = null;
            callback?.Invoke();
        }

        void ResetAll()
        {
            if (!AcceptsInput || operation != null || waitRelease) return;
            var map = InputSystem.actions != null ? InputSystem.actions.FindActionMap("Player") : null;
            if (map == null) return;
            map.RemoveAllBindingOverrides();
            SetStatus(string.Empty);
            Saved();
        }

        void StartRebind(RebindRow row, bool gamepad)
        {
            if (!AcceptsInput || operation != null || waitRelease) return;
            var action = row.Action;
            int index = row.BindingIndex(gamepad);
            if (action == null || index < 0) return;

            string before = action.bindings[index].effectivePath;
            DisableMaps();
            SetStatus(string.Empty);
            row.ShowWaiting(gamepad);
            listeningRow = row;
            listeningGamepad = gamepad;
            listenStart = Time.unscaledTime;

            operation = action.PerformInteractiveRebinding(index)
                .WithControlsHavingToMatchPath(gamepad ? "<Gamepad>" : "<Keyboard>")
                .OnMatchWaitForAnother(0.05f)
                .OnPotentialMatch(op => DropReserved(op, gamepad))
                .OnComplete(_ => Completed(row, gamepad, before))
                .OnCancel(_ => Finished());
            if (!gamepad) operation.WithCancelingThrough("<Keyboard>/escape");
            operation.Start();
        }

        // Las teclas reservadas no se pueden asignar: se ignoran como si no se hubieran pulsado.
        static void DropReserved(InputActionRebindingExtensions.RebindingOperation op, bool gamepad)
        {
            var candidates = op.candidates;
            for (int i = candidates.Count - 1; i >= 0; i--)
                if (!BindingRules.IsAllowed(candidates[i].path, gamepad)) op.RemoveCandidate(candidates[i]);
        }

        void Completed(RebindRow row, bool gamepad, string before)
        {
            string after = row.CurrentPath(gamepad);
            Finished();

            CollectCurrent(gamepad);
            string otherId = BindingRules.FindConflict(current, row.Id, after);
            var other = otherId != null ? FindRow(otherId) : null;
            if (other != null)
            {
                // Si la tecla que recibe es la suya de serie, se quita el cambio en vez de guardarlo.
                int otherIndex = other.BindingIndex(gamepad);
                if (other.Action.bindings[otherIndex].path == before) other.Action.RemoveBindingOverride(otherIndex);
                else other.Action.ApplyBindingOverride(otherIndex, before);
                SetStatus(L10n.Format(L10nKeys.ControlsSwapped, other.DisplayName));
            }
            Saved();
        }

        void Finished()
        {
            operation?.Dispose();
            operation = null;
            waitRelease = true;
            var row = listeningRow;
            listeningRow = null;
            if (row != null)
            {
                row.Refresh();
                var button = listeningGamepad ? row.GamepadButton : row.KeyboardButton;
                var es = EventSystem.current;
                if (button != null && es != null) es.SetSelectedGameObject(button.gameObject);
            }
        }

        void CancelListening()
        {
            if (operation == null) return;
            operation.Cancel();
            operation?.Dispose();
            operation = null;
            listeningRow = null;
        }

        void Saved()
        {
            SettingsStore.Save();
            SettingsStore.NotifyBindingsChanged();
            RefreshAll();
        }

        void CollectCurrent(bool gamepad)
        {
            current.Clear();
            for (int i = 0; i < rows.Length; i++)
            {
                if (gamepad && rows[i].GamepadButton == null) continue;
                string path = rows[i].CurrentPath(gamepad);
                if (!string.IsNullOrEmpty(path)) current.Add((rows[i].Id, path));
            }
        }

        RebindRow FindRow(string id)
        {
            for (int i = 0; i < rows.Length; i++)
                if (rows[i].Id == id) return rows[i];
            return null;
        }

        void RefreshAll()
        {
            for (int i = 0; i < rows.Length; i++)
                if (rows[i] != listeningRow) rows[i].Refresh();
        }

        void SetStatus(string text)
        {
            if (statusText != null) statusText.text = text;
        }

        // Mientras se espera la tecla, ni el mapa de juego ni el de menús reaccionan. La acción tiene que estar
        // desactivada para reasignarla.
        void DisableMaps()
        {
            var player = InputSystem.actions != null ? InputSystem.actions.FindActionMap("Player") : null;
            playerMapWasEnabled = player != null && player.enabled;
            if (player != null) player.Disable();

            var es = EventSystem.current;
            uiMap = es != null && es.currentInputModule is InputSystemUIInputModule module && module.submit != null
                ? module.submit.action?.actionMap
                : null;
            uiMapWasEnabled = uiMap != null && uiMap.enabled;
            if (uiMap != null) uiMap.Disable();
        }

        void RestoreMaps()
        {
            if (!waitRelease && operation == null) return;
            waitRelease = false;
            if (playerMapWasEnabled && InputSystem.actions != null) InputSystem.actions.FindActionMap("Player")?.Enable();
            if (uiMapWasEnabled && uiMap != null) uiMap.Enable();
            playerMapWasEnabled = false;
            uiMapWasEnabled = false;
            uiMap = null;
        }

        static bool AnyButtonHeld()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.isPressed) return true;
            var pad = Gamepad.current;
            if (pad == null) return false;
            var controls = pad.allControls;
            for (int i = 0; i < controls.Count; i++)
                if (controls[i] is ButtonControl button && !button.synthetic && button.isPressed) return true;
            return false;
        }
    }
}
