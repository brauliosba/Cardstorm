using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cardstorm.Core
{
    public static class SceneReload
    {
        // Vuelve a cargar la escena actual. La escala de tiempo sobrevive a la carga:
        // si se reinicia desde una pausa, la escena empezaría congelada.
        public static void ReloadActive()
        {
            Time.timeScale = 1f;
            var scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
            else SceneManager.LoadScene(scene.name);
        }
    }
}
