using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Cardstorm.Core
{
    public enum TransitionStyle { Fade, Wipe, Checker, DiagonalBars }

    // Panel negro entre escenas. Tapa la escena con un estilo al azar, carga la siguiente y
    // la destapa con el mismo estilo, siguiendo el mismo sentido. Vive entre escenas y se crea al usarlo.
    // Usa tiempo real: se puede salir desde la pausa.
    public class SceneTransition : MonoBehaviour
    {
        const float CoverTime = 0.5f;
        const float RevealTime = 0.5f;
        const int SortingOrder = 1000;
        const int CheckerColumns = 16;
        const int MaxCheckerRows = 12;
        const int BarCount = 10;
        static readonly Color PanelColor = Color.black;

        static SceneTransition instance;

        Canvas canvas;
        RectTransform root;
        Image full;
        RectTransform checkerParent;
        Image[] cells;
        int rows;
        RectTransform barParent;
        Image[] bars;

        TransitionStyle style;
        int direction;  // Wipe: 0 izq→der, 1 der→izq, 2 abajo→arriba, 3 arriba→abajo. Checker: diagonal invertida o no.
        bool running;

        public static bool IsRunning => instance != null && instance.running;

        // Devuelve false si ya hay una transición o la escena no está en la build.
        public static bool Play(string scene)
        {
            if (IsRunning) return false;
            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                Debug.LogError($"[SceneTransition] La escena {scene} no está en Build Settings.");
                return false;
            }

            if (instance == null) Create();
            instance.StartCoroutine(instance.Run(scene));
            return true;
        }

        static void Create()
        {
            var go = new GameObject("SceneTransition");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<SceneTransition>();
            instance.Build();
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        IEnumerator Run(string scene)
        {
            running = true;
            style = (TransitionStyle)Random.Range(0, 4);
            direction = Random.Range(0, 4);
            Prepare();

            // La escena que se va no recibe más clics ni teclas de menú.
            var es = EventSystem.current;
            if (es != null) es.enabled = false;

            canvas.enabled = true;
            yield return Animate(true, CoverTime);

            SceneManager.sceneLoaded += OnSceneLoaded;
            var load = SceneManager.LoadSceneAsync(scene);
            while (!load.isDone) yield return null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            yield return null;

            yield return Animate(false, RevealTime);
            canvas.enabled = false;
            running = false;
        }

        // La pausa o la pantalla de fin dejan el tiempo parado. Mientras carga, la escena que se va sigue
        // parada; la nueva empieza con él en marcha antes de su primer Start.
        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Time.timeScale = 1f;

        IEnumerator Animate(bool cover, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                Apply(t / duration, cover);
                yield return null;
                t += Time.unscaledDeltaTime;
            }
            Apply(1f, cover);
        }

        // Al tapar, p = 1 es todo negro; al destapar, p = 1 es todo visible.
        void Apply(float p, bool cover)
        {
            switch (style)
            {
                case TransitionStyle.Fade:
                    float a = Ease(p);
                    full.color = WithAlpha(cover ? a : 1f - a);
                    break;

                case TransitionStyle.Wipe:
                    SetStripe(full.rectTransform, Ease(p), cover, direction, 0f, 1f);
                    break;

                case TransitionStyle.Checker:
                    ApplyChecker(p, cover);
                    break;

                case TransitionStyle.DiagonalBars:
                    for (int i = 0; i < bars.Length; i++)
                    {
                        float q = Ease(Local(p, i / (float)(bars.Length - 1) * 0.4f, 0.6f));
                        float y0 = i / (float)bars.Length;
                        SetStripe(bars[i].rectTransform, q, cover, i % 2, y0, y0 + 1f / bars.Length);
                    }
                    break;
            }
        }

        // Primero una mitad del tablero y después la otra, cada una barriendo en diagonal.
        void ApplyChecker(float p, bool cover)
        {
            int maxDiagonal = CheckerColumns + rows - 2;
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < CheckerColumns; x++)
                {
                    int diagonal = direction % 2 == 0 ? x + y : (CheckerColumns - 1 - x) + y;
                    float start = (x + y) % 2 * 0.35f + diagonal / (float)maxDiagonal * 0.25f;
                    float q = Ease(Local(p, start, 0.4f));
                    float s = cover ? q : 1f - q;
                    cells[y * CheckerColumns + x].rectTransform.localScale = new Vector3(s, s, 1f);
                }
            }
        }

        // Franja que entra por un lado al tapar y sale por el contrario al destapar.
        // axisDir: 0 izq→der, 1 der→izq, 2 abajo→arriba, 3 arriba→abajo. y0–y1: franja en el otro eje.
        static void SetStripe(RectTransform r, float q, bool cover, int axisDir, float y0, float y1)
        {
            float from = cover ? 0f : q;
            float to = cover ? q : 1f;
            if (axisDir == 1 || axisDir == 3)
            {
                float f = 1f - to;
                to = 1f - from;
                from = f;
            }

            if (axisDir < 2)
            {
                r.anchorMin = new Vector2(from, y0);
                r.anchorMax = new Vector2(to, y1);
            }
            else
            {
                r.anchorMin = new Vector2(y0, from);
                r.anchorMax = new Vector2(y1, to);
            }
        }

        void Prepare()
        {
            full.gameObject.SetActive(style == TransitionStyle.Fade || style == TransitionStyle.Wipe);
            full.color = WithAlpha(1f);
            full.rectTransform.anchorMin = Vector2.zero;
            full.rectTransform.anchorMax = Vector2.one;

            checkerParent.gameObject.SetActive(style == TransitionStyle.Checker);
            barParent.gameObject.SetActive(style == TransitionStyle.DiagonalBars);

            // Sin CanvasScaler, el lienzo mide lo que la pantalla.
            var size = new Vector2(Screen.width, Screen.height);
            if (style == TransitionStyle.Checker) LayoutChecker(size);
            if (style == TransitionStyle.DiagonalBars)
            {
                // Cuadrado girado que cubre la pantalla entera.
                float d = size.magnitude;
                barParent.sizeDelta = new Vector2(d, d);
                barParent.localRotation = Quaternion.Euler(0f, 0f, direction % 2 == 0 ? 45f : -45f);
            }

            Apply(0f, true);
        }

        // Casillas casi cuadradas para la proporción de la pantalla.
        void LayoutChecker(Vector2 size)
        {
            float aspect = size.x > 0f ? size.y / size.x : 9f / 16f;
            rows = Mathf.Clamp(Mathf.RoundToInt(CheckerColumns * aspect), 1, MaxCheckerRows);
            for (int y = 0; y < MaxCheckerRows; y++)
            {
                for (int x = 0; x < CheckerColumns; x++)
                {
                    var r = cells[y * CheckerColumns + x].rectTransform;
                    bool used = y < rows;
                    r.gameObject.SetActive(used);
                    if (!used) continue;
                    r.anchorMin = new Vector2(x / (float)CheckerColumns, y / (float)rows);
                    r.anchorMax = new Vector2((x + 1) / (float)CheckerColumns, (y + 1) / (float)rows);
                }
            }
        }

        void Build()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            // Tapa los clics de la escena mientras se ve.
            gameObject.AddComponent<GraphicRaycaster>();
            root = (RectTransform)transform;

            full = CreateImage("Full", root);

            checkerParent = CreateRect("Checker", root);
            cells = new Image[CheckerColumns * MaxCheckerRows];
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i] = CreateImage($"Cell_{i:000}", checkerParent);
                // Un píxel de más para que no se vean juntas entre casillas.
                cells[i].rectTransform.sizeDelta = new Vector2(1f, 1f);
            }

            barParent = CreateRect("Bars", root);
            barParent.anchorMin = barParent.anchorMax = new Vector2(0.5f, 0.5f);
            bars = new Image[BarCount];
            for (int i = 0; i < bars.Length; i++)
            {
                bars[i] = CreateImage($"Bar_{i:00}", barParent);
                bars[i].rectTransform.sizeDelta = new Vector2(0f, 2f);
            }

            canvas.enabled = false;
        }

        static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var r = (RectTransform)go.transform;
            r.SetParent(parent, false);
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.sizeDelta = Vector2.zero;
            return r;
        }

        static Image CreateImage(string name, Transform parent)
        {
            var image = CreateRect(name, parent).gameObject.AddComponent<Image>();
            image.color = PanelColor;
            return image;
        }

        static Color WithAlpha(float a)
        {
            var c = PanelColor;
            c.a = a;
            return c;
        }

        // Avance de una pieza que empieza en start y dura duration, dentro del total 0–1.
        static float Local(float p, float start, float duration) => Mathf.Clamp01((p - start) / duration);

        static float Ease(float t) => t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
    }
}
