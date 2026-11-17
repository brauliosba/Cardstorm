using System.Collections.Generic;

namespace Cardstorm.Audio
{
    // Cuántas copias de cada sonido suenan y cuándo empezó la última. Con cientos de enemigos, los impactos y
    // las muertes se repiten tanto que sin esto se tapan entre sí.
    public class VoiceLimiter
    {
        struct State
        {
            public bool Started;
            public double LastStart;
            public int Active;
        }

        readonly Dictionary<int, State> states = new Dictionary<int, State>();

        // maxVoices = 0 no limita las copias.
        public bool TryStart(int cueId, double now, float minInterval, int maxVoices)
        {
            states.TryGetValue(cueId, out var s);
            if (s.Started && now - s.LastStart < minInterval) return false;
            if (maxVoices > 0 && s.Active >= maxVoices) return false;

            s.Started = true;
            s.LastStart = now;
            s.Active++;
            states[cueId] = s;
            return true;
        }

        public void Release(int cueId)
        {
            if (!states.TryGetValue(cueId, out var s) || s.Active == 0) return;
            s.Active--;
            states[cueId] = s;
        }

        public int Active(int cueId) => states.TryGetValue(cueId, out var s) ? s.Active : 0;
    }
}
