using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace Cardstorm.UI
{
    // Botón del menú de pausa que pasa al siguiente idioma. El selector de PlayerPrefs del paquete guarda
    // la elección para la próxima partida.
    [RequireComponent(typeof(Button))]
    public class LanguageButton : MonoBehaviour
    {
        Button button;

        void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(Next);
        }

        void OnDestroy() => button.onClick.RemoveListener(Next);

        static void Next()
        {
            // Hasta que el paquete no ha elegido el primer idioma no hay a qué cambiar.
            var selected = LocalizationSettings.SelectedLocaleAsync;
            if (!selected.IsValid() || !selected.IsDone || selected.Result == null) return;

            var locales = LocalizationSettings.AvailableLocales.Locales;
            if (locales.Count < 2) return;
            int index = locales.IndexOf(selected.Result);
            LocalizationSettings.SelectedLocale = locales[(index + 1) % locales.Count];
        }
    }
}
