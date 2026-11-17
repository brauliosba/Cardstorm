using System;
using UnityEngine;

namespace Cardstorm.Telemetry
{
    // Código de jugador en PlayerPrefs. Se crea la primera vez que se pide y ya no cambia.
    public static class PlayerCodeStore
    {
        const string Key = "telemetry.code";

        public static string GetOrCreate()
        {
            string code = PlayerPrefs.GetString(Key, "");
            if (code.Length == PlayerCode.Length) return code;

            code = PlayerCode.Generate(new System.Random(Environment.TickCount));
            PlayerPrefs.SetString(Key, code);
            PlayerPrefs.Save();
            return code;
        }
    }
}
