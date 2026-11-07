using Cardstorm.Progression;

namespace Cardstorm.UI
{
    // Pantalla de victoria: se abre al ganar la partida.
    public class VictoryScreen : RunEndScreen
    {
        void OnEnable() => RunEvents.RunWon += OnRunWon;

        void OnDisable() => RunEvents.RunWon -= OnRunWon;

        void OnRunWon(double time, int level) => Show(time, level);
    }
}
