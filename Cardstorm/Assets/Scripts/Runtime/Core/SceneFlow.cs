using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cardstorm.Core
{
    // Cambios de escena del juego, siempre con la transición. Reiniciar desde los trucos sigue
    // siendo instantáneo (SceneReload).
    public static class SceneFlow
    {
        public const string MainMenu = "MainMenu";
        public const string Gameplay = "Gameplay";

        // Mientras se tapa, carga o destapa la escena.
        public static bool IsLoading => SceneTransition.IsRunning;

        public static void LoadMenu() => SceneTransition.Play(MainMenu);

        public static void LoadGameplay() => SceneTransition.Play(Gameplay);

        public static void Restart() => SceneTransition.Play(SceneManager.GetActiveScene().name);

        // En el editor sale del modo Play.
        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
