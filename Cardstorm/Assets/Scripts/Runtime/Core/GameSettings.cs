using System;

namespace Cardstorm.Core
{
    public enum ShakeLevel { Off, Reduced, Normal }

    // Ajustes del jugador. Los carga y guarda SettingsStore; el idioma lo guarda la localización.
    public class GameSettings
    {
        public const float DefaultMusicVolume = 0.7f;
        public const float DefaultSfxVolume = 0.8f;
        public const float ReducedFlashScale = 0.35f;

        public static GameSettings Current { get; internal set; } = new GameSettings();

        // Al guardar, o al mover un ajuste que debe notarse en el momento.
        public static event Action Changed;

        public float MusicVolume = DefaultMusicVolume;
        public float SfxVolume = DefaultSfxVolume;
        public ShakeLevel Shake = ShakeLevel.Normal;
        public bool ReduceFlashes;

        // JSON de los controles reasignados. Vacío si no hay ninguno.
        public string BindingOverrides = "";

        public float ShakeScale => Shake switch
        {
            ShakeLevel.Off => 0f,
            ShakeLevel.Reduced => 0.5f,
            _ => 1f,
        };

        public float FlashScale => ReduceFlashes ? ReducedFlashScale : 1f;

        // El parpadeo de invulnerabilidad no se quita (avisa de algo), pero va a la mitad de rápido.
        public float BlinkIntervalScale => ReduceFlashes ? 2f : 1f;

        public void Clamp()
        {
            MusicVolume = Clamp01(MusicVolume);
            SfxVolume = Clamp01(SfxVolume);
            if (!Enum.IsDefined(typeof(ShakeLevel), Shake)) Shake = ShakeLevel.Normal;
            if (BindingOverrides == null) BindingOverrides = "";
        }

        public static void NotifyChanged() => Changed?.Invoke();

        // NaN también vuelve al mínimo.
        static float Clamp01(float v) => v > 0f ? (v < 1f ? v : 1f) : 0f;
    }
}
