using System;
using System.Collections.Generic;

namespace Cardstorm.Player
{
    // Reglas de la reasignación, sobre rutas de texto («<Keyboard>/i», «/XInputControllerWindows/buttonSouth»).
    public static class BindingRules
    {
        // Depuración, y las alternativas fijas (flechas y 1–4), que no se pueden quitar.
        static readonly HashSet<string> ReservedKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "f1", "f2", "f3", "f4", "f5", "f6", "f7", "f8", "f9", "f10", "f11", "f12", "r",
            "uparrow", "downarrow", "leftarrow", "rightarrow", "1", "2", "3", "4",
            "anykey", "escape",
        };

        // El stick izquierdo mueve y la cruceta navega los menús.
        static readonly string[] ReservedGamepadPrefixes = { "leftstick", "dpad" };

        // Fila que ya usa newPath en el mismo dispositivo, sin contar la propia. Null si no hay.
        public static string FindConflict(IReadOnlyList<(string id, string path)> current, string id, string newPath)
        {
            string target = ControlName(newPath);
            if (target.Length == 0) return null;
            for (int i = 0; i < current.Count; i++)
            {
                var (otherId, otherPath) = current[i];
                if (otherId == id) continue;
                if (ControlName(otherPath) == target) return otherId;
            }
            return null;
        }

        public static bool IsAllowed(string path, bool gamepad)
        {
            string device = DeviceName(path);
            string control = ControlName(path);
            if (control.Length == 0) return false;

            if (gamepad)
            {
                if (device.Contains("mouse") || device.Contains("keyboard")) return false;
                for (int i = 0; i < ReservedGamepadPrefixes.Length; i++)
                    if (control.StartsWith(ReservedGamepadPrefixes[i], StringComparison.Ordinal)) return false;
                return true;
            }

            if (!device.Contains("keyboard")) return false;
            return !ReservedKeys.Contains(control);
        }

        // Nombre del control sin el dispositivo, en minúsculas: «<Keyboard>/I» → «i». Mayús izquierda y
        // derecha cuentan como la misma tecla que Mayús, para que choquen entre sí.
        public static string ControlName(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            string p = path.Trim();
            int start;
            if (p.StartsWith("<", StringComparison.Ordinal))
            {
                int close = p.IndexOf('>');
                start = close >= 0 && close + 1 < p.Length && p[close + 1] == '/' ? close + 2 : -1;
            }
            else if (p.StartsWith("/", StringComparison.Ordinal))
            {
                int slash = p.IndexOf('/', 1);
                start = slash >= 0 ? slash + 1 : -1;
            }
            else
            {
                start = 0;
            }
            if (start < 0 || start >= p.Length) return string.Empty;

            string name = p.Substring(start).ToLowerInvariant();
            return name switch
            {
                "leftshift" or "rightshift" => "shift",
                "leftctrl" or "rightctrl" => "ctrl",
                "leftalt" or "rightalt" => "alt",
                "leftmeta" or "rightmeta" => "meta",
                _ => name,
            };
        }

        static string DeviceName(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            string p = path.Trim();
            if (p.StartsWith("<", StringComparison.Ordinal))
            {
                int close = p.IndexOf('>');
                return close > 1 ? p.Substring(1, close - 1).ToLowerInvariant() : string.Empty;
            }
            if (p.StartsWith("/", StringComparison.Ordinal))
            {
                int slash = p.IndexOf('/', 1);
                return (slash > 1 ? p.Substring(1, slash - 1) : p.Substring(1)).ToLowerInvariant();
            }
            return string.Empty;
        }
    }
}
