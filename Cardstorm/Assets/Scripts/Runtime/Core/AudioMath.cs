using System;

namespace Cardstorm.Core
{
    // Conversión del volumen de los deslizadores (0–1) a los decibelios del mixer.
    public static class AudioMath
    {
        public const float SilentDb = -80f;
        const float SilentLinear = 0.0001f;

        // 0 silencia y 1 queda a 0 dB.
        public static float LinearToDb(float v) => v <= SilentLinear ? SilentDb : 20f * (float)Math.Log10(v);
    }
}
