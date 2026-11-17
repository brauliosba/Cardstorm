using System;
using System.IO;
using Cardstorm.Localization;
using Cardstorm.Telemetry;
using TMPro;
using UnityEngine;

namespace Cardstorm.UI
{
    // Código de jugador y carpeta de datos, compartidos por las pantallas de fin y de pausa.
    public static class TelemetryUi
    {
        public static bool IsOn(TelemetryRecorder telemetry) =>
            telemetry != null && telemetry.isActiveAndEnabled && !string.IsNullOrEmpty(telemetry.PlayerCode);

        // Sin telemetría el texto se oculta.
        public static void ShowCode(TMP_Text text, TelemetryRecorder telemetry)
        {
            if (text == null) return;
            bool on = IsOn(telemetry);
            text.gameObject.SetActive(on);
            if (on) text.text = L10n.Format(L10nKeys.TelemetryCode, telemetry.PlayerCode);
        }

        // Sin grabador, como en el menú.
        public static void ShowCode(TMP_Text text, string code)
        {
            if (text == null) return;
            bool on = !string.IsNullOrEmpty(code);
            text.gameObject.SetActive(on);
            if (on) text.text = L10n.Format(L10nKeys.TelemetryCode, code);
        }

        public static void OpenFolder(TelemetryRecorder telemetry)
        {
            try
            {
                // Se crea por si la partida no ha llegado a escribir nada.
                Directory.CreateDirectory(telemetry.Folder);
                Application.OpenURL(new Uri(telemetry.Folder).AbsoluteUri);
            }
            catch (Exception e)
            {
                Debug.LogError($"[TelemetryUi] No se pudo abrir {telemetry.Folder}: {e.Message}");
            }
        }
    }
}
